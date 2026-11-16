# Spec Delta

## Purpose

Entrega ao gestor, por API, o relatório por time de um período: capacidade, horas alocadas, ocupação, ociosidade e sobrecarga por semana, e as demandas não previsíveis de cada time (RF-17; DT-24).

## ADDED Requirements

### Requirement: Consultar o relatório por período
`GET /api/reports/teams?from=&to=` SHALL devolver o período efetivamente coberto (semanas completas) e, para cada time, ordenado por nome sem diferenciar maiúsculas, as semanas em ordem cronológica e a contagem de demandas não previsíveis (RF-17).

#### Scenario: Relatório de duas semanas
- **WHEN** o gestor consulta de uma quarta-feira a uma terça-feira da semana seguinte
- **THEN** a resposta é `200` com `periodStart` na segunda da primeira semana, `periodEnd` no domingo da segunda e duas semanas por time

#### Scenario: Dados de cada semana
- **WHEN** o relatório é devolvido
- **THEN** cada semana traz `weekStart`, `weekEnd`, `capacityHours`, `allocatedHours`, `occupancy` (nula quando a capacidade é zero), `idleHours`, `isOverloaded` e `overloadedDemands` (id e título)

#### Scenario: Sem times
- **WHEN** ainda não há times
- **THEN** a resposta é `200` com a lista de times vazia

#### Scenario: Time sem pessoas e sem demandas
- **WHEN** o time existe sem pessoas
- **THEN** ele aparece com capacidade zero e ocupação nula em todas as semanas

### Requirement: O relatório reflete o planejamento vigente
O relatório SHALL ser calculado com o motor sobre o planejamento atual, o mesmo que gera as previsões gravadas, sem persistir nada e sem alterar o cronograma (RF-17; DT-02, DT-03).

#### Scenario: Mesmo consumo da previsão
- **WHEN** uma demanda de 16 h com uma pessoa de 8 h por dia começa em uma segunda-feira
- **THEN** as horas alocadas do time na semana são 16 e a ociosidade é a capacidade menos 16

#### Scenario: Concluída não consome
- **WHEN** uma demanda é concluída
- **THEN** ela deixa de somar horas alocadas nas semanas seguintes

#### Scenario: Consulta não escreve
- **WHEN** o relatório é consultado
- **THEN** nenhuma previsão, demanda ou configuração é alterada

### Requirement: Período obrigatório e válido
`from` e `to` SHALL ser obrigatórios, no formato `yyyy-MM-dd`; `to` SHALL NOT ser anterior a `from`; o período coberto SHALL ter no máximo 52 semanas (DT-18).

#### Scenario: Parâmetro ausente ou inválido
- **WHEN** `from` ou `to` falta ou não é uma data
- **THEN** a resposta é `400` em Problem Details com o erro no campo

#### Scenario: Fim antes do início
- **WHEN** `to` é anterior a `from`
- **THEN** a resposta é `400` com o erro no campo `to`

#### Scenario: Período longo demais
- **WHEN** o período cobre mais de 52 semanas
- **THEN** a resposta é `400` com o erro no campo `to`

### Requirement: Rota protegida
`GET /api/reports/teams` SHALL responder `401` sem token válido (RNF-04).

#### Scenario: Sem token
- **WHEN** a rota é chamada sem `Authorization`
- **THEN** a resposta é `401`
