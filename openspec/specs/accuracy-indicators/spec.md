# accuracy-indicators Specification

## Purpose
Calcula, para o setor e para cada time, quanto as previsões e as estimativas acertaram nas demandas já concluídas, e permite filtrar pelo período de conclusão (RF-23). O cálculo é feito no motor puro, a partir dos dados gravados na conclusão e nas linhas de base.

## Requirements

### Requirement: Desvio médio de prazo
O sistema SHALL calcular, sobre as demandas concluídas no período, o desvio médio de prazo em dias corridos em relação à primeira previsão (a linha de base) e em relação à previsão vigente na conclusão. O desvio de cada demanda é `CompletedOn − data de referência`, e um valor positivo é atraso (DT-27; RF-23).

#### Scenario: Desvio médio contra a linha de base
- **WHEN** duas demandas concluídas com desvios de +4 e −2 dias em relação à linha de base
- **THEN** o desvio médio contra a linha de base é +1 dia

#### Scenario: Desvio médio contra a previsão da conclusão
- **WHEN** a mesma demanda é concluída antes da previsão vigente na conclusão
- **THEN** o desvio contra a previsão da conclusão é negativo

### Requirement: Percentual de demandas concluídas até a data prevista
O sistema SHALL calcular o percentual de demandas concluídas no período cuja data de conclusão é igual ou anterior à primeira previsão (linha de base). A referência é sempre a primeira previsão, nunca a vigente.

#### Scenario: Percentual com uma demanda atrasada
- **WHEN** três demandas são concluídas no período e uma delas depois da linha de base
- **THEN** o percentual no prazo é de 66,7%

#### Scenario: Demanda concluída sem linha de base
- **WHEN** uma demanda concluída não tem linha de base gravada
- **THEN** ela fica de fora do percentual e a API informa quantas ficaram de fora

### Requirement: Diferença média entre estimado e real
O sistema SHALL calcular a diferença média, em percentual, entre o esforço estimado vigente na conclusão e o esforço real, somente para as demandas com esforço real registrado (RF-22). A diferença de cada demanda é `(real − estimado) / estimado`, e o indicador é a média dessas diferenças, em que cada demanda pesa igual.

*Premissa aprovada pelo autor em 2026-11-18:* média simples por demanda, não soma total. Motivo: uma demanda grande não deve esconder as pequenas.

#### Scenario: Demanda gastou mais do que o estimado
- **WHEN** uma demanda estimada em 40 horas foi concluída com 50 horas reais
- **THEN** a diferença dela é +25%

#### Scenario: Demanda sem esforço real
- **WHEN** uma demanda concluída não tem esforço real registrado
- **THEN** ela fica de fora desse indicador, sem erro

### Requirement: Indicadores por setor e por time
Os indicadores SHALL ser apresentados para o setor (todas as demandas concluídas no período) e para cada time. Uma demanda com vários times entra nos indicadores de cada time envolvido, usando o esforço total estimado e o esforço real da demanda, porque o esforço real é registrado para a demanda como um todo (RF-22).

*Premissa aprovada pelo autor em 2026-11-18:* o indicador por time usa o total da demanda, não a parte do time, para comparar com o real.

#### Scenario: Demanda com dois times
- **WHEN** uma demanda concluída envolve os times "Alfa" e "Beta"
- **THEN** ela entra nos indicadores de "Alfa", de "Beta" e do setor, uma vez em cada

### Requirement: Filtro por período de conclusão
O gestor SHALL filtrar os indicadores por período de conclusão, com data inicial e final inclusive. Uma data inicial posterior à final SHALL ser recusada com erro de campo.

#### Scenario: Período com demandas
- **WHEN** o gestor filtra de 2027-01-01 a 2027-01-31
- **THEN** só entram as demandas concluídas nessas datas

#### Scenario: Período invertido
- **WHEN** a data inicial é posterior à final
- **THEN** a API recusa com erro de campo

### Requirement: Times com diferença recorrente evidenciados
Na comparação entre times, o sistema SHALL evidenciar os times cuja diferença média entre estimado e real, em valor absoluto, for maior que a do setor.

*Premissa aprovada pelo autor em 2026-11-18:* a regra "maior que o setor" substitui um limite fixo, que o PRD não define. A evidência aparece no gráfico e na tabela, com o texto, e não só com cor.

#### Scenario: Time acima do setor
- **WHEN** o time "Alfa" tem diferença média de +30% e o setor tem +10%
- **THEN** "Alfa" aparece evidenciado na comparação, com o texto "acima do setor"

#### Scenario: Nenhum time acima do setor
- **WHEN** nenhum time tem diferença absoluta maior que a do setor
- **THEN** nenhum time aparece evidenciado

### Requirement: Demandas concluídas antes desta change
As demandas concluídas antes desta change não têm previsão nem esforço de conclusão gravados. Elas SHALL ser contadas separadamente, e a API SHALL informar quantas ficaram de fora de cada indicador.

#### Scenario: Período com demandas antigas
- **WHEN** o período inclui demandas concluídas antes desta change
- **THEN** a resposta informa quantas ficaram de fora e os indicadores usam só as demais
