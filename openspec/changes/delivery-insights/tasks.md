## 0. Pré-requisito

- [x] 0.1 Pedir ao autor a aprovação dos três ajustes do TDD (colunas em `Demand`, `ForecastHistory` e `EstimateHistory` como Must, `estimateChangeReason`) e editar o `docs/TDD.md` só depois dela (regra do projeto: mudança no TDD exige aprovação)

## 1. Motor: causa da mudança e indicadores

- [ ] 1.1 Adicionar `ForecastChangeCause` e a lista de mudanças de data prevista ao resultado do recálculo (`Coordin.Engine/Forecasting`), com testes de unidade para cada causa e para demanda cuja data não muda (RF-21; design, decisão 1)
- [ ] 1.2 Criar `Coordin.Engine/Accuracy` com o cálculo do desvio médio de prazo, do percentual no prazo, da diferença média estimado × real e dos indicadores por setor e por time, com testes para: desvio positivo e negativo, percentual com uma demanda atrasada (66,7%), demanda sem linha de base, demanda sem esforço real, demanda com dois times, período e demandas de fora (RF-23; spec `accuracy-indicators`)
- [ ] 1.3 Testes de determinismo do módulo: mesma entrada em ordem diferente dá o mesmo resultado, e o teste de arquitetura do motor continua verde (sem `DateTime`, `TimeZoneInfo` nem `Random`)

## 2. Banco e persistência

- [ ] 2.1 Migração `AddDeliveryInsights`: tabelas `ForecastHistory` e `EstimateHistory` com chaves e `CHECK` (valor maior que zero), colunas `ActualEffortHours`, `ForecastEndDateAtCompletion` e `EstimatedEffortHoursAtCompletion` em `Demands`, e triggers de somente inclusão (design, decisão 4)
- [ ] 2.2 Testes de integração com SQL direto: `UPDATE` em histórico recusado, `DELETE` direto recusado, exclusão da demanda com histórico passando pela cascata, `CHECK` de esforço real zero recusado (spec `demand-history`, requisito de somente inclusão)
- [ ] 2.3 Atualizar `MigrationOnStartupTests` com a nova migration (lição da change 4)

## 3. API: histórico e conclusão

- [ ] 3.1 `ForecastRecalculator` grava `ForecastHistory` com a causa informada; `PlanningTransaction.RunAsync` recebe a causa de cada escrita, e todos os chamadores atuais são atualizados com a causa correta (design, decisão 1)
- [ ] 3.2 Cadastro da demanda grava `EstimateHistory` quando um time muda de estimativa, com o motivo opcional, e não grava quando o valor é o mesmo (spec `demand-history`, cenários de estimativa)
- [ ] 3.3 Conclusão aceita `actualEffortHours` opcional, valida maior que zero e grava os dois snapshots depois do recálculo, na mesma transação, com a variante pós-recálculo do `PlanningTransaction` (spec `completion-effort`; design, decisão 3)
- [ ] 3.4 Testes de integração: primeira previsão gera o primeiro registro; mudança por dependência, por alocação e por cenário aprovado geram a causa certa; previsão que não muda não gera registro; segunda conclusão responde 409 e não muda os snapshots
- [ ] 3.5 Endpoints `GET /api/demands/{id}/estimate-history`, `GET /api/demands/{id}/forecast-history` (ordem cronológica, 404 para demanda inexistente) e `GET /api/reports/accuracy?from=&to=` (datas obrigatórias, período invertido com erro de campo, contagem das demandas de fora) (spec `demand-history`, `accuracy-indicators`)
- [ ] 3.6 Regenerar o cliente Angular com o OpenAPI exportado da fábrica de testes (lições da change 5: `--ignoreUnusedModels false`, restaurar `api-configuration.ts`, pasta `api` em LF)

## 4. Front: telas

- [ ] 4.1 Rota `/demands/:id` com as duas listas de histórico, estados de carregamento e falha, e a mensagem de demanda inexistente (spec `delivery-web`)
- [ ] 4.2 Link para o detalhe em cada demanda da lista; testes de componente do link
- [ ] 4.3 Campo opcional de esforço real no fluxo de conclusão, com a validação de maior que zero antes do envio; campo de motivo opcional na edição de estimativa
- [ ] 4.4 Página `/reports/accuracy` com filtro de período, gráfico por time e setor (`ChartComponent`), tabela com os mesmos valores, contagem das demandas de fora e marcação dos times acima do setor com texto
- [ ] 4.5 Layout em 360 px: tabelas viram cartões abaixo de 640 px e a página não rola na horizontal (RNF-03)
- [ ] 4.6 Testes de componente e de store, e teste de que os valores do gráfico e da tabela são iguais

## 5. Verificação

- [ ] 5.1 Executar os comandos do TDD 8.6 em cópia limpa: `dotnet build`, `dotnet format --verify-no-changes`, `dotnet test` com cobertura e a categoria de desempenho, e `npm ci`, `npm run lint`, `npm run format:check`, `npm test` nos fusos UTC, Tóquio e Los Angeles, e `npm run build` com código 0
- [ ] 5.2 Cobertura do motor de cálculo medida e informada no PR (meta de 90% das linhas, sem bloquear o merge)
- [ ] 5.3 Verificação manual no Chrome headless, com API e PostgreSQL descartáveis, credenciais temporárias e autorização do autor: concluir com e sem esforço real, ver os históricos no detalhe, filtrar indicadores por período, conferir gráfico e tabela, layout em 360 px e 1280 px, e zero violações de CSP
- [ ] 5.4 `openspec validate delivery-insights --strict` válido
