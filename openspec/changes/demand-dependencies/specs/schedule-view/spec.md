# Spec Delta

## ADDED Requirements

### Requirement: Pré-requisitos de cada item
Cada item do cronograma SHALL trazer `prerequisites`, a lista dos pré-requisitos da demanda, cada um com `demandId` e `title`, na ordem de prioridade das demandas, e a lista SHALL ser vazia quando não há pré-requisitos. O `unpredictableReason` SHALL poder ser `PrerequisiteUnpredictable` (RF-10, RF-11, RF-18; DT-23).

#### Scenario: Demanda com pré-requisitos
- **WHEN** C depende de A e de B
- **THEN** o item de C traz `prerequisites` com A e B, cada um com id e título

#### Scenario: Demanda sem pré-requisitos
- **WHEN** a demanda não depende de nenhuma outra
- **THEN** o item traz `prerequisites` como lista vazia

#### Scenario: Pré-requisito não previsível
- **WHEN** B depende de A e A é não previsível
- **THEN** o item de B traz `isPredictable` falso, o `unpredictableReason` `PrerequisiteUnpredictable` e as três datas previstas nulas

#### Scenario: Início previsto adiado pelo pré-requisito
- **WHEN** B depende de A e a data de início de B é anterior ao fim previsto de A
- **THEN** o item de B traz `startDate` com a data registrada e `forecastStartDate` posterior ao `forecastEndDate` de A
