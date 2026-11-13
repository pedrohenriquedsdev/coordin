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
