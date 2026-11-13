# Spec Delta — team-registry

## Purpose

Permite ao gestor cadastrar os times do setor e consultar a capacidade diária de cada um, que é a soma da capacidade das pessoas do time. É o primeiro dado de que o cálculo de capacidade e de previsão depende (RF-01, RF-02; DT-10).

## ADDED Requirements

### Requirement: Cadastrar e consultar times
A API SHALL expor `POST /api/teams` com o `name` do time, respondendo `201` com `Location` e o time criado, e `GET /api/teams`, que devolve todos os times ordenados por nome sem diferenciar maiúsculas (RF-01; DT-16).

#### Scenario: Cadastrar um time
- **WHEN** o gestor envia `POST /api/teams` com o nome "Pagamentos"
- **THEN** a resposta é `201` com o cabeçalho `Location` e o corpo `id`, `name`, `peopleCount` e `dailyCapacityHours`
- **AND** o time novo tem `peopleCount` e `dailyCapacityHours` iguais a zero

#### Scenario: Sete ou mais times
- **WHEN** o gestor cadastra 8 times com nomes diferentes
- **THEN** `GET /api/teams` devolve os 8 times, ordenados por nome

#### Scenario: Sem times cadastrados
- **WHEN** ainda não há times
- **THEN** `GET /api/teams` responde `200` com uma lista vazia

### Requirement: Nome do time único e obrigatório
O nome de um time SHALL ser obrigatório, ter no máximo 100 caracteres e ser único sem diferenciar maiúsculas de minúsculas; espaços nas pontas são descartados (RF-01; DT-18).

#### Scenario: Nome repetido
- **WHEN** já existe o time "Pagamentos" e o gestor cadastra "pagamentos"
- **THEN** a resposta é `409` em Problem Details com o `code` `team-name-taken` e uma mensagem em português

#### Scenario: Nome vazio ou só com espaços
- **WHEN** o gestor envia o nome vazio ou "   "
- **THEN** a resposta é `400` em Problem Details com o erro no campo `name`

#### Scenario: Nome longo demais
- **WHEN** o gestor envia um nome com 101 caracteres
- **THEN** a resposta é `400` com o erro no campo `name`

#### Scenario: Espaços nas pontas
- **WHEN** o gestor cadastra "  Web  "
- **THEN** o time é guardado e devolvido com o nome "Web"

#### Scenario: Cadastros simultâneos com o mesmo nome
- **WHEN** duas requisições simultâneas cadastram o mesmo nome
- **THEN** exatamente uma responde `201` e a outra responde `409` com `team-name-taken`

### Requirement: Editar o nome de um time
A API SHALL expor `PUT /api/teams/{id}`, que troca o nome do time e responde `200` com o time atualizado, aplicando as mesmas regras de nome do cadastro (RF-01).

#### Scenario: Renomear
- **WHEN** o gestor envia `PUT /api/teams/{id}` com um nome novo e livre
- **THEN** a resposta é `200` com o time e o novo nome

#### Scenario: Salvar o mesmo nome
- **WHEN** o gestor envia `PUT` com o nome que o próprio time já tem, mudando só as maiúsculas
- **THEN** a resposta é `200`, sem conflito com ele mesmo

#### Scenario: Nome de outro time
- **WHEN** o gestor renomeia um time para o nome de outro
- **THEN** a resposta é `409` com `team-name-taken`

#### Scenario: Time inexistente
- **WHEN** o `id` não existe
- **THEN** a resposta é `404` em Problem Details

### Requirement: Excluir só time sem pessoas
A API SHALL expor `DELETE /api/teams/{id}`, que responde `204`, mas SHALL recusar a exclusão de um time que tenha pessoas, para não deixar pessoas sem time (RF-01, RF-02; DT-18; TDD 4.4).

#### Scenario: Time sem pessoas
- **WHEN** o gestor exclui um time sem pessoas
- **THEN** a resposta é `204` e o time deixa de aparecer em `GET /api/teams`

#### Scenario: Time com pessoas
- **WHEN** o gestor exclui um time que tem ao menos uma pessoa
- **THEN** a resposta é `409` com o `code` `team-has-people`, e o time e as pessoas continuam como estavam

#### Scenario: Time inexistente
- **WHEN** o `id` não existe
- **THEN** a resposta é `404`

### Requirement: Capacidade diária derivada do time
A resposta de cada time SHALL trazer `peopleCount` e `dailyCapacityHours`, em que a capacidade do time é a soma da capacidade diária das pessoas dele, calculada pelo motor (RF-02; DT-02, DT-10).

#### Scenario: Soma das pessoas
- **WHEN** um time tem pessoas com 8, 6 e 6 horas por dia útil
- **THEN** o time tem `peopleCount` 3 e `dailyCapacityHours` 20

#### Scenario: Pessoa com capacidade zero
- **WHEN** um time tem uma pessoa com 8 horas e outra com 0 hora
- **THEN** `peopleCount` é 2 e `dailyCapacityHours` é 8

#### Scenario: Time cujas pessoas têm capacidade zero
- **WHEN** todas as pessoas do time têm capacidade 0
- **THEN** `dailyCapacityHours` é 0, o que o sistema trata como time sem capacidade

#### Scenario: A capacidade acompanha as mudanças
- **WHEN** uma pessoa muda de capacidade, de time ou é excluída
- **THEN** a capacidade e a contagem dos times envolvidos refletem a mudança na consulta seguinte

### Requirement: Times protegidos por login
Todos os endpoints de times SHALL exigir o gestor autenticado, como os demais endpoints da API (RNF-04).

#### Scenario: Sem token
- **WHEN** o cliente chama qualquer endpoint de `/api/teams` sem `Authorization`
- **THEN** a resposta é `401` em Problem Details
