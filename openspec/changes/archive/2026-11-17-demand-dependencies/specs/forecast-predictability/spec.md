# Spec Delta

## MODIFIED Requirements

### Requirement: Um só motivo, na ordem de precedência
Quando mais de um motivo se aplica, a demanda SHALL receber o primeiro da ordem `MissingStartDate`, `MissingEstimate`, `NoCapacity`, `PrerequisiteUnpredictable` (RF-10, RF-18).

#### Scenario: Sem data de início e sem estimativa
- **WHEN** a demanda não tem data de início e também falta a estimativa de um time
- **THEN** o motivo informado é `MissingStartDate`

#### Scenario: Sem estimativa e sem pessoa
- **WHEN** a demanda tem data de início, falta a estimativa de um time e a parte de outro time não tem pessoa
- **THEN** o motivo informado é `MissingEstimate`

#### Scenario: Problema próprio vence o do pré-requisito
- **WHEN** a demanda não tem data de início e o pré-requisito dela também não é previsível
- **THEN** o motivo informado é `MissingStartDate`, e não `PrerequisiteUnpredictable`

## ADDED Requirements

### Requirement: Pré-requisito não previsível torna a dependente não previsível
Uma demanda não concluída em que algum pré-requisito, direto ou indireto, é não previsível SHALL ser não previsível com o motivo `PrerequisiteUnpredictable`, e SHALL NOT receber datas previstas nem consumir capacidade (RF-10, RF-18; regra inviolável 5).

#### Scenario: Pré-requisito sem estimativa
- **WHEN** B depende de A e falta a estimativa de um time de A
- **THEN** A é não previsível com `MissingEstimate` e B é não previsível com `PrerequisiteUnpredictable`

#### Scenario: Propagação em cadeia
- **WHEN** C depende de B, B depende de A e A é não previsível
- **THEN** B e C são não previsíveis com `PrerequisiteUnpredictable`

#### Scenario: Pré-requisito que não termina em cinco anos
- **WHEN** B depende de A e A estoura o horizonte de cinco anos e vira `NoCapacity`
- **THEN** B é não previsível com `PrerequisiteUnpredictable`

#### Scenario: Dado faltante registrado destrava a dependente
- **WHEN** a estimativa que faltava em A passa a constar no snapshot
- **THEN** A e B deixam de ser não previsíveis e recebem previsão

### Requirement: Pré-requisito concluído é cumprido
Uma demanda cujo pré-requisito está concluído SHALL ser classificada sem considerar esse pré-requisito, mesmo que ele não tenha previsão (RF-12, RF-18).

#### Scenario: Concluída não bloqueia
- **WHEN** B depende de A, A está concluída e B tem todos os dados
- **THEN** B é previsível e não espera por A
