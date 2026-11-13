# Tasks — forecast-engine

Regras para todas as tasks:
- Nenhum pacote novo é necessário (o motor não pode ter nenhuma dependência, DT-02). Se surgir a necessidade de instalar algum, a task pausa e pede a autorização do autor (`CLAUDE.md` §11).
- Só o motor e os testes do motor mudam: nenhum arquivo de `Coordin.Api`, `coordin-web`, migrations, `package*.json` ou CI.
- Ao fim de cada grupo, o build e os testes (novos e antigos) passam, e o autor recebe **uma** lista de comandos de commit, com **aproximadamente um commit por grupo** e as marcações deste `tasks.md` dentro do commit do código a que se referem. Cada commit compila e passa nos testes sozinho.
- Testes no padrão Arrange–Act–Assert, nomes `Metodo_Situacao_ResultadoEsperado`, construindo os cenários com o `PlanningSnapshotBuilder` (task 1.2).
- Nenhum dado real ou segredo entra no repositório, nem nos testes.

## 1. Entrada, saída e validação do snapshot

- [x] 1.1 Criar os `record`s imutáveis de `Planning/` (`PlanningSnapshot`, `TeamInput`, `PersonInput`, `AbsenceInput`, `HolidayInput`, `DemandInput`, `DemandPartInput`, `AssignmentInput`, `ExecutionOrderMode`) e de `Forecasting/` (`ForecastResult`, `DemandForecast`, `DemandStatus`, `UnpredictableReason`, `ConsumedHours`) conforme a D2; verificar com `dotnet build` sem avisos e com o teste de arquitetura do motor ainda passando
- [x] 1.2 Criar a validação do snapshot (D6) e o `PlanningSnapshotBuilder` nos testes; verificar com testes de unidade que recusam com erro de argumento: capacidade negativa, esforço zero ou negativo, margem negativa, `Id` duplicado, alocação a pessoa ou demanda inexistente, alocação de pessoa cujo time não é da demanda e ausência com fim antes do início, e que um snapshot vazio e um snapshot válido são aceitos (cenários "Dados inválidos são rejeitados" e "Planejamento vazio")

## 2. Calendário e capacidade por data

- [ ] 2.1 Criar `Capacity/WorkCalendar` (`IsBusinessDay`, `NextBusinessDay`, `AddBusinessDays`) com os feriados; verificar com os cenários da spec `daily-capacity`: "Dia de semana comum", "Fim de semana", "Feriado em dia de semana", "Pular o fim de semana", "Pular um feriado" e "Avançar zero dias"
- [ ] 2.2 Criar `Capacity/PersonCapacityCalendar` (`CapacityOf(person, date)` e `CapacityOf(team, date)`), reaproveitando o `TeamCapacity`; verificar com os cenários da spec `daily-capacity`: "Dia útil sem ausência", "Ausência cobre a data", "Limites da ausência", "Ausência de outra pessoa", "Capacidade zero cadastrada", "Uma pessoa ausente", "Time sem pessoas", e um teste que roda o mesmo cálculo com `TimeZoneInfo` diferente do fuso da máquina e confere as mesmas datas ("Mesmo resultado em qualquer fuso")

## 3. Previsibilidade

- [ ] 3.1 Criar `Forecasting/PredictabilityClassifier` (concluída, `MissingStartDate`, `MissingEstimate`, `NoCapacity`, previsível, com a precedência da spec); verificar com os cenários da spec `forecast-predictability`: "Demanda concluída", "Sem data de início", "Um dos dois times sem estimativa", "Demanda sem nenhuma estimativa cadastrada", "Demanda sem times", "Parte sem pessoa alocada", "Pessoa alocada com capacidade zero", "Capacidade perdida só por ausência", "Sem data de início e sem estimativa" e "Sem estimativa e sem pessoa"

## 4. Simulação e margem

- [ ] 4.1 Criar `Forecasting/SafetyMargin` (dias de margem e data final, DT-22); verificar com os cenários da spec `delivery-forecast`: "Margem de 20% em 10 dias úteis", "Margem de 20% em 1 dia útil", "Margem de 0%" e "Margem que cruza fim de semana e feriado"
- [ ] 4.2 Criar `Forecasting/ForecastSimulator` com a fila por pessoa (D4), o modo de ordem de execução (D5), a elegibilidade isolada em um método e o horizonte de 5 anos com a segunda passada (D7); verificar com os cenários "Uma pessoa, 16 horas", "Esforço que não fecha em dias inteiros", "Início em fim de semana", "Duas pessoas no mesmo time", "Ausência no meio da execução", "Feriado no meio da execução", "Fim é o da última parte", "Dividir o esforço não dá a metade do prazo", "Demanda posterior espera", "Sobra do dia vai para a próxima", "Demanda posterior não toma capacidade da anterior", "Demanda ainda não iniciada não segura a fila", "Pessoa sem nenhuma demanda elegível", "Modo FollowPriority", "Modo Custom", "Empate", "Capacidade irrisória" (cálculo termina e demandas atrás da que estourou o horizonte são calculadas sem ela)
- [ ] 4.3 Criar `Forecasting/ForecastEngine.Calculate`, que valida, classifica, simula, aplica a margem e monta o `ForecastResult` ordenado (D5); verificar com os cenários "Demanda não previsível antes de uma previsível", "Estimativa chega depois", "Demanda seguinte começa depois do esforço, não da margem", "Esforço consumido bate com o estimado", "Capacidade nunca ultrapassada" (num caso montado à mão) e "Sem demandas"

## 5. Invariantes e determinismo

- [ ] 5.1 Criar o `RandomPlanningGenerator(seed)` nos testes (DT-34); verificar com testes que a mesma semente gera o mesmo snapshot e que ele cobre times, pessoas, ausências, feriados, demandas sem dados e vários times na mesma demanda
- [ ] 5.2 Escrever os testes de invariantes sobre pelo menos 300 snapshots de semente fixa; verificar os cenários "Invariante em planejamentos gerados" (margem nunca reduz, percentuais de 0% a 100%), "Capacidade nunca ultrapassada", "Esforço consumido bate com o estimado", "Listas embaralhadas" (resultado idêntico), "Sem relógio", e um invariante da regra 4: acrescentar um time a uma demanda nunca faz o fim sem margem ficar anterior ao da parte mais lenta; um teste de sanidade com uma mutação (por exemplo, margem negativa forçada) deve fazer o invariante falhar

## 6. Verificação final e documentos

- [ ] 6.1 Rodar os comandos do TDD 8.6 numa cópia limpa do commit: `dotnet build Coordin.slnx` (0 avisos), `dotnet format Coordin.slnx --verify-no-changes`, `dotnet test Coordin.slnx --collect:"XPlat Code Coverage"` (informar o total e a cobertura de linhas do `Coordin.Engine`, meta de 90%, sem bloquear), `npm ci`, `npm run lint`, `npm test` e `npm run build` em `src/coordin-web` (devem continuar iguais) e `openspec validate forecast-engine --strict`
- [ ] 6.2 Atualizar o `CLAUDE.md` §13 com a change 4 e as lições, preparar a descrição do PR em português com o template, e entregar a lista final de comandos de commit (nenhum `git commit` ou `git push` é executado por Claude)
