# Spec Delta

## MODIFIED Requirements

### Requirement: Demanda não previsível mostra o motivo
A demanda não previsível SHALL aparecer sem datas previstas e com o motivo em português (RF-10, RF-18).

#### Scenario: Os três motivos
- **WHEN** o motivo é `MissingEstimate`, `MissingStartDate` ou `NoCapacity`
- **THEN** a tela mostra, respectivamente, "Falta a estimativa de algum time", "Sem data de início" e "Sem capacidade: falta pessoa alocada ou a capacidade é zero"

#### Scenario: Motivo vindo do pré-requisito
- **WHEN** o motivo é `PrerequisiteUnpredictable`
- **THEN** a tela mostra "Depende de uma demanda que não tem previsão"

#### Scenario: Sem datas inventadas
- **WHEN** a demanda não é previsível
- **THEN** os campos de previsão mostram "—" e nenhuma data é exibida

## ADDED Requirements

### Requirement: Pré-requisitos no cronograma
Cada demanda com pré-requisitos SHALL mostrar, na tabela e nos cartões, "Depende de:" com os títulos dos pré-requisitos, e a demanda sem pré-requisitos NÃO SHALL mostrar essa linha (RF-11, RF-18).

#### Scenario: Demanda com pré-requisitos
- **WHEN** a demanda depende de A e de B
- **THEN** a tela mostra "Depende de: A, B" no computador e no celular

#### Scenario: Demanda sem pré-requisitos
- **WHEN** a demanda não depende de nenhuma
- **THEN** nenhuma linha "Depende de" aparece

#### Scenario: Início adiado pelo pré-requisito
- **WHEN** o início previsto é posterior à data de início registrada por causa de um pré-requisito
- **THEN** a tela mostra o início previsto calculado e mantém visível a linha "Depende de"
