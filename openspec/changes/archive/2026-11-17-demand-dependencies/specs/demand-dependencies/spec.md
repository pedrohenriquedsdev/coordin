# Spec Delta

## Purpose

Permite ao gestor registrar que uma demanda só pode começar depois que outra terminar, entre demandas inteiras, e garante que o conjunto de dependências nunca forme um ciclo. É a entrada de dados que o motor usa para não prever datas otimistas demais (RF-18; DT-23).

## ADDED Requirements

### Requirement: Informar os pré-requisitos no agregado da demanda
`POST /api/demands` e `PUT /api/demands/{id}` SHALL aceitar `prerequisiteIds`, a lista dos ids das demandas de que a demanda depende, e `GET` SHALL devolvê-la em cada demanda. A ausência do campo equivale a nenhum pré-requisito, e a lista enviada SHALL substituir a anterior (RF-18; DT-17, DT-23).

#### Scenario: Cadastrar com pré-requisito
- **WHEN** o gestor cadastra a demanda B com `prerequisiteIds` contendo o id de A
- **THEN** a resposta `201` traz `prerequisiteIds` com o id de A e a consulta seguinte de B devolve o mesmo

#### Scenario: Corpo sem o campo
- **WHEN** o corpo não traz `prerequisiteIds`
- **THEN** a demanda fica sem pré-requisitos e as dependências que ela tinha, se for uma alteração, são removidas

#### Scenario: Trocar os pré-requisitos
- **WHEN** B depende de A e o gestor envia `PUT` com `prerequisiteIds` contendo só C
- **THEN** B passa a depender só de C

### Requirement: Validação dos pré-requisitos
A API SHALL responder `400` com o erro no campo `prerequisiteIds` quando a lista tiver um id que não corresponde a nenhuma demanda ou um id repetido (RF-18).

#### Scenario: Demanda inexistente
- **WHEN** `prerequisiteIds` contém um id que não existe
- **THEN** a resposta é `400` em Problem Details com o erro no campo `prerequisiteIds` e nada é gravado

#### Scenario: Id repetido
- **WHEN** `prerequisiteIds` contém o mesmo id duas vezes
- **THEN** a resposta é `400` em Problem Details com o erro no campo `prerequisiteIds`

### Requirement: Dependência que forma ciclo é rejeitada
A API SHALL rejeitar com `409` e o `code` `dependency-cycle` toda gravação cujas dependências formem um ciclo, direto, indireto ou de uma demanda com ela mesma, com uma mensagem em português que cite os títulos das demandas do ciclo em ordem (RF-18; DT-18, DT-23). Nada SHALL ser gravado.

#### Scenario: Ciclo direto
- **WHEN** B depende de A e o gestor altera A para depender de B
- **THEN** a resposta é `409` com `code` `dependency-cycle` e a mensagem cita os títulos, como "A → B → A"

#### Scenario: Ciclo indireto
- **WHEN** C depende de B, B depende de A e o gestor altera A para depender de C
- **THEN** a resposta é `409` com `code` `dependency-cycle` e as dependências de A continuam como estavam

#### Scenario: Demanda dependendo dela mesma
- **WHEN** o gestor altera A com o próprio id de A em `prerequisiteIds`
- **THEN** a resposta é `409` com `code` `dependency-cycle`

#### Scenario: Cadeia sem ciclo
- **WHEN** C depende de B e B depende de A
- **THEN** a gravação é aceita

### Requirement: Pré-requisito concluído é aceito
Uma demanda SHALL poder ter como pré-requisito uma demanda já concluída, que conta como cumprida. A demanda concluída continua travada: suas próprias dependências NÃO SHALL ser alteradas (RF-12, RF-18).

#### Scenario: Depender de uma concluída
- **WHEN** o gestor cadastra B com A, já concluída, em `prerequisiteIds`
- **THEN** a gravação é aceita

#### Scenario: Alterar as dependências de uma concluída
- **WHEN** o gestor envia `PUT` para uma demanda concluída com outros pré-requisitos
- **THEN** a resposta é `409` com `code` `completed-demand-locked`

### Requirement: Pré-requisito não é excluído
`DELETE /api/demands/{id}` SHALL responder `409` com o `code` `demand-has-dependents` se a demanda é pré-requisito de outra, com uma mensagem que cite os títulos das dependentes. Excluir uma demanda SHALL remover as dependências em que ela é a dependente (RF-18; DT-18).

#### Scenario: Excluir um pré-requisito
- **WHEN** B depende de A e o gestor exclui A
- **THEN** a resposta é `409` com `code` `demand-has-dependents`, a mensagem cita B e A continua cadastrada

#### Scenario: Excluir depois de remover a dependência
- **WHEN** o gestor altera B para não depender de A e depois exclui A
- **THEN** a exclusão responde `204`

#### Scenario: Excluir uma dependente
- **WHEN** B depende de A e o gestor exclui B
- **THEN** a resposta é `204` e A não tem mais dependentes

### Requirement: Mudar as dependências recalcula as previsões
Toda gravação que cria, altera ou remove dependências SHALL recalcular e gravar as previsões na mesma transação, como as demais escritas que afetam o cálculo; se qualquer passo falhar, nada SHALL ser gravado (RF-08, RF-18; DT-03, DT-14).

#### Scenario: Dependência adia a dependente
- **WHEN** B tem início em uma data anterior ao fim previsto de A e o gestor faz B depender de A
- **THEN** a previsão gravada de B passa a começar depois do fim previsto de A, na mesma resposta de gravação

#### Scenario: Remover a dependência
- **WHEN** o gestor remove o pré-requisito de B
- **THEN** a previsão de B volta a depender só da sua data de início e da capacidade
