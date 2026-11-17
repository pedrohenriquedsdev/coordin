# Design

## Context

- **O motor hoje:** `ForecastEngine.Calculate(snapshot)` valida o snapshot, classifica as demandas, monta o calendário e a capacidade, simula dia a dia (`ForecastSimulator`) e devolve previsões e consumo. A ordem da fila vem de `snapshot.OrderMode` (`FollowPriority` usa `PriorityRank`; `Custom` usa `ExecutionRank`) com desempate por id. Dependências, margem e estouro do horizonte já estão resolvidos (change 8). Tudo é puro, sem relógio nem aleatoriedade.
- **Custo medido (2026-11-17):** com 15 times, 150 pessoas e 200 demandas (o volume do RNF-01), uma chamada de `Calculate` custa cerca de **18 ms** a quente. O DT-26 prevê até 3.000 avaliações, ou seja, cerca de 55 s contra os 10 s do RNF-01. Cada `Calculate` refaz a validação, a classificação, o calendário e a capacidade, que não dependem da ordem, e aloca um `ConsumedHours` por pessoa, demanda e dia, que a avaliação de um cenário nem usa. O DT-26 diz que o limite exato é "confirmado por um teste de desempenho com o volume do RNF-01", então fixar o orçamento por medição está dentro do que o DT já prevê.
- **A API hoje:** toda escrita que afeta o cálculo passa por `PlanningTransaction` (lock do planejamento, escrita, recálculo, commit). `PlanningSnapshotLoader` monta o snapshot a partir do banco. `DemandEndpoints` tem um `SetRanksAsync` privado que grava prioridade e ordem de execução em duas fases (para o índice único). `PUT /api/settings` não altera o modo: ele só muda por aprovação de cenário.
- **Já aprovado:** TDD 5.2 lista as rotas de cenários, de `reset-to-priority` e de `POST /api/demands/simulate`; 5.3 define o formato de um cenário; DT-25 define o score; DT-26 define a busca; 6.5 define a simulação de nova demanda.
- Ver `proposal.md` para a motivação e o que fica fora (as telas são da change 9b).

## Goals / Non-Goals

**Goals:**
- Avaliar uma ordem de execução barato o bastante para a busca caber nos 10 s do RNF-01, sem mudar nenhum resultado do cálculo.
- Score e parcelas exatamente como o DT-25, com a soma das parcelas igual à diferença do score (é a justificativa do cenário).
- Resultado determinístico (RNF-02): o limite da busca é por número de avaliações, e nada depende de ordem de lista, relógio ou máquina.
- Recomendar e simular nunca gravam; aprovar e voltar à prioridade gravam atomicamente e preservam a prioridade.
- Código que um estudante consiga seguir: poucos métodos pequenos, cada um com seus testes.

**Non-Goals:**
- Telas e gráficos (change 9b), exportação (10) e validação por simulação (11).
- Outros critérios de otimização ou outros métodos de busca; mudar alocações ou capacidade.
- Persistir cenários ou guardar histórico deles.
- Tornar a recomendação assíncrona ou em segundo plano: roda na requisição, dentro do limite do RNF-01.

## Decisions

### D1. Onde cada coisa mora

Motor: nova pasta `Coordin.Engine/Scenarios/` (`ExecutionOrders`, `Scenario` e seus `record`s, `ScenarioEvaluator`, `ScenarioScorer`, `ScenarioSearch`, `NewDemandSimulation`, `DependencyCheck`); a aceleração mora em `Forecasting/` (`PreparedPlanning`). API: `Features/Scenarios/` (contratos, rotas e erros) e `POST /api/demands/simulate` em `Features/Demands`. A regra de cálculo fica só no motor (DT-02): a API monta o snapshot, chama o motor e traduz o resultado.

### D2. A ordem é um parâmetro do cálculo, e o caminho único é o preparado

