# Spec Delta

## ADDED Requirements

### Requirement: Dependente só começa depois do fim previsto do pré-requisito
O motor SHALL tornar uma demanda elegível a receber horas só a partir do dia útil seguinte ao `ForecastEndDate`, com a margem, de todos os seus pré-requisitos, mesmo que a data de início registrada seja anterior. O `ForecastStartDate` da dependente NUNCA SHALL ser anterior nem igual a esse `ForecastEndDate` (RF-18; DT-23).

#### Scenario: Início registrado anterior ao fim do pré-requisito
- **WHEN** A termina com margem em uma sexta-feira, B depende de A e a data de início de B é anterior
- **THEN** o `ForecastStartDate` de B é a segunda-feira seguinte, dia útil depois do `ForecastEndDate` de A

#### Scenario: A margem do pré-requisito conta
- **WHEN** A termina sem margem na terça-feira e com margem na sexta-feira e B depende de A
- **THEN** B começa só depois da sexta-feira

#### Scenario: Início registrado posterior ao fim do pré-requisito
- **WHEN** B depende de A e a data de início de B é depois do fim previsto de A
- **THEN** o `ForecastStartDate` de B continua sendo calculado a partir da data de início de B

#### Scenario: Fim de semana e feriado
- **WHEN** o dia seguinte ao fim previsto do pré-requisito é sábado, domingo ou feriado
- **THEN** a dependente começa no primeiro dia útil depois dele

### Requirement: Dependências em cadeia são respeitadas
O motor SHALL respeitar dependências em cadeia: em A → B → C, C só se torna elegível depois do `ForecastEndDate` de B, que por sua vez só começa depois do `ForecastEndDate` de A (RF-18; RNF-08).

#### Scenario: Cadeia de três demandas
- **WHEN** C depende de B, B depende de A e as três têm a mesma data de início
- **THEN** o início previsto de B é posterior ao fim previsto de A e o de C é posterior ao fim previsto de B

#### Scenario: Demanda com dois pré-requisitos
- **WHEN** C depende de A e de B, e B termina depois de A
- **THEN** C começa depois do `ForecastEndDate` de B

### Requirement: A espera não bloqueia as demais demandas
Enquanto uma demanda espera os pré-requisitos, as pessoas dela SHALL seguir para a próxima parte elegível da fila, e a demanda NÃO SHALL consumir horas nesse período (RF-18; DT-21).

#### Scenario: Pessoa livre durante a espera
- **WHEN** B, de maior prioridade, espera A, e a mesma pessoa tem a demanda D, de menor prioridade, sem dependências
- **THEN** a pessoa trabalha em D enquanto B espera, e passa a trabalhar em B quando ela se torna elegível

#### Scenario: Ordem de execução não viola a dependência
- **WHEN** a dependente vem antes do pré-requisito na ordem de execução, em qualquer um dos dois modos
- **THEN** a dependente ainda começa só depois do fim previsto do pré-requisito, e o motor não reordena nenhuma demanda

### Requirement: Estouro do horizonte retira só quem não espera ninguém
Ao retirar demandas que não terminam em cinco anos, o motor SHALL apontar como causa a de maior prioridade entre as não terminadas que não aguardam nenhuma outra não terminada, SHALL marcá-la `NoCapacity` e SHALL retirar com ela as demandas que dependem dela, direta ou indiretamente, com o motivo `PrerequisiteUnpredictable`, refazendo a simulação (RF-10, RF-18; DT-21).

#### Scenario: Dependente de maior prioridade
- **WHEN** B, de maior prioridade, depende de A, A não termina em cinco anos e outras demandas terminam
- **THEN** A é `NoCapacity`, B é `PrerequisiteUnpredictable` e as demais demandas recebem previsão

#### Scenario: Demandas independentes ficam intactas
- **WHEN** uma demanda sem relação com A e B é prevista normalmente
- **THEN** a previsão dela não muda por causa da retirada de A e B

### Requirement: Snapshot com ciclo ou dependência inválida é rejeitado
O motor SHALL rejeitar com erro de argumento um snapshot em que as dependências formem um ciclo, incluindo uma demanda dependendo dela mesma, ou apontem para uma demanda que não está no snapshot, ou repitam um pré-requisito. O motor SHALL também oferecer a detecção de ciclos, que devolve a sequência de demandas do ciclo ou nenhuma (RF-18; DT-23; RNF-08).

#### Scenario: Ciclo direto
- **WHEN** A depende de B e B depende de A
- **THEN** o cálculo lança erro de argumento

#### Scenario: Ciclo indireto
- **WHEN** A depende de B, B de C e C de A
- **THEN** a detecção devolve o ciclo e o cálculo lança erro de argumento

#### Scenario: Sem ciclo
- **WHEN** as dependências formam um grafo sem ciclo, ainda que dois caminhos cheguem à mesma demanda
- **THEN** a detecção não devolve ciclo e o cálculo prossegue

#### Scenario: Pré-requisito fora do snapshot
- **WHEN** uma demanda lista um pré-requisito que não está no snapshot
- **THEN** o cálculo lança erro de argumento

### Requirement: Determinismo com dependências
O resultado com dependências SHALL ser idêntico para o mesmo snapshot, independentemente da ordem das listas de entrada, incluindo a lista de pré-requisitos de cada demanda (RNF-02; DT-02).

#### Scenario: Listas embaralhadas
- **WHEN** o mesmo planejamento com dependências é calculado com as listas em outra ordem
- **THEN** as previsões e o consumo diário são idênticos

### Requirement: Invariantes com dependências
Para todo planejamento, para cada dependência entre duas demandas previsíveis, o `ForecastStartDate` da dependente SHALL ser posterior ao `ForecastEndDate` do pré-requisito, e a soma das horas de uma pessoa em um dia NUNCA SHALL ultrapassar a capacidade dela. A margem continua só ampliando a previsão (RF-18; regra inviolável 1; RNF-08).

#### Scenario: Planejamentos gerados
- **WHEN** o cálculo roda sobre muitos planejamentos gerados com semente fixa, com dependências sem ciclo
- **THEN** em todos, cada dependente começa depois do fim previsto de cada pré-requisito, a capacidade nunca é ultrapassada e `ForecastEndDate` nunca é anterior a `EndDateWithoutMargin`

#### Scenario: Dependência pode liberar capacidade de outra demanda
- **WHEN** a dependente, de maior prioridade, passa a esperar o pré-requisito e uma demanda de menor prioridade usa as mesmas pessoas
- **THEN** a demanda de menor prioridade pode terminar antes do que terminava sem a dependência, sem violar nenhuma invariante
