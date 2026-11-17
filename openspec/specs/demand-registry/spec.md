# demand-registry Specification

## Purpose
Permite ao gestor cadastrar e manter as demandas do setor, com seus times, estimativas por time, pessoas alocadas, datas e ordem de prioridade, e registrar a conclusão real. É a entrada de dados do cálculo de previsão (RF-04 a RF-07, RF-12; DT-11 a DT-13, DT-17).

## Requirements

### Requirement: Cadastrar e consultar demandas
A API SHALL expor `POST /api/demands`, que recebe a demanda como um agregado (título, datas, times com estimativa e pessoas alocadas) e responde `201` com `Location` e a demanda criada, `GET /api/demands`, que lista todas ordenadas pela prioridade, e `GET /api/demands/{id}` (RF-04; DT-17).

#### Scenario: Cadastrar uma demanda completa
- **WHEN** o gestor envia título "Novo checkout", `startDate`, `informedEndDate`, dois times (80 h e 40 h) e uma pessoa de cada time
- **THEN** a resposta é `201` com `Location`, o `id`, `priorityRank`, `executionRank`, os times com estimativa e as pessoas alocadas
- **AND** `GET /api/demands/{id}` devolve a mesma demanda

#### Scenario: Demanda sem estimativa
- **WHEN** o gestor cadastra uma demanda com um time e `estimatedEffortHours` nulo, sem data de início e sem pessoas
- **THEN** a resposta é `201` e a demanda é aceita (regra inviolável 5)

#### Scenario: Demanda sem times
- **WHEN** o gestor cadastra uma demanda só com o título
- **THEN** a resposta é `201`, com a lista de times vazia

#### Scenario: Listar na ordem de prioridade
- **WHEN** há três demandas cadastradas
- **THEN** `GET /api/demands` as devolve ordenadas por `priorityRank`

#### Scenario: Demanda inexistente
- **WHEN** o gestor consulta, altera ou exclui um `id` que não existe
- **THEN** a resposta é `404`

### Requirement: Validação do corpo da demanda
O título SHALL ser obrigatório (até 200 caracteres, sem espaços nas pontas), cada time só pode aparecer uma vez, o esforço informado SHALL ser maior que zero com até duas casas decimais, e `teamId` e `assignedPersonIds` SHALL existir (RF-06; DT-18).

#### Scenario: Título vazio
- **WHEN** o título é vazio ou só espaços
- **THEN** a resposta é `400` em Problem Details com o erro no campo `title`

#### Scenario: Esforço zero ou negativo
- **WHEN** um time vem com `estimatedEffortHours` igual a 0 ou -5
- **THEN** a resposta é `400` com mensagem clara em português e a demanda não é gravada

#### Scenario: Time repetido
- **WHEN** o mesmo `teamId` aparece duas vezes nos times
- **THEN** a resposta é `400`

#### Scenario: Time ou pessoa inexistente
- **WHEN** o corpo cita um `teamId` ou uma pessoa que não existem
- **THEN** a resposta é `400` e nada é gravado

### Requirement: Pessoa alocada precisa ser de um time da demanda
Cada pessoa em `assignedPersonIds` SHALL pertencer a um dos times da demanda; caso contrário a resposta SHALL ser `409` com o `code` `person-not-in-demand-teams` (RF-05; DT-12, DT-18).

#### Scenario: Pessoa de outro time
- **WHEN** a demanda envolve só o time A e a pessoa alocada é do time B
- **THEN** a resposta é `409` com `code` `person-not-in-demand-teams` e nada é gravado

### Requirement: Ordem de prioridade única e contínua
Cada demanda SHALL ter um `priorityRank` único, contínuo a partir de 1. A nova demanda SHALL entrar no fim da ordem de prioridade e no fim da ordem de execução, a menos que o corpo traga `position` (de 1 a `n + 1`, com `n` o número de demandas): então SHALL entrar nessa posição nas duas ordens, e as demandas dessa posição em diante SHALL descer uma posição (RF-04, RF-16; DT-13).

#### Scenario: Novas demandas entram no fim
- **WHEN** o gestor cadastra três demandas em sequência
- **THEN** elas recebem `priorityRank` 1, 2 e 3 e `executionRank` 1, 2 e 3

#### Scenario: Cadastros simultâneos
- **WHEN** 10 demandas são cadastradas ao mesmo tempo
- **THEN** os rankings resultantes são exatamente 1 a 10, sem repetição e sem falha

