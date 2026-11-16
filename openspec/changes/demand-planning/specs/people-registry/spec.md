# Spec Delta

## ADDED Requirements

### Requirement: Pessoa alocada a demanda aberta não é excluída
`DELETE /api/people/{id}` SHALL responder `409` com o `code` `person-has-open-assignments` se a pessoa está alocada a alguma demanda não concluída; as alocações a demandas concluídas SHALL ser removidas junto com a pessoa (TDD 4.4; DT-18).

#### Scenario: Pessoa alocada a demanda aberta
- **WHEN** a pessoa está alocada a uma demanda não concluída e o gestor tenta excluí-la
- **THEN** a resposta é `409` com `code` `person-has-open-assignments` e a pessoa continua cadastrada

#### Scenario: Pessoa alocada só a demanda concluída
- **WHEN** a pessoa só tem alocações em demandas concluídas
- **THEN** a exclusão responde `204` e as alocações dela deixam de existir

### Requirement: Mudar de time respeita os times das demandas
`PUT /api/people/{id}` SHALL responder `409` com o `code` `person-not-in-demand-teams` quando o novo time da pessoa não está entre os times de alguma demanda não concluída a que ela está alocada (RF-05; DT-12, DT-18).

#### Scenario: Novo time fora da demanda
- **WHEN** a pessoa do time A está alocada a uma demanda aberta que envolve só o time A e o gestor a muda para o time B
- **THEN** a resposta é `409` com `code` `person-not-in-demand-teams` e a pessoa continua no time A

#### Scenario: Novo time dentro da demanda
- **WHEN** a demanda aberta envolve os times A e B e a pessoa do time A é movida para o time B
- **THEN** a resposta é `200`

#### Scenario: Pessoa sem alocações
- **WHEN** a pessoa não está alocada a nenhuma demanda
- **THEN** a mudança de time continua livre, como antes

### Requirement: Capacidade e time recalculam as previsões
Alterar a capacidade diária ou o time de uma pessoa alocada a demandas SHALL recalcular as previsões na mesma transação (RF-08; DT-03).

#### Scenario: Capacidade reduzida
- **WHEN** a pessoa alocada a uma demanda previsível tem a capacidade reduzida de 8 para 4 horas
- **THEN** a previsão guardada da demanda passa para uma data posterior

#### Scenario: Capacidade zerada
- **WHEN** a única pessoa alocada à parte de um time passa a ter capacidade 0
- **THEN** a demanda passa a ser não previsível com o motivo `NoCapacity`
