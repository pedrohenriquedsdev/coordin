# Design

## Context

- O motor (change 4) já tem o ponto onde a dependência entra: `ForecastSimulator.IsEligible` está documentado como "o único lugar onde a change de dependências acrescentará os pré-requisitos já terminaram". O `PlanningSnapshot` tem hoje `Demands`, `Assignments` etc. como listas separadas, espelhando as tabelas; o `PlanningSnapshotLoader` da API só lê.
- A previsão é gravada a cada escrita, na mesma transação, por `PlanningTransaction` + `ForecastRecalculator` (DT-03). O lock do planejamento (`pg_advisory_xact_lock`) já serializa todas as escritas, então uma checagem de ciclo na API não corre risco de corrida.
- A margem de uma demanda só é conhecida depois que a simulação descobre o início e o fim dela (`SafetyMargin.Apply(start, end, percent, calendar)`). O DT-23 pede que a dependente espere o fim **com margem** do pré-requisito, então esse cálculo precisa acontecer **dentro** da simulação, no dia em que a demanda termina.
- O agregado da demanda (DT-17) leva times, estimativas e pessoas no corpo; as dependências entram do mesmo jeito (`prerequisiteIds`), sem sub-recurso `/dependencies` (alternativa já descartada no DT-17).
- Ver `proposal.md` para a motivação e o que fica fora.

## Goals / Non-Goals

**Goals:**
- A elegibilidade por dependência é uma função isolada, testável sem a simulação inteira.
- O ciclo é detectado em um só lugar (o motor), usado pela API para dar a mensagem clara e pelo validador como rede de segurança.
- O resultado continua determinístico e sem relógio (RNF-02): nada novo depende de ordem de lista, de fuso ou de "hoje".
- Código que um estudante consiga acompanhar: poucos métodos pequenos, cada um com seus testes.

**Non-Goals:**
- Dependência entre etapas de uma demanda, cenários e exportação (ver "Fora desta change" na proposta).
- Mostrar o grafo desenhado ou criar dependências arrastando.
- Otimizar a simulação além do que o teste de 200 demandas da change 9 pedir.

## Decisions

### D1. As dependências entram no snapshot como lista própria

`PlanningSnapshot` ganha `Dependencies`, uma lista de `DependencyInput(Guid DemandId, Guid PrerequisiteId)`, como último parâmetro, **obrigatório** (sem valor padrão). É o mesmo formato de `Assignments` e da tabela `DemandDependencies`, o que deixa o `PlanningSnapshotLoader` trivial.

*Alternativas:* (a) `DemandInput.PrerequisiteIds` (lista dentro da demanda): também funciona, mas mistura duas formas de representar relações no mesmo snapshot; (b) parâmetro opcional com padrão `null`: evitaria mexer nos chamadores, mas deixaria o esquecimento passar em silêncio. Como só há dois pontos que constroem o snapshot (o loader e um teste da API), o custo de tornar obrigatório é pequeno e o compilador aponta onde falta.

### D2. Detecção de ciclos: busca em profundidade com três estados, no motor

Novo `DependencyGraph` em `Coordin.Engine/Planning/` com `FindCycle(demandIds, dependencies)`: cada nó está *não visitado*, *em visita* ou *concluído*; encontrar um nó *em visita* fecha um ciclo, e a pilha da busca dá o caminho (`A → B → A`). Devolve a lista de ids do ciclo ou `null`. Autodependência é um ciclo de um nó. Os vizinhos são percorridos em ordem de `Guid` para o resultado ser determinístico. O DT-23 já escolhe essa técnica; tempo linear no número de arestas.

*Alternativa:* ordenação topológica de Kahn (também detecta ciclo, mas só diz que existe, sem o caminho). Descartada porque a mensagem ao gestor precisa dos títulos do ciclo.

O `PlanningSnapshotValidator` chama o detector e rejeita ciclo, autodependência, pré-requisito que não está no snapshot e aresta repetida com `ArgumentException`, como já faz com as demais referências: na API essas condições são barradas nas bordas, então aqui seria defeito de quem montou o snapshot.

### D3. Classificação: propagação em ordem topológica, com um novo motivo

`PredictabilityClassifier` passa a decidir a demanda em duas partes: primeiro o que é dela (os três motivos atuais, na precedência atual) e, só se ela estiver livre desses, os pré-requisitos. Uma demanda cujo pré-requisito **não concluído** é não previsível recebe o motivo novo `PrerequisiteUnpredictable`; pré-requisito concluído conta como cumprido. A precedência fica `MissingStartDate`, `MissingEstimate`, `NoCapacity`, `PrerequisiteUnpredictable`: o problema próprio da demanda é mais acionável do que o do pré-requisito, e o gestor corrige primeiro o que é dela.

