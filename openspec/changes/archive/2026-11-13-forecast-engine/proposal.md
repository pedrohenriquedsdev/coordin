# Proposal

## Why

O Coordin já cadastra times, pessoas, capacidade, ausências e feriados (change 3), mas ainda não calcula nada com isso. O motor de previsão é o coração do produto: transforma esforço estimado e capacidade em datas de entrega, e concentra as regras invioláveis 1, 4 e 5. Ele vem antes das demandas (change 5), do relatório por time (change 6) e dos cenários (change 9), que são todos consumidores dele, e é o item que mais precisa de testes (RNF-02, RNF-08).

## What Changes

- Novos tipos de entrada e saída imutáveis (`record`s) em `Coordin.Engine`: o **snapshot do planejamento** (times, pessoas, ausências, feriados, demandas com partes por time, alocações, ordem de execução e percentual da margem) e o **resultado do cálculo** (previsão por demanda e consumo diário por pessoa).
- **Capacidade diária por data** (TDD 6.2, DT-10): `capacity(person, date)` e o conceito de dia útil, com feriados e ausências. Estende o `TeamCapacity` da change 3.
- **Previsibilidade** (TDD 6.3, RF-10): classifica a demanda não concluída como previsível ou não previsível, com o motivo `MissingStartDate`, `MissingEstimate` ou `NoCapacity`.
- **Simulação dia a dia com fila por pessoa** (DT-21, DT-12, DT-11): calcula `ForecastStartDate` e `EndDateWithoutMargin` de cada demanda previsível, com horizonte de 5 anos.
- **Margem de segurança percentual sobre a duração, em dias úteis** (DT-22): calcula `ForecastEndDate` e a parte da margem, sempre arredondando para cima e nunca reduzindo o prazo.
- **Testes de unidade** com todos os casos da seção 8.2 que pertencem a esta change e um **gerador de snapshots de semente fixa** para verificar os invariantes (DT-34): margem nunca reduz o prazo, adicionar um time não divide o prazo pela metade, determinismo.
- Nenhuma mudança em API, banco, front ou pacotes.

Roadmap (TDD 9.1): change 4, `forecast-engine`, branch `feat/forecast-engine`, Must, data alvo 2026-11-17.
Atende: **RF-05**, **RF-08**, **RF-09**, **RF-10**, **RNF-02**, **RNF-08**; **DT-02**, **DT-10**, **DT-11**, **DT-12**, **DT-21**, **DT-22**, **DT-34**; regras invioláveis **1**, **4** e **5**.

## Capabilities

### New Capabilities

- `daily-capacity`: capacidade de cada pessoa e de cada time em uma data, considerando dias úteis, feriados e ausências.
- `forecast-predictability`: classificação de cada demanda em previsível ou não previsível, com o motivo.
- `delivery-forecast`: previsão de datas de entrega por simulação dia a dia (fila por pessoa, vários times, ordem de execução), com a margem de segurança e as garantias de determinismo.

### Modified Capabilities

Nenhuma. A spec vigente `capacity-calendar` descreve o cadastro (API) e não muda; a capacidade por data é uma capacidade nova do motor.

## Impact

- **Código:** só `src/Coordin.Engine` (pasta `Capacity/` estendida; novas pastas `Planning/` e `Forecasting/`, a confirmar no design) e `tests/Coordin.Engine.Tests`. O teste de arquitetura existente continua valendo: o motor não ganha nenhuma dependência.
- **API, banco, front, pacotes NuGet e npm, CI:** sem alteração. Ninguém chama o motor ainda; a API passa a montar o snapshot na change 5.
- **Fora desta change (e onde entra):**
  - **Dependências entre demandas** (grafo, ciclos, `PrerequisiteUnpredictable`, elegibilidade após o pré-requisito) → change 8 `demand-dependencies` (RF-18, DT-23). O snapshot desta change **não** tem dependências.
  - **Demandas como agregado, persistência, recálculo na escrita (DT-03), linha de base e `PlanningSettings`/`PUT /api/settings`** → change 5.
  - **Relatório por time (DT-24)** → change 6; **cenários e score (DT-25, DT-26)** → change 9; **desvio (DT-27)** → change 5; **avaliação da validação (6.6)** → change 11.
  - **Teste de desempenho com 200 demandas (8.5)** → a partir da change 9, como manda o TDD 8.1.
- **Risco de prazo:** M4 é 2026-11-21. Manter esta change só com o motor (sem API nem tela) é o que a mantém dentro do dia 17.
