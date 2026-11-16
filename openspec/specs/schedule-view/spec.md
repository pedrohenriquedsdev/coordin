# schedule-view Specification

## Purpose
Entrega ao gestor o cronograma consolidado de todas as demandas na ordem de execução, com a prioridade ao lado, as datas previstas, o alerta de data de fim informada e o desvio das concluídas (RF-11, RF-12).

## Requirements

### Requirement: Cronograma na ordem de execução
`GET /api/schedule` SHALL devolver o `executionOrderMode` e os itens de todas as demandas na ordem de execução: pela prioridade no modo `FollowPriority` e pelo ranking de execução no modo `Custom`. Cada item SHALL trazer a prioridade e o ranking de execução (RF-11; DT-13).

#### Scenario: Ordem padrão
- **WHEN** há três demandas e o modo é `FollowPriority`
- **THEN** os itens vêm por `priorityRank`, cada um com `priorityRank` e `executionRank`

#### Scenario: Sem demandas
- **WHEN** não há demandas
- **THEN** a resposta é `200` com a lista de itens vazia

### Requirement: Dados de cada item
Cada item SHALL trazer: título, times com nome e estimativa, esforço total, data de início, se é previsível e o motivo quando não for, as datas previstas (início, fim sem margem e fim com margem), a data de fim informada, o alerta e `completedOn` (RF-08, RF-09, RF-11).

#### Scenario: Demanda previsível
- **WHEN** a demanda é previsível
- **THEN** o item traz `isPredictable` verdadeiro, `forecastStartDate`, `endDateWithoutMargin` e `forecastEndDate`, de modo que a parte da margem é a diferença entre as duas datas finais

#### Scenario: Demanda não previsível
- **WHEN** a demanda não é previsível
- **THEN** o item traz `isPredictable` falso, o `unpredictableReason` e as três datas previstas nulas

#### Scenario: Esforço total
- **WHEN** a demanda tem times de 80 h e 40 h e um terceiro sem estimativa
- **THEN** `totalEstimatedEffortHours` é 120 e o time sem estimativa vem com `estimatedEffortHours` nulo

### Requirement: Alerta de data de fim informada
O item SHALL marcar `exceedsInformedEndDate` verdadeiro quando a demanda é previsível, tem data de fim informada e o fim com margem é posterior a ela. A data informada NÃO SHALL alterar a previsão (RF-07, RF-11).

#### Scenario: Previsão passa do prazo
- **WHEN** o fim com margem é 2026-12-16 e a data informada é 2026-12-10
- **THEN** `exceedsInformedEndDate` é verdadeiro

#### Scenario: Previsão dentro do prazo
- **WHEN** o fim com margem é igual ou anterior à data informada, ou não há data informada
- **THEN** `exceedsInformedEndDate` é falso

#### Scenario: Data informada não muda o cálculo
- **WHEN** o gestor altera só a data informada
- **THEN** as datas previstas da demanda continuam as mesmas

### Requirement: Divergência em relação à prioridade
O item SHALL marcar `isOutOfPriorityOrder` verdadeiro quando o modo é `Custom` e o ranking de execução da demanda difere da prioridade (RF-11, RF-15).

#### Scenario: Modo padrão
- **WHEN** o modo é `FollowPriority`
- **THEN** `isOutOfPriorityOrder` é falso em todos os itens

#### Scenario: Modo Custom
- **WHEN** o modo é `Custom` e uma demanda tem `priorityRank` 1 e `executionRank` 3
- **THEN** o item dela tem `isOutOfPriorityOrder` verdadeiro

### Requirement: Desvio das demandas concluídas
Para cada demanda concluída que tinha previsão, o item SHALL trazer a `baselineEndDate`, o `baselineDeviationDays` (`completedOn` menos a linha de base) e o `forecastDeviationDays` (`completedOn` menos a previsão vigente na conclusão), em dias corridos, positivo quando atrasou (RF-12; DT-27).

#### Scenario: Concluída com atraso
- **WHEN** a linha de base era 2026-12-10, a previsão vigente era 2026-12-14 e a conclusão foi em 2026-12-16
- **THEN** `baselineDeviationDays` é 6 e `forecastDeviationDays` é 2

#### Scenario: Concluída antes
- **WHEN** a conclusão foi dois dias antes da previsão vigente
- **THEN** `forecastDeviationDays` é -2

#### Scenario: Concluída sem previsão
- **WHEN** a demanda foi concluída sem nunca ter sido previsível
- **THEN** `baselineEndDate` e os dois desvios vêm nulos

### Requirement: Rota protegida
`GET /api/schedule` SHALL responder `401` sem token válido (RNF-04).

#### Scenario: Sem token
- **WHEN** a rota é chamada sem `Authorization`
- **THEN** a resposta é `401`