#### Scenario: Nova demanda na posição escolhida
- **WHEN** há três demandas e o gestor cadastra outra com `position` 2
- **THEN** a nova recebe `priorityRank` 2 e `executionRank` 2, e as duas últimas antigas passam a 3 e 4 nas duas ordens

#### Scenario: Posição fora do intervalo
- **WHEN** o `position` é 0 ou maior que `n + 1`
- **THEN** a resposta é `400` com o erro no campo `position` e nada é gravado

### Requirement: Reordenar a prioridade
`PUT /api/demands/priority-order` SHALL receber a lista completa dos ids das demandas na nova ordem e gravar os `priorityRank` correspondentes. A lista SHALL conter todas as demandas, sem repetição e sem ids desconhecidos (RF-04).

#### Scenario: Trocar a ordem
- **WHEN** o gestor envia os três ids na ordem inversa
- **THEN** a resposta é `204` e `GET /api/demands` devolve a ordem nova com ranks 1, 2 e 3

#### Scenario: Lista incompleta, repetida ou com id desconhecido
- **WHEN** a lista omite uma demanda, repete um id ou traz um id que não existe
- **THEN** a resposta é `400` e a ordem não muda

#### Scenario: A prioridade manda na execução por padrão
- **WHEN** o modo de ordem é `FollowPriority` e o gestor reordena a prioridade
- **THEN** o `executionRank` de cada demanda passa a ser igual ao seu novo `priorityRank`

### Requirement: Alterar uma demanda
`PUT /api/demands/{id}` SHALL substituir o agregado inteiro (dados, times com estimativa e pessoas alocadas), com as mesmas validações da criação, sem mudar os rankings (RF-05, RF-06, RF-07).

#### Scenario: Registrar a estimativa que faltava
- **WHEN** o gestor altera a demanda trocando `estimatedEffortHours` nulo por 40
- **THEN** a resposta é `200` com a demanda alterada

#### Scenario: Remover um time
- **WHEN** o gestor envia a demanda sem um dos times que ela tinha
- **THEN** a parte e as alocações das pessoas desse time deixam de existir

### Requirement: Excluir uma demanda
`DELETE /api/demands/{id}` SHALL excluir uma demanda não concluída, com suas partes, alocações e previsão, e SHALL recompactar os rankings de prioridade e de execução (TDD 4.4).

#### Scenario: Excluir a do meio
- **WHEN** há as demandas 1, 2 e 3 e o gestor exclui a 2
- **THEN** a resposta é `204` e as restantes ficam com os rankings 1 e 2

### Requirement: Registrar a conclusão real
`PUT /api/demands/{id}/completion` SHALL receber `completedOn` (data de calendário) e marcar a demanda como concluída. A data SHALL NOT ser anterior à data de início da demanda, quando ela existir (RF-12).

#### Scenario: Concluir uma demanda
- **WHEN** o gestor envia `completedOn` igual ou posterior ao `startDate`
- **THEN** a resposta é `200` com a demanda e `completedOn`

#### Scenario: Conclusão antes do início
- **WHEN** `completedOn` é anterior ao `startDate`
- **THEN** a resposta é `400` com o erro no campo `completedOn` e a demanda não é concluída

#### Scenario: Data de calendário sem fuso
- **WHEN** o gestor conclui uma demanda com `completedOn` "2026-12-31"
- **THEN** a consulta seguinte devolve exatamente "2026-12-31", em qualquer fuso do servidor

### Requirement: Demanda concluída fica travada
Uma demanda concluída SHALL NOT ser alterada nem excluída nem concluída de novo: `PUT /api/demands/{id}`, `DELETE /api/demands/{id}` e `PUT /api/demands/{id}/completion` SHALL responder `409` com o `code` `completed-demand-locked` (TDD 4.4; DT-18).

#### Scenario: Alterar ou excluir uma concluída
- **WHEN** o gestor tenta alterar ou excluir uma demanda concluída
- **THEN** a resposta é `409` com `code` `completed-demand-locked`

#### Scenario: Concluir duas vezes
- **WHEN** o gestor envia a conclusão de uma demanda já concluída
- **THEN** a resposta é `409` com `code` `completed-demand-locked`

### Requirement: Todas as rotas exigem o gestor autenticado
Todos os endpoints de demandas SHALL responder `401` sem token válido, pela política padrão (RNF-04).

#### Scenario: Sem token
- **WHEN** qualquer rota de `/api/demands` é chamada sem `Authorization`
- **THEN** a resposta é `401`
