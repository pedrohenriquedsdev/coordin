# Proposal

## Why

A dor central do Coordin (PRD 2) é o trabalho manual do gestor para **achar ociosidade e equilibrar a carga entre os times**. O motor já calcula quando cada demanda termina, mas ainda não diz **onde há gente parada e onde há fila esperando**. Esta change entrega o relatório por time (RF-17, promovido a Must em 2026-11-12): por semana, quanta capacidade cada time tem, quantas horas estão alocadas, quantas estão livres e em quais semanas o trabalho na fila excede a capacidade. É também o que a validação por simulação (change 11) usa para medir a eficiência do setor.

## What Changes

- **Cálculo (motor):** `Coordin.Engine/Reporting/TeamReport`, função pura que, a partir do snapshot e do resultado da previsão, devolve para cada time e cada semana (segunda a domingo) a capacidade, as horas alocadas, a ocupação, a ociosidade e a sobrecarga, e quantas demandas não previsíveis o time tem. Não muda o simulador: tudo sai do consumo diário que ele já devolve.
- **API:** `GET /api/reports/teams?from=&to=`, que carrega o planejamento, calcula e devolve o relatório. Nada é gravado.
- **Tela "Relatório por time" (T09, M03):** `/reports/teams` com o período, o resumo por time, o detalhe semanal do time escolhido e o **gráfico** (capacidade × horas alocadas, com ociosidade e sobrecarga destacadas) com a **tabela** dos mesmos valores. No celular, em cartões, sem rolagem horizontal.
- **`ChartComponent` (DT-36):** componente próprio sobre o `chart.js` 4.6.x, que os cenários (change 9) e os indicadores (change 12) vão reaproveitar.
- **Dependência nova:** `chart.js` (npm), **autorizada pelo autor em 2026-11-16** (CLAUDE.md §11).
- **Definição de sobrecarga, esclarecida (decisão do autor, 2026-11-16):** semana em que o time usou **toda** a capacidade e ainda restou parte elegível com esforço por fazer. O texto anterior do DT-24 ("elegível e terminou o período com esforço restante") marcaria quase toda semana de trabalho normal. O DT-24 é atualizado.
- **Cliente Angular** regenerado a partir do OpenAPI.

Roadmap (TDD 9.1): change 6, `team-report`, branch `feat/team-report`, Must, data alvo 2026-11-20.
Atende: **RF-17**; **DT-24**, **DT-36** (só o gráfico do relatório), **DT-02**; **RNF-02** (determinismo), **RNF-03** (relatório no celular), **RNF-08** (verificabilidade), **RNF-07**; regras invioláveis **1**, **4** e **5** (o relatório não muda nenhuma delas: só lê o cálculo).

## Capabilities

### New Capabilities

- `team-report-calculation`: o cálculo semanal de capacidade, horas alocadas, ocupação, ociosidade e sobrecarga por time (motor).
- `team-report-api`: a consulta do relatório por período, com validação e proteção por login.
- `team-report-web`: a tela do relatório por time, o gráfico e a tabela, no computador e no celular.

### Modified Capabilities

Nenhuma. A casca ganha um link, o que a spec nova `team-report-web` descreve.

## Impact

- **Código:** `src/Coordin.Engine/Reporting/` (novo), `src/Coordin.Api/Features/Reports/` (novo), `src/coordin-web/src/app/reports/` e `charts/` (novos), `app.routes.ts` e `app.html`, e `package.json` com o `package-lock.json` (só o `chart.js`). O simulador, as demandas e o banco **não mudam**; nenhuma migration.
- **Documentos:** TDD DT-24 (sobrecarga) e seção 5.2 (parâmetros do relatório), e `CLAUDE.md` §13 ao final.
- **Fora desta change (e onde entra):**
  - **Gráficos de cenários** (change 9) e de **indicadores de precisão** (change 12): o `ChartComponent` fica pronto, as telas não.
  - **Período em mês:** o relatório é semanal (DT-24); o período parametrizável fica no código do motor, sem tela para trocá-lo.
  - **Realocação automática ou sugestão de quem pode apoiar quem:** o relatório só mostra a ociosidade; o gestor decide (princípio central).
  - **Exportação do relatório** e **dependências entre demandas** (change 8): a elegibilidade de uma parte com dependências será revista na change 8.
- **Risco de prazo:** o M4 é 2026-11-21 e falta a change 7 (implantação) depois desta. Por isso o relatório é calculado **sob demanda** (sem tabela nova nem migration) e a tela cobre só o necessário: período, resumo, detalhe e gráfico.
