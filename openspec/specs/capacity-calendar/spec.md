# capacity-calendar Specification

## Purpose
Guarda as datas em que a capacidade diminui: as ausências de cada pessoa (férias e outras) e os feriados do setor. Durante uma ausência a pessoa não contribui; em um feriado ninguém contribui (RF-03; DT-10). Esta spec só registra os dados; o recálculo das previsões chega com o motor e as demandas (changes 4 e 5).

## Requirements

### Requirement: Registrar a ausência de uma pessoa
A API SHALL expor `POST /api/people/{id}/absences`, com `type` (`Vacation` ou `Other`), `startDate` e `endDate`, respondendo `201` com `Location` e a ausência criada (RF-03; TDD 4.2).

#### Scenario: Registrar férias
- **WHEN** o gestor envia `POST /api/people/{id}/absences` com o tipo `Vacation`, início 2027-01-04 e fim 2027-01-15
- **THEN** a resposta é `201` com `Location` e o corpo `id`, `personId`, `personName`, `teamId`, `teamName`, `type`, `startDate` e `endDate`

#### Scenario: Pessoa inexistente
- **WHEN** o `id` da pessoa não existe
- **THEN** a resposta é `404` em Problem Details

#### Scenario: Ausências sobrepostas
- **WHEN** a mesma pessoa registra duas ausências que se sobrepõem
- **THEN** as duas são aceitas, porque a capacidade é zero em qualquer dia coberto por alguma ausência

### Requirement: Só o tipo e o período, nunca o motivo
O sistema SHALL guardar de cada ausência apenas o tipo e o período, sem nenhum campo de motivo, e as respostas SHALL NOT trazer motivo (RNF-05; RF-03).

#### Scenario: Campo de motivo ignorado
- **WHEN** o corpo da requisição traz uma propriedade `reason` com um texto
- **THEN** a ausência é criada sem ela, e nenhuma resposta traz essa propriedade ou o texto

### Requirement: Período e tipo válidos
O fim da ausência SHALL NOT ser anterior ao início, e uma ausência de um único dia (início igual ao fim) é válida. Tipo, início e fim são obrigatórios (RF-03; TDD 4.2).

#### Scenario: Fim antes do início
- **WHEN** o gestor envia início 2027-01-15 e fim 2027-01-04
- **THEN** a resposta é `400` em Problem Details com o erro no campo `endDate`, e nada é guardado

#### Scenario: Ausência de um dia
- **WHEN** o início e o fim são 2027-02-08
- **THEN** a ausência é aceita

#### Scenario: Tipo desconhecido ou campos faltando
- **WHEN** o tipo não é `Vacation` nem `Other`, ou falta o tipo, o início ou o fim
- **THEN** a resposta é `400`, e nada é guardado

#### Scenario: Data em formato inválido
- **WHEN** uma data não está no formato `yyyy-MM-dd`, como 31/12/2026
- **THEN** a resposta é `400`

### Requirement: Consultar as ausências
A API SHALL expor `GET /api/people/{id}/absences`, com as ausências de uma pessoa, e `GET /api/absences`, com as de todas as pessoas, ambos ordenados por início e depois por nome da pessoa, com os dados da pessoa e do time em cada item (RF-03; TDD 5.2).

#### Scenario: Ausências de uma pessoa
- **WHEN** o gestor chama `GET /api/people/{id}/absences`
- **THEN** a resposta lista só as ausências dessa pessoa, por início

#### Scenario: Ausências de todos
- **WHEN** o gestor chama `GET /api/absences`
- **THEN** a resposta lista as ausências de todas as pessoas, cada uma com `personName`, `teamId` e `teamName`

#### Scenario: Pessoa inexistente na consulta
- **WHEN** o `id` da pessoa não existe em `GET /api/people/{id}/absences`
- **THEN** a resposta é `404`

#### Scenario: Sem ausências
- **WHEN** não há ausências
- **THEN** as duas consultas respondem `200` com uma lista vazia

### Requirement: Editar e excluir uma ausência
A API SHALL expor `PUT /api/absences/{id}`, que altera tipo e período com as mesmas regras do registro, e `DELETE /api/absences/{id}`, que responde `204` (RF-03).

#### Scenario: Alterar o período
- **WHEN** o gestor envia `PUT /api/absences/{id}` com um período válido
- **THEN** a resposta é `200` com a ausência atualizada

#### Scenario: Período inválido na edição
- **WHEN** o `PUT` traz um fim anterior ao início
- **THEN** a resposta é `400` com o erro no campo `endDate`, e a ausência não muda

#### Scenario: Excluir
- **WHEN** o gestor exclui uma ausência
- **THEN** a resposta é `204` e ela deixa de aparecer nas consultas

#### Scenario: Ausência inexistente
- **WHEN** o `id` não existe em `PUT` ou `DELETE`
- **THEN** a resposta é `404`

### Requirement: Cadastrar e remover feriados
A API SHALL expor `POST /api/holidays`, com `date` e `description` opcional (até 200 caracteres), respondendo `201` com `Location`; `GET /api/holidays`, ordenado por data; e `DELETE /api/holidays/{id}`, que responde `204`. Um feriado vale para todas as pessoas (RF-03).

#### Scenario: Cadastrar um feriado
- **WHEN** o gestor envia a data 2026-12-25 e a descrição "Natal"
- **THEN** a resposta é `201` com `Location` e o corpo `id`, `date` e `description`

#### Scenario: Feriado sem descrição
- **WHEN** o gestor envia só a data
- **THEN** o feriado é aceito, e `description` volta vazia

#### Scenario: Listar por data
- **WHEN** há feriados cadastrados fora de ordem
- **THEN** `GET /api/holidays` os devolve ordenados por data

#### Scenario: Remover
- **WHEN** o gestor exclui um feriado
- **THEN** a resposta é `204`, e a data deixa de ser feriado; um `id` inexistente responde `404`

#### Scenario: Dados inválidos
- **WHEN** falta a data, ela tem formato inválido ou a descrição passa de 200 caracteres
- **THEN** a resposta é `400`

### Requirement: Data de feriado única
Não SHALL existir dois feriados na mesma data (RF-03; TDD 4.2; DT-18).

#### Scenario: Data repetida
- **WHEN** o gestor cadastra um feriado em uma data que já é feriado
- **THEN** a resposta é `409` com o `code` `holiday-date-taken` e uma mensagem em português

#### Scenario: Cadastros simultâneos na mesma data
- **WHEN** duas requisições simultâneas cadastram a mesma data
- **THEN** exatamente uma responde `201` e a outra responde `409` com `holiday-date-taken`

### Requirement: Datas de calendário sem fuso
As datas de ausências e feriados SHALL ser datas de calendário (`yyyy-MM-dd`), guardadas e devolvidas exatamente como enviadas, sem depender do fuso do servidor ou do cliente (DT-15; RNF-07).

#### Scenario: Ida e volta
- **WHEN** o gestor registra uma ausência de 2026-12-31 a 2027-01-02 e um feriado em 2026-12-31
- **THEN** as consultas devolvem `2026-12-31`, `2027-01-02` e `2026-12-31`, sem deslocar nenhum dia

### Requirement: Ausências e feriados protegidos por login
Todos os endpoints de ausências e feriados SHALL exigir o gestor autenticado (RNF-04).

#### Scenario: Sem token
- **WHEN** o cliente chama qualquer endpoint de `/api/absences`, `/api/holidays` ou `/api/people/{id}/absences` sem `Authorization`
- **THEN** a resposta é `401` em Problem Details
