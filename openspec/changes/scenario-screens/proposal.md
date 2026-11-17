# Proposal

## Why

A change 9 (`sequencing-scenarios`) deixou no ar o motor e a API que recomendam, simulam e aprovam cenários de sequenciamento e simulam uma nova demanda, mas o gestor ainda não consegue usar nada disso: não há tela. O que o Coordin existe para fazer (achar sequências melhores sem trabalho manual, RF-13 a RF-16) só vira valor quando o gestor **vê** o cenário, entende **por que** ele é melhor e **decide** (regra inviolável 2: o sistema nunca reordena sozinho). Esta change entrega essas telas, só no front, sobre uma API que já foi verificada com o volume do RNF-01.

## What Changes

- **Tela `/scenarios` (nova, link "Cenários" no menu):**
  - **Recomendações (RF-13):** botão "Buscar recomendações" (a busca leva alguns segundos, então não roda sozinha ao abrir a tela) que mostra até 3 cenários, ou a mensagem de que nenhuma alternativa melhora o resultado.
  - **Cada cenário** mostra o resumo (critério atual × cenário e a diferença, prazo do setor e dias a mais ou a menos), a **justificativa** (gráfico de barras com `scoreContributionDelta` das demandas que mais contribuem, **acompanhado da tabela com os mesmos valores**, DT-36) e a **tabela completa** com prioridade registrada ao lado da nova posição, datas atuais e novas e a diferença em dias.
  - **Simular uma ordem própria (RF-14):** a lista das demandas abertas na ordem de execução vigente, com **Subir** e **Descer** (sem arrastar e sem `@angular/cdk`, decisão do autor), e o botão "Simular", que mostra o resultado **no mesmo formato** dos cenários recomendados, sem gravar nada.
  - **Aprovar ou rejeitar (RF-15):** "Aprovar este cenário" pede confirmação, grava a ordem de execução e refaz a tela; "Rejeitar" descarta o cenário da tela e não muda nada no servidor. A prioridade registrada nunca muda.
- **Cronograma (`/`):** quando o modo é `Custom`, aviso "A ordem de execução difere da ordem de prioridade" com o botão **Voltar à ordem de prioridade** (RF-15). As marcas "Fora da ordem de prioridade" por demanda já existem desde a change 5b.
- **Formulário de demanda (`/demands/new`):** campo opcional **Posição** (1 a n + 1) e botão **Simular impacto** (RF-16), que mostra as demandas que mudam de data, em quantos dias, o prazo do setor e a previsão da própria demanda, **sem gravar**. "Salvar demanda" cria na mesma posição; descartar é não salvar. Só na criação: na edição o campo não aparece.
- **Casca:** o link **Cenários** entra no menu.
- Nenhuma mudança no back-end, no banco, no cliente gerado nem em pacotes (o `ChartComponent` e o `chart.js` já existem).

Roadmap (TDD 9.1): change 9b, `scenario-screens`, branch `feat/scenario-screens`, Should, divisão da linha 9 decidida pelo autor em 2026-11-17 (o TDD ainda lista as telas dentro da linha 9; o ajuste é proposto na última task e só editado com aprovação).
Atende (telas): **RF-13**, **RF-14**, **RF-15**, **RF-16**; **RNF-03** (a tela de cenários usa os mesmos padrões responsivos do cronograma), **RNF-07** (pt-BR, datas `dd/MM/yyyy`); **DT-25** (o que o critério mostra), **DT-36** (gráfico em componente próprio, sempre com tabela); regras invioláveis **2** e **3** (nada é aplicado sem o gestor aprovar, e a prioridade nunca muda).

## Capabilities

### New Capabilities

- `scenarios-web`: a tela de cenários, com a recomendação, a simulação de uma ordem própria, a justificativa em gráfico e tabela e a aprovação ou rejeição.
- `demand-impact-web`: a simulação do impacto de uma nova demanda dentro do formulário, com a posição opcional.

### Modified Capabilities

- `schedule-web`: o cronograma ganha o aviso do modo `Custom` e o botão de voltar à ordem de prioridade (um requisito acrescentado, sem mexer nos existentes).

Nenhum requisito existente de `demands-web` muda: o formulário só ganha comportamento novo, descrito em `demand-impact-web`, e o link "Cenários" do menu fica em `scenarios-web`. (O requisito "Navegação de planejamento" de `demands-web` lista cinco links e já está defasado desde a change 6, que acrescentou "Relatório por time"; esta change não o reescreve para não ampliar o escopo, e o desvio fica registrado aqui.)

## Impact

- **Código:** só `src/coordin-web` (nova pasta `scenarios/` com a tela, o estado, as funções puras e o gráfico; ajustes em `schedule/`, `demands/`, `planning/`, `app.routes.ts` e `app.html`, e nos testes que citam o menu). A API, o banco, o cliente gerado, os pacotes e a CI não mudam.
- **Documentos:** `CLAUDE.md` §13 ao final; ajuste da linha 9 do roadmap do TDD (divisão em 9 e 9b), só com aprovação do autor.
- **Fora desta change (e onde entra):**
  - **Arrastar e soltar** a ordem: exige `@angular/cdk`, decisão do autor; Subir e Descer cumprem a função.
  - **Guardar cenários** ou comparar vários ao mesmo tempo: o gestor vê os cenários da última busca ou simulação, sem histórico (o back-end também não guarda).
  - **Simular nova demanda com dependências na tela de cenários:** a simulação fica só no formulário; os pré-requisitos já existem lá.
  - **Exportar CSV** (RF-19) → change 10; **validação por simulação** (RF-24) → change 11; **indicadores de precisão** (RF-23) → change 12.
  - **Cenários no celular:** o PRD manda os cadastros e as ferramentas de decisão para o computador (RNF-03); a tela não quebra em 360 px (sem rolagem horizontal da página), mas só o cronograma é otimizado em cartões.
- **Risco de prazo:** o M5 é 2026-11-24 e restam, depois desta, as changes 10 e 11. As tasks são fatiadas para execução rápida, com a lógica em funções puras testadas à parte.
