---
name: generate-tdd
description: Escreve ou continua o TDD do Coordin (docs/TDD.md), uma seção por vez, registrando cada decisão técnica como DT-* com alternativas e justificativa, sempre com aprovação do usuário via AskUserQuestion. Use quando o usuário pedir para criar, continuar, revisar ou ajustar o TDD, ou disser "generate tdd" ou "/generate-tdd [seção]".
metadata:
  author: Pedro Henrique dos Santos
  version: "1.0"
---

Conduz a etapa 2 do fluxo (TDD) definida no `CLAUDE.md` §4.2. O TDD define **o como**, alinhado ao PRD aprovado.

**Argumento opcional:** número da seção a trabalhar (ex.: `/generate-tdd 6`). Sem argumento, siga o "Próximo passo" do `CLAUDE.md` §13.

---

## Antes de começar

1. Leia o `CLAUDE.md` inteiro. Em conflito, ele e as instruções do usuário na sessão prevalecem sobre esta skill.
2. Confira se o `docs/PRD.md` está com Status "aprovado". Se não estiver, avise e pergunte se deve seguir mesmo assim; o TDD não pode contradizer o PRD.
3. Confira `git status` e a branch. O TDD é trabalhado na branch `docs/tdd`; se estiver em outra, pergunte antes de criar ou trocar.
4. Leia o PRD inteiro, a seção 9 dele (decisões delegadas ao TDD) e a proposta `docs/references/tcc-proposal.pdf` (se o Read não renderizar, use `pdftotext -layout` para o scratchpad).
5. Leia o `docs/TDD.md` atual, se existir. **Atualize, nunca recrie.** Seções e `DT-*` aprovados só mudam com aprovação explícita.

## Estrutura do TDD

| # | Seção | Conteúdo esperado |
|---|---|---|
| 1 | Contexto e objetivos técnicos | Resumo do PRD, `RNF-*` que guiam a arquitetura, restrições (prazo, usuário único) |
| 2 | Arquitetura | Componentes e responsabilidades; motor de cálculo isolado e puro (`CLAUDE.md` §8.1) |
| 3 | Stack | Linguagem, frameworks, banco, front, ferramentas de qualidade, com versões confirmadas em documentação oficial |
| 4 | Modelo de dados | Entidades, atributos, relacionamentos e regras de integridade |
| 5 | Contratos de API | Endpoints/operações, entradas, saídas e erros |
| 6 | Algoritmos de cálculo | Capacidade, previsão, margem, paralelismo, dependências, ociosidade/sobrecarga, otimização de cenários |
| 7 | Segurança e dados | Autenticação do usuário único, dados pessoais, segredos |
| 8 | Estratégia de testes | Testes de unidade do motor (casos de borda do `CLAUDE.md` §8.2), demais níveis, comandos de verificação |
| 9 | Roadmap de implementação | Ordem das changes do OpenSpec, com nome kebab-case, branch, `RF-*` atendidos e prioridade MoSCoW |
| 10 | Riscos técnicos e pendências | Riscos, mitigação e o que ainda não foi decidido |

O cabeçalho do TDD tem uma linha **Status** com as seções aprovadas e a seção em revisão.

## Decisões técnicas (`DT-*`)

Toda escolha relevante vira uma decisão no formato:

```
#### DT-NN — Título
- **Contexto:** o problema e os RF/RNF envolvidos.
- **Alternativas consideradas:** 2 ou mais, com prós e contras.
- **Decisão:** a escolhida.
- **Justificativa:** por que, incluindo trade-offs aceitos.
- **Atende:** RF-NN, RNF-NN.
```

- Os pontos em aberto do `CLAUDE.md` §7.3 e as decisões delegadas no PRD precisam terminar fechados em algum `DT-*`.
- A preferência por C#/.NET (`CLAUDE.md` §8.1) é o ponto de partida da stack, mas precisa ser confirmada pelo usuário.
- Versões, APIs e compatibilidade são verificadas em documentação oficial (Context7 ou sites oficiais) antes de entrar no texto. Não presuma suporte sem verificar.

## Ciclo de cada seção

1. **Levante as fontes:** o que o PRD, o `CLAUDE.md` e as seções aprovadas já determinam.
2. **Apresente as decisões da seção** pelo `AskUserQuestion` (até 4 por chamada): alternativas como opções, a recomendada marcada com "(Recomendado)" e o trade-off principal na descrição. Explique o porquê em poucas linhas no texto antes da pergunta.
3. **Escreva só a seção da vez**, em português, com identificadores de código em inglês e nomes do domínio (`Demand`, `Team`, `Capacity`, `Forecast`, `Scenario`, `SafetyMargin`).
4. **Verifique a coerência:** nenhum algoritmo ou contrato pode violar as regras invioláveis (`CLAUDE.md` §7.1) nem os critérios de aceite do PRD. Liste as suposições.
5. **Peça revisão** pelo `AskUserQuestion` (Aprovar / Pedir ajustes).
6. **Ao aprovar:** atualize a linha Status do TDD e o `CLAUDE.md` §13.
7. **Entregue o plano de commits** com os comandos prontos. **Nunca execute `git commit` nem `git push`.** Um commit por bloco lógico; se dois blocos ficarem no mesmo arquivo, guie o `git add -p` bloco a bloco, um comando por vez.

Pare depois do passo 7 e aguarde o usuário.

Se uma decisão técnica exigir mudar o PRD, pare, mostre o conflito e peça aprovação antes de editar o PRD.

## Ao concluir o TDD

1. Confirme que todos os pontos do `CLAUDE.md` §7.3 estão fechados por `DT-*`.
2. Mude o Status para "aprovado".
3. Proponha a atualização do `openspec/config.yaml` para referenciar `docs/PRD.md` e `docs/TDD.md` como fonte da verdade: mostre o conteúdo atual e a mudança pretendida antes de editar (`CLAUDE.md` §5).
4. Atualize o §13 com o próximo passo: a primeira change do roadmap via `/opsx:propose`.
5. Compare o roadmap com o prazo (`CLAUDE.md` §12) e proponha cortes de Should/Could se houver risco.
