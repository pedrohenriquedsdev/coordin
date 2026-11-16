# Design

## Context

- O simulador (`ForecastSimulator`, change 4) já devolve o **consumo diário**: `ConsumedHours(PersonId, DemandId, TeamId, Date, Hours)` para cada dia, pessoa e demanda. Como o relatório é derivado dele (DT-24), **o simulador não muda**.
- O consumo diário **não é persistido** (só as datas previstas de cada demanda, DT-03). O relatório recalcula com o motor sob demanda: para o volume do projeto (até 200 demandas, TDD 8.5) o cálculo leva milissegundos.
- A API tem o padrão por feature (`Features/<Nome>`: contratos e endpoints estáticos), `PlanningSnapshotLoader` (banco → snapshot), `ApiProblems.InvalidField` e testes com PostgreSQL real. O front tem o padrão de tela da change 5b: *store* com *signals*, página com `templateUrl`, tabela no computador e cartões no celular (`hidden md:table` e `md:hidden`), datas em `core/dates`, mensagens por `messageFor`.
- Ver proposal.md para o escopo e o que fica de fora.

## Goals / Non-Goals

**Goals:**
- Um cálculo puro, testado com os casos de borda e com invariantes em planejamentos gerados, que o relatório da tela e a avaliação da validação (change 11) usem igual.
- Uma tela que responda duas perguntas do gestor: "quem tem capacidade livre?" e "quem está com fila acima da capacidade?", com tabela sempre ao lado do gráfico.

**Non-Goals:**
- Persistir o relatório, histórico de relatórios, exportação, período em mês na tela, e sugestão automática de realocação (o gestor decide).

## Decisions

### D1. O cálculo vive em `Coordin.Engine/Reporting/TeamReport.cs`

Função pura `TeamReport.Build(PlanningSnapshot, ForecastResult, DateOnly from, DateOnly to)`, mais `TeamReport.AlignedPeriod(from, to)` (para a API validar o tamanho do período sem repetir a regra das semanas). Tipos de saída `record`s imutáveis: `TeamReportResult` (`PeriodStart`, `PeriodEnd`, `Teams`), `TeamReportTeam` (`TeamId`, `Weeks`, `UnpredictableDemandCount`) e `TeamReportWeek` (`WeekStart`, `WeekEnd`, `CapacityHours`, `AllocatedHours`, `Occupancy`, `IdleHours`, `IsOverloaded`, `OverloadedDemandIds`). Sem dependências, sem relógio.

### D2. Tudo sai do snapshot e do resultado da previsão

- **Semanas:** o início vira a segunda-feira da semana de `from`; o fim, o domingo da semana de `to`. Cada semana tem 7 dias.
- **Capacidade** da semana: soma, de segunda a domingo, de `PersonCapacityCalendar.TeamCapacityOn(team, dia)` (já considera fim de semana, feriado e ausência).
- **Horas alocadas:** soma das `Hours` de `ConsumedHours` do time na semana.
- **Ocupação** = alocadas ÷ capacidade (`null` se a capacidade é zero). **Ociosidade** = `max(capacidade − alocadas, 0)`.
- **Não previsíveis:** demandas com `Status = Unpredictable` no resultado que têm parte do time.

### D3. Sobrecarga (decisão do autor, 2026-11-16)

Uma semana é sobrecarregada para o time quando **(a)** a capacidade é maior que zero, **(b)** as horas alocadas **igualam** a capacidade (toda a capacidade foi usada) e **(c)** ao fim da semana ainda existe parte do time **elegível com esforço restante**. Elegível: a demanda é previsível (aparece como `Predictable` no resultado) e a data de início é igual ou anterior ao domingo da semana. Esforço restante: o esforço estimado da parte menos o que foi consumido até o fim da semana (soma acumulada das `Hours` do time para aquela demanda, até a data final da semana) é maior que zero. A lista de demandas afetadas vem na ordem da fila (ranking do modo de ordem, depois `Id`). O DT-24 é atualizado com essa definição; a avaliação da validação (TDD 6.6) usa o mesmo relatório.

*Alternativa:* "qualquer parte elegível com esforço restante" (texto antigo do DT-24). Descartada: toda demanda em andamento tem esforço restante no fim da semana, então marcaria sobrecarga na maioria das semanas, mesmo com capacidade sobrando.

### D4. A API calcula sob demanda e não grava nada

