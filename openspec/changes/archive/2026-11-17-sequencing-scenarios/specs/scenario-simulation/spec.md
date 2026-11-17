# Spec Delta

## Purpose

Permite ao gestor testar uma sequência que ele mesmo propõe, por exemplo trocar a posição de duas demandas, e ver o impacto no mesmo formato dos cenários recomendados, sem alterar o cronograma vigente (RF-14; regra inviolável 2).

## ADDED Requirements

### Requirement: Simular uma ordem proposta pelo gestor
`POST /api/scenarios/simulate` SHALL receber `executionOrder`, a lista com todas as demandas na ordem proposta, e devolver um cenário no mesmo formato das recomendações: itens com datas e diferenças, prazo do setor e `score` atual, do cenário e a diferença (RF-14; DT-25).

#### Scenario: Trocar a posição de duas demandas
- **WHEN** o gestor envia a ordem atual com duas demandas trocadas
- **THEN** a resposta é `200` com o cenário, o impacto por demanda e o `score` com as parcelas

#### Scenario: A ordem atual não muda nada
- **WHEN** o gestor envia exatamente a ordem de execução vigente
- **THEN** todos os `deltaDays` e o `score.delta` são zero

### Requirement: Simular não altera o cronograma
A simulação SHALL NOT gravar nada: nem a ordem de execução, nem o modo, nem as previsões, até o gestor aprovar o cenário (RF-14, RF-15; regra inviolável 2).

#### Scenario: Banco intacto
- **WHEN** o gestor simula qualquer ordem
- **THEN** o cronograma vigente e os rankings gravados continuam iguais

### Requirement: Validação da ordem proposta
A API SHALL responder `400` com o erro no campo `executionOrder` quando a lista estiver ausente, tiver um id repetido ou desconhecido, ou não trouxer todas as demandas, sem calcular nada (RF-14).

#### Scenario: Lista incompleta
- **WHEN** a lista omite uma demanda existente
- **THEN** a resposta é `400` com o erro em `executionOrder` e a mensagem em português

#### Scenario: Id desconhecido ou repetido
- **WHEN** a lista traz um id que não existe ou o mesmo id duas vezes
- **THEN** a resposta é `400` com o erro em `executionOrder`

### Requirement: Dependências respeitadas em qualquer ordem
A simulação SHALL respeitar as dependências mesmo quando a ordem proposta põe uma demanda antes do seu pré-requisito: a dependente espera o fim previsto do pré-requisito, e a ordem não é corrigida pelo sistema (RF-18; regra inviolável 2).

#### Scenario: Dependente antes do pré-requisito
- **WHEN** o gestor propõe B, que depende de A, antes de A
- **THEN** o cenário mostra B começando depois do fim previsto de A, e a sequência devolvida é a proposta

### Requirement: Rota protegida por login
`POST /api/scenarios/simulate` SHALL exigir o gestor autenticado, pela política padrão (RNF-04).

#### Scenario: Sem token
- **WHEN** a rota é chamada sem `Authorization`, com corpo
- **THEN** a resposta é `401` em Problem Details
