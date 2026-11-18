# Proposal

## Why

O prazo do TCC não permite acompanhar demandas reais do início à conclusão. A validação é uma **simulação** (PRD 7.3): compara o planejamento do processo atual com o planejamento feito com o Coordin, sobre um conjunto fictício, medindo as métricas de eficiência do setor (PRD 7.2) e o desvio das datas (PRD 7.1). Sem essa área, o TCC não tem como demonstrar o resultado do sistema. O RF-24 não pode ser cortado (PRD 5.4). Esta change entrega o **back-end** da validação (motor, banco, API e conjunto embutido); o assistente na tela é a change 11b.

## What Changes

- **Motor (`Coordin.Engine`):** o `ValidationEvaluator` (TDD 6.6, DT-37), puro e determinístico. Recebe o planejamento do processo atual (guardado), o planejamento vigente, as datas de linha de base e os parâmetros (`minEffortFactor`, `maxEffortFactor`, `seed`), e devolve:
  - as **métricas de eficiência** dos dois planejamentos (horas ociosas, equilíbrio de ocupação, períodos de sobrecarga e prazo total do setor);
  - o **realizado simulado** (esforço × um fator reproduzível por demanda, margem zero) e o **desvio** da linha de base e das previsões do Coordin, por demanda e na média;
  - a **checagem das mais prioritárias** (as 5 demandas de maior prioridade, previsão do processo atual × previsão com o Coordin), que sustenta o critério "sem piorar o prazo das mais prioritárias" do PRD 7.2.
- **Banco:** duas tabelas, `ValidationBaseline` (data de linha de base por demanda) e `ValidationCurrentPlan` (o planejamento do processo atual, guardado uma única vez), e a migration `AddValidation`.
- **API, só com `Validation__Enabled=true`** (desligada por padrão; desligada, as rotas respondem `404`): `GET /api/validation/status`, `POST /api/validation/dataset` (exige `confirmReplaceAll: true`), `GET` e `PUT /api/validation/baseline`, `POST /api/validation/current-plan` e `POST /api/validation/evaluate`.
- **Conjunto fictício embutido** na API (recurso JSON versionado): 7 times, cerca de 35 pessoas, 30 demandas, ausências e feriados, sem nenhum dado real (PRD 7.3, M2). O carregamento substitui todo o planejamento do banco, em uma transação com recálculo, e **preserva a conta do gestor** e os tokens de sessão.
- **Cliente Angular regenerado** a partir do OpenAPI (sem tela nesta change).
- Nenhuma mudança nas regras de previsão, na margem, nos cenários nem no relatório por time: a validação os **reutiliza**.

Roadmap (TDD 9.1): linha 11, `simulation-validation`, `feat/simulation-validation`, apoio à validação (não cortada). A divisão em 11 (back-end) e 11b (assistente na tela) foi **decidida pelo autor em 2026-11-18**; o ajuste da linha do roadmap é proposto na última task e só é editado com a aprovação dele. Datas alvo propostas: 2026-11-21 (11) e 2026-11-23 (11b), dentro da janela do M6.
Atende: **RF-24** (back-end), **RNF-02** (mesmo resultado para os mesmos dados e parâmetros), **RNF-05** (sem dado real); **DT-37**, **DT-21**, **DT-22**, **DT-24**, **DT-27**; PRD 7.1, 7.2 e 7.3; regras invioláveis **1** (a margem não reduz prazo: o realizado simulado usa margem zero só para medir o real, sem tocar na previsão) e **3** (a prioridade registrada não é alterada).

## Capabilities

### New Capabilities

- `validation-evaluation`: o cálculo da avaliação no motor (fatores reproduzíveis, realizado simulado, eficiência, desvios e mais prioritárias).
- `validation-dataset`: o conjunto fictício embutido e a regra do que ele contém.
- `validation-api`: a habilitação por configuração, o carregamento do conjunto, a linha de base, o plano atual e a avaliação.

### Modified Capabilities

Nenhuma. As specs existentes (`delivery-forecast`, `team-report-calculation`, `scenario-*`) continuam valendo e são reutilizadas sem mudança.

## Impact

- **Código:** `src/Coordin.Engine` (nova pasta `Validation/`), `src/Coordin.Api` (nova pasta `Features/Validation/` com entidades, configuração do EF, migration, opções, endpoints e o recurso embutido), cliente Angular regenerado. Testes em `tests/Coordin.Engine.Tests` e `tests/Coordin.Api.Tests`.
- **Banco:** a migration `AddValidation` (duas tabelas novas). Nenhuma tabela existente muda.
- **Configuração:** a variável `Validation__Enabled` (desligada por padrão). Em produção, ligá-la é uma decisão do autor, **só durante a validação** (RT-11).
- **Dependências:** nenhuma.
- **Documentos:** TDD (5.3, 6.6, DT-18 e a linha do roadmap), com a aprovação do autor na última task; `CLAUDE.md` §13 ao final.
- **Fora desta change (e onde entra):**
  - **O assistente na tela e os gráficos** → change 11b.
  - **O questionário do gestor** (PRD 7.4): é feito fora do sistema.
  - **Persistir o resultado da avaliação:** o resultado é recalculado a cada pedido (DT-37), como os cenários.
  - **Importar um conjunto próprio:** só existe o conjunto embutido.
- **Risco de prazo:** esta é a maior das changes restantes. O ponto de atenção é o conteúdo do conjunto fictício, que o autor revisa antes do carregador (uma pausa nas tasks), e depois o gestor (P-03).
