## Context

A API e o motor da validação estão arquivados e verificados (changes 11 e PR #35/#36): `GET /api/validation/status`, `POST /api/validation/dataset`, `GET`/`PUT /api/validation/baseline`, `POST /api/validation/current-plan` e `POST /api/validation/evaluate`, com as funções geradas em `src/coordin-web/src/app/api/fn/validation/`. O front já tem o padrão das telas de planejamento: store com sinais (`scenarios.store.ts`), `ChartComponent` sobre `chart.js` (DT-36), casca com menu condicionado à sessão (`web-shell`) e tratamento de erro do Problem Details pelo `readProblem`. Ver proposal.md, "Why", para a motivação.

## Goals / Non-Goals

**Goals:**
- Uma tela `/validation` com quatro etapas na ordem do PRD 7.3, em que cada etapa só libera a seguinte quando a anterior está concluída.
- Confirmação explícita antes da carga, que é a única ação destrutiva da área.
- Resultado com gráficos e tabelas com os mesmos valores, no computador e no celular (RNF-03), sem rolagem horizontal em 360 px.
- Toda regra de apresentação (validação dos parâmetros, texto dos motivos, montagem das séries dos gráficos) em funções puras testadas à parte, como em `planning/labels.ts` (lição da change 5b).

**Non-Goals:**
- Nenhuma regra de cálculo no front: prazos, desvios, médias e checagem vêm prontos da API (DT-02).
- Nenhuma persistência do resultado nem do estado do assistente entre recarregamentos: o estado da etapa fica na memória da tela; quem recarrega volta à etapa que o servidor indica (status).
- Nenhuma edição do conjunto fictício pela tela.
- Nenhum teste de navegador automatizado (Playwright fica na change 13); a verificação é manual, com Chrome headless.

## Decisions

**1. A etapa atual vem do servidor, não só da memória.** O status (`GET /api/validation/status`) informa se o conjunto foi carregado, quantas linhas de base existem e se o plano está guardado. A tela calcula a etapa a partir desse status ao abrir, e o assistente só guarda na memória o que o gestor digitou (datas, parâmetros). *Alternativa:* guardar a etapa no `localStorage`. Rejeitada: o servidor é a fonte da verdade e recarregar a página não pode levar a um passo que a API já desfez.

**2. Visibilidade pelo status, com cache na sessão.** A casca consulta `GET /api/validation/status` uma vez após o login: sucesso mostra o item de menu; 404 esconde. *Alternativa:* uma flag no front, vinda do build. Rejeitada: a flag é do ambiente (`Validation__Enabled`), e o front não deve ter um segundo interruptor que possa divergir da API.

**3. Confirmação em dois passos, como na change 9b.** A carga só é enviada quando a caixa "Entendo que todo o planejamento será substituído" está marcada e o botão é acionado. *Alternativa:* um diálogo modal. Rejeitada por ser um segundo mecanismo de confirmação no mesmo projeto; a caixa marcada deixa o aviso visível no próprio passo.

**4. Gráficos com `ChartComponent` e tabela ao lado.** Eficiência: barras agrupadas (planejamento atual × com o Coordin) para ociosidade e sobrecargas, e um valor isolado para dispersão e prazo do setor, que não cabem na mesma escala. Desvios: barras com a média com sinal e a absoluta, e uma tabela por demanda. Checagem das prioritárias: tabela com destaque das que pioram. A montagem das séries é função pura (`validation/labels.ts`, com testes), e o gráfico recebe só essas séries.

**5. Erros de regra viram mensagem, não falha genérica.** Os códigos `validation-current-plan-exists` (plano já guardado: informação), `validation-baseline-incomplete`, `validation-current-plan-missing` e `validation-nothing-to-compare` têm textos próprios em `validation/labels.ts`. Qualquer outro erro cai na mensagem genérica da casca.

**6. Datas de calendário sem fuso.** A linha de base é uma data de calendário (sem horário), tratada pelo `core/dates` e pelo `Clock` injetável, como na change 3 (lição 5). Os testes rodam em UTC, Tóquio e Los Angeles.

## Risks / Trade-offs

- **Carga acidental.** A confirmação reduz o risco, mas o carregamento apaga o plano guardado e a linha de base. Mitigação: o texto da etapa lista o que será apagado, e a API continua exigindo `confirmReplaceAll: true` (defesa em profundidade).
- **Linha de base digitada à mão.** São 30 datas. Mitigação: o formulário mostra as demandas na ordem de prioridade e aceita a mesma data para várias demandas num campo de preenchimento rápido, sem gravar nada até o botão de salvar.
- **Tamanho do bundle.** A tela não adiciona biblioteca nova, mas o `ChartComponent` já está no bundle; o aviso de orçamento de 500 kB já existe desde a change 8 (627 kB na change 10). Não é corrigido nesta change; registrar, se for o caso, na verificação.
- **Celular.** As tabelas de desvio por demanda têm 30 linhas. Mitigação: em 360 px, cada linha vira um cartão, como no cronograma (change 5b).
- **Conflito com o TDD.** Nenhum. O TDD 6.6 e DT-36 descrevem o assistente de quatro etapas com gráfico e tabela; esta change não muda nenhum DT-* nem a regra de cálculo.
