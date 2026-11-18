# Spec Delta

## Purpose

Calcula, no motor puro e de forma reprodutível, a comparação entre o planejamento do processo atual e o planejamento com o Coordin: eficiência, desvio das datas e a checagem das demandas mais prioritárias (RF-24; PRD 7.1 a 7.3; DT-37).

## ADDED Requirements

### Requirement: Fator de esforço reproduzível por demanda
O fator de esforço de cada demanda SHALL ser `min + (max − min) × u`, com `u` em [0, 1) gerado por um gerador pseudoaleatório próprio (SplitMix64) a partir da semente combinada com um hash estável do id da demanda. O fator SHALL NOT depender da ordem das demandas, do relógio nem de `System.Random`.

#### Scenario: Mesma semente, mesmo fator
- **WHEN** o fator da mesma demanda é calculado duas vezes com a mesma semente e a mesma faixa
- **THEN** os dois valores são idênticos

#### Scenario: Ordem das demandas
- **WHEN** as demandas chegam em ordens diferentes
- **THEN** cada demanda recebe o mesmo fator

#### Scenario: Dentro da faixa
- **WHEN** a faixa é de 0,9 a 1,1
- **THEN** todo fator está entre 0,9 (inclusive) e 1,1 (exclusive)

#### Scenario: Sementes diferentes
- **WHEN** a semente muda
- **THEN** os fatores das demandas mudam

### Requirement: Realizado simulado
O realizado simulado SHALL ser calculado simulando de novo o planejamento do processo atual com o esforço de cada parte multiplicado pelo fator da demanda (arredondado a duas casas) e **margem zero**; a data real simulada de cada demanda é o seu fim sem margem (PRD 7.3, passo 6). A previsão do processo atual SHALL NOT ser alterada por esse cálculo.

#### Scenario: Fator um reproduz a previsão sem margem
- **WHEN** os dois fatores limites são 1
- **THEN** a data real simulada de cada demanda é igual ao fim sem margem do processo atual

#### Scenario: Esforço maior atrasa
- **WHEN** o fator de uma demanda é maior que 1 e nada mais muda
- **THEN** a data real simulada dessa demanda não é anterior ao fim sem margem do processo atual

### Requirement: Métricas de eficiência dos dois planejamentos
Para cada planejamento, o motor SHALL calcular, sobre o período comparado, as horas ociosas (soma da ociosidade de todos os times), o equilíbrio de ocupação (a maior menos a menor ocupação média dos times, em pontos percentuais), os períodos de sobrecarga (pares time × semana com sobrecarga) e o prazo total do setor (a última previsão com margem) (PRD 7.2). O período comparado SHALL ir da menor data de início à maior previsão entre os dois planejamentos, em semanas completas (DT-24).

#### Scenario: Cenário montado à mão
- **WHEN** um planejamento tem dois times com ocupações conhecidas e uma semana sobrecarregada
- **THEN** as quatro métricas batem com os valores calculados à mão

#### Scenario: Os dois planejamentos usam o mesmo período
- **WHEN** os dois planejamentos terminam em datas diferentes
- **THEN** as métricas dos dois são calculadas sobre o mesmo período

#### Scenario: Time sem capacidade
- **WHEN** um time tem capacidade zero no período
- **THEN** ele não entra no equilíbrio de ocupação, para não dividir por zero

### Requirement: Desvio da linha de base e do Coordin
Para cada demanda previsível no processo atual, o motor SHALL calcular o desvio da linha de base (data real simulada menos a data de linha de base) e o desvio do Coordin (data real simulada menos a previsão do **planejamento com o Coordin**, com margem; PRD 7.3, passo 7), em dias corridos, positivo para atraso (DT-27). O resultado SHALL trazer a média com sinal e a média dos valores absolutos de cada um (PRD 7.1).

#### Scenario: Desvio com sinal
- **WHEN** a data real é 3 dias depois da previsão
- **THEN** o desvio do Coordin da demanda é 3, e um desvio de entrega antecipada é negativo

#### Scenario: Média absoluta
- **WHEN** duas demandas têm desvios de +10 e −10
- **THEN** a média com sinal é 0 e a média absoluta é 10

#### Scenario: Demanda não previsível
- **WHEN** uma demanda não é previsível no processo atual
- **THEN** ela fica fora dos desvios e das médias

### Requirement: Checagem das demandas mais prioritárias
O resultado SHALL comparar, para as 5 demandas de maior prioridade previsíveis nos dois planejamentos, a previsão do processo atual com a previsão do planejamento com o Coordin, com a diferença em dias corridos (positiva é atraso), e SHALL informar quantas dessas demandas pioraram (PRD 7.2, "sem piorar o prazo das demandas mais prioritárias").

#### Scenario: Nenhuma piora
- **WHEN** as 5 mais prioritárias têm a mesma data ou data anterior com o Coordin
- **THEN** a quantidade de demandas que pioraram é zero

#### Scenario: Uma piora
- **WHEN** uma das 5 tem a data do Coordin depois da do processo atual
- **THEN** ela aparece com a diferença positiva e a quantidade que piorou é um

#### Scenario: Menos de cinco previsíveis
- **WHEN** há menos de 5 demandas previsíveis nos dois planejamentos
- **THEN** a checagem usa as que existem, sem erro

### Requirement: Mesmo resultado para os mesmos dados
O motor SHALL produzir o mesmo resultado para os mesmos planejamentos, a mesma linha de base e os mesmos parâmetros, independentemente da ordem das listas e da máquina (RNF-02). O avaliador SHALL NOT acessar banco, relógio, rede nem usar `System.Random`.

#### Scenario: Execuções repetidas
- **WHEN** a avaliação é executada duas vezes com a mesma entrada
- **THEN** os resultados serializados são idênticos

#### Scenario: Listas embaralhadas
- **WHEN** as listas do snapshot chegam em outra ordem
- **THEN** o resultado é o mesmo

#### Scenario: Parâmetros inválidos
- **WHEN** a faixa tem `min <= 0` ou `min > max`
- **THEN** a avaliação é recusada com um erro de validação
