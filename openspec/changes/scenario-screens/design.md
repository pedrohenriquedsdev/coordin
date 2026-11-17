# Design

## Context

A API de cenários (change 9, arquivada) já existe e foi verificada: `POST /api/scenarios/recommendations` (3,4 a 3,6 s no volume do RNF-01), `/simulate`, `/apply`, `/reset-to-priority` e `POST /api/demands/simulate`, com `position` opcional na criação. O cliente Angular já foi gerado (`api/fn/scenarios`, `api/models/scenario-*`, `demand-simulation-response`). O front tem o `ChartComponent` (DT-36), o padrão de estado por *store* com `signal`, a confirmação inline ("Confirmar exclusão"), Subir e Descer em `/demands`, `readProblem`/`messageFor` para erros, `core/dates` para datas de calendário e `planning/labels.ts` para os textos. Os números das respostas chegam tipados `number | string` e a regra do projeto é converter com `Number(...)` em função pura. Ver `proposal.md` para a motivação.

Fatos do back-end que moldam a tela (confirmados no código, não supostos):
- A ordem enviada a `simulate` e `apply` precisa conter **todas** as demandas, uma vez cada, inclusive as concluídas (`ValidateOrderAsync`); do contrário, `400` no campo `executionOrder`.
- Demandas concluídas e não previsíveis **não aparecem** em `items` do cenário (correção do TDD 5.3 feita na change 9), mas continuam em `executionOrder`.
- `apply` responde `200` com o cenário aplicado e `reset-to-priority` responde `204`.
- `isComparable` falso significa que o cenário deixaria uma demanda previsível de ficar previsível; a recomendação nunca devolve esse cenário, mas uma simulação própria pode.

## Goals / Non-Goals

**Goals:**
- Mostrar cenários recomendados e simulados no **mesmo componente**, para o RF-14 sair "no mesmo formato" do RF-13 sem código duplicado.
- Toda regra de apresentação (escolher as 10 barras, montar a ordem completa, rótulos de diferença e de sinal) em **funções puras** testadas à parte, sem Angular.
- Nunca chamar `apply` ou `reset-to-priority` sem um clique de confirmação do gestor (regra inviolável 2).
- Nenhuma regra de negócio no front: pontuação, datas e diferenças vêm prontas da API, e a tela não recalcula nada.

**Non-Goals:**
- Arrastar e soltar, histórico de cenários, comparação lado a lado de vários cenários, telas no celular otimizadas, qualquer mudança na API ou nos pacotes.

## Decisions

### D1. Uma pasta `scenarios/` com tela, estado, funções puras e gráfico

`scenarios/` ganha `scenarios.ts` (mapeamento das respostas para tipos do front, com `Number(...)`), `scenario-view.ts` (funções puras), `scenario-chart.ts` (a configuração do gráfico, como `team-report-chart.ts`), `scenarios.store.ts` (estado e chamadas), `scenarios-page.ts/html` (a tela) e `scenario-card.ts/html` (o cartão de um cenário, usado nas recomendações e na simulação). A simulação de nova demanda fica em `demands/` por viver dentro do formulário. **Alternativa:** tudo em um componente grande. Descartada: o cartão é reutilizado e a tela ficaria difícil de testar.

### D2. Busca sob pedido, e um único estado de "ocupado"

A tela não busca ao abrir, porque a recomendação leva alguns segundos e roda a busca local; abrir a tela só para olhar não deve custar isso. O *store* tem um `signal` de `busy` (`'idle' | 'searching' | 'simulating' | 'applying'`) que desabilita todos os botões de ação ao mesmo tempo, o que evita duas escritas ou uma aprovação com uma busca no meio. **Alternativa:** buscar ao abrir, com cache. Descartada: ocultaria que existe uma busca e esconderia a diferença entre "nenhuma melhoria" e "ainda não busquei".

### D3. A ordem completa montada por uma função pura

`buildFullOrder(currentOrder: string[], openIds: Set<string>, editedOpen: string[]): string[]` recebe a ordem de execução vigente (todas as demandas, do cronograma), o conjunto das abertas e a lista das abertas já reordenada pelo gestor, e devolve a ordem completa: as posições que hoje têm uma demanda **aberta** recebem, na sequência, as demandas da lista editada; as posições de demandas **concluídas** ficam intactas. Assim a API recebe o que valida, e as concluídas (que não consomem capacidade) ficam onde estavam, a mesma convenção da busca no motor. Testes: sem concluídas, concluídas no começo, no meio e no fim, lista editada igual à atual (devolve a ordem atual) e permutação.

### D4. Gráfico: as 10 maiores contribuições, com a tabela completa ao lado

`topContributions(items, limit = 10)` ordena por `|scoreContributionDelta|` decrescente (desempate por título e depois por id, para o resultado ser estável) e descarta as de contribuição zero. O gráfico é de barras **horizontais** (`indexAxis: 'y'`, aceito pelo `ChartComponent` tipado em `'bar'`), com o rótulo da demanda à esquerda, que lê melhor títulos longos do que o eixo X. Cor: barra **negativa** (a demanda ajuda a diminuir o critério) em verde-escuro e **positiva** em vermelho-escuro; o sentido também vai em texto na tabela ("melhora" e "piora"), então a cor nunca é a única informação. A tabela da justificativa traz as mesmas linhas do gráfico (título, peso, contribuição, sentido); a tabela completa do cenário traz todas as demandas de `items`. **Alternativa:** gráfico da data prevista antes e depois, que o PRD cita como exemplo. Descartada pelo autor em favor das contribuições do DT-36, que somam a diferença do critério e, por isso, explicam o cenário.

