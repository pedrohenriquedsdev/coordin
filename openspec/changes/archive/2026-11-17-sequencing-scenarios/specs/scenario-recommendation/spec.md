# Spec Delta

## Purpose

Procura, com um método simples e determinístico, ordens de execução que melhoram o resultado geral do planejamento e as apresenta ao gestor como recomendações, sem aplicar nada. É a resposta à dor central de encontrar sequências mais eficazes à mão (RF-13; DT-26; RNF-01, RNF-02).

## ADDED Requirements

### Requirement: Recomendar até três cenários melhores
`POST /api/scenarios/recommendations` SHALL devolver uma lista com até 3 cenários cujo `score` é menor que o do cronograma atual: a melhor ordem encontrada e os dois melhores movimentos únicos a partir da ordem atual, sem repetir sequências, do melhor para o pior (RF-13; DT-25, DT-26).

#### Scenario: Há melhoria
- **WHEN** existe uma ordem com `score` menor que o atual
- **THEN** a resposta traz de 1 a 3 cenários, cada um com a sequência, as datas por demanda, o prazo do setor e o `score`

#### Scenario: Sem repetição
- **WHEN** a melhor ordem encontrada é igual a um dos movimentos únicos
- **THEN** a sequência aparece uma só vez

### Requirement: Informar quando nada melhora
Quando nenhuma alternativa melhora o `score`, a resposta SHALL ser uma lista vazia, e o cliente SHALL poder informar isso ao gestor (RF-13).

#### Scenario: Cronograma já é o melhor encontrado
- **WHEN** nenhum vizinho da ordem atual tem `score` menor
- **THEN** a resposta é `200` com a lista vazia

#### Scenario: Planejamento vazio ou de uma demanda
- **WHEN** há nenhuma ou uma demanda aberta e previsível
- **THEN** a resposta é `200` com a lista vazia

### Requirement: Busca local determinística
A busca SHALL partir da ordem de execução atual e gerar sempre na mesma ordem a vizinhança de mover cada demanda de 1 a 3 posições para cima ou para baixo, adotar o primeiro vizinho de `score` menor e recomeçar a varredura, e parar quando nenhum vizinho melhorar ou quando o orçamento de avaliações acabar (DT-26; RNF-02).

#### Scenario: Primeira melhoria
- **WHEN** a varredura encontra um vizinho melhor
- **THEN** ele vira a ordem corrente e a varredura recomeça dele

#### Scenario: Mesmo resultado sempre
- **WHEN** a recomendação roda duas vezes, ou com as listas do snapshot em outra ordem
- **THEN** a lista de cenários é idêntica

### Requirement: Orçamento por número de avaliações
O limite da busca SHALL ser um número de avaliações distintas, nunca um tempo de relógio, para o resultado não variar com a máquina, e o valor SHALL ser o maior que cumpre o RNF-01 no volume de referência, definido por medição em teste (DT-26; RNF-01).

#### Scenario: Orçamento esgotado
- **WHEN** a busca atinge o limite de avaliações
- **THEN** ela devolve o melhor encontrado até ali, e o mesmo planejamento sempre para no mesmo ponto

#### Scenario: Sequência já avaliada
- **WHEN** a busca gera uma sequência que já avaliou
- **THEN** ela reaproveita o resultado e não gasta orçamento

### Requirement: Só demandas abertas e previsíveis se movem
Demandas concluídas e não previsíveis SHALL manter a sua posição na ordem, e só as abertas e previsíveis SHALL ser movidas pela busca (RF-10; regra inviolável 5).

#### Scenario: Concluída mantém o lugar
- **WHEN** a busca move demandas ao redor de uma demanda concluída
- **THEN** a concluída continua na mesma posição da ordem completa

### Requirement: Nenhum cenário é aplicado e a prioridade é preservada
A recomendação SHALL NOT gravar nada: nem a ordem de execução, nem o modo, nem as previsões. Cada cenário SHALL mostrar a prioridade registrada ao lado da nova posição, e a prioridade registrada NUNCA SHALL mudar (RF-13; regras invioláveis 2 e 3).

#### Scenario: Banco intacto
- **WHEN** a recomendação é chamada várias vezes
- **THEN** as prioridades, as ordens de execução, o modo e as previsões gravadas continuam exatamente como estavam

### Requirement: Desempenho no volume de referência
Com 15 times, 150 pessoas e 200 demandas, a recomendação SHALL terminar em até 10 segundos, comprovado por um teste de desempenho com esse volume (RNF-01).

#### Scenario: Volume do RNF-01
- **WHEN** a recomendação roda sobre o planejamento de referência
- **THEN** termina em menos de 10 segundos e devolve o mesmo resultado em toda execução

### Requirement: Rota protegida por login
`POST /api/scenarios/recommendations` SHALL exigir o gestor autenticado, pela política padrão (RNF-04).

#### Scenario: Sem token
- **WHEN** a rota é chamada sem `Authorization`
- **THEN** a resposta é `401` em Problem Details
