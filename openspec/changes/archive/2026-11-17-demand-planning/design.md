# Design

## Context

- O motor (`ForecastEngine.Calculate(PlanningSnapshot)`, change 4) é puro e já cobre capacidade, previsibilidade, simulação e margem. Esta change só o chama, a partir de uma camada de persistência e de endpoints; **nenhuma regra de cálculo** é escrita na API (DT-02).
- A API já tem o padrão por feature (`Features/<Nome>`: entidade, `IEntityTypeConfiguration`, contratos, endpoints estáticos com `MapGroup`), a política de autorização padrão, `ApiProblems.Conflict` e `ApiProblems.InvalidField`, `PostgresErrors`, `MaxDecimalPlaces`, o `JsonStringEnumConverter` e testes com PostgreSQL real (Testcontainers) e uma classe de teste por banco. Tudo isso é reaproveitado.
- O volume é pequeno (até 200 demandas, TDD 8.5): recalcular o planejamento inteiro a cada escrita é aceitável (DT-02, DT-03).
- Fora do escopo: dependências (change 8), cenários e simulação (change 9), históricos (change 12) e as telas (change 5b). Ver proposal.md.

## Goals / Non-Goals

**Goals:**
- Toda escrita que afeta o cálculo termina com as previsões gravadas e coerentes com o motor, na mesma transação.
- Rankings sempre únicos e contínuos, mesmo com escritas simultâneas.
- Código repetível: cada endpoint segue o padrão de `TeamEndpoints`, para as tasks poderem ser feitas por cópia e adaptação.

**Non-Goals:**
- Cálculo incremental ou fila de recálculo: o planejamento inteiro é recalculado, de forma síncrona.
- Campos de dependências, `estimateChangeReason` e `ActualEffortHours` no corpo da demanda.
- Alterar o modo de ordem (`Custom`) por qualquer rota desta change.

## Decisions

### D1. Duas pastas novas, uma por assunto

- `Features/Demands/`: `Demand`, `DemandTeam`, `DemandAssignment` (entidades), uma `IEntityTypeConfiguration` por entidade, `DemandContracts.cs` (requests e responses), `DemandEndpoints.cs` e `DemandErrors.cs` (mensagens e códigos).
- `Features/Planning/`: `Forecast`, `PlanningSettings` (entidades e configurações), `PlanningSnapshotLoader` (banco → snapshot), `ForecastRecalculator` (snapshot → motor → `Forecasts`), `SettingsEndpoints`, `ScheduleEndpoints` e seus contratos.

*Por quê:* o padrão atual é "uma pasta por feature". Demandas e planejamento (previsão, margem, cronograma) mudam por motivos diferentes.

### D2. Modelo de dados (TDD 4.2), com as colunas que esta change precisa

- `Demand`: `Id` (`Guid` v7), `Title` (até 200), `PriorityRank`, `ExecutionRank` (únicos, `CHECK >= 1`), `StartDate?`, `InformedEndDate?`, `CompletedOn?` (`DateOnly`), `CHECK (CompletedOn IS NULL OR StartDate IS NULL OR CompletedOn >= StartDate)`. `ActualEffortHours` fica para a change 12.
- `DemandTeam`: chave (`DemandId`, `TeamId`), `EstimatedEffortHours?` (`decimal(10,2)`, `CHECK > 0`). FK para `Demand` em `CASCADE` e para `Team` em `RESTRICT`.
- `DemandAssignment`: chave (`DemandId`, `PersonId`). FK para `Demand` em `CASCADE` e para `Person` em `CASCADE` (a exclusão de pessoa só chega ao banco quando a API já barrou as alocações abertas; as de demandas concluídas saem com ela).
- `Forecast`: `DemandId` (chave e FK `CASCADE`), `IsPredictable`, `UnpredictableReason?` (texto), `ForecastStartDate?`, `EndDateWithoutMargin?`, `ForecastEndDate?`, `BaselineEndDate?`, `CalculatedAt` (`DateTimeOffset`, UTC), `CHECK (ForecastEndDate IS NULL OR ForecastEndDate >= EndDateWithoutMargin)`.
- `PlanningSettings`: chave inteira fixa `1` (`CHECK (Id = 1)`), `ExecutionOrderMode` (texto), `SafetyMarginPercent` (`decimal(6,2)`, `CHECK >= 0`). A migration insere a linha com `FollowPriority` e 20.
- **Rankings:** índices únicos em `PriorityRank` e em `ExecutionRank`.

### D3. Recálculo: carregar, calcular, gravar, na mesma transação

`ForecastRecalculator.RecalculateAsync(CoordinDbContext db, CancellationToken)`:
1. `PlanningSnapshotLoader.LoadAsync` lê do banco, com `AsNoTracking`, times, pessoas, ausências, feriados, demandas com partes, alocações e `PlanningSettings`, e monta o `PlanningSnapshot`.
2. Chama `ForecastEngine.Calculate`.
3. Atualiza ou insere as linhas de `Forecasts` (D4) e chama `SaveChangesAsync`.

O recalculador **não abre transação**: quem escreve abre uma (`db.Database.BeginTransactionAsync`), faz a escrita com `SaveChangesAsync`, chama o recalculador e confirma. A leitura do recalculador enxerga a escrita porque está na mesma conexão e transação. Se algo lançar, o `Dispose` da transação a desfaz e nada é gravado.

*Alternativa:* um `SaveChanges` interceptado que recalcula sozinho. Descartada: esconde o recálculo, é difícil de testar e de entender. Um método explícito, chamado em cada escrita, é mais fácil de seguir (e de ver quando alguém esquece de chamar).

