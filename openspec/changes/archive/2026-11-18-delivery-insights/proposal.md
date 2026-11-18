# Proposal

## Why

Hoje o Coordin calcula a previsão, mas não guarda como ela e as estimativas mudaram, nem quanto esforço realmente foi gasto. Sem isso, o gestor não consegue responder "a previsão costuma errar para mais ou para menos?" nem ver quais times estimam mal, que é a dor central do PRD (seção 2). A change fecha a linha 12 do roadmap (TDD 9.1) e cobre os requisitos Could RF-20 a RF-23. Ela é a única pendente com valor de uso real antes da validação (M7, 2026-11-26), por isso segue depois da change 11b, sem corte até que o prazo exija decisão do autor.

## What Changes

- **Histórico de estimativas (RF-20):** cada alteração de estimativa de um time em uma demanda é gravada com o valor anterior, o novo, a data e um motivo opcional. Somente inclusão (DT-14).
- **Histórico da data prevista (RF-21):** cada mudança da data prevista de uma demanda é gravada com a data anterior, a nova, a data da mudança e a causa (estimativa, alocação, capacidade, ausência, dependência, cenário aprovado ou nova demanda). A primeira previsão aparece como primeiro registro. Somente inclusão.
- **Tela de detalhe da demanda (`/demands/:id`):** mostra os dois históricos em ordem cronológica. É o único lugar de consulta dos históricos nesta change.
- **Esforço real na conclusão (RF-22):** a conclusão aceita `actualEffortHours` opcional, maior que zero. A conclusão também grava, no próprio registro da demanda, a data prevista vigente e o esforço estimado vigentes naquele momento.
- **Indicadores de precisão (RF-23):** desvio médio de prazo (em relação à linha de base e à previsão na conclusão), percentual de demandas concluídas até a data prevista (linha de base), e diferença média entre estimado e real (só demandas com esforço real), para o setor e por time, filtrados pelo período de conclusão. Calculados no motor puro.
- **Página `/reports/accuracy`:** filtro por período, gráfico por time com o setor, e tabela com os mesmos valores (DT-36).
- **Ajustes no TDD, a aprovar antes de editar (não feitos nesta proposta):**
  1. Em `Demand`: colunas `ActualEffortHours` (que o TDD marca como Could), `ForecastEndDateAtCompletion` e `EstimatedEffortHoursAtCompletion`, gravadas na conclusão.
  2. `ForecastHistory` e `EstimateHistory` deixam de ser Could e passam a ser Must nesta change.
  3. Campo opcional `estimateChangeReason` no corpo de alteração de estimativa (RF-20: "motivo opcional").
- **BREAKING:** nenhum. A conclusão continua aceitando o corpo atual; o campo de esforço real é opcional.

**Fora desta change:**
- Previsto × realizado detalhado com datas reais de conclusão por time (item Could do escopo do PRD, fora de RF-20 a RF-23).
- Edição ou exclusão de histórico (proibido por DT-14).
- Recalcular indicadores de demandas concluídas antes desta change: elas não têm snapshot e aparecem sem o dado correspondente (ver design).
- Importar esforço real de outras ferramentas.

## Capabilities

### New Capabilities

- `demand-history`: registro somente de inclusão das alterações de estimativa e da data prevista, com a causa, e a consulta por demanda em ordem cronológica (RF-20, RF-21; DT-14).
- `completion-effort`: esforço real opcional na conclusão e o registro da previsão e do esforço estimado vigentes na conclusão (RF-22; DT-27).
- `accuracy-indicators`: cálculo dos indicadores de precisão para o setor e por time, com filtro de período de conclusão e a regra de demanda com vários times (RF-23).
- `delivery-web`: a tela de detalhe da demanda com os históricos e a página de indicadores, com gráfico e tabela (RF-20, RF-21, RF-23; DT-36; RNF-03).

### Modified Capabilities

Nenhuma. Os contratos de cronograma, previsão e demandas continuam como estão; a conclusão ganha um campo opcional, o que é aditivo e fica dentro de `completion-effort`.

## Impact

- **Motor (`src/Coordin.Engine`):** novo módulo de indicadores (puro, sem relógio) e a causa de mudança de previsão no resultado do recálculo.
- **API (`src/Coordin.Api`):** duas tabelas novas (histórico de estimativa e de previsão), três colunas em `Demands`, uma migração, `GET /api/demands/{id}/estimate-history`, `GET /api/demands/{id}/forecast-history`, `GET /api/reports/accuracy?from=&to=`, e a causa propagada pelo `PlanningTransaction`.
- **Front (`src/coordin-web`):** rota `/demands/:id`, rota `/reports/accuracy`, link de cada demanda para o detalhe, e campo opcional de esforço real no fluxo de conclusão.
- **Dependências:** nenhuma nova.
- **Prazo:** Could em risco de prazo: a change depende de aprovação dos ajustes do TDD antes de implementar, e fica depois da validação (M7) se o prazo apertar.
