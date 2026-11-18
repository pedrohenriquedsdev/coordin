## Purpose

Permite registrar, ao concluir uma demanda, quanto esforço ela realmente consumiu, e grava a previsão e o esforço estimado vigentes naquele momento, para que os indicadores de precisão comparem o que foi previsto com o que aconteceu (RF-22; DT-27).

## ADDED Requirements

### Requirement: Esforço real opcional na conclusão
Ao registrar a conclusão de uma demanda, o gestor SHALL poder informar `actualEffortHours`, as horas realmente gastas na demanda como um todo (não por time). O campo é opcional: a conclusão SHALL ser aceita sem ele. Quando informado, o valor SHALL ser maior que zero (RF-22).

#### Scenario: Conclusão com esforço real
- **WHEN** o gestor conclui uma demanda informando 52 horas reais
- **THEN** a demanda fica concluída com 52 horas de esforço real gravadas

#### Scenario: Conclusão sem esforço real
- **WHEN** o gestor conclui uma demanda sem informar horas
- **THEN** a conclusão é aceita e o esforço real fica vazio

#### Scenario: Esforço real zero ou negativo
- **WHEN** o gestor informa zero ou um valor negativo de horas reais
- **THEN** a API recusa com erro de campo em `actualEffortHours` e nada é gravado

### Requirement: Previsão e estimativa vigentes na conclusão
Na conclusão, o sistema SHALL gravar na própria demanda a data prevista vigente (`ForecastEndDateAtCompletion`) e o esforço estimado vigente (`EstimatedEffortHoursAtCompletion`, a soma das estimativas dos times), calculados depois da escrita da conclusão e do recálculo na mesma transação. Esses valores SHALL NOT mudar depois da conclusão, mesmo que a previsão da demanda seja recalculada.

#### Scenario: Valores gravados na conclusão
- **WHEN** uma demanda com previsão 2027-03-10 e estimativas de 40 e 20 horas é concluída
- **THEN** a demanda guarda a previsão 2027-03-10 e o esforço estimado de 60 horas

#### Scenario: Previsão muda depois da conclusão
- **WHEN** uma escrita posterior altera a previsão de uma demanda já concluída
- **THEN** os valores gravados na conclusão permanecem os mesmos

### Requirement: Conclusão com esforço e previsão só uma vez
A conclusão SHALL gravar os valores de RF-22 uma única vez. Uma demanda já concluída continua respondendo 409 para nova conclusão, como hoje.

#### Scenario: Segunda conclusão
- **WHEN** o gestor tenta concluir uma demanda já concluída
- **THEN** a API responde 409 e os valores gravados não mudam

### Requirement: Compatibilidade com demandas concluídas antes desta change
As demandas concluídas antes desta change não têm os valores de RF-22 gravados. Elas SHALL continuar aparecendo normalmente na lista e no cronograma, e SHALL ficar de fora dos indicadores que dependem desses valores, sem erro.

#### Scenario: Demanda antiga concluída
- **WHEN** uma demanda concluída antes desta change é consultada
- **THEN** a resposta traz o esforço real e os valores de conclusão vazios, sem erro