### D5. Aprovar e voltar à prioridade: confirmação inline em dois passos

Como em `/demands`, o primeiro clique troca o botão por "Confirmar aprovação" e "Cancelar" (sem diálogo nativo, que é ruim de testar e de usar com teclado). O texto da confirmação diz a consequência: "A ordem de execução passa a ser a deste cenário. A prioridade registrada não muda." O estado de "pedindo confirmação" vive no cartão (um `signal` local), e `apply` só é chamado no segundo clique. Depois de aprovar, o *store* limpa as recomendações e a simulação (agora desatualizadas, pois o cronograma mudou) e recarrega o cronograma. O botão de voltar à prioridade reaproveita o mesmo componente de confirmação em dois lugares (tela de cenários e cronograma), então ele é um componente pequeno `ResetToPriority` (aviso, botão, confirmação, chamada), usado pelas duas telas, em vez de duas cópias.

### D6. Cronograma: só o aviso e o botão

O `ScheduleStore` já guarda `executionOrderMode`. O cronograma mostra o `ResetToPriority` quando o modo é `Custom`. Ao concluir, recarrega com `load()`. Nada mais muda na tabela nem nos cartões.

### D7. Formulário de nova demanda: simular com o mesmo corpo do salvar

O corpo é montado por **uma só função** (`buildBody()`, extraída do `save()` atual) usada por "Salvar demanda" e por "Simular impacto", para os dois nunca divergirem (RF-16: o que se simula é o que se cria). A validação é a mesma (título, esforço) e a nova validação de `position` (inteiro de 1 a n + 1; n é o número de demandas já carregado em `store.candidates()`). O resultado da simulação fica num `signal`; qualquer alteração em campo, time, pessoa, pré-requisito ou posição o marca como **desatualizado** (um `signal` de "alterado depois de simular"), em vez de apagá-lo, para o gestor ainda ver o que tinha antes, com o aviso. "Descartar simulação" apaga só o resultado. **Alternativa:** apagar a simulação a cada mudança. Descartada: some a informação que o gestor estava comparando.

### D8. Textos e números

Rótulos novos entram em `planning/labels.ts` ou em `scenarios/scenario-view.ts`: `deltaDaysLabel` (reaproveita `deviationLabel` do cronograma, com o mesmo significado: positivo é atraso), `contributionLabel` ("melhora" para negativo, "piora" para positivo, "neutra" para zero), `scoreLabel` (número pt-BR sem casas) e `unpredictableReasonLabel` já existente para a nova demanda não previsível. Datas de calendário passam por `core/dates`, sem `Date` nem `DatePipe` (lição da change 1). Os números `number | string` do cliente viram `number` no mapeamento, nunca na tela.

### D9. Testes

Funções puras com Vitest simples (D3, D4, D7, D8). *Stores* e telas com `HttpTestingController`, como nas telas atuais; o gráfico com o `CHART_FACTORY` de teste (`chart-test-double`), sem tocar em `vi.mock` (lição da change 6). Cada tela tem teste de: carregamento, falha, caminho feliz, cada mensagem de erro e **"nada é chamado sem confirmação"**. A tela é verificada à mão no Chrome contra a API real e um banco descartável (largura de 1280 e de 360 px), como nas changes 5b e 6.

## Risks / Trade-offs

- **A busca leva de 3 a 4 s (medida em Debug).** Mitigação: botão sob pedido, estado "Calculando…" e botões desabilitados; sem tempo-limite no front além do padrão. Se a API devolver erro no meio, os cenários anteriores continuam na tela.
- **Cenários mostrados ficam velhos se o cronograma mudar por outro caminho** (por exemplo, em outra aba). A API valida a ordem na hora de aprovar e responde com erro se ela não for mais válida; a tela mostra a mensagem. Não há tempo real nem *polling*; o botão "Buscar recomendações" refaz tudo.
- **O gráfico mostra só 10 demandas.** Mitigação: a tabela completa e a soma das contribuições ficam na tela, e o texto diz "10 demandas que mais contribuem, de N".
- **Chart.js (já presente) em barras horizontais** funciona com `ChartConfiguration<'bar'>` e `indexAxis: 'y'`. Se a tipagem do componente não aceitar, o ajuste é na configuração, sem tocar no componente (verificado na primeira task).
- **O TDD (linha 9 do roadmap) ainda inclui as telas na change 9.** O ajuste é uma frase e só entra com a aprovação do autor (última task).

## Decisions delegadas ao autor

Nenhuma aberta. As quatro escolhas de escopo foram tomadas em 2026-11-17 (tela própria `/scenarios`, barras com tabela, botão "Simular impacto" no formulário e Subir e Descer); o limite de 10 barras, o botão de busca sob pedido e a confirmação inline são suposições minhas, registradas aqui para a revisão.