`PreparedPlanning` guarda tudo o que não depende da ordem: snapshot validado, classificação, calendário, capacidade de cada pessoa por dia, partes e pré-requisitos de cada demanda, filas de alocação por pessoa. Seu método `Run(executionOrder, collectConsumption)` recebe a lista completa de demandas na ordem de execução e devolve o resultado. `ForecastEngine.Calculate(snapshot)` passa a ser `Prepare(snapshot).Run(ExecutionOrders.Current(snapshot), collectConsumption: true)`. Com **um só caminho**, o cálculo e a avaliação de cenários não podem divergir por construção, e um teste confirma a equivalência em centenas de planejamentos. A ordem atual vem de uma função única (`ExecutionOrders.Current`) que replica a regra existente: o ranking do modo, com desempate por id.

*Alternativa:* um segundo simulador "rápido" só para cenários. Descartada: duplica a regra de cálculo e abre espaço para os dois divergirem.

### D3. Aceleração guiada por medição, sem mudar resultados

A primeira task do grupo mede onde o tempo vai (perfil por região: validação, classificação, calendário e capacidade, laço dos dias, alocações) e registra os números. As ações previstas, só se a medição confirmar:
- calcular uma vez a capacidade de cada pessoa por dia útil (hoje é consultada por pessoa e por dia dentro do laço) e reaproveitá-la;
- não gerar `ConsumedHours` na avaliação (`collectConsumption: false`), que só precisa das datas;
- trocar `LINQ` e dicionários no laço dos dias por arrays indexados, mantendo a mesma ordem de percurso (determinismo);
- pular a validação e a classificação, já feitas uma vez.

O critério é o **resultado idêntico** (toda a suíte do motor, as invariantes de 400 planejamentos e a equivalência com ordens aleatórias) e a **meta** de cerca de 4 ms por avaliação no volume de referência (meta, não requisito: o requisito é o RNF-01).

### D4. Score, parcelas e desempates (DT-25)

Para a demanda `i` pertencente ao conjunto do score (previsível no cronograma atual **e** no cenário): `dᵢ = CountBusinessDays(menorStartDate, ForecastEndDate)`, com `menorStartDate` a menor data de início registrada entre as demandas do conjunto, e `wᵢ = n − PriorityRankᵢ + 1`, com `n` a **maior** prioridade registrada (assim todo peso é ao menos 1, mesmo com lacunas por demandas concluídas). A parcela é `wᵢ × dᵢ`, e `scoreContributionDelta` é a parcela do cenário menos a atual, então a soma delas fecha com `score.delta` por construção (verificada por teste). O desempate é um `IComparer` único: `score`, depois `sectorEndDate`, depois o número de demandas deslocadas em relação à ordem atual, depois a ordem lexicográfica da sequência de ids. Valores de `score` são `long` (peso × dias não estoura).

Se o conjunto de demandas previsíveis do cenário diferir do atual (só acontece quando uma demanda estoura o horizonte de cinco anos com outra ordem), o cenário é marcado `isComparable = false`, os itens sem data saem do score, e a busca o descarta.

### D5. A ordem completa e as posições fixas

A ordem de um cenário lista **todas** as demandas (como `PUT /api/demands/priority-order` faz), porque é isso que `apply` grava em `ExecutionRank`. A busca só move as demandas abertas e previsíveis: as concluídas e as não previsíveis (que não consomem capacidade nem entram no score) ficam **na mesma posição absoluta**, e as movíveis preenchem as demais posições na ordem que a busca decidir. Assim a busca gasta o orçamento só no que muda o resultado, e a posição das fixas nunca muda sem o gestor ver.

### D6. Busca local, memoização e orçamento (DT-26)

Seguindo o DT-26, com dois detalhes de implementação:
- **Vizinhança:** para cada demanda movível, na ordem atual, as distâncias 1, 2 e 3, subindo antes de descer, descartando as que saem da lista. Sempre a mesma ordem de geração.
- **Memoização:** o resultado de cada sequência avaliada é guardado por uma chave da sequência. Reavaliar uma sequência já vista não gasta orçamento (o orçamento conta avaliações **distintas**).
- **Duas fases com o mesmo orçamento `B`:** (1) avaliar os vizinhos da ordem atual, no máximo `B/2` avaliações, guardando os que melhoram, o que dá os "dois melhores movimentos únicos"; (2) subida de encosta com primeira melhoria a partir da ordem atual (os vizinhos já avaliados na fase 1 saem do cache), até nenhum vizinho melhorar ou o orçamento acabar. A saída é a melhor ordem da fase 2 mais os dois melhores movimentos únicos, sem repetir sequências, ordenados pelo comparador do D4, no máximo 3.
- **Cancelamento:** a busca recebe um `CancellationToken` e o verifica a cada avaliação, para uma requisição cancelada não continuar calculando. O token não afeta o resultado quando não cancela.

