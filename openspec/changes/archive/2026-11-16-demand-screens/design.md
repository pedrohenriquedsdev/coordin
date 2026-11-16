# Design

## Context

- O back-end já entrega tudo que as telas pedem (change 5): `GET`/`POST`/`PUT`/`DELETE /api/demands`, `PUT /api/demands/priority-order`, `PUT /api/demands/{id}/completion`, `GET /api/schedule` e `GET`/`PUT /api/settings`. O cliente Angular gerado (`src/app/api/fn/demands`, `schedule`, `settings`) já está no repositório.
- O front tem o padrão das telas de cadastro (change 3): um *store* com *signals* por tela (`TeamsStore`, `CalendarStore`), páginas e seções standalone com `templateUrl`, formulários reativos tipados, mensagens de erro por `messageFor(readProblem(error), fallback)`, datas de calendário como texto (`core/dates`) e `Clock` injetável para "hoje". Tudo é reaproveitado; nenhuma dependência nova.
- Os números das respostas geradas vêm tipados como `number | string` (ex.: `executionRank`, `totalEstimatedEffortHours`); os *stores* existentes os convertem com `Number(...)` para interfaces locais. Esta change faz o mesmo.
- Ver proposal.md para o escopo e o que fica de fora.

## Goals / Non-Goals

**Goals:**
- O cronograma como tela inicial, legível no computador (projetado em reunião) e no celular a partir de 360 px, sem rolagem horizontal.
- Cadastro de demandas completo, com a ordem de prioridade, a conclusão e a margem, seguindo o padrão das telas existentes.
- Nenhuma regra de cálculo no front: o front só mostra o que a API devolveu e rotula (motivo, alerta, desvio).

**Non-Goals:**
- Arrastar para reordenar, tela de detalhe, dependências no formulário, simulação, exportação (ver proposal.md).
- Testes E2E no navegador (change 13); a verificação do navegador aqui é manual, por roteiro.

## Decisions

### D1. Pastas e arquivos

`src/app/planning/` (tipos locais e mapeamento das respostas, rótulos em português), e uma pasta por tela: `schedule/`, `demands/`, `settings/`. Cada tela tem seu *store* e sua página, no padrão de `teams/`. A pasta `home/` e os testes dela saem; a rota `/` passa a ser o cronograma.

### D2. Tipos locais e mapeamento (`planning/planning.ts`)

Interfaces `ScheduleItem`, `Schedule`, `Demand`, `DemandTeam` e funções puras `mapSchedule(response)` e `mapDemand(response)` que convertem `number | string` em `number` e preservam `null`. As páginas e os *stores* nunca usam os tipos gerados diretamente. As datas continuam como texto `yyyy-MM-dd`.

### D3. Rótulos em português (`planning/labels.ts`)

Funções puras, testadas à parte:
- `unpredictableReasonLabel`: `MissingEstimate` → "Falta a estimativa de algum time"; `MissingStartDate` → "Sem data de início"; `NoCapacity` → "Sem capacidade: falta pessoa alocada ou a capacidade é zero"; outro valor → "Sem previsão".
- `deviationLabel(days)`: positivo → "N dia(s) de atraso" (singular em 1); negativo → "N dia(s) antes"; zero → "no prazo".

### D4. Cronograma: um dado, duas apresentações

`SchedulePage` busca o cronograma uma vez e o desenha de duas formas no mesmo componente: uma `<table class="hidden md:table">` e uma `<ul class="md:hidden">` de cartões. A troca é só CSS (Tailwind, ponto `md` = 768 px), sem JavaScript nem `matchMedia`, o que funciona igual em qualquer fuso e é fácil de testar (os dois existem no DOM; os testes consultam cada um pelo `aria-label`). Alertas e estado usam **texto**, nunca só cor.

*Alternativa:* uma tabela com rolagem horizontal. Descartada: viola o RNF-03 ("sem rolagem horizontal da página").

### D5. Margem separada

O cronograma mostra "Sem margem: dd/MM/yyyy" e "Com margem: dd/MM/yyyy" (as duas datas já vêm da API); a parte da margem é a diferença visível entre elas (RF-09). O front não calcula dias de margem.

### D6. Reordenar: trocar duas posições da lista completa

A API exige a lista **completa** dos ids (inclusive as concluídas, que mantêm a prioridade). O *store* guarda a lista completa em ordem de prioridade. "Subir" e "Descer" acham o vizinho **ativo** (não concluído) mais próximo na direção pedida e **trocam as duas posições** na lista completa, sem mexer nas outras (concluídas ficam onde estão). Só então chama `reorderDemands`. Se a API recusar, o estado não muda.

*Alternativa:* arrastar (Angular CDK). Fora do escopo por decisão do autor (dependência nova); os botões também são acessíveis pelo teclado.

### D7. Conclusão inline

"Concluir" abre, na própria linha, um formulário pequeno (data + Confirmar + Cancelar), com a data inicial vinda de `Clock.today()`. A tela verifica "antes do início" com `isEndBeforeStart(startDate, completedOn)` quando a demanda tem início; a API repete a regra.

### D8. Formulário de demanda: times como linhas, pessoas derivadas

O formulário reativo tem `title`, `startDate`, `informedEndDate`, e um *signal* `selectedTeams` (mapa `teamId → texto das horas`) e `assignedPersonIds` (conjunto). Os times são listados com uma caixa "Envolver o time"; ao marcar, aparece o campo de horas (vazio = ainda sem estimativa). As pessoas oferecidas são as dos times marcados; ao desmarcar um time, as pessoas dele saem da alocação. Na edição, o formulário é preenchido com `GET /api/demands/{id}`. Uma demanda concluída mostra só a mensagem de que não pode ser alterada.

### D9. Erros e estados

Toda escrita mostra erro com `messageFor(readProblem(error), fallback)`, sem perder o que foi digitado, como as telas de cadastro. Todas as telas têm os estados "Carregando…", vazio e falha com "Tentar de novo".

### D10. Rotas e casca

Todas as rotas novas ficam no grupo de filhas de `''` com `authGuard`: `''` (cronograma), `demands`, `demands/new`, `demands/:id/edit` e `settings`. A casca (`app.html`) ganha os links, em ordem: Cronograma (com `routerLinkActiveOptions: { exact: true }`, para não ficar destacado em todas as telas), Demandas, Times e pessoas, Calendário e Configurações.

### D11. Datas

Datas de calendário só passam por `CalendarDatePipe`/`formatCalendarDate` e `<input type="date">` (que já usa texto `yyyy-MM-dd`). Nenhum `Date`, `DatePipe` ou `new Date(texto)`. Os testes das datas rodam também com `TZ=Asia/Tokyo` e `TZ=America/Los_Angeles`.

## Risks / Trade-offs

- **[Risco]** O cronograma tem muita informação para 360 px (R-08 do PRD). → *Mitigação:* cartões com os dados em linhas curtas e rótulos; verificação manual em 360 px, sem rolagem horizontal (task final).
- **[Risco]** Os dois desenhos (tabela e cartões) duplicam o markup. → *Mitigação:* os rótulos e formatações vêm de funções e pipes compartilhados; só o *layout* é duplicado.
- **[Trade-off]** Botões em vez de arrastar: um pouco mais lento para mover uma demanda muito longe, mas acessível e sem dependência. O arrastar pode vir depois sem mudar a API.
- **[Trade-off]** Sem tela de detalhe: o desvio e a margem ficam na lista e no cronograma; o detalhe completo (indicadores, RF-23) vem na change 12.