A decisão é memoizada por demanda e percorre os pré-requisitos recursivamente (o grafo já é acíclico, garantido pelo validador; a recursão tem uma guarda contra ciclo, pois o classificador é público). O enum `UnpredictableReason` ganha o valor no **fim**, para não mudar os valores numéricos dos existentes; a API grava o nome (texto), então não há migração de dados.

*Alternativa:* deixar o motivo `NoCapacity` para a dependente. Descartada: o RF-18 pede "o motivo indicado", e dizer "sem capacidade" para quem na verdade espera outra demanda mandaria o gestor ao lugar errado.

### D4. Simulação: a elegibilidade usa o fim com margem do pré-requisito

`ForecastSimulator` guarda, por demanda terminada, o `ForecastEndDate` com margem, calculado **no momento em que ela termina**, com `SafetyMargin.Apply(firstWorked, day, percent, calendar)`. A função isolada vira:

`IsEligible(parte, dia) = parte.Remaining > 0 && parte.StartDate <= dia && todos os pré-requisitos terminaram e dia > fimComMargem(pré-requisito)`

Como o fim com margem é sempre maior ou igual ao dia em que a demanda terminou, uma dependente nunca fica elegível no mesmo dia em que o pré-requisito termina, qualquer que seja a ordem em que as pessoas são percorridas dentro do dia. Isso mantém o resultado determinístico sem depender da ordem das filas. `day > fimComMargem` basta para "dia útil seguinte", porque a simulação só visita dias úteis.

Pré-requisitos concluídos nem entram na checagem. A margem não consome capacidade (DT-22), então segue valendo: a dependente espera até o fim **comunicado**, mas as pessoas ficam livres para outras demandas durante a margem.

O `PartState` ganha a lista de pré-requisitos da demanda; a checagem é feita por demanda e, por haver poucas dependências por demanda, é barata. O percurso das filas por pessoa já salta partes inelegíveis, então uma demanda de maior prioridade que espera não trava as pessoas: elas seguem para a próxima elegível (spec `delivery-forecast`, "A espera não bloqueia as demais").

*Alternativa:* usar o fim **sem** margem. Descartada pelo DT-23: "não começa antes da conclusão prevista" usa a data comunicada, que inclui a margem.

### D5. Estouro do horizonte: retirar a causa e seus dependentes

O laço de `ForecastEngine` que retira demandas que não terminam em cinco anos escolhia a de maior prioridade entre as não terminadas. Com dependências isso erra: uma dependente de maior prioridade fica "não terminada" só por esperar um pré-requisito que estourou, e seria apontada por engano como a causa, deixando o verdadeiro culpado na simulação. A regra nova: a causa é a de maior prioridade entre as não terminadas **que não esperam nenhuma outra não terminada** (nenhum pré-requisito não concluído e não terminado). Ela vira `NoCapacity` e leva junto, com `PrerequisiteUnpredictable`, todas as que dependem dela direta ou indiretamente; a simulação é refeita. O conjunto de candidatas nunca é vazio, porque o grafo é acíclico: toda cadeia de espera termina em uma demanda sem pré-requisito pendente.

### D6. API: `prerequisiteIds` no agregado e validação em duas etapas

- **Banco:** tabela `DemandDependencies` (`DemandId`, `PrerequisiteDemandId`), chave composta, `CHECK ("DemandId" <> "PrerequisiteDemandId")`, FK da dependente com `ON DELETE CASCADE` e FK do pré-requisito com `ON DELETE RESTRICT`. Índice no `PrerequisiteDemandId` (a consulta de dependentes). O banco garante integridade e unicidade; a API traduz o erro `23001` (RESTRICT) como já faz em `team-has-demands`, distinguindo pelo nome da FK.
- **Validação em `ValidateAsync`** (400 por campo, como as demais): ids existentes e sem repetição. Fica antes de qualquer gravação.
- **Ciclo (409):** depois da validação, e dentro da transação com o lock, a API monta o grafo com as arestas gravadas de **todas as outras** demandas mais a lista do corpo para a demanda em questão, chama `DependencyGraph.FindCycle` e, se houver ciclo, responde `409` `dependency-cycle` com os títulos, lidos do banco, em ordem do ciclo. Como a demanda nova ainda não tem id no `POST`, ela não pode fechar ciclo; o `POST` só precisa da validação de existência. A regra do ciclo mora no motor; a API só monta o grafo e traduz o resultado (DT-02).
- **Substituição:** `ApplyRequest` troca o conjunto de arestas da demanda pelo do corpo (mesmo estilo dos times e pessoas).
- **Concluída travada:** o `PUT` de demanda concluída já responde `409 completed-demand-locked` antes de olhar o corpo, então suas dependências não mudam. Uma demanda pode depender de uma concluída (decisão do autor, 2026-11-17).
- **Exclusão:** antes de excluir, a API procura dependentes; se houver, `409 demand-has-dependents` com os títulos. A checagem prévia dá a mensagem clara, e a `RESTRICT` do banco é a rede de segurança contra corrida (mas o lock já serializa). Excluir uma dependente remove as arestas dela por `CASCADE`.
- **Resposta:** `DemandResponse.PrerequisiteIds` na ordem de prioridade das demandas, para ser estável.

