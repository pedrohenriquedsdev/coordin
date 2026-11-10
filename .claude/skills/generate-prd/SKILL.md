---
name: generate-prd
description: Escreve ou continua o PRD do Coordin (docs/PRD.md), uma seção por vez, com entrevista via AskUserQuestion, suposições explícitas e aprovação do usuário a cada parte. Use quando o usuário pedir para criar, continuar, revisar ou ajustar o PRD, ou disser "generate prd" ou "/generate-prd [seção]".
metadata:
  author: Pedro Henrique dos Santos
  version: "1.0"
---

Conduz a etapa 1 do fluxo (PRD) definida no `CLAUDE.md` §4.1. O PRD define **o quê e por quê**; nunca a solução técnica.

**Argumento opcional:** número da seção a trabalhar (ex.: `/generate-prd 6`). Sem argumento, siga o "Próximo passo" do `CLAUDE.md` §13.

---

## Antes de começar

1. Leia o `CLAUDE.md` inteiro. Em conflito, ele e as instruções do usuário na sessão prevalecem sobre esta skill.
2. Confira `git status` e a branch. O PRD é trabalhado na branch `docs/prd`; se estiver em outra, pergunte antes de trocar.
3. Leia a proposta `docs/references/tcc-proposal.pdf` inteira. Se o Read não renderizar o PDF, extraia o texto com `pdftotext -layout` para o scratchpad (não instale ferramentas sem autorização).
4. Leia o `docs/PRD.md` atual, se existir. **Atualize, nunca recrie.** Seções aprovadas só mudam com aprovação explícita.

## Estrutura do PRD

| # | Seção | Conteúdo esperado |
|---|---|---|
| 1 | Visão geral | O que é, objetivo central, princípio, envolvidos, contexto, resultados esperados |
| 2 | Problema | Contexto, situação atual, dores, síntese |
| 3 | Personas | Papéis, quem usa o sistema, responsabilidades, objetivos, dores |
| 4 | Fluxo de uso | Etapas (quem, o quê, resultado) e replanejamento |
| 5 | Requisitos funcionais | `RF-*` por MoSCoW (5.1 Must, 5.2 Should, 5.3 Could), cada um com critérios de aceite verificáveis |
| 6 | Requisitos não funcionais e regras invioláveis | `RNF-*` verificáveis + regras do `CLAUDE.md` §7.1 |
| 7 | Métricas de sucesso | Métrica principal (desvio previsto × realizado) e como medir |
| 8 | Escopo e não objetivos | O que entra e o que explicitamente não entra |
| 9 | Premissas, riscos e decisões delegadas ao TDD | Pontos em aberto do `CLAUDE.md` §7.3 e os que surgirem |
| 10 | Marcos até 2026-11-28 | Datas ISO 8601 por etapa |

O cabeçalho do PRD tem uma linha **Status** com as seções aprovadas e a seção em revisão.

## Ciclo de cada seção (ou subseção)

1. **Levante as fontes.** Diga em poucas linhas o que a proposta, o `CLAUDE.md` e as seções aprovadas já definem para a seção.
2. **Entreviste antes de escrever.** O que faltar e mudar o comportamento ou o escopo vira pergunta pelo `AskUserQuestion` (até 4 por chamada), com opções claras e a recomendada marcada com "(Recomendado)". Prefira perguntas que destravem requisitos, não detalhes cosméticos. Se uma resposta conflitar com uma seção aprovada, diga qual e o ajuste necessário.
3. **Escreva só a seção da vez**, em português, usando apenas fontes e respostas. Sem stack, algoritmo ou modelo de dados; o que for "como" vira decisão delegada ao TDD.
4. **Revise contra invenção.** Releia procurando frequências, números, nomes ou regras sem fonte. O que for interpretação sua para tornar um critério verificável vai para uma lista de **suposições** na resposta: menores (aprovar em bloco) e que mudam comportamento (pergunta individual).
5. **Peça revisão** pelo `AskUserQuestion` (Aprovar / Pedir ajustes), junto com as perguntas sobre suposições.
6. **Ao aprovar:** atualize a linha Status do PRD e o `CLAUDE.md` §13 (checklist, decisões já tomadas para as próximas seções e "Próximo passo").
7. **Entregue o plano de commits** com os comandos prontos. **Nunca execute `git commit` nem `git push`.** Um commit por bloco lógico: a seção do PRD, ajustes em seções aprovadas e a atualização do `CLAUDE.md` são commits separados. Se dois blocos ficarem no mesmo arquivo, guie o `git add -p` bloco a bloco (qual hunk recebe `y` ou `n`) e lembre que ele é interativo: um comando por vez.

Pare depois do passo 7 e aguarde o usuário.

## Regras de conteúdo

- **Requisitos funcionais:** `#### RF-NN — Título`, uma frase do que o usuário faz ou o sistema garante, e critérios de aceite em lista, cada um verificável por teste ou demonstração. Numeração contínua entre Must, Should e Could.
- **Não funcionais:** `RNF-NN`, com critério mensurável.
- **Regras invioláveis** (`CLAUDE.md` §7.1) aparecem nos requisitos em que se aplicam e não podem ser contrariadas por nenhum critério.
- Esforço e capacidade sempre em horas; datas em ISO 8601.
- Referencie requisitos por ID quando um depender de outro.

## Ao concluir o PRD

Quando todas as seções estiverem aprovadas: mude o Status para "aprovado", atualize o §13 com o próximo passo (TDD com `/generate-tdd`), compare o andamento com o prazo (`CLAUDE.md` §12) e avise se Should ou Could estiverem em risco.
