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
- `POST /api/demands/simulate` com o corpo de `POST /api/demands` (incluindo o `position` opcional): `200` com a previsão da nova demanda, o impacto e o `position` usado (D10). `POST /api/demands` passa a aceitar o mesmo `position` opcional.
- Cenário: o formato do TDD 5.3 (`executionOrder`, `items[]` com `demandId`, `title`, `priorityRank`, `newExecutionRank`, `currentForecastEndDate`, `forecastStartDate`, `forecastEndDate`, `deltaDays`, `weight`, `scoreContributionDelta`; `sectorEndDate`, `sectorDeltaDays`; `score { current, scenario, delta }`) mais `isComparable`.
- Erros: `400` com `executionOrder` (lista incompleta, repetida ou com id desconhecido, mensagem reaproveitada de `DemandErrors.ReorderInvalid`); `409` `scenario-violates-dependencies`. Os corpos de `simulate`/`apply` sempre vão com conteúdo: sem corpo, a rota de escrita responde `404`, como nas outras changes.
- Datas como texto `yyyy-MM-dd`, números como número, igual ao cronograma.

### D9. Aplicar e voltar à prioridade

Extrai-se de `DemandEndpoints` para um auxiliar compartilhado (`DemandRanking`, `internal static`): `SetRanksAsync`, `OrderedIdsAsync`, a constante de deslocamento e a validação "lista completa, uma vez cada". É refatoração **necessária** ao reaproveitamento, sem mudar comportamento, coberta pelos testes de demandas que já existem. `apply`, dentro de `PlanningTransaction`: valida a ordem, avalia o cenário contra o snapshot atual, confere as dependências, grava `ExecutionRank` pela ordem aprovada **mantendo a ordem de prioridade atual** em `PriorityRank`, passa `PlanningSettings.ExecutionOrderMode` para `Custom`, e o recálculo e o commit são do `PlanningTransaction`. `reset-to-priority`: modo `FollowPriority` e `ExecutionRank` igual a `PriorityRank` (as duas listas iguais à ordem de prioridade), com o mesmo recálculo. Rejeitar não tem rota.

### D10. Simulação de nova demanda e posição (RF-04, RF-16)

**Achado que mudou o plano (2026-11-17):** com a demanda nova no **fim** da fila, nenhuma demanda existente muda de data. Cada pessoa dá as horas à primeira parte elegível da fila, então uma demanda no fim só recebe as horas que sobram; medido em 297 planejamentos gerados, 0 demandas existentes mudaram. Sem escolher a posição, a lista de "demandas deslocadas" do RF-16 seria sempre vazia. Por decisão do autor, o corpo de `POST /api/demands` e de `POST /api/demands/simulate` ganha um `position` **opcional** (de 1 a `n + 1`, padrão `n + 1`), e confirmar cria exatamente onde foi simulado (DT-17: "simulação idêntica à gravação real").

**Semântica da posição:** a demanda nova entra na posição `position` da ordem de prioridade, e as que estavam daquela posição em diante descem uma. A **mesma posição** vale para a ordem de execução (no modo `FollowPriority` as duas já coincidem; no modo `Custom` a nova demanda entra no mesmo índice da ordem de execução aprovada, e as demais descem uma). É uma simplificação: o gestor que aprovou uma ordem de execução própria pode reposicioná-la depois por um cenário.

`NewDemandSimulation.Simulate(snapshot, newDemand, assignments, dependencies, position)` põe a demanda na posição (`PriorityRank` e `ExecutionRank` iguais a `position`, e as existentes com ranking maior ou igual a `position` sobem um), calcula o cronograma sem e com ela e devolve a previsão da nova demanda, as demandas cuja `ForecastEndDate` muda com `deltaDays` em dias corridos, `sectorEndDate`, `sectorDeltaDays` e a `position` usada. O snapshot recebido não é alterado. Uma demanda existente que passaria a ser não previsível (estouro do horizonte de cinco anos) não entra na lista de mudanças: o caso é extremo e fica documentado no código. Uma nova demanda não previsível não desloca ninguém (regra inviolável 5).

**API:** a validação do corpo é extraída para servir às duas rotas; `position` fora de 1 a `n + 1` responde `400` no campo `position`. A criação com `position` cria a demanda no fim, como hoje, e depois move o id para o índice pedido nas duas listas e regrava os rankings com o auxiliar de duas fases (`DemandRanking`), tudo dentro do `PlanningTransaction` da criação. Sem `position`, nada muda no `POST` existente.

### D11. Leituras puras e consistência

`recommendations`, `simulate` e `demands/simulate` só leem: não abrem transação de escrita nem tomam o lock. Com um único gestor, ler o snapshot em consultas separadas é suficiente; o resultado reflete o banco no instante da leitura. `apply` revalida contra o estado atual dentro do lock, então um cenário visto antes nunca é aplicado sobre dados que mudaram sem o recálculo.