### D7. Cronograma: pré-requisitos com título e o motivo novo

`ScheduleItemResponse` ganha `Prerequisites` (lista de `{ demandId, title }`). O `unpredictableReason` já é texto vindo da previsão gravada, então `PrerequisiteUnpredictable` passa sem mudar o contrato, só o comentário e o OpenAPI. O cliente Angular é regenerado com as regras já anotadas em lições anteriores (`--ignoreUnusedModels false`, restaurar `api-configuration.ts`, OpenAPI exportado da fábrica de testes).

### D8. Tela

- **Formulário:** nova seção "Depende de", com uma lista de caixas de marcação com todas as demais demandas (ativas e concluídas, as concluídas marcadas como tal), alimentada pelas listas que o `demands.store` já carrega; na edição, a própria demanda não aparece. Sem biblioteca nova (nada de `@angular/cdk`).
- **Erro de ciclo:** o formulário já mostra a mensagem do `409` e mantém os campos; nada novo no fluxo, só o texto vem da API.
- **Cronograma:** linha "Depende de: A, B" na tabela e nos cartões, só quando há pré-requisitos; `planning/labels.ts` ganha o rótulo do motivo novo como função pura testada à parte (lição da change 5b).
- Datas continuam de calendário, sem `Date` nem `DatePipe` (lição da change 3).

### D9. Documentos aprovados que esta change toca

O TDD precisa de três ajustes pequenos, que **só serão feitos com aprovação do autor** (regra do CLAUDE.md §11): (1) `demand-has-dependents` na lista de códigos do DT-18; (2) `PrerequisiteUnpredictable` na seção 6.3 e na precedência; (3) `prerequisiteIds` no contrato de demanda (5.2). Nenhum DT é contrariado: o DT-23 é seguido ao pé da letra.

## Risks / Trade-offs

- **O fim com margem é calculado dentro da simulação** → duplica a chamada de `SafetyMargin.Apply` (uma na simulação, outra ao montar o resultado). Mitigação: o resultado final reaproveita o valor guardado pela simulação, para os dois nunca divergirem; um teste compara os dois.
- **Dependência pode antecipar uma demanda sem relação** (a dependente deixa de ocupar a pessoa e libera horas) → é comportamento correto e está nos cenários do spec, mas pode surpreender. Mitigação: teste explícito e cenário no spec; não há texto novo na tela, para não crescer o escopo.
- **Dependente de maior prioridade que espera** → no cronograma ela aparece acima do pré-requisito, mas com início depois dele. Mitigação: a linha "Depende de" explica; reordenar automaticamente seria violar a regra inviolável 2.
- **Cadeias longas pesam a simulação?** → o custo extra é uma consulta a um dicionário por parte e dia; sem risco com 30 demandas. O teste de desempenho de 200 demandas é da change 9.
- **Mudança de assinatura do snapshot** → quebra a compilação em dois lugares conhecidos. Mitigação: é intencional (D1); os testes existentes passam uma lista vazia.
- **Prazo (Should, M5 em 2026-11-24)** → a change é média: motor e testes, migration e API, tela. Se o tempo apertar, a tela pode ser entregue com a lista simples e sem o ajuste do cartão no celular, mas nunca sem os testes do motor.

## Migration Plan

1. Migration `AddDemandDependencies` só **cria** a tabela; não altera dados nem tabelas existentes.
2. Em produção (Render + Neon), as migrations rodam ao iniciar (change 7): o deploy aplica a nova tabela sozinho. Sem demandas dependentes, o cálculo é idêntico ao anterior, então o recálculo não muda nenhuma previsão gravada.
3. **Rollback:** reverter o merge e implantar a versão anterior; a tabela nova fica sem uso, e pode ser removida com `dotnet ef migrations remove`/`database update` para a migration anterior em um banco descartável. Nenhum dado existente é perdido.

## Open Questions

- Nenhuma que mude specs, abordagem ou tasks. (A lista "Depende de" mostra todas as demandas; se ficar longa demais para 30 demandas, uma busca por título pode entrar depois, sem alterar o contrato.)
