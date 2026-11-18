## Context

Hoje, `PUT /api/demands/{id}/completion` grava só `CompletedOn` e roda dentro de `PlanningTransaction.RunAsync`, que faz a escrita, recalcula todas as previsões com `ForecastRecalculator` e confirma. O recálculo compara a previsão gravada com a nova e atualiza a tabela `Forecasts`, mas não registra o que mudou nem por quê. A estimativa por time (`DemandTeams.EstimatedEffortHours`) é alterada pelo cadastro da demanda, também dentro de uma transação que recalcula. Não há histórico, nem `ActualEffortHours`, nem indicadores no código. O TDD (seção 4.2) já prevê as tabelas `ForecastHistory` e `EstimateHistory` como somente de inclusão (DT-14), e `Demand.ActualEffortHours` como Could. Ver proposal.md, "What Changes", para os ajustes que precisam de aprovação.

## Goals / Non-Goals

**Goals:**
- Registrar, dentro da mesma transação da escrita, cada mudança de estimativa e de data prevista, com a causa.
- Gravar, na conclusão, a previsão e o esforço estimado vigentes, para os indicadores não dependerem de estado futuro.
- Calcular os indicadores no motor puro, com entradas e saídas explícitas (DT-02).
- Tela de detalhe e página de indicadores em gráfico e tabela, no mesmo padrão das changes 6 e 9b.

**Non-Goals:**
- Previsto × realizado com datas reais por time (fora do RF-20 a RF-23).
- Edição, exclusão ou correção de registros de histórico.
- Reconstituir o histórico de demandas anteriores a esta change.
- Relatório exportado (CSV) dos indicadores: não está no escopo.

## Decisions

**1. Causa propagada pelo `PlanningTransaction`.** `ForecastRecalculator.RecalculateAsync` recebe uma `ForecastChangeCause` (enum: `Estimate`, `Allocation`, `Capacity`, `Absence`, `Dependency`, `ApprovedScenario`, `NewDemand`) e grava um registro em `ForecastHistory` para cada demanda cuja data prevista mudou. `PlanningTransaction.RunAsync` ganha esse parâmetro obrigatório, e cada chamador informa a causa da própria escrita. *Alternativa:* deduzir a causa comparando estados. Rejeitada: uma escrita pode mexer em vários fatores e a comparação não diz qual deles mudou a data.

**2. Estimativa registrada no cadastro da demanda.** Quando `DemandTeams.EstimatedEffortHours` muda, o cadastro grava um registro em `EstimateHistory` (time, anterior, novo, motivo opcional, data). O motivo vem de um campo opcional `estimateChangeReason` no corpo da demanda, a aprovar no TDD. Se nenhum valor mudou, nada é gravado (cenário da spec).

**3. Conclusão grava o snapshot depois do recálculo.** `CompleteDemandAsync` passa a fazer a escrita, recalcular e só então gravar `ForecastEndDateAtCompletion` (lido de `Forecasts`) e `EstimatedEffortHoursAtCompletion` (soma das estimativas) na mesma transação. Para isso, `PlanningTransaction` ganha uma variante com um passo pós-recálculo, sem mudar o comportamento das outras chamadas. *Alternativa:* gravar antes do recálculo. Rejeitada: a previsão lida seria a de antes da conclusão, e não a vigente na conclusão.

**4. Histórico somente de inclusão no banco.** Um trigger `BEFORE UPDATE` nas duas tabelas recusa qualquer atualização. Um trigger `BEFORE DELETE` recusa exclusão quando `pg_trigger_depth() = 0`, ou seja, fora da cascata da demanda. A API não expõe edição nem exclusão. O teste usa SQL direto, como na change 3 e na 8, porque o EF recusa antes do banco.

**5. Indicadores no motor, sem relógio.** `Coordin.Engine/Accuracy` recebe as demandas concluídas (data de conclusão, linha de base, previsão na conclusão, estimado e real, times) e o período, e devolve os indicadores do setor e por time. Usa só `DateOnly` e não lê o relógio, como o resto do motor (teste de arquitetura da change 4). *Alternativa:* calcular em SQL. Rejeitada: a regra ficaria fora do motor testado.

**6. Demandas sem snapshot ficam de fora, contadas.** A consulta separa as demandas do período em: usadas, sem linha de base, sem esforço real e antigas (sem snapshot). Cada categoria sai na resposta, para a tela dizer o motivo.

**7. Telas no padrão existente.** `/demands/:id` é uma tela nova com as duas listas. `/reports/accuracy` usa o `ChartComponent` com barras por time e setor (DT-36), uma tabela com os mesmos valores, e cartões abaixo de 640 px.

## Risks / Trade-offs

- **Mudança no TDD antes de implementar.** Três ajustes (colunas, tabelas de Could para Must, campo `estimateChangeReason`) precisam de aprovação. Se não forem aprovados, a change não é implementada.
- **Histórico que começa agora.** Demandas anteriores não têm histórico nem snapshot. Mitigação: a spec exige que elas fiquem de fora e a tela diga quantas.
- **Custo do recálculo.** Gravar `ForecastHistory` a cada mudança soma linhas em toda escrita. Mitigação: só quando a data mudou; o volume do RNF-01 (200 demandas) é pequeno, e o teste de desempenho do CI é rodado de novo.
- **Premissas dos indicadores.** A média simples por demanda e o indicador por time com o total da demanda são premissas. Se o gestor preferir outra regra, a mudança é no motor e na spec, e os testes de regra ajustam.
- **Trigger no banco.** Um `pg_trigger_depth()` mal lido pode bloquear a cascata. Mitigação: teste de exclusão da demanda com histórico, na suíte da API.