`B` é uma constante nomeada no motor. O DT-26 sugere 3.000. Com o custo medido, **`B` será o maior valor tal que a recomendação de referência (15 times, 150 pessoas, 200 demandas) termina em até cerca de 5 s na máquina de desenvolvimento**, uma folga de 2× sobre o limite de 10 s para o CI mais lento. O teste de desempenho do RNF-01 trava em 10 s. Se `B` ficar abaixo de 3.000, o DT-26 precisa ser ajustado, o que só acontece com a **aprovação do autor** (decisão dele de 2026-11-17).

*Alternativas:* limite por tempo de relógio (descartada: resultado variaria com a máquina, contra o RNF-02); algoritmo genético (já descartado no DT-26).

### D7. Dependências e conferência final

O motor já faz a dependente esperar o pré-requisito em qualquer ordem, então nenhuma ordem produz datas que violem uma dependência. `DependencyCheck.Violations(snapshot, result)` lista, por defesa em profundidade, as dependências cujo início previsto da dependente não é posterior ao fim previsto do pré-requisito. A avaliação não precisa dela; `apply` a chama sobre o cronograma resultante antes de gravar e responde `409 scenario-violates-dependencies` se vier algo (caminho que não deve ser alcançável, mas é barato e dá a garantia que o TDD pede: "valida de novo (dependências)").

### D8. API: rotas, formatos e erros

- `POST /api/scenarios/recommendations` (sem corpo): `200` com uma **lista** de cenários, possivelmente vazia (RF-13).
- `POST /api/scenarios/simulate` com `{ "executionOrder": [...] }`: `200` com um cenário.
- `POST /api/scenarios/apply` com `{ "executionOrder": [...] }`: `200` com o cenário aplicado, calculado **contra o cronograma anterior**, e a gravação na mesma transação.
- `POST /api/scenarios/reset-to-priority` (sem corpo): `204`.
- `POST /api/demands/simulate` com o corpo de `POST /api/demands`: `200` com a previsão da nova demanda e o impacto (D10).
- Cenário: o formato do TDD 5.3 (`executionOrder`, `items[]` com `demandId`, `title`, `priorityRank`, `newExecutionRank`, `currentForecastEndDate`, `forecastStartDate`, `forecastEndDate`, `deltaDays`, `weight`, `scoreContributionDelta`; `sectorEndDate`, `sectorDeltaDays`; `score { current, scenario, delta }`) mais `isComparable`.
- Erros: `400` com `executionOrder` (lista incompleta, repetida ou com id desconhecido, mensagem reaproveitada de `DemandErrors.ReorderInvalid`); `409` `scenario-violates-dependencies`. Os corpos de `simulate`/`apply` sempre vão com conteúdo: sem corpo, a rota de escrita responde `404`, como nas outras changes.
- Datas como texto `yyyy-MM-dd`, números como número, igual ao cronograma.

### D9. Aplicar e voltar à prioridade

Extrai-se de `DemandEndpoints` para um auxiliar compartilhado (`DemandRanking`, `internal static`): `SetRanksAsync`, `OrderedIdsAsync`, a constante de deslocamento e a validação "lista completa, uma vez cada". É refatoração **necessária** ao reaproveitamento, sem mudar comportamento, coberta pelos testes de demandas que já existem. `apply`, dentro de `PlanningTransaction`: valida a ordem, avalia o cenário contra o snapshot atual, confere as dependências, grava `ExecutionRank` pela ordem aprovada **mantendo a ordem de prioridade atual** em `PriorityRank`, passa `PlanningSettings.ExecutionOrderMode` para `Custom`, e o recálculo e o commit são do `PlanningTransaction`. `reset-to-priority`: modo `FollowPriority` e `ExecutionRank` igual a `PriorityRank` (as duas listas iguais à ordem de prioridade), com o mesmo recálculo. Rejeitar não tem rota.