`GET /api/reports/teams?from=&to=`: valida `from` e `to` como texto `yyyy-MM-dd` (formato inválido ou ausente → `400` no campo; `to` antes de `from` → `400` em `to`; mais de 52 semanas → `400` em `to`), carrega o snapshot com `PlanningSnapshotLoader`, roda `ForecastEngine.Calculate`, monta o relatório com `TeamReport.Build` e acrescenta os nomes dos times e os títulos das demandas lidos do banco. Os times saem em ordem alfabética (ordinal, sem diferenciar maiúsculas, como em `TeamEndpoints`). Não usa transação nem recalculador: só leitura.

*Alternativa:* persistir o consumo diário para consultar sem recalcular. Descartada: nova tabela e migration, mais código e mais risco para o prazo, sem ganho perceptível no volume do projeto.

### D5. Contrato da resposta

`periodStart`, `periodEnd` e `teams[]` com `teamId`, `teamName`, `unpredictableDemandCount` e `weeks[]`; cada semana com `weekStart`, `weekEnd`, `capacityHours`, `allocatedHours`, `occupancy` (nulo quando a capacidade é zero), `idleHours`, `isOverloaded` e `overloadedDemands[]` (`id`, `title`). Datas `yyyy-MM-dd`; horas e ocupação como número.

### D6. Gráfico: Chart.js em um componente próprio (DT-36, dependência autorizada)

`chart.js` 4.6.x, com os componentes registrados individualmente (`BarController`, `BarElement`, `CategoryScale`, `LinearScale`, `Tooltip`, `Legend`; sem `chart.js/auto`), em `charts/chart-setup.ts`. O `ChartComponent` (`app-chart`) recebe a configuração por `input.required()`, pega o `<canvas>` por `viewChild.required()`, cria o gráfico e o atualiza em um `effect()`, e o destrói no `DestroyRef`. Não tem regra de negócio: a tela monta a configuração por uma função pura (`buildTeamChart`), testada sem canvas. O teste do componente simula o módulo `chart.js` (`vi.mock`), porque o jsdom não desenha em `<canvas>`.

*O gráfico:* barras empilhadas por semana, com **horas alocadas** (cinza escuro, vermelho nas semanas sobrecarregadas) e **horas ociosas** (cinza claro); as duas somam a capacidade. Destaque não depende só de cor: a tabela mostra "Sobrecarga" e a legenda do gráfico nomeia as séries.

### D7. Organização do front

`charts/` (`chart-setup.ts`, `chart.component.ts`) e `reports/` (`team-report.ts` com os tipos locais, o mapeamento e `summarize`; `team-report-chart.ts`; `team-report.store.ts`; `team-report-page.ts` e `.html`). Os números do cliente gerado vêm como `number | string`: converta com `Number(...)`, como nas outras telas.

### D8. Datas sem `Date` local

`core/dates` ganha funções puras `addDays`, `mondayOf`, `sundayOf` e `weeksBetween`, feitas com aritmética em **UTC** (`Date.UTC` e `getUTC*`, que não dependem do fuso do navegador) sobre o texto `yyyy-MM-dd`. O período padrão da tela (segunda-feira da semana de hoje até o domingo da oitava semana) usa `Clock.today()`. Os testes rodam também com `TZ=Asia/Tokyo` e `TZ=America/Los_Angeles`.

### D9. Responsivo

Resumo e semanas aparecem em tabela (`hidden md:table`) e em cartões (`md:hidden`), no padrão do cronograma; o gráfico fica num contêiner de largura total e altura fixa (`h-72`), com `responsive: true` e `maintainAspectRatio: false`. Verificado em 1280 px e 360 px.

## Risks / Trade-offs

- **[Risco]** O `chart.js` é uma dependência nova. → *Mitigação:* autorizada pelo autor; versão e licença conferidas no registro antes de instalar; `npm audit` informado no PR; nada além do `chart.js` (e seu `@kurkle/color`) entra no `package-lock.json`.
- **[Risco]** A definição de sobrecarga depende de a parte ser "elegível". Com as dependências (change 8), a elegibilidade muda. → *Mitigação:* a elegibilidade está isolada em um método do `TeamReport`, que a change 8 ajusta.
- **[Trade-off]** Recalcular o motor a cada consulta em vez de ler dados gravados: um pouco mais de CPU por requisição, mas sem migration e sempre coerente com o planejamento atual.
- **[Trade-off]** O relatório é semanal fixo na tela (DT-24 deixa o período como parâmetro do motor); trocar para mês depois é mudança de tela e de rótulos, não do cálculo.
