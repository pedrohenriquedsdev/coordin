# Spec Delta

## ADDED Requirements

### Requirement: Time usado por demandas não é excluído
`DELETE /api/teams/{id}` SHALL responder `409` com o `code` `team-has-demands` se o time faz parte de alguma demanda, concluída ou não, mesmo sem pessoas (RF-01, RF-05; DT-18).

#### Scenario: Time em uma demanda
- **WHEN** o time não tem pessoas mas aparece como parte de uma demanda
- **THEN** a resposta é `409` com `code` `team-has-demands` e o time continua cadastrado

#### Scenario: Time em nenhuma demanda
- **WHEN** o time não tem pessoas nem demandas
- **THEN** a exclusão continua respondendo `204`

#### Scenario: Time com pessoas e demandas
- **WHEN** o time tem pessoas e também demandas
- **THEN** a resposta é `409` (com o `code` `team-has-people` ou `team-has-demands`) e nada é excluído