### D12. Documentos aprovados que esta change toca

Propostos ao autor na última task, e só editados com a aprovação dele: (1) TDD 5.3, o campo opcional `position` no corpo da demanda (a criação entra no fim por padrão, e na posição pedida quando ela vem), o formato da resposta de `POST /api/demands/simulate`, o campo `isComparable` do cenário, e as respostas de `apply` (cenário aplicado) e `reset-to-priority` (`204`); (2) DT-18, o código `scenario-violates-dependencies`; (3) DT-26, o valor do orçamento de avaliações medido e a correção da justificativa, que dizia "milissegundos" por avaliação (medido: cerca de 18 ms antes da aceleração). (4) a linha "Testes .NET (com cobertura)" do TDD 8.6, para excluir a categoria `Performance`, já que o teste de desempenho do RNF-01 não roda sob a instrumentação da cobertura e o CI ganhou, com a aprovação do autor em 2026-11-17, um passo separado para essa categoria. O PRD não muda. Nenhum DT é contrariado: o DT-25 e o DT-26 são seguidos à risca, com o orçamento por medição que o próprio DT-26 prevê.

## Risks / Trade-offs

- **A aceleração pode não bastar para 3.000 avaliações** → é esperado: o orçamento é o maior valor que cabe em 10 s por medição (D6), e o DT-26 é ajustado com aprovação. Quanto mais rápida a avaliação, mais forte a busca, por isso a aceleração vem antes da busca no plano.
- **Mexer no caminho do cálculo pode mudar um resultado sem querer** → um só caminho (D2), toda a suíte do motor intocada e verde, as invariantes de 400 planejamentos, e hashes de referência do resultado de 400 planejamentos, gravados **antes** de mexer no cálculo e comparados depois de cada passo, e um teste novo de equivalência com ordens aleatórias.
- **Busca local fica presa em ótimos locais** → aceito pelo DT-26 (simples, explicável, determinística); o gestor pode simular a sequência que quiser (RF-14).
- **A recomendação ocupa a requisição por alguns segundos** → o cancelamento é verificado a cada avaliação (D6); com um só gestor isso é aceitável. Se virar problema, vira task futura.
- **Mover só as demandas abertas e previsíveis** → uma demanda concluída ou não previsível nunca muda de posição sozinha (D5), o que também preserva a prioridade registrada (regra 3).
- **`position` no modo `Custom` usa o mesmo índice nas duas ordens** → simplificação deliberada (D10): a nova demanda pode ficar numa posição de execução que o gestor não escolheu. Mitigação: a resposta da simulação mostra exatamente o efeito, e um cenário reposiciona depois.
- **Refatorar `DemandEndpoints` para extrair o auxiliar** → sem mudança de comportamento e coberta pelos testes de demandas existentes; é a menor alteração que evita duplicar a lógica das duas fases de ranks.
- **Prazo** (Should; marco original da 9 em 2026-11-23, M5 em 2026-11-24) → os grupos 1 a 4 (motor) são o caminho crítico; a 9b e a 10 dependem da API desta change.

## Migration Plan

1. Nenhuma migration: a ordem de execução e o modo já existem.
2. A mudança no `Calculate` é interna e não altera nenhum resultado; as rotas são novas.
3. **Rollback:** reverter o merge e implantar a versão anterior; nada gravado por esta change precisa ser desfeito, porque `apply` só grava campos que já existiam, e `reset-to-priority` devolve o estado ao padrão do sistema.

## Open Questions

- Nenhuma que mude specs, abordagem ou tasks. O valor exato de `B` sai da medição durante a implementação (D6) e, se ficar abaixo de 3.000, vira uma pergunta ao autor na última task, já prevista.

## Medições

Volume de referência do RNF-01: 15 times, 150 pessoas, 200 demandas (semente fixa 3), na máquina de desenvolvimento, a quente (uma execução de aquecimento e 30 a 200 repetições, em milissegundos por chamada).

### 2026-11-17, antes da aceleração (task 2.2)

| Região | Custo |
|---|---|
| `ForecastEngine.Calculate` inteiro | 18,37 ms |
| Validação do snapshot | 0,375 ms |
| Classificação de previsibilidade | 0,561 ms |
| Montagem do calendário e da capacidade | 0,042 ms |
| Consulta de capacidade de todas as pessoas em todos os 758 dias (limite superior do que o laço consulta) | 6,36 ms |
| Ordenar as 10.307 entradas de consumo no fim do `Calculate` | 4,76 ms |
| `CountBusinessDays` para 200 demandas (custo do score) | 0,62 ms |