*Auxiliar:* `PlanningTransaction.RunAsync(db, Func<Task<IResult>> write, ct)` abre a transação e o *advisory lock* (D6), executa `write`, recalcula **só se `write` devolveu sucesso** (`IResult` com status 2xx; as escritas rejeitadas devolvem `400`/`404`/`409` e fazem *rollback* sem recalcular) e confirma. Todos os endpoints que escrevem usam esse auxiliar, para a regra existir em um lugar só.

### D4. Gravação das previsões e linha de base

Para cada demanda do resultado do motor:
- **Concluída** (`DemandStatus.Completed`): a linha de `Forecasts` **não é tocada** (previsão congelada, DT-14, DT-27).
- **Previsível:** grava `IsPredictable = true`, o motivo nulo, as três datas e `CalculatedAt`; se `BaselineEndDate` é nulo, grava-o com o `ForecastEndDate` (primeira previsão); nunca o altera depois.
- **Não previsível:** grava `IsPredictable = false`, o motivo (`MissingStartDate`, `MissingEstimate`, `NoCapacity`), as datas nulas e `CalculatedAt`; a `BaselineEndDate` existente é preservada.

A demanda sem linha em `Forecasts` ganha uma na primeira passagem do recalculador. O `CalculatedAt` vem de um `TimeProvider` injetado (nunca `DateTime.UtcNow` solto), para os testes fixarem o instante.

### D5. Snapshot sem alocações de demandas concluídas

O motor rejeita uma alocação cuja pessoa não é de um time da demanda. Depois que uma demanda é concluída, a pessoa pode mudar de time sem que isso importe. Por isso o `PlanningSnapshotLoader` **não inclui as alocações de demandas concluídas** (o motor ignora demandas concluídas mesmo). Os endpoints de pessoa validam só as demandas abertas (spec `people-registry`).

### D6. Serialização das escritas de demanda com *advisory lock*

Criar, excluir e reordenar demandas mexe em rankings que precisam continuar únicos e contínuos. Os índices únicos protegem o resultado, mas duas escritas simultâneas leriam o mesmo "último ranking". A solução: dentro da transação, `SELECT pg_advisory_xact_lock(<chave fixa>)`, que serializa essas escritas e se libera sozinho no fim da transação. A chave é uma constante nomeada (`PlanningLocks.Demands`).

*Alternativas:* repetir ao dar erro de índice único (código mais difícil de entender e de testar) e nível de isolamento `Serializable` (erros de serialização espalhados). O *advisory lock* é a mais simples. Todas as escritas que recalculam usam o mesmo auxiliar (D3) e, portanto, o mesmo lock; isso também serializa os recálculos entre si, sem custo prático para um usuário só.

### D7. Reordenar sem violar o índice único: duas fases

Mudar os ranks de várias linhas de uma vez viola o índice único no meio da operação. Dentro da transação: (1) soma `1_000_000` ao `PriorityRank` de todas as demandas (um `UPDATE` só, em SQL via `ExecuteUpdateAsync`); (2) grava os ranks finais um a um com `SaveChanges`. No modo `FollowPriority`, o `ExecutionRank` acompanha o `PriorityRank` pelo mesmo caminho. Excluir recompacta com a mesma técnica.

### D8. Contrato da demanda (TDD 5.3) sem os campos das changes futuras

Corpo de `POST`/`PUT`: `title`, `startDate?`, `informedEndDate?`, `teams` (`teamId`, `estimatedEffortHours?`), `assignedPersonIds`. Resposta: o mesmo mais `id`, `priorityRank`, `executionRank`, `completedOn?` e, em cada time, o `teamName`. `prerequisiteDemandIds` e `estimateChangeReason` entram nas changes 8 e 12.

### D9. Cronograma lê as previsões gravadas

`GET /api/schedule` junta `Demands`, `DemandTeams`/`Teams` e `Forecasts`, e ordena em memória pelo modo de ordem (`ExecutionOrderMode`), com a prioridade e o ranking de execução em cada item. Não chama o motor: devolve o que foi persistido (DT-03). O alerta e os desvios (em dias corridos, `DateOnly.DayNumber`) são calculados na montagem da resposta, a partir de datas já gravadas; não são regras de previsão.

### D10. Tempo e fuso

`CompletedOn`, `StartDate` e `InformedEndDate` são `DateOnly`/`date`, sem fuso (lição da change 3: usar `DateOnly?` com `[Required]` quando obrigatório). Só `CalculatedAt` é um instante.

## Risks / Trade-offs

- **[Risco]** Esquecer o recálculo em alguma escrita nova (ou nas existentes de pessoa, ausência e feriado). → *Mitigação:* o auxiliar único `PlanningTransaction` (D3) e um teste por tipo de escrita da spec `forecast-recalculation` ("Escritas que recalculam").
- **[Risco]** O recálculo do planejamento inteiro pode ficar lento com 200 demandas. → *Mitigação:* o motor roda em poucos milissegundos nesse volume (teste da change 4); o teste de desempenho oficial é da change 9.
- **[Trade-off]** Lock global por escrita de planejamento: serializa tudo, o que é seguro e aceitável para um único gestor (PRD, usuário único).
- **[Trade-off]** `team-has-demands` é um código novo no DT-18 (acréscimo, como `team-name-taken` na change 3); o TDD será atualizado na última task.
- **[Risco]** A migration cria a linha de `PlanningSettings` com `InsertData`; um banco já migrado ganha a linha ao aplicar a migration. Os testes de migração conferem que ela existe.
