# delivery-forecast Specification

## Purpose
Calcula a data prevista de entrega de cada demanda previsível a partir do esforço por time, da capacidade das pessoas alocadas e da ordem de execução, e aplica a margem de segurança. É o cálculo central do Coordin (RF-05, RF-08, RF-09; DT-11, DT-12, DT-21, DT-22).

## Requirements

### Requirement: Previsão de uma demanda simples
Para uma demanda previsível, o motor SHALL simular os dias úteis a partir da data de início, entregando a cada pessoa alocada as horas da sua capacidade diária, e SHALL informar `ForecastStartDate` (primeiro dia em que a demanda recebe horas) e `EndDateWithoutMargin` (dia em que o esforço restante da última parte chega a zero) (RF-08; DT-21).

#### Scenario: Uma pessoa, 16 horas
- **WHEN** a demanda tem 16 h para um time, uma pessoa de 8 h por dia está alocada e a data de início é uma segunda-feira
- **THEN** `ForecastStartDate` é a segunda-feira e `EndDateWithoutMargin` é a terça-feira seguinte

#### Scenario: Esforço que não fecha em dias inteiros
- **WHEN** a demanda tem 20 h e a pessoa alocada tem 8 h por dia
- **THEN** o fim sem margem é o terceiro dia útil (8 h, 8 h e 4 h)

#### Scenario: Início em fim de semana
- **WHEN** a data de início é um sábado
- **THEN** a demanda começa a receber horas na segunda-feira seguinte

#### Scenario: Duas pessoas no mesmo time
- **WHEN** a parte do time tem 32 h e duas pessoas de 8 h por dia estão alocadas
- **THEN** o fim sem margem é o segundo dia útil

### Requirement: Ausências e feriados empurram o fim
O motor SHALL descontar da simulação os dias em que a pessoa alocada está ausente ou que são feriados, empurrando o fim pelos dias de capacidade perdidos (RF-03, RF-08).

#### Scenario: Ausência no meio da execução
- **WHEN** a demanda tem 40 h, a pessoa tem 8 h por dia e está ausente no terceiro dia útil
- **THEN** o fim sem margem é o sexto dia útil, e não o quinto

#### Scenario: Feriado no meio da execução
- **WHEN** a demanda tem 24 h, a pessoa tem 8 h por dia e o segundo dia útil é feriado
- **THEN** o fim sem margem é o quarto dia útil e ninguém trabalha no feriado

### Requirement: Vários times na mesma demanda
Cada parte por time SHALL ser executada só pelas pessoas desse time alocadas à demanda, e a demanda SHALL terminar no dia em que termina a última parte. Adicionar um time NÃO SHALL dividir o prazo automaticamente (RF-05, RF-08; regra inviolável 4; DT-11).

#### Scenario: Fim é o da última parte
- **WHEN** a parte do time A (40 h, pessoa de 8 h) termina no 5º dia útil e a do time B (20 h, pessoa de 4 h) termina no 5º dia útil
- **THEN** o fim sem margem da demanda é o 5º dia útil

#### Scenario: Dividir o esforço não dá a metade do prazo
- **WHEN** uma demanda de 100 h com um time (pessoa de 10 h por dia) termina em 10 dias úteis, e depois é refeita com partes de 50 h para dois times, o segundo com capacidade de 5 h por dia
- **THEN** o novo fim sem margem é o 10º dia útil, determinado pela parte do time mais lento, e não o 5º

#### Scenario: Parte de outro time não recebe horas de quem não é do time
- **WHEN** uma pessoa do time B está alocada à demanda mas a demanda não envolve o time B
- **THEN** o motor rejeita o snapshot com erro de argumento

### Requirement: Fila por pessoa na ordem de execução
Cada pessoa SHALL trabalhar uma parte de cada vez, na ordem de execução, e uma demanda posterior SHALL receber horas dela só quando as anteriores elegíveis não precisarem mais delas. A sobra de horas de um dia SHALL passar para a próxima parte elegível da fila (RF-05, RF-08; DT-12, DT-21).

#### Scenario: Demanda posterior espera
- **WHEN** a pessoa de 8 h por dia está alocada às demandas 1 (16 h) e 2 (8 h), ambas já iniciadas
- **THEN** a demanda 1 termina no 2º dia útil e a 2 no 3º dia útil

#### Scenario: Sobra do dia vai para a próxima
- **WHEN** a demanda 1 precisa de 4 h no último dia e a pessoa tem 8 h
- **THEN** as 4 h restantes do dia vão para a demanda 2 no mesmo dia

#### Scenario: Demanda posterior não toma capacidade da anterior
- **WHEN** a demanda 2 compartilha a pessoa com a demanda 1, que vem antes na ordem de execução
- **THEN** a previsão da demanda 1 é a mesma que seria sem a demanda 2

#### Scenario: Demanda ainda não iniciada não segura a fila
- **WHEN** a demanda 1 só começa na semana seguinte e a demanda 2 já pode começar
- **THEN** a pessoa trabalha na demanda 2 até a data de início da 1 chegar

#### Scenario: Pessoa sem nenhuma demanda elegível
- **WHEN** nenhuma parte da fila da pessoa está elegível em um dia
- **THEN** a pessoa não consome horas nesse dia

