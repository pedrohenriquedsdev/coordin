# planning-settings Specification

## Purpose
Permite ao gestor ver e ajustar o percentual da margem de segurança aplicada às previsões, que nunca reduz o prazo (RF-09; DT-22).

## Requirements

### Requirement: Consultar as configurações
`GET /api/settings` SHALL devolver o `safetyMarginPercent` e o `executionOrderMode` vigentes. Antes de qualquer alteração, a margem SHALL ser 20 e o modo `FollowPriority` (DT-22, DT-13).

#### Scenario: Valores iniciais
- **WHEN** o gestor consulta as configurações em um sistema novo
- **THEN** a resposta é `200` com `safetyMarginPercent` 20 e `executionOrderMode` `FollowPriority` (texto, nunca número)

### Requirement: Alterar o percentual da margem
`PUT /api/settings` SHALL gravar `safetyMarginPercent`, aceitando só valores maiores ou iguais a zero e menores ou iguais a 1000, com até duas casas decimais, e SHALL recalcular as previsões (RF-09; DT-22; regra inviolável 1).

#### Scenario: Alterar a margem
- **WHEN** o gestor envia `safetyMarginPercent` 30
- **THEN** a resposta é `200` com o valor novo e as previsões passam a usar 30%

#### Scenario: Margem zero
- **WHEN** o gestor envia 0
- **THEN** a resposta é `200` e o fim com margem de cada demanda é igual ao fim sem margem

#### Scenario: Margem negativa ou excessiva
- **WHEN** o valor é -1, 1000,01 ou tem mais de duas casas decimais
- **THEN** a resposta é `400` com o erro no campo `safetyMarginPercent` e nada muda

#### Scenario: A margem nunca reduz o prazo
- **WHEN** o gestor altera a margem para qualquer valor aceito
- **THEN** em nenhuma demanda previsível o fim com margem é anterior ao fim sem margem

### Requirement: O modo de ordem não é alterado por esta rota
`PUT /api/settings` SHALL ignorar qualquer `executionOrderMode` enviado: o modo só muda pela aprovação de um cenário (RF-15; regras invioláveis 2 e 3).

#### Scenario: Modo enviado
- **WHEN** o gestor envia `executionOrderMode` `Custom` junto com a margem
- **THEN** o modo continua `FollowPriority`

### Requirement: Rotas protegidas
`GET` e `PUT /api/settings` SHALL responder `401` sem token válido (RNF-04).

#### Scenario: Sem token
- **WHEN** a rota é chamada sem `Authorization`
- **THEN** a resposta é `401`
