# Spec Delta

## MODIFIED Requirements

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