### Requirement: Ordem de execução por prioridade ou personalizada
O motor SHALL usar como ordem da fila o ranking de prioridade no modo `FollowPriority` e o ranking de execução no modo `Custom`; em empate, SHALL desempatar pelo identificador da demanda (RF-08; DT-13). O motor NÃO SHALL alterar nenhum ranking (regras invioláveis 2 e 3).

#### Scenario: Modo FollowPriority
- **WHEN** o modo é `FollowPriority` e as prioridades são diferentes dos rankings de execução
- **THEN** a fila segue as prioridades

#### Scenario: Modo Custom
- **WHEN** o modo é `Custom`
- **THEN** a fila segue os rankings de execução

#### Scenario: Empate
- **WHEN** duas demandas têm o mesmo ranking
- **THEN** a de menor identificador vem primeiro, sempre

### Requirement: Horizonte de cinco anos
A simulação SHALL parar ao atingir cinco anos a partir da menor data de início entre as demandas previsíveis; a demanda que não terminar até lá SHALL virar não previsível com o motivo `NoCapacity` (DT-21).

#### Scenario: Capacidade irrisória
- **WHEN** a demanda tem 100 000 h e a única pessoa alocada tem 1 h por dia
- **THEN** a demanda é não previsível com o motivo `NoCapacity` e o cálculo termina

### Requirement: Margem de segurança percentual sobre a duração
O motor SHALL calcular `duration` como o número de dias úteis de `ForecastStartDate` a `EndDateWithoutMargin`, inclusive, e `marginDays = ceil(duration × percent / 100)`. `ForecastEndDate` SHALL ser `EndDateWithoutMargin` avançada `marginDays` dias úteis, e a parte da margem SHALL ser informada junto (RF-09; DT-22).

#### Scenario: Margem de 20% em 10 dias úteis
- **WHEN** a duração é de 10 dias úteis e a margem é de 20%
- **THEN** a margem é de 2 dias úteis e `ForecastEndDate` é 2 dias úteis depois do fim sem margem

#### Scenario: Margem de 20% em 1 dia útil
- **WHEN** a duração é de 1 dia útil e a margem é de 20%
- **THEN** a margem é de 1 dia útil (arredondada para cima)

#### Scenario: Margem de 0%
- **WHEN** o percentual é zero
- **THEN** `ForecastEndDate` é igual a `EndDateWithoutMargin` e a margem é de 0 dias

#### Scenario: Margem que cruza fim de semana e feriado
- **WHEN** a margem é de 3 dias úteis a partir de uma quinta-feira e a segunda-feira seguinte é feriado
- **THEN** `ForecastEndDate` é a quarta-feira da semana seguinte

### Requirement: A margem nunca reduz o prazo
Para toda demanda previsível, `ForecastEndDate` SHALL ser igual ou posterior a `EndDateWithoutMargin`, para qualquer planejamento e qualquer percentual não negativo (RF-09; regra inviolável 1).

#### Scenario: Invariante em planejamentos gerados
- **WHEN** centenas de planejamentos gerados com semente fixa são calculados com percentuais de 0% a 100%
- **THEN** em nenhum deles `ForecastEndDate` é anterior a `EndDateWithoutMargin`

### Requirement: A margem não consome capacidade
A margem SHALL ser aplicada só à data comunicada e NÃO SHALL ocupar horas das pessoas: as demandas seguintes na fila usam a capacidade a partir do fim sem margem (RF-09; DT-22).

#### Scenario: Demanda seguinte começa depois do esforço, não da margem
- **WHEN** a demanda 1 termina o esforço no dia 5 e tem 2 dias de margem, e a demanda 2 usa a mesma pessoa
- **THEN** a demanda 2 pode receber horas da pessoa a partir das horas sobrantes do dia 5, e não só a partir do dia 8

### Requirement: Consumo diário por pessoa
O resultado SHALL incluir, para cada pessoa e cada dia, as horas consumidas por demanda, e a soma das horas de uma pessoa em um dia NUNCA SHALL ultrapassar a capacidade dela naquele dia (RF-05, RF-17).

#### Scenario: Capacidade nunca ultrapassada
- **WHEN** qualquer planejamento é calculado
- **THEN** em todo dia as horas consumidas de cada pessoa são menores ou iguais à sua capacidade naquele dia

#### Scenario: Esforço consumido bate com o estimado
- **WHEN** uma demanda é previsível
- **THEN** a soma das horas consumidas por ela é igual ao esforço total estimado

### Requirement: Determinismo
Para o mesmo snapshot, o resultado SHALL ser idêntico, independentemente da ordem das listas de entrada e de quantas vezes o cálculo for repetido (RF-08, RNF-02; DT-02).

#### Scenario: Listas embaralhadas
- **WHEN** o mesmo planejamento é calculado com times, pessoas, demandas, alocações, ausências e feriados em ordens diferentes
- **THEN** os resultados são idênticos

#### Scenario: Sem relógio
- **WHEN** o cálculo é repetido em dias diferentes com o mesmo snapshot
- **THEN** o resultado não muda, porque o motor nunca lê a data do sistema

### Requirement: Planejamento vazio
Um snapshot sem demandas SHALL produzir um resultado vazio, sem erro (RF-08).

#### Scenario: Sem demandas
- **WHEN** o snapshot não tem nenhuma demanda
- **THEN** o resultado não tem previsões nem consumo
