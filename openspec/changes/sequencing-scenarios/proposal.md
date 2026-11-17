# Proposal

## Why

Hoje o gestor só tem a ordem de prioridade: o sistema calcula as datas nessa ordem, mas não diz se outra sequência de execução aproveitaria melhor a capacidade e antecipa as entregas mais importantes. Achar essas sequências à mão é a dor central do projeto (PRD seção 2): o gestor de desenvolvimento perde tempo procurando ociosidade e testando trocas na cabeça. Esta change entrega o **motor e a API** da recomendação, da simulação e da aprovação de cenários (RF-13 a RF-15) e da simulação de nova demanda (RF-16), sempre com o gestor decidindo (regra inviolável 2) e a prioridade preservada (regra inviolável 3). As telas ficam na change 9b `scenario-screens`; a validação por simulação (change 11) usa este motor.

Há um risco medido que molda a change: uma avaliação do motor custa cerca de **18 ms** a quente com 15 times, 150 pessoas e 200 demandas (o volume do RNF-01), e o DT-26 prevê até 3.000 avaliações, o que daria cerca de 55 s contra o limite de **10 s** do RNF-01. O próprio DT-26 diz que o limite exato é confirmado por teste de desempenho, então esta change inclui acelerar o motor sem mudar nenhum resultado e fixar o orçamento por medição (decisão do autor em 2026-11-17).

## What Changes

- **Motor, avaliação e score (DT-25):** dado um snapshot e uma ordem de execução, o motor calcula o cronograma daquela ordem e o `score = Σ wᵢ × dᵢ` (menor é melhor) com a **parcela de cada demanda**, as datas atuais e do cenário, o prazo total do setor e a diferença em dias. As parcelas somam exatamente a diferença do `score`, que é a justificativa mostrada ao gestor (RF-13). Desempates conforme o DT-25.
- **Motor, aceleração:** o plano (validação, classificação, calendário e capacidade) é preparado uma vez e reaproveitado entre as avaliações, e a simulação perde custos desnecessários. Nenhum resultado muda: um teste compara o caminho rápido com `ForecastEngine.Calculate` em centenas de planejamentos. Perfil medido antes de mexer.
- **Motor, busca (DT-26):** busca local determinística (mover cada demanda de 1 a 3 posições, primeira melhoria, parada por número de avaliações, nunca por tempo), até 3 cenários recomendados (a melhor ordem e os dois melhores movimentos únicos) ou lista vazia quando nada melhora. O orçamento de avaliações é o maior valor que cabe em 10 s no volume do RNF-01, medido em teste; se ficar abaixo de 3.000, o DT-26 só é ajustado com a aprovação do autor.
- **Motor, nova demanda (RF-16):** compara o cronograma sem e com uma demanda nova, **na posição da prioridade que o gestor escolher**, e informa as demandas cuja data prevista muda, em dias corridos. (Com a demanda no fim da fila, nenhuma demanda existente muda de data: medido em 297 planejamentos, zero. Por isso a posição é opcional, decisão do autor em 2026-11-17.)
- **API:** `POST /api/scenarios/recommendations`, `POST /api/scenarios/simulate` (corpo com `executionOrder`), `POST /api/scenarios/apply` e `POST /api/scenarios/reset-to-priority`, mais `POST /api/demands/simulate` (mesmo corpo de `POST /api/demands`, com o `position` opcional, sem gravar). `POST /api/demands` também passa a aceitar o `position` opcional, para confirmar a inclusão exatamente onde foi simulada. `recommendations` e `simulate` nunca gravam nada. `apply` e `reset-to-priority` gravam a ordem de execução e o modo na **mesma transação** e recalculam as previsões, sem tocar a prioridade registrada.
- **Dependências:** nenhum cenário viola uma dependência (RF-18): o motor já espera o pré-requisito em qualquer ordem, e `apply` confere de novo o resultado antes de gravar.
- **Documentos:** o TDD 5.3 recebe o campo opcional `position` no corpo da demanda e o formato da resposta de `POST /api/demands/simulate`, o `reset-to-priority` já consta em 5.2, e o DT-26 só muda se a medição exigir (com aprovação do autor). Nenhuma edição será feita sem aprovação.

Nenhuma mudança incompatível: tudo é rota nova e código novo no motor.

## Capabilities

### New Capabilities

- `scenario-scoring`: avaliar uma ordem de execução e calcular o score ponderado do DT-25 com as parcelas por demanda, as datas atuais e do cenário e o prazo total do setor (RF-13, RF-14; DT-25).
- `scenario-recommendation`: buscar e recomendar até 3 cenários melhores que o atual, de forma determinística e dentro do orçamento de avaliações, ou informar que nenhum melhora (RF-13; DT-26; RNF-01, RNF-02).
- `scenario-simulation`: simular uma ordem proposta pelo gestor, no mesmo formato das recomendações e sem gravar nada (RF-14).
- `scenario-approval`: aplicar um cenário (ordem de execução e modo gravados, previsões recalculadas, prioridade intacta) e voltar a ordem de execução para a prioridade (RF-15).
- `new-demand-simulation`: simular o impacto de uma nova demanda no cronograma, sem gravar nada (RF-16).

### Modified Capabilities

- `demand-registry`: o requisito "Ordem de prioridade única e contínua" passa a admitir o `position` opcional, e a nova demanda entra nessa posição nas duas ordens (sem `position`, continua no fim).

As demais specs não mudam de comportamento: `planning-settings` já diz que o modo de ordem só muda pela aprovação de um cenário, e `schedule-view` já indica `isOutOfPriorityOrder`.

## Impact

- **Código:** `src/Coordin.Engine` (nova pasta `Scenarios/`; `Forecasting/` ganha o plano preparado), `src/Coordin.Api` (nova pasta `Features/Scenarios/`; o auxiliar de ranks de `DemandEndpoints` é extraído para ser reaproveitado; `POST /api/demands/simulate` em `Features/Demands`), testes do motor e da API, cliente Angular regenerado (as telas ficam na 9b).
- **API:** cinco rotas novas, protegidas pela política padrão de login; códigos `400` por campo e `409` com `code` estável.
- **Banco:** nenhuma migration. A ordem de execução e o modo já existem (`Demands.ExecutionRank`, `PlanningSettings.ExecutionOrderMode`).
- **Dependências de pacote:** nenhuma.
- **Roadmap:** linha 9 do TDD 9.1 (`sequencing-scenarios`, `feat/sequencing-scenarios`, Should, marco 2026-11-23), dividida em 9 (motor e API) e 9b (telas e gráficos) por decisão do autor, como a 5 e a 5b. **Requisitos:** RF-13, RF-14, RF-15, RF-16; RNF-01, RNF-02, RNF-08. **Decisões:** DT-25, DT-26, DT-13, DT-21 (e DT-36 na 9b). As regras invioláveis 2 e 3 são o centro desta change: o sistema nunca reordena sozinho, e a prioridade registrada nunca muda.

## Fora desta change

- Telas, gráficos e a aprovação ou rejeição pela interface (change 9b `scenario-screens`): rejeitar um cenário não tem rota, porque rejeitar é não chamar `apply`.
- Exportação do cronograma (change 10) e a avaliação da validação por simulação (change 11).
- Persistir cenários, histórico de cenários ou comparar mais de um cenário aplicado.
- Outros critérios de otimização além do DT-25 e outros métodos de busca (algoritmo genético, solver).
- Recomendar mudanças de alocação de pessoas ou de capacidade: os cenários só mudam a **ordem de execução**.
- Mexer nos Must: cálculo, margem, relatório por time e telas existentes ficam como estão.
