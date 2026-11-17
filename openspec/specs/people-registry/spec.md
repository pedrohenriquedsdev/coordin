# people-registry Specification

## Purpose
Permite ao gestor cadastrar as pessoas, associar cada uma a um único time e informar a capacidade dela em horas por dia útil. A capacidade das pessoas é a base da capacidade dos times e, depois, da previsão de entrega (RF-02; DT-10).

## Requirements

### Requirement: Cadastrar e consultar pessoas
A API SHALL expor `POST /api/people`, com `name`, `teamId` e `dailyCapacityHours`, respondendo `201` com `Location` e a pessoa criada, e `GET /api/people`, que devolve todas as pessoas ordenadas por nome do time e depois por nome da pessoa (RF-02; DT-16).

#### Scenario: Cadastrar uma pessoa
- **WHEN** o gestor envia `POST /api/people` com o nome "Ana", um time existente e capacidade 8
- **THEN** a resposta é `201` com `Location` e o corpo `id`, `name`, `teamId`, `teamName` e `dailyCapacityHours`

#### Scenario: Pessoas com o mesmo nome
- **WHEN** o gestor cadastra duas pessoas chamadas "Ana"
- **THEN** as duas são aceitas, porque o nome de uma pessoa não precisa ser único

#### Scenario: Consultar as pessoas
- **WHEN** o gestor chama `GET /api/people`
- **THEN** a resposta lista todas as pessoas, ordenadas pelo nome do time e depois pelo nome, sem diferenciar maiúsculas

### Requirement: Capacidade obrigatória e dentro da faixa
A capacidade de uma pessoa SHALL ser informada, entre 0 e 24 horas por dia útil, inclusive, com no máximo duas casas decimais. Capacidade zero é aceita e significa que a pessoa não contribui para as previsões (RF-02; TDD 4.2).

#### Scenario: Sem capacidade informada
- **WHEN** o gestor tenta salvar uma pessoa sem `dailyCapacityHours`
- **THEN** a resposta é `400` em Problem Details com o erro no campo `dailyCapacityHours`, e nada é guardado

#### Scenario: Capacidade zero
- **WHEN** o gestor salva uma pessoa com capacidade 0
- **THEN** a pessoa é guardada com capacidade 0

#### Scenario: Limites da faixa
- **WHEN** o gestor salva capacidades 24 e 0,5
- **THEN** as duas são aceitas

#### Scenario: Fora da faixa
- **WHEN** o gestor envia capacidade negativa ou maior que 24, como 24,01
- **THEN** a resposta é `400` com o erro no campo `dailyCapacityHours`

#### Scenario: Casas decimais demais
- **WHEN** o gestor envia 8,123
- **THEN** a resposta é `400` com o erro no campo `dailyCapacityHours`

### Requirement: Cada pessoa em um único time existente
Toda pessoa SHALL pertencer a exatamente um time, que precisa existir; o nome dela é obrigatório, com no máximo 100 caracteres e sem espaços nas pontas (RF-02).

#### Scenario: Sem time
- **WHEN** o gestor tenta salvar uma pessoa sem `teamId`
- **THEN** a resposta é `400` com o erro no campo `teamId`

#### Scenario: Time inexistente
- **WHEN** o `teamId` não corresponde a nenhum time
- **THEN** a resposta é `400` com o erro no campo `teamId`, e nada é guardado

#### Scenario: Nome inválido
- **WHEN** o nome está vazio, só com espaços ou tem mais de 100 caracteres
- **THEN** a resposta é `400` com o erro no campo `name`

### Requirement: Editar pessoa e mudar de time
A API SHALL expor `PUT /api/people/{id}`, que altera nome, time e capacidade, aplicando as regras do cadastro e respondendo `200` com a pessoa atualizada (RF-02).

#### Scenario: Mudar a capacidade
- **WHEN** o gestor altera a capacidade de uma pessoa
- **THEN** a resposta é `200` e a capacidade do time dela muda na consulta seguinte

#### Scenario: Mudar de time
- **WHEN** o gestor altera o `teamId` de uma pessoa
- **THEN** a resposta é `200`, e a contagem e a capacidade dos dois times refletem a mudança

#### Scenario: Pessoa inexistente
- **WHEN** o `id` não existe
- **THEN** a resposta é `404` em Problem Details

### Requirement: Excluir pessoa e suas ausências
A API SHALL expor `DELETE /api/people/{id}`, que responde `204` e remove também as ausências da pessoa (TDD 4.4).

#### Scenario: Excluir uma pessoa com ausências
- **WHEN** o gestor exclui uma pessoa que tem ausências
- **THEN** a resposta é `204`, a pessoa deixa de aparecer em `GET /api/people` e as ausências dela deixam de existir

#### Scenario: Pessoa inexistente
- **WHEN** o `id` não existe
- **THEN** a resposta é `404`

### Requirement: Pessoas protegidas por login
Todos os endpoints de pessoas SHALL exigir o gestor autenticado (RNF-04).

#### Scenario: Sem token
- **WHEN** o cliente chama qualquer endpoint de `/api/people` sem `Authorization`
- **THEN** a resposta é `401` em Problem Details

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
