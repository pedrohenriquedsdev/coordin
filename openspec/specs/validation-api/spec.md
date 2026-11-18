# validation-api Specification

## Purpose
Expõe a validação por simulação pela API, habilitada só por configuração, com o carregamento protegido contra perda de dados (RF-24; DT-37; RT-11).

## Requirements

### Requirement: Habilitação por configuração
As rotas de `/api/validation` SHALL existir somente com `Validation__Enabled=true`, desligada por padrão. Com a variável desligada, todas as rotas SHALL responder `404`, também para o gestor autenticado. Com a variável ligada, as rotas SHALL exigir o gestor autenticado (RNF-04).

#### Scenario: Desligada
- **WHEN** a variável está desligada e o gestor chama qualquer rota de validação
- **THEN** a resposta é `404`

#### Scenario: Ligada sem sessão
- **WHEN** a variável está ligada e a chamada não traz o token
- **THEN** a resposta é `401`

#### Scenario: Status
- **WHEN** a variável está ligada e o gestor chama `GET /api/validation/status`
- **THEN** a resposta é `200` com o estado da validação: quantas demandas há, quantas têm linha de base e se o plano atual foi guardado

### Requirement: Carregar o conjunto exige confirmação
`POST /api/validation/dataset` SHALL exigir `confirmReplaceAll: true` no corpo. Sem a confirmação, SHALL responder `400` informando o que seria substituído (times, pessoas, ausências, feriados, demandas, previsões, linha de base e plano guardado) e SHALL NOT gravar nada. Com a confirmação, SHALL substituir todo o planejamento pelo conjunto embutido em uma única transação, recalculando as previsões.

#### Scenario: Sem confirmação
- **WHEN** o corpo não traz `confirmReplaceAll` ou o valor é falso
- **THEN** a resposta é `400`, com o que seria substituído, e o banco não muda

#### Scenario: Com confirmação
- **WHEN** o corpo traz `confirmReplaceAll: true`
- **THEN** o planejamento passa a ser o do conjunto, com as previsões calculadas, e a resposta informa as quantidades carregadas

#### Scenario: Falha no meio
- **WHEN** o carregamento falha
- **THEN** nenhuma mudança é gravada, e o planejamento anterior continua

### Requirement: A conta do gestor é preservada
O carregamento SHALL NOT alterar a conta do gestor nem os tokens de sessão, e SHALL NOT alterar a configuração da margem além de a definir com o valor do conjunto. O gestor SHALL continuar autenticado depois de carregar.

#### Scenario: Sessão mantida
- **WHEN** o conjunto é carregado
- **THEN** o gestor continua autenticado e a conta e os tokens existem como antes

### Requirement: Linha de base do gestor
`PUT /api/validation/baseline` SHALL receber a data de linha de base de cada demanda e gravá-las substituindo as anteriores. `GET /api/validation/baseline` SHALL devolver as demandas com a data informada (ou vazia). Uma demanda inexistente ou uma data inválida SHALL ser recusada com `400`.

#### Scenario: Gravar a linha de base
- **WHEN** o gestor envia as datas das 30 demandas
- **THEN** elas ficam gravadas e o `GET` as devolve

#### Scenario: Demanda desconhecida
- **WHEN** o corpo traz o id de uma demanda que não existe
- **THEN** a resposta é `400` e nada é gravado

#### Scenario: Sem datas ainda
- **WHEN** nenhuma data foi informada
- **THEN** o `GET` devolve as demandas com a data vazia

### Requirement: Guardar o plano atual uma única vez
`POST /api/validation/current-plan` SHALL guardar o planejamento do processo atual, na ordem de prioridade, como o retrato serializado do planejamento, uma única vez. Uma segunda chamada SHALL responder `409` com `validation-current-plan-exists`. O plano guardado SHALL NOT mudar depois, nem com decisões posteriores do gestor, e só um novo carregamento do conjunto o apaga.

#### Scenario: Guardar
- **WHEN** o gestor guarda o plano atual
- **THEN** a resposta é `201` e o status passa a informar o plano como guardado

#### Scenario: Guardar de novo
- **WHEN** o gestor tenta guardar o plano atual uma segunda vez
- **THEN** a resposta é `409` com `validation-current-plan-exists`, e o plano guardado não muda

#### Scenario: Decisões depois de guardar
- **WHEN** o gestor aprova um cenário depois de guardar o plano
- **THEN** o plano guardado continua o mesmo

### Requirement: Avaliar a validação
`POST /api/validation/evaluate` SHALL receber `minEffortFactor`, `maxEffortFactor` e `seed` e devolver, sem gravar nada, as métricas de eficiência dos dois planejamentos, o desvio por demanda e na média (com sinal e absoluto) e a checagem das mais prioritárias. Ele SHALL usar o plano guardado, o planejamento vigente e a linha de base, e SHALL recusar a avaliação quando faltar o plano guardado ou a linha de base, ou quando os fatores forem inválidos.

#### Scenario: Avaliar
- **WHEN** o plano está guardado e a linha de base está completa
- **THEN** a resposta é `200` com a eficiência dos dois planejamentos, os desvios e a checagem das prioritárias

#### Scenario: Sem plano guardado
- **WHEN** o plano atual não foi guardado
- **THEN** a resposta é `409` com `validation-current-plan-missing`

#### Scenario: Linha de base incompleta
- **WHEN** alguma demanda não tem data de linha de base
- **THEN** a resposta é `409` com `validation-baseline-incomplete`

#### Scenario: Fatores inválidos
- **WHEN** `minEffortFactor` é zero ou maior que `maxEffortFactor`
- **THEN** a resposta é `400`

#### Scenario: Resultado reproduzível
- **WHEN** a mesma avaliação é pedida duas vezes
- **THEN** as duas respostas são idênticas, e nada foi gravado entre elas
