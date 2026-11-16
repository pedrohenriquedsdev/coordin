# Proposal

## Why

O back-end das demandas está no ar (change 5): a API cadastra demandas, recalcula as previsões e devolve o cronograma, mas o gestor ainda não vê nada disso. Esta change entrega as telas: o **cronograma**, que passa a ser a tela inicial e é a razão de existir do Coordin (RF-08, RF-11), o cadastro e a **ordem de prioridade** das demandas (RF-04 a RF-07) e o **registro da conclusão** com o desvio (RF-12). Sem as telas, as changes 6 (relatório por time) e 7 (implantação) não têm o que mostrar nem implantar.

## What Changes

- **Cronograma (T02, M01):** a tela inicial `/` passa a ser o cronograma consolidado, na ordem de execução, com a prioridade ao lado, esforço por time, início, previsão com e sem margem, fim informado, situação e motivo das não previsíveis, alertas (prazo informado excedido e ordem diferente da prioridade) e, nas concluídas, o desvio contra a linha de base e contra a previsão vigente. **Tabela** no computador e **cartões** a partir de 360 px, sem rolagem horizontal da página (RNF-03).
- **Demandas e prioridade (T03):** `/demands` lista as demandas ativas na ordem de prioridade, com **Subir** e **Descer** para reordenar (salva a lista inteira), edição, exclusão com confirmação e a aba **Concluídas**, só de leitura, com a data de conclusão e os desvios.
- **Nova e editar demanda (T04):** `/demands/new` e `/demands/:id/edit` com título, datas, times (com estimativa opcional em horas) e pessoas, aceitando salvar sem estimativa (regra inviolável 5).
- **Registrar conclusão (T06):** botão **Concluir** em cada demanda ativa, com a data real (padrão: hoje), recusando data anterior ao início.
- **Configurações:** `/settings` com a margem de segurança (percentual, de 0 a 1000).
- **Casca:** os links **Cronograma**, **Demandas**, **Times e pessoas**, **Calendário** e **Configurações**; a página inicial provisória (`home/`) sai.
- Nenhuma mudança no back-end, no banco, no cliente gerado nem em pacotes.

Roadmap (TDD 9.1): change 5b, `demand-screens`, branch `feat/demand-screens`, Must, data alvo 2026-11-19.
Atende (telas): **RF-04**, **RF-05**, **RF-06**, **RF-07**, **RF-09** (margem configurável e separada na previsão), **RF-10** (motivo das não previsíveis), **RF-11**, **RF-12**; **RNF-03** (cronograma no celular, sem rolagem horizontal) e **RNF-07** (pt-BR, datas `dd/MM/yyyy`); **DT-13**, **DT-17**, **DT-18**, **DT-27**; regra inviolável **5** e regras **2** e **3** (a tela nunca reordena sozinha).

## Capabilities

### New Capabilities

- `schedule-web`: o cronograma consolidado como tela inicial, em tabela no computador e em cartões no celular.
- `demands-web`: a navegação de planejamento, a lista de demandas com a ordem de prioridade, o formulário de demanda e o registro da conclusão.
- `settings-web`: a tela de configuração da margem de segurança.

### Modified Capabilities

Nenhuma. A spec `registry-web` continua valendo (a casca ganha links a mais, o que a nova spec `demands-web` descreve como um conjunto maior); a página inicial provisória não tinha spec própria.

## Impact

- **Código:** só `src/coordin-web` (novas pastas `planning/`, `schedule/`, `demands/` e `settings/`; ajustes em `app.routes.ts`, `app.html` e nos testes que citavam a página inicial provisória). A API, o banco, os pacotes e a CI não mudam.
- **Documentos:** `CLAUDE.md` §13 ao final.
- **Fora desta change (e onde entra):**
  - **Arrastar** para reordenar (a especificação fala em "arrastando"): fica de fora por exigir o `@angular/cdk`, decisão do autor em 2026-11-16; os botões Subir e Descer cumprem a função.
  - **Tela de detalhe** da demanda (T13, M02): o cronograma já mostra datas, margem, alerta e desvios; o detalhe completo (indicadores de precisão, RF-23) → change 12.
  - **Exportar CSV** (RF-19) → change 10; **Simular nova demanda** (T05) e cenários (T10, T11) → change 9; **dependências** entre demandas no formulário (T04) → change 8; **esforço real** (RF-22) → change 12; **relatório por time** (T09) → change 6.
  - **Modo de ordem `Custom`:** os alertas de "fora da ordem de prioridade" já aparecem se o modo existir, mas nada o liga até a change 9.
  - **Telas de cadastro no celular:** o PRD manda cadastros para o computador (RNF-03); só o cronograma é otimizado para 360 px.
- **Risco de prazo:** o M4 é 2026-11-21 e restam as changes 6 e 7 depois desta. As tasks são explícitas e fatiadas para execução rápida.