Leitura: o consumo diário (alocar 10 mil registros e ordená-los) e a consulta de capacidade dentro do laço respondem por cerca de metade do tempo, e a avaliação de um cenário não usa o consumo. A validação e a classificação são pequenas, mas se repetem a cada avaliação sem necessidade. O resto é o próprio laço dos dias.

### 2026-11-17, durante a aceleração (task 2.4)

A máquina de desenvolvimento tem ruído de 20% a 30% entre execuções (outros processos em segundo plano). As medições abaixo usam o **mínimo de 9 lotes de 20 execuções**, repetidas 2 a 3 vezes na mesma sessão, e os valores comparáveis entre si são os da mesma sessão. As medições da tabela anterior (18,4 ms o `Calculate` inteiro; 4,76 ms para ordenar o consumo) foram feitas numa sessão mais carregada: refeitas depois com o mesmo código, o `Calculate` dava 11 a 12 ms.

| Passo (cada um com o `ResultGoldenTests` e a suíte do motor verdes) | Avaliação sem consumo | Com consumo | `Calculate` de uma chamada só |
|---|---|---|---|
| Caminho único `PreparedPlanning` (2.3), sem otimizar o laço | 7,7 a 8,4 ms | 9,1 a 11,5 ms | 10,9 a 12,0 ms |
| (b) não montar `ConsumedHours` quando o consumo não é pedido | 3,6 a 4,6 ms | 9,3 a 11,5 ms | 10,8 a 12,2 ms |
| (a) + (c) tabelas indexadas e capacidade por dia guardada | **2,25 a 2,40 ms** | 4,8 a 5,4 ms | 11,2 a 12,1 ms |
| Contagem acumulada de dias úteis no score (avaliador) | menos 0,5 ms por cenário | n/a | n/a |

Leituras: (1) quase todo o ganho vem de **não alocar nem ordenar o consumo diário** (cerca de 10 mil registros por rodada) e de **trocar `LINQ`/dicionários do laço por arrays com a capacidade guardada**; (2) a avaliação de cenário passou de cerca de 11 a 18 ms para cerca de **2,3 ms**; (3) o `Calculate` de uma chamada só **não melhorou**, porque a preparação (validar, classificar, montar as tabelas) e o preenchimento do cache de capacidade, que antes eram diluídos no laço, agora são pagos a cada chamada: isso não afeta a busca, que prepara uma vez, e não piora o que já existia; (4) o consumo continua custando cerca de 2,5 ms por rodada quando pedido, o que só importa para o cálculo gravado.

### 2026-11-17, orçamento da busca (task 3.4)

Planejamento de referência do RNF-01 (15 times, 150 pessoas, 200 demandas, com vários times por demanda e cerca de um quarto das demandas com dependência; `ReferencePlanning` nos testes), na ordem de prioridade, a quente. Cada avaliação dentro da busca custa de 3,0 a 4,6 ms (a simulação sozinha custa cerca de 2,3 ms; o resto é compor a ordem, a chave de memoização e o score).

| Orçamento (avaliações distintas) | Tempo da recomendação | Melhor `score.delta` | Cenários |
|---|---|---|---|
| 200 | 0,6 s | −7.672 | 3 |
| 400 | 1,3 s | −12.099 | 3 |
| 800 | 3,7 s | −19.462 | 3 |
| 1.000 (três execuções) | 3,6 s, 4,0 s e 4,3 s | −25.165 (o mesmo nas três) | 3 |
| 1.600 | 6,5 s | −32.806 | 3 |
| 3.000 (o valor do DT-26) | **12,9 s** | −43.722 | 3 |

**Decisão:** `ScenarioSearch.EvaluationBudget = 1000`. Cumpre o RNF-01 (menos de 10 s) com folga de 2,4× para o CI, que pode ser mais lento que a máquina de desenvolvimento; 1.200 chegaria a cerca de 5 s, sem essa folga. O teste `ScenarioSearchPerformanceTests` trava o limite de 10 s e passou três vezes seguidas, com o mesmo resultado em toda execução.

**Diferença em relação ao TDD (a propor ao autor na task 7.2, sem editar antes):** o DT-26 prevê até **3.000 avaliações** e justifica com "custo da ordem de milissegundos" por avaliação. Medido, cada avaliação custa cerca de 4 ms depois de acelerar o motor (cerca de 11 a 18 ms antes), e 3.000 levariam 12,9 s. O próprio DT-26 diz que o limite exato "é confirmado por um teste de desempenho com o volume do RNF-01", então o valor passa a **1.000**, e a justificativa precisa trocar "milissegundos" pelo custo medido. A qualidade da busca com menos avaliações é menor (o melhor delta cresce com o orçamento: −25.165 com 1.000 contra −43.722 com 3.000 neste planejamento), o que é o preço de cumprir o RNF-01; o gestor ainda pode simular qualquer sequência (RF-14).
