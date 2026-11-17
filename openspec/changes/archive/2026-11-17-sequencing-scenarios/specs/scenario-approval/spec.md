# Spec Delta

## Purpose

Aplica, por decisão do gestor, uma ordem de execução diferente da prioridade e permite voltar à prioridade a qualquer momento, sempre preservando a prioridade registrada e recalculando as previsões na mesma operação (RF-15; regras invioláveis 2 e 3).

## ADDED Requirements

### Requirement: Aprovar um cenário aplica a ordem de execução
`POST /api/scenarios/apply` SHALL receber `executionOrder` e, na mesma transação, gravar a ordem de execução, passar o modo para `Custom` e recalcular e gravar as previsões, sem alterar a prioridade registrada de nenhuma demanda. A resposta `200` SHALL trazer o cenário aplicado, calculado contra o cronograma anterior (RF-15; DT-13, DT-14; regra inviolável 3).

#### Scenario: Ordem aplicada
- **WHEN** o gestor aprova um cenário com a ordem B, A, C
- **THEN** os `ExecutionRank` passam a 1, 2 e 3 nessa ordem, o modo é `Custom` e o cronograma já reflete as novas datas

#### Scenario: Prioridade intacta
- **WHEN** qualquer cenário é aplicado
- **THEN** os `PriorityRank` de todas as demandas são exatamente os de antes

### Requirement: Rejeitar não muda nada
Rejeitar um cenário SHALL NOT exigir nenhuma chamada e SHALL NOT alterar o cronograma: enquanto o gestor não chamar `apply`, as ordens, o modo e as previsões gravadas permanecem como estão (RF-15; regra inviolável 2).

#### Scenario: Cenários vistos e não aprovados
- **WHEN** o gestor consulta recomendações e simulações e não chama `apply`
- **THEN** o cronograma e a ordem de execução continuam os mesmos

### Requirement: Validar a ordem antes de gravar
A API SHALL responder `400` com o erro no campo `executionOrder` quando a lista estiver ausente, tiver id repetido ou desconhecido ou não trouxer todas as demandas, e `409` com o `code` `scenario-violates-dependencies` se o cronograma resultante violasse alguma dependência, sem gravar nada em ambos os casos (RF-15, RF-18; DT-18).

#### Scenario: Ordem inválida
- **WHEN** a lista não traz todas as demandas
- **THEN** a resposta é `400` e nada é gravado

#### Scenario: Conferência final das dependências
- **WHEN** o cronograma calculado para a ordem aprovada tem uma dependência violada
- **THEN** a resposta é `409` com `code` `scenario-violates-dependencies` e a ordem anterior continua gravada

### Requirement: Escrita atômica e serializada
`apply` e `reset-to-priority` SHALL rodar sob o lock do planejamento, como as demais escritas, e qualquer falha SHALL desfazer tudo: nem a ordem, nem o modo, nem as previsões são gravados (RF-08, RF-15; DT-03, DT-14).

#### Scenario: Falha no recálculo
- **WHEN** o recálculo falha depois de gravar os rankings
- **THEN** nada é confirmado e o cronograma anterior permanece

### Requirement: O cronograma mostra o que foi deslocado
Depois de aplicar um cenário, `GET /api/schedule` SHALL mostrar o modo `Custom` e `isOutOfPriorityOrder` verdadeiro nas demandas cuja ordem de execução difere da prioridade (RF-15).

#### Scenario: Demandas deslocadas
- **WHEN** um cenário move a demanda de prioridade 3 para a primeira posição
- **THEN** o cronograma sinaliza as demandas deslocadas e mostra a prioridade ao lado da posição

### Requirement: Voltar a ordem de execução para a prioridade
`POST /api/scenarios/reset-to-priority` SHALL passar o modo para `FollowPriority`, igualar a ordem de execução à prioridade e recalcular as previsões na mesma transação, respondendo `204`, a qualquer momento e quantas vezes o gestor quiser (RF-15).

#### Scenario: Voltar à prioridade
- **WHEN** há um cenário aplicado e o gestor volta à prioridade
- **THEN** o modo é `FollowPriority`, cada `ExecutionRank` é igual ao `PriorityRank` e o cronograma volta ao cálculo na ordem de prioridade

#### Scenario: Já está na prioridade
- **WHEN** o modo já é `FollowPriority`
- **THEN** a resposta é `204` e nada muda

### Requirement: Rotas protegidas por login
`apply` e `reset-to-priority` SHALL exigir o gestor autenticado, pela política padrão (RNF-04).

#### Scenario: Sem token
- **WHEN** qualquer das duas rotas é chamada sem `Authorization`, com corpo
- **THEN** a resposta é `401` em Problem Details
