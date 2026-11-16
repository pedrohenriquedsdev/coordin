# Proposal

## Why

O motor de previsão (change 4) já calcula datas, mas ninguém o chama: ainda não existem demandas no sistema. Esta change liga as pontas no back-end: o gestor passa a cadastrar demandas com times, estimativas, pessoas e datas, e toda escrita que afeta o cálculo recalcula e persiste as previsões (DT-03). Sem isso o Coordin não entrega o seu produto central (RF-08) nem alimenta o relatório por time (change 6) e a implantação (change 7).

## What Changes

- **Banco:** migration `AddDemandPlanning` com `Demands`, `DemandTeams`, `DemandAssignments`, `Forecasts` e `PlanningSettings` (registro único, criado com margem de 20% e modo `FollowPriority`), com a integridade garantida pelo PostgreSQL (rankings únicos, `CHECK`, chaves estrangeiras).
- **API de demandas (agregado, DT-17):** `GET`/`POST /api/demands`, `GET`/`PUT`/`DELETE /api/demands/{id}` e `PUT /api/demands/priority-order`. A demanda aceita ser salva sem estimativa (regra inviolável 5), com ranking de prioridade único e contínuo; novas demandas entram no fim da ordem.
- **Recálculo na escrita (DT-03):** toda escrita que muda o cálculo (demanda, estimativa, alocação, datas, ordem, margem, conclusão, e também pessoa, ausência e feriado) carrega o planejamento, chama o `ForecastEngine`, grava as previsões em `Forecasts` e confirma tudo na **mesma transação**. A primeira previsão de cada demanda vira a linha de base, gravada uma única vez (DT-14).
- **Configurações:** `GET`/`PUT /api/settings` com o percentual da margem (maior ou igual a zero; DT-22).
- **Conclusão e desvio (RF-12):** `PUT /api/demands/{id}/completion` registra a data real; a demanda concluída fica travada, deixa de consumir capacidade e mantém a previsão congelada para o desvio em dias corridos (DT-27).
- **Cronograma (API):** `GET /api/schedule` na ordem de execução, com prioridade ao lado, esforço por time, datas previstas, alerta de data de fim informada e desvios das demandas concluídas.
- **Regras de integridade nos cadastros existentes:** pessoa alocada a demanda aberta não pode ser excluída (`person-has-open-assignments`), a mudança de time de uma pessoa exige que o novo time esteja nas demandas em que ela está alocada (`person-not-in-demand-teams`) e time usado por demandas não pode ser excluído (`team-has-demands`, código novo).
- **Cliente Angular regenerado** a partir do OpenAPI (sem telas; as telas são a change 5b).
- **Roadmap (TDD 9.1 e DT-35):** a change 5 original foi dividida em **5** (este back-end) e **5b** `demand-screens` (telas), aprovado pelo autor em 2026-11-16, por prazo (M4 em 2026-11-21) e para PRs revisáveis.

Roadmap (TDD 9.1): change 5, `demand-planning`, branch `feat/demand-planning`, Must, data alvo 2026-11-18.
Atende: **RF-04**, **RF-05**, **RF-06**, **RF-07**, **RF-11** (dados do cronograma), **RF-12**; RF-03 (recálculo ao mudar ausências e feriados), RF-08 a RF-10 (previsões persistidas); **DT-03**, **DT-11** a **DT-14**, **DT-17**, **DT-18**, **DT-27**; regras invioláveis **2**, **3** e **5**.

## Capabilities

### New Capabilities

- `demand-registry`: cadastro de demandas como agregado (dados, times com estimativa, pessoas alocadas), ordem de prioridade, exclusão e conclusão real.
- `forecast-recalculation`: recálculo e persistência das previsões na mesma transação de cada escrita, linha de base gravada uma única vez e previsão congelada das demandas concluídas.
- `planning-settings`: configuração do percentual da margem de segurança.
- `schedule-view`: consulta do cronograma consolidado com alerta de data de fim e desvios.

### Modified Capabilities

- `people-registry`: pessoa alocada a demanda aberta não pode ser excluída, mudança de time respeita os times das demandas, e escritas que mudam a capacidade recalculam as previsões (requisitos novos).
- `capacity-calendar`: registrar, alterar ou remover ausência e feriado recalcula as previsões na mesma transação (requisitos novos).
- `team-registry`: time usado por demandas não pode ser excluído (requisito novo).

## Impact

- **Código:** `src/Coordin.Api` (novas pastas `Features/Demands` e `Features/Planning`; ajustes em `Features/People`, `Features/Calendar` e `Features/Teams`), uma migration nova, `tests/Coordin.Api.Tests`, o cliente gerado em `src/coordin-web/src/app/api/` e o OpenAPI. O motor (`Coordin.Engine`) **não** muda.
- **Documentos:** TDD 9.1 e DT-35 (divisão 5 e 5b), TDD 5.2 e DT-18 (`team-has-demands`, `GET /api/schedule` com campos de desvio) e `CLAUDE.md` §13.
- **Pacotes e CI:** nenhum pacote novo.
- **Fora desta change (e onde entra):**
  - **Telas** de demandas, ordem de prioridade, cronograma e conclusão → change 5b `demand-screens`.
  - **Dependências entre demandas** (`prerequisiteDemandIds`, `dependency-cycle`, `PrerequisiteUnpredictable`) → change 8; o corpo da demanda desta change **não** tem esses campos.
  - **Históricos e `estimateChangeReason`, `ActualEffortHours`** (RF-20 a RF-23, Could) → change 12.
  - **Cenários e simulação de nova demanda** (`/api/scenarios/*`, `/api/demands/simulate`) e modo `Custom` por aprovação de cenário → change 9. O modo `Custom` já é respeitado se existir no banco, mas nesta change nada o liga.
  - **Relatório por time** → change 6; **exportação CSV** → change 10.
- **Risco de prazo:** M4 é 2026-11-21 e restam as changes 5b, 6 e 7 depois desta. Por isso a change é só back-end e tem tasks muito explícitas, para execução rápida.
