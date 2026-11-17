# Spec Delta

## Purpose

Avalia uma ordem de execução alternativa e mede o quanto ela é melhor ou pior que a atual, com um critério único que respeita a prioridade registrada e com a justificativa por demanda. É a base da recomendação, da simulação e da aprovação de cenários (RF-13, RF-14; DT-25; RNF-02).

## ADDED Requirements

### Requirement: Avaliar uma ordem de execução
Dado um snapshot e uma ordem com todas as demandas, o motor SHALL calcular o cronograma como se a fila de execução seguisse aquela ordem, sem alterar o snapshot e sem decidir nada sozinho (RF-14; DT-13, DT-21; regra inviolável 2).

#### Scenario: A ordem atual reproduz o cronograma atual
- **WHEN** a ordem avaliada é a ordem de execução vigente
- **THEN** as datas previstas são as do cronograma vigente e o `score` do cenário é igual ao atual, com diferença zero

#### Scenario: Outra ordem muda as datas conforme a capacidade
- **WHEN** duas demandas que dividem a mesma pessoa trocam de posição
- **THEN** a que passou à frente termina antes e a outra termina depois, como na simulação dia a dia

### Requirement: Score ponderado pela prioridade
O motor SHALL calcular `score = Σ wᵢ × dᵢ` sobre as demandas previsíveis tanto no cronograma atual quanto no cenário, com `dᵢ` igual ao número de dias úteis, contados nas duas pontas, da menor data de início do planejamento ao `ForecastEndDate` da demanda, e `wᵢ = n − PriorityRankᵢ + 1`, com `n` igual à maior prioridade registrada. Menor é melhor (DT-25; regra inviolável 3).

#### Scenario: Pesos pela prioridade registrada
- **WHEN** há três demandas previsíveis de prioridades 1, 2 e 3
- **THEN** os pesos são 3, 2 e 1

#### Scenario: Antecipar a mais prioritária melhora o score
- **WHEN** um cenário antecipa a demanda de prioridade 1 em 2 dias úteis e atrasa a de prioridade 3 em 2 dias úteis
- **THEN** o `score` do cenário é menor que o atual em `(3 − 1) × 2` unidades

#### Scenario: Pesos sempre positivos
- **WHEN** há demandas concluídas ou excluídas e as prioridades registradas têm lacunas
- **THEN** todo peso é maior ou igual a 1

### Requirement: Parcelas por demanda somam a diferença do score
Cada demanda do cenário SHALL trazer o seu `weight` e o `scoreContributionDelta`, a diferença entre a parcela `wᵢ × dᵢ` no cenário e no cronograma atual, e a soma dos `scoreContributionDelta` SHALL ser igual à diferença do `score` (RF-13; DT-25, DT-36).

#### Scenario: A soma das parcelas fecha com o score
- **WHEN** um cenário muda as datas de várias demandas
- **THEN** a soma dos `scoreContributionDelta` é exatamente `score.scenario − score.current`

#### Scenario: Demanda que contribui para a melhoria
- **WHEN** uma demanda termina antes no cenário
- **THEN** o seu `scoreContributionDelta` é negativo

### Requirement: Dados de cada demanda no cenário
Cada item SHALL trazer `demandId`, `title`, `priorityRank`, `newExecutionRank` (a posição na ordem avaliada, a partir de 1), `currentForecastEndDate`, `forecastStartDate`, `forecastEndDate` e `deltaDays`, a diferença em dias corridos entre a data prevista do cenário e a atual (RF-13, RF-14; DT-27).

#### Scenario: Prioridade ao lado da nova posição
- **WHEN** uma demanda de prioridade 5 vai para a posição 2
- **THEN** o item traz `priorityRank` 5 e `newExecutionRank` 2, deixando visível o que muda em relação à prioridade

#### Scenario: Atraso positivo e antecipação negativa
- **WHEN** a demanda termina 3 dias corridos depois no cenário
- **THEN** `deltaDays` é 3, e uma que termina 3 dias antes tem `deltaDays` −3

### Requirement: Prazo total do setor
O cenário SHALL informar `sectorEndDate`, a maior `ForecastEndDate` entre as demandas previsíveis, e `sectorDeltaDays`, a diferença em dias corridos em relação ao cronograma atual (RF-13).

#### Scenario: Efeito no conjunto do setor
- **WHEN** a última demanda termina 6 dias corridos antes no cenário
- **THEN** `sectorDeltaDays` é −6

### Requirement: Demandas fora do score
Demandas concluídas e não previsíveis SHALL NOT entrar no `score` nem nos itens, e SHALL NOT consumir capacidade. Se uma demanda previsível no cronograma atual deixar de ser previsível no cenário (não termina em cinco anos), o cenário SHALL ser marcado como não comparável e SHALL NOT ser recomendado (RF-10; regra inviolável 5; DT-21).

#### Scenario: Concluída e não previsível ficam de fora
- **WHEN** o planejamento tem uma demanda concluída e outra sem estimativa
- **THEN** nenhuma das duas aparece nos itens nem pesa no `score`

#### Scenario: Cenário que torna uma demanda não previsível
- **WHEN** a ordem avaliada faz uma demanda estourar o horizonte de cinco anos
- **THEN** o cenário é marcado como não comparável e a recomendação o descarta

### Requirement: Desempate entre cenários
Ao comparar dois cenários de mesmo `score`, o motor SHALL desempatar, nesta ordem, pelo prazo total do setor (a menor `sectorEndDate`), pelo menor número de demandas deslocadas em relação à ordem atual e pela ordem lexicográfica da sequência de ids (DT-25; RNF-02).

#### Scenario: Mesmo score, prazo total diferente
- **WHEN** dois cenários têm o mesmo `score` e o primeiro termina o setor mais cedo
- **THEN** o primeiro é o melhor

#### Scenario: Empate completo
- **WHEN** todos os critérios empatam
- **THEN** vence a sequência cujos ids vêm primeiro na ordem lexicográfica, sempre a mesma

### Requirement: Nenhum cenário viola uma dependência
Em qualquer ordem avaliada, cada demanda previsível SHALL ter `forecastStartDate` posterior ao `forecastEndDate` de cada pré-requisito previsível, e o motor SHALL oferecer uma verificação que lista as violações, vazia em todo cenário (RF-18; RNF-08).

#### Scenario: Dependente posta antes do pré-requisito
- **WHEN** a ordem avaliada põe B, que depende de A, antes de A
- **THEN** B começa depois do fim previsto de A e a verificação não lista violações

#### Scenario: Muitos planejamentos gerados
- **WHEN** ordens aleatórias são avaliadas em planejamentos de semente fixa com dependências
- **THEN** a verificação nunca lista violações

### Requirement: Caminho rápido idêntico ao cálculo completo
A avaliação SHALL reaproveitar o que não depende da ordem (validação, classificação, calendário e capacidade) sem mudar nenhum resultado: para o mesmo snapshot e a mesma ordem, o cronograma SHALL ser idêntico ao de `ForecastEngine.Calculate` (RNF-01, RNF-02; DT-02).

#### Scenario: Equivalência em muitos planejamentos
- **WHEN** o caminho rápido e o cálculo completo rodam sobre centenas de planejamentos de semente fixa e ordens aleatórias
- **THEN** as previsões e o consumo diário são idênticos

#### Scenario: Determinismo
- **WHEN** a mesma avaliação roda duas vezes, ou com as listas do snapshot embaralhadas
- **THEN** o resultado é o mesmo
