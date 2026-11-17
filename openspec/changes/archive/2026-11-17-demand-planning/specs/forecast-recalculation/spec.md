# Spec Delta

## Purpose

Garante que as previsões gravadas no sistema sempre correspondam a um cálculo do motor sobre os dados atuais, e preserva a linha de base de cada demanda. Assim o cronograma é rápido de consultar e o desvio previsto × realizado é confiável (RF-08, RF-12; DT-03, DT-14).

## ADDED Requirements

### Requirement: Recalcular na mesma transação da escrita
Toda escrita que afeta o cálculo SHALL recalcular as previsões de todas as demandas com o motor e gravá-las na mesma transação da escrita; se qualquer parte falhar, nada SHALL ser gravado (DT-03).

#### Scenario: Previsão aparece com a demanda
- **WHEN** o gestor cadastra uma demanda com início, estimativa e pessoa alocada
- **THEN** logo em seguida a previsão dela está gravada, com `forecastEndDate` igual ao resultado do motor para os mesmos dados

#### Scenario: Escrita rejeitada não deixa previsão
- **WHEN** uma escrita é rejeitada por validação ou por conflito
- **THEN** as previsões gravadas continuam exatamente as mesmas de antes

#### Scenario: Escritas que recalculam
- **WHEN** o gestor altera uma demanda (estimativa, pessoas, datas), reordena a prioridade, exclui uma demanda, conclui uma demanda, muda a margem, altera a capacidade ou o time de uma pessoa, ou registra, altera ou remove uma ausência ou um feriado
- **THEN** as previsões gravadas passam a refletir um novo cálculo

### Requirement: Previsão persistida por demanda
O sistema SHALL guardar, para cada demanda, se é previsível, o motivo quando não for, as datas previstas (início, fim sem margem e fim com margem) e o instante do cálculo, e SHALL manter `forecastEndDate` igual ou posterior ao fim sem margem (RF-08, RF-09, RF-10; regra inviolável 1).

#### Scenario: Demanda previsível
- **WHEN** a demanda tem início, estimativa de todos os times e pessoas alocadas com capacidade
- **THEN** a previsão guardada tem as três datas, e o fim com margem não é anterior ao fim sem margem

#### Scenario: Demanda não previsível
- **WHEN** falta a estimativa de um time, a data de início ou a capacidade
- **THEN** a previsão guardada diz que não é previsível, com o motivo `MissingEstimate`, `MissingStartDate` ou `NoCapacity`, sem datas

#### Scenario: Dado faltante registrado
- **WHEN** o gestor registra a estimativa que faltava
- **THEN** a demanda passa a ser previsível e recebe as datas

### Requirement: Linha de base gravada uma única vez
A primeira previsão calculada de uma demanda quando ela se torna previsível SHALL ser gravada como linha de base (`baselineEndDate`) e NUNCA SHALL ser alterada, por nenhum recálculo posterior (RF-12; DT-14).

#### Scenario: Replanejamento
- **WHEN** a demanda foi prevista para o dia 10 e depois uma ausência a empurra para o dia 15
- **THEN** o `forecastEndDate` passa a ser o dia 15 e o `baselineEndDate` continua o dia 10

#### Scenario: Demanda que volta a ser não previsível
- **WHEN** a demanda perde a estimativa depois de ter linha de base
- **THEN** a linha de base é preservada

#### Scenario: Linha de base só quando previsível
- **WHEN** a demanda ainda é não previsível
- **THEN** não há linha de base, e ela é gravada no primeiro cálculo em que a demanda se torna previsível

### Requirement: Concluída não consome capacidade e tem a previsão congelada
Uma demanda concluída SHALL ser tratada pelo motor como concluída (sem consumir capacidade das demais) e a sua previsão guardada SHALL ficar congelada no valor vigente no momento da conclusão (RF-10, RF-12; DT-27).

#### Scenario: Concluir libera a capacidade
- **WHEN** a demanda 1 está antes da 2 na fila da mesma pessoa e o gestor conclui a 1
- **THEN** a previsão da 2 é recalculada sem as horas da 1

#### Scenario: Previsão congelada
- **WHEN** uma demanda é concluída e depois outros dados mudam
- **THEN** a previsão guardada dela (`forecastEndDate` e linha de base) não muda

### Requirement: Cálculo igual ao do motor
As datas guardadas SHALL ser exatamente as devolvidas por `ForecastEngine.Calculate` sobre o planejamento carregado do banco (times, pessoas, ausências, feriados, demandas, alocações, modo de ordem e margem), sem regra de cálculo fora do motor (RNF-02, RNF-08; DT-02).

#### Scenario: Mesmo resultado do motor
- **WHEN** um planejamento com vários times, uma ausência e um feriado é gravado pela API
- **THEN** as previsões guardadas coincidem com as do motor calculado a partir do mesmo planejamento montado à mão

#### Scenario: Alocação de demanda concluída
- **WHEN** uma pessoa alocada a uma demanda concluída muda de time
- **THEN** o recálculo continua funcionando, sem erro de snapshot inválido
