# forecast-predictability Specification

## Purpose
Decide, para cada demanda, se o sistema consegue prever a entrega ou se faltam dados, e dizer qual é o motivo. Assim demandas sem estimativa, sem data de início ou sem capacidade aparecem como "não previsíveis" em vez de receber uma data inventada (RF-06, RF-10; regra inviolável 5).

## Requirements

### Requirement: Demanda concluída fica fora da previsão
Uma demanda já concluída SHALL ser tratada como concluída, sem previsão, e SHALL NOT consumir capacidade de ninguém (RF-10, RF-12).

#### Scenario: Demanda concluída
- **WHEN** o snapshot traz uma demanda marcada como concluída
- **THEN** o resultado a classifica como concluída, sem datas previstas
- **AND** nenhuma hora de nenhuma pessoa é consumida por ela

### Requirement: Demanda sem data de início
Uma demanda não concluída sem data de início SHALL ser não previsível com o motivo `MissingStartDate` (RF-07, RF-10).

#### Scenario: Sem data de início
- **WHEN** a demanda tem esforço estimado em todos os times, mas não tem data de início
- **THEN** ela é não previsível com o motivo `MissingStartDate` e sem data prevista

### Requirement: Demanda sem estimativa de algum time
Uma demanda não concluída em que algum time alocado ainda não estimou o esforço SHALL ser não previsível com o motivo `MissingEstimate` (RF-06, RF-10; regra inviolável 5).

#### Scenario: Um dos dois times sem estimativa
- **WHEN** a demanda envolve os times A (80 h) e B (sem estimativa)
- **THEN** ela é não previsível com o motivo `MissingEstimate`

#### Scenario: Demanda sem nenhuma estimativa cadastrada
- **WHEN** a demanda foi cadastrada sem estimativa de nenhum time
- **THEN** ela é aceita e tratada como não previsível com o motivo `MissingEstimate`, sem erro

#### Scenario: Demanda sem times
- **WHEN** a demanda não envolve nenhum time
- **THEN** ela é não previsível com o motivo `MissingEstimate`

### Requirement: Parte sem capacidade
Uma demanda não concluída em que alguma parte por time não tem pessoa alocada, ou cujas pessoas alocadas têm todas capacidade diária zero, SHALL ser não previsível com o motivo `NoCapacity` (RF-02, RF-10).

#### Scenario: Parte sem pessoa alocada
- **WHEN** o time A tem 40 h estimadas e nenhuma pessoa alocada à demanda
- **THEN** a demanda é não previsível com o motivo `NoCapacity`

#### Scenario: Pessoa alocada com capacidade zero
- **WHEN** a única pessoa alocada à parte do time tem capacidade diária zero
- **THEN** a demanda é não previsível com o motivo `NoCapacity`

#### Scenario: Capacidade perdida só por ausência
- **WHEN** a pessoa alocada tem capacidade diária maior que zero, mas está ausente durante um período
- **THEN** a demanda continua previsível, e o fim é empurrado pelos dias perdidos

### Requirement: Um só motivo, na ordem de precedência
Quando mais de um motivo se aplica, a demanda SHALL receber o primeiro da ordem `MissingStartDate`, `MissingEstimate`, `NoCapacity` (RF-10).

#### Scenario: Sem data de início e sem estimativa
- **WHEN** a demanda não tem data de início e também falta a estimativa de um time
- **THEN** o motivo informado é `MissingStartDate`

#### Scenario: Sem estimativa e sem pessoa
- **WHEN** a demanda tem data de início, falta a estimativa de um time e a parte de outro time não tem pessoa
- **THEN** o motivo informado é `MissingEstimate`

### Requirement: Não previsível não consome capacidade
Uma demanda não previsível SHALL NOT consumir capacidade de ninguém, e SHALL NOT atrasar nem alterar a previsão das demais (RF-10; regra inviolável 5).

#### Scenario: Demanda não previsível antes de uma previsível
- **WHEN** a demanda 1, não previsível, vem antes da demanda 2 na ordem de execução e ambas usam a mesma pessoa
- **THEN** a previsão da demanda 2 é a mesma que seria se a demanda 1 não existisse

### Requirement: Dado faltante registrado torna a demanda previsível
Quando o dado que faltava passa a constar no snapshot, a demanda SHALL deixar de ser não previsível e SHALL receber previsão (RF-10).

#### Scenario: Estimativa chega depois
- **WHEN** o time B informa 40 h para uma demanda que estava `MissingEstimate`
- **THEN** o novo cálculo a trata como previsível e lhe dá datas previstas

### Requirement: Dados inválidos são rejeitados
O motor SHALL rejeitar com erro de argumento um snapshot com esforço menor ou igual a zero em uma estimativa informada, percentual de margem negativo ou referências inexistentes (por exemplo, uma alocação para uma pessoa que não está no snapshot) (RNF-08).

#### Scenario: Esforço zero
- **WHEN** uma parte traz esforço estimado de 0 h
- **THEN** o motor lança erro de argumento, em vez de tratar a demanda como previsível

#### Scenario: Margem negativa
- **WHEN** o percentual da margem é negativo
- **THEN** o motor lança erro de argumento
