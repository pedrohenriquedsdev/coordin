# team-report-calculation Specification

## Purpose
Calcula, para cada time e cada semana, a capacidade disponível, as horas alocadas, a ocupação, a ociosidade e a sobrecarga, a partir do mesmo consumo diário que gera a previsão. Mostra onde há capacidade livre e onde há trabalho esperando sem capacidade (RF-17; DT-24).

## Requirements

### Requirement: Semanas completas, de segunda a domingo
O relatório SHALL cobrir semanas completas de segunda a domingo: o início do período vira a segunda-feira da sua semana e o fim vira o domingo da sua semana. Um período inválido (fim anterior ao início) SHALL ser recusado (DT-24).

#### Scenario: Período no meio da semana
- **WHEN** o período vai de uma quarta-feira a uma terça-feira da semana seguinte
- **THEN** o relatório cobre duas semanas completas, da segunda da primeira ao domingo da segunda

#### Scenario: Uma semana só
- **WHEN** o início e o fim estão na mesma semana
- **THEN** o relatório tem uma única semana

#### Scenario: Fim antes do início
- **WHEN** o fim é anterior ao início
- **THEN** o cálculo recusa o período com erro de argumento

### Requirement: Capacidade do time por semana
A capacidade de um time em uma semana SHALL ser a soma, dia a dia, da capacidade das pessoas do time, descontando fins de semana, feriados e ausências (RF-02, RF-03; DT-10).

#### Scenario: Time de duas pessoas
- **WHEN** o time tem duas pessoas de 8 h por dia e a semana tem cinco dias úteis
- **THEN** a capacidade da semana é 80 h

#### Scenario: Ausência e feriado
- **WHEN** uma pessoa de 8 h está ausente dois dias da semana e há um feriado na quarta
- **THEN** a capacidade dela naquela semana é 8 h × 2 dias, e o feriado não conta para ninguém

#### Scenario: Time sem pessoas
- **WHEN** o time não tem pessoas
- **THEN** a capacidade de toda semana é zero

### Requirement: Horas alocadas
As horas alocadas de um time em uma semana SHALL ser as horas que as pessoas do time dedicaram, na semana, às partes de demandas previsíveis, conforme o consumo da simulação. Demandas não previsíveis e demandas concluídas NÃO SHALL contar (RF-10, RF-17).

#### Scenario: Trabalho previsto na semana
- **WHEN** a simulação consome 40 h do time na semana
- **THEN** as horas alocadas da semana são 40

#### Scenario: Não previsível não consome
- **WHEN** o time tem uma demanda não previsível
- **THEN** ela não soma horas alocadas

### Requirement: Ocupação
A ocupação SHALL ser as horas alocadas divididas pela capacidade da semana, e SHALL ser ausente (nula) quando a capacidade é zero (RF-17).

#### Scenario: Ocupação parcial
- **WHEN** a capacidade é 80 h e as horas alocadas são 60 h
- **THEN** a ocupação é 0,75

#### Scenario: Capacidade zero
- **WHEN** a capacidade da semana é zero
- **THEN** a ocupação é nula, sem erro de divisão

### Requirement: Ociosidade
A ociosidade SHALL ser a capacidade menos as horas alocadas, e NUNCA SHALL ser negativa. Um time sem nenhuma demanda SHALL ter toda a sua capacidade ociosa (RF-17; DT-24).

#### Scenario: Capacidade livre
- **WHEN** a capacidade é 80 h e as horas alocadas são 60 h
- **THEN** a ociosidade é 20 h

#### Scenario: Time sem demandas
- **WHEN** o time não tem partes de demandas
- **THEN** a ociosidade de cada semana é igual à capacidade da semana

### Requirement: Sobrecarga
Uma semana SHALL ser marcada como sobrecarregada para o time quando a capacidade é maior que zero, **toda** a capacidade foi usada (horas alocadas iguais à capacidade) e, ao fim da semana, ainda existe pelo menos uma parte do time **elegível** (demanda previsível já iniciada) com esforço por fazer. O relatório SHALL listar essas demandas (RF-17; DT-24, definição do autor de 2026-11-16).

#### Scenario: Fila maior que a capacidade
- **WHEN** o time tem 40 h por semana e 100 h de partes já iniciadas
- **THEN** a primeira semana é sobrecarregada, com a demanda na lista, e a última (que termina o trabalho com folga) não é

#### Scenario: Trabalho em andamento com capacidade sobrando
- **WHEN** o time tem 40 h por semana, aloca 30 h e termina a parte no meio da semana seguinte, sem fila
- **THEN** nenhuma semana é marcada como sobrecarregada

#### Scenario: Capacidade usada e nada esperando
- **WHEN** as horas alocadas igualam a capacidade, mas todas as partes terminaram na semana
- **THEN** a semana não é sobrecarregada

#### Scenario: Demanda ainda não iniciada
- **WHEN** a data de início da demanda é depois do fim da semana
- **THEN** ela não conta como esperando naquela semana

### Requirement: Demandas não previsíveis do time
O relatório SHALL informar, para cada time, quantas demandas não concluídas e não previsíveis têm parte do time (RF-10, RF-17).

#### Scenario: Contagem
- **WHEN** duas demandas não previsíveis envolvem o time e uma previsível também
- **THEN** a contagem do time é 2

### Requirement: Determinístico e coerente
O resultado SHALL ser idêntico para o mesmo planejamento, qualquer que seja a ordem das listas de entrada, e SHALL satisfazer, em toda semana, `horas alocadas <= capacidade` e `horas alocadas + ociosidade = capacidade` (RNF-02, RNF-08; DT-02).

#### Scenario: Invariantes em planejamentos gerados
- **WHEN** centenas de planejamentos gerados com semente fixa são calculados
- **THEN** em toda semana de todo time valem as duas igualdades e a ocupação nunca passa de 1

#### Scenario: Listas embaralhadas
- **WHEN** o mesmo planejamento é calculado com as listas em outras ordens
- **THEN** os relatórios são idênticos