### D10. Simulação de nova demanda (RF-16)

`NewDemandSimulation.Simulate(snapshot, newDemand)` recebe o snapshot atual e a demanda nova (com um id temporário dado pela API, que nunca é gravado), a põe no fim da prioridade e da ordem de execução (como `POST /api/demands`), calcula as duas previsões e devolve a previsão da nova demanda e as demandas cuja `ForecastEndDate` muda, com `deltaDays` em dias corridos, mais `sectorEndDate` e `sectorDeltaDays`. A API reaproveita a validação do `POST` (extraída para ser usada pelas duas rotas) e monta a demanda nova a partir do corpo, incluindo as dependências. Uma nova demanda não previsível não desloca ninguém (regra inviolável 5). Confirmar é o `POST` que já existe.

### D11. Leituras puras e consistência

`recommendations`, `simulate` e `demands/simulate` só leem: não abrem transação de escrita nem tomam o lock. Com um único gestor, ler o snapshot em consultas separadas é suficiente; o resultado reflete o banco no instante da leitura. `apply` revalida contra o estado atual dentro do lock, então um cenário visto antes nunca é aplicado sobre dados que mudaram sem o recálculo.

### D12. Documentos aprovados que esta change toca

Propostos ao autor na última task, e só editados com a aprovação dele: (1) TDD 5.3, o formato da resposta de `POST /api/demands/simulate`, o campo `isComparable` do cenário, e as respostas de `apply` (cenário aplicado) e `reset-to-priority` (`204`); (2) DT-18, o código `scenario-violates-dependencies`; (3) DT-26, o valor do orçamento de avaliações medido e a correção da justificativa, que dizia "milissegundos" por avaliação (medido: cerca de 18 ms antes da aceleração). O PRD não muda. Nenhum DT é contrariado: o DT-25 e o DT-26 são seguidos à risca, com o orçamento por medição que o próprio DT-26 prevê.

## Risks / Trade-offs

- **A aceleração pode não bastar para 3.000 avaliações** → é esperado: o orçamento é o maior valor que cabe em 10 s por medição (D6), e o DT-26 é ajustado com aprovação. Quanto mais rápida a avaliação, mais forte a busca, por isso a aceleração vem antes da busca no plano.
- **Mexer no caminho do cálculo pode mudar um resultado sem querer** → um só caminho (D2), toda a suíte do motor intocada e verde, as invariantes de 400 planejamentos, e hashes de referência do resultado de 400 planejamentos, gravados **antes** de mexer no cálculo e comparados depois de cada passo, e um teste novo de equivalência com ordens aleatórias.
- **Busca local fica presa em ótimos locais** → aceito pelo DT-26 (simples, explicável, determinística); o gestor pode simular a sequência que quiser (RF-14).
- **A recomendação ocupa a requisição por alguns segundos** → o cancelamento é verificado a cada avaliação (D6); com um só gestor isso é aceitável. Se virar problema, vira task futura.
- **Mover só as demandas abertas e previsíveis** → uma demanda concluída ou não previsível nunca muda de posição sozinha (D5), o que também preserva a prioridade registrada (regra 3).
- **Refatorar `DemandEndpoints` para extrair o auxiliar** → sem mudança de comportamento e coberta pelos testes de demandas existentes; é a menor alteração que evita duplicar a lógica das duas fases de ranks.
- **Prazo** (Should; marco original da 9 em 2026-11-23, M5 em 2026-11-24) → os grupos 1 a 4 (motor) são o caminho crítico; a 9b e a 10 dependem da API desta change.

## Migration Plan

1. Nenhuma migration: a ordem de execução e o modo já existem.
2. A mudança no `Calculate` é interna e não altera nenhum resultado; as rotas são novas.
3. **Rollback:** reverter o merge e implantar a versão anterior; nada gravado por esta change precisa ser desfeito, porque `apply` só grava campos que já existiam, e `reset-to-priority` devolve o estado ao padrão do sistema.

## Open Questions

- Nenhuma que mude specs, abordagem ou tasks. O valor exato de `B` sai da medição durante a implementação (D6) e, se ficar abaixo de 3.000, vira uma pergunta ao autor na última task, já prevista.
