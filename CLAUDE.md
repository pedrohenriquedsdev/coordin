# CLAUDE.md — Coordin

Instruções permanentes para o Claude Code neste repositório. Leia este arquivo inteiro no início de cada sessão e siga-o em todas as etapas. Em caso de conflito, as instruções explícitas do usuário na sessão prevalecem; registre a divergência quando ela afetar o fluxo.

---

## 1. Sobre o projeto

**Coordin** é o software do meu TCC: um sistema de planejamento e apoio à decisão que transforma demandas priorizadas, esforço estimado e capacidade dos times em previsões de entrega, compara sequências alternativas de execução e aponta ociosidade e sobrecarga.

- **Usuário principal:** gestor de desenvolvimento. **Recebe as previsões:** gestor de produto. **Fornecem estimativas:** os times.
- **Princípio central:** o gestor sempre decide; o sistema calcula, mostra o impacto e recomenda.
- **Fonte original:** `docs/references/tcc-proposal.pdf`. Leia antes de qualquer etapa de documentação.
- **Prazo de entrega do TCC:** 2026-11-28.

---

## 2. Como trabalhamos

- **Você escreve, eu reviso.** Você produz documentos, especificações e código; eu reviso e aprovo cada entrega. Nada é considerado aprovado sem confirmação explícita minha.
- **Com calma, uma parte por vez.** Uma etapa do fluxo por vez; dentro de um documento, uma seção (ou poucas) por vez. Ao terminar cada parte, **pare e aguarde minha revisão**.
- **Perguntas sempre pela ferramenta de questões do harness (`AskUserQuestion`)**, nunca no texto da resposta. Vale para entrevistas, esclarecimentos, escolhas, confirmações de edição e aprovação de artefatos. Ofereça opções claras e marque a recomendada com "(Recomendado)". Se a ferramenta estiver indisponível, informe o bloqueio e pare; nunca presuma aprovação.
- **Não invente.** Use só o que está na proposta, nos documentos aprovados, neste arquivo ou no que eu disser. O que faltar vira pergunta ou "ponto em aberto" explícito. Não declare execuções, testes, aprovações, PRs ou merges que não aconteceram.
- **Seja direto.** Respostas curtas e objetivas; explique o porquê quando ajudar a decidir.
- **Alerte riscos cedo.** Ao perceber risco de prazo, de escopo ou de regra de negócio, avise imediatamente, sem esperar pergunta.
- **Ao fim de cada etapa ou sessão:** atualize a seção "Estado atual" (§13) e diga qual é o próximo passo.

---

## 3. Estrutura e convenções do repositório

- **Nomes de pastas, arquivos, branches, commits e identificadores de código: sempre em inglês.**
- **Conteúdo dos documentos** (PRD, TDD, specs, descrição de PR): em português.
- **A estrutura de pastas do código ainda será definida** (no TDD). Até lá, não crie pastas de código nem proponha estrutura sem discutir comigo.

```
coordin/
├── CLAUDE.md                     # este arquivo
├── .claude/                      # comandos e skills do OpenSpec para o Claude Code
├── .github/
│   └── pull_request_template.md  # template obrigatório de PR
├── docs/
│   ├── PRD.md
│   ├── TDD.md
│   └── references/
│       └── tcc-proposal.pdf
└── openspec/
    ├── config.yaml               # contexto do projeto; aponta para PRD e TDD
    ├── specs/                    # specs vigentes (fonte da verdade do comportamento)
    └── changes/                  # changes em andamento e arquivadas
```

- **IDs rastreáveis:** requisitos funcionais `RF-01`, não funcionais `RNF-01`, decisões técnicas do TDD `DT-01`. Changes do OpenSpec, commits relevantes e PRs citam os IDs que atendem.
- **Datas** em ISO 8601 (`2026-11-28`), fuso `America/Sao_Paulo`. **Esforço** sempre em horas.

---

## 4. Fluxo de trabalho: PRD → TDD → SDD → PR

Toda funcionalidade ou mudança de comportamento segue o fluxo completo, em uma **feature branch criada a partir da `main`**, sem pular etapas e sem trabalhar diretamente na `main`.

| Etapa | Artefato | Pronto quando |
|---|---|---|
| 1. PRD | `docs/PRD.md` | Problema, personas, fluxo, requisitos (MoSCoW) com critérios de aceite, métricas e escopo aprovados por mim |
| 2. TDD | `docs/TDD.md` | Arquitetura, stack, modelo de dados, contratos, algoritmos de cálculo, estratégia de testes e roadmap aprovados por mim |
| 3. SDD (OpenSpec) | `openspec/changes/<change>/` | Proposta, specs e tasks revisadas e aprovadas **antes** de qualquer código; depois, implementação e testes seguindo as tasks |
| 4. Pull Request | PR da feature branch → `main` | Implementação concluída, verificações executadas, PR aberto com o template preenchido |

### 4.1 PRD
- Define **o quê e por quê**: problema, personas, fluxo de uso, requisitos com critérios de aceite verificáveis, métricas, escopo e não objetivos, premissas e riscos.
- **Sem solução técnica.** Algoritmos, stack e modelo de dados vão para o TDD. Os pontos em aberto da proposta entram como "decisões delegadas ao TDD".

### 4.2 TDD
- Define **o como**, alinhado ao PRD: arquitetura, stack, modelo de dados, contratos de API, algoritmos (previsão, margem, paralelismo, dependências, otimização), segurança, estratégia de testes e **roadmap de implementação** (ordem das changes).
- Cada decisão recebe um ID `DT-*`, com alternativas consideradas e justificativa.
- Fecha os pontos em aberto listados em §7.3.

### 4.3 SDD com OpenSpec
- Uma change por funcionalidade do roadmap, rastreável aos `RF-*` e `DT-*`.
- Revise e valide proposta, specs e tasks comigo antes de implementar.
- Implemente seguindo as tasks aprovadas; se uma decisão mudar no caminho, atualize PRD, TDD e specs para manter tudo coerente.

### 4.4 Pull Request
- Somente após implementação e verificações. Detalhes em §10.

### 4.5 Regras gerais do fluxo
- Em mudanças sobre documentos existentes, **atualize** os artefatos em vez de recriá-los.
- Se uma etapa não se aplicar (ex.: correção puramente editorial), registre a justificativa no PR.
- Pendências de validação ou aprovação nunca contam como concluídas.

---

## 5. OpenSpec

- Já inicializado para o Claude Code: comandos e skills em `.claude/`, configuração em `openspec/config.yaml` (schema `spec-driven`). Antes da etapa SDD, confirme que a CLI responde (`openspec --version`).
- `openspec/config.yaml` deve referenciar `docs/PRD.md` e `docs/TDD.md` como fonte da verdade. Antes de editar esse arquivo, mostre o conteúdo atual e a mudança pretendida.
- Ciclo de uma change:
  1. `/opsx:propose "<descrição>"` → gera proposta, specs (deltas), design e tasks. **Pare para minha revisão.**
  2. Ajuste até eu aprovar.
  3. `/opsx:apply` → implemente as tasks, uma por vez, marcando o progresso em `tasks.md`.
  4. Abra o PR (§10).
  5. Após o merge, `/opsx:archive` para consolidar as specs em `openspec/specs/`.
- Nenhum código de aplicação fora de uma change aprovada.
- Nomes de changes em inglês e kebab-case, alinhados à branch (ex.: change `forecast-engine` na branch `feat/forecast-engine`).

---

## 6. Ferramentas necessárias

Verifique a disponibilidade antes da etapa que depende de cada ferramenta. Esta lista define requisitos, não afirma que tudo esteja instalado. **Não instale dependências, configure serviços nem obtenha credenciais sem minha autorização.**

| Ferramenta | Uso | Requisito |
|---|---|---|
| `AskUserQuestion` | Toda interação que exige resposta minha | Obrigatória. Indisponível → informar o bloqueio e parar. |
| Read / Glob / Grep / Edit / Write | Consultar fontes; manter PRD, TDD, specs, código e template de PR | Obrigatórias. Prefira as ferramentas dedicadas ao shell para ler e editar. |
| Terminal + Git | Inspecionar o repositório, criar branches, rodar verificações, preparar os comandos de commit | Obrigatórios. Confira `git status` e preserve alterações existentes antes de operar. |
| OpenSpec CLI + comandos `/opsx:*` | Etapa SDD | Obrigatória no SDD. A existência de `openspec/` não prova configuração válida. |
| Documentação oficial (Context7 ou sites oficiais) | Confirmar APIs, versões e compatibilidade da stack | Necessária sempre que uma decisão ou implementação depender disso. Não presuma suporte sem verificar. |
| Runtime, gerenciador de pacotes e ferramentas de qualidade definidos no TDD | Build, testes, lint, formatação | Conforme a stack aprovada; não presuma stack antes do TDD. |
| GitHub CLI (`gh`) autenticado | Abrir e acompanhar PRs | Necessário na etapa de PR. Não declare PR aberto ou integrado sem confirmação da plataforma. |

Se uma ferramenta obrigatória faltar, registre a etapa bloqueada e o que falta, sem simular execução. Use alternativa equivalente só se ela preservar as validações do fluxo.

---

## 7. Domínio e regras de negócio

### 7.1 Regras invioláveis
1. **A margem de segurança nunca reduz o prazo**: ela só pode ampliar a previsão.
2. **O sistema nunca reordena demandas sozinho.** Mudança de sequência é sempre um cenário apresentado com seu impacto, aguardando aprovação ou rejeição do gestor.
3. **A prioridade definida entre produto e desenvolvimento é preservada**; alternativas aparecem como recomendação, nunca como substituição silenciosa.
4. **Adicionar um time não divide o prazo automaticamente pela metade**: a previsão considera a capacidade efetiva.
5. **O esforço pode não existir quando a demanda é cadastrada**: o sistema deve aceitar demandas sem estimativa e tratá-las como "não previsíveis" até que a estimativa chegue.

Qualquer mudança nessas regras exige minha aprovação explícita.

### 7.2 Decisões já tomadas
- **Contexto:** setor de desenvolvimento real, com 7 ou mais times.
- **Estimativas:** em horas, informadas pelos times e registradas pelo gestor.
- **Dependências** entre demandas acontecem às vezes: suportadas, mas não são o caso principal.
- **Escopo:** tudo o que está na proposta, priorizado com MoSCoW:
  - **Must:** cadastros (times, pessoas, capacidade, demandas), cálculo de previsão com margem, cronograma.
  - **Should:** cenários de sequenciamento com aprovação do gestor, relatório por time (ocupação, ociosidade, sobrecarga).
  - **Could:** previsto × realizado detalhado.
  - Ajustes podem ser propostos, com justificativa.
- **Métrica principal de sucesso:** desvio entre a data prevista e a data realizada.

### 7.3 Pontos em aberto (decidir no TDD)
Modelo de capacidade (horas por pessoa e por time, férias, ausências, outras alocações) · paralelismo entre times na mesma demanda · representação de dependências · tipo da margem (percentual, fixa ou configurável) e etapa em que é aplicada · critério de otimização (prazo total, prioritárias, utilização ou combinação) · registro de mudanças de estimativa e comparação previsto × realizado.

---

## 8. Engenharia

### 8.1 Arquitetura
- **Motor de cálculo isolado**: previsão, margem, paralelismo, dependências e otimização ficam em um módulo puro, sem banco, sem HTTP e sem interface. Entradas e saídas explícitas, comportamento determinístico (sem relógio do sistema ou aleatoriedade sem injeção).
- API e front consomem o motor; regras de negócio não ficam espalhadas em controllers ou componentes.
- Stack, padrões e camadas definidos no TDD. Preferência pessoal: **C#/.NET** como linguagem principal (confirmar no TDD).

### 8.2 Testes
- O motor de cálculo tem **testes de unidade obrigatórios**, cobrindo os critérios de aceite do PRD e casos de borda: esforço zero ou ausente, time sem capacidade, ausência no meio do período, vários times na mesma demanda, dependência em cadeia, ciclo de dependência (deve ser rejeitado), margem aplicada e arredondada para cima.
- Cada task de implementação vem com seus testes. Nenhuma task é concluída com testes falhando.
- Rode as verificações (build, testes, lint/formatação definidos no TDD) antes de cada commit de código e antes do PR, e informe os comandos e resultados reais.

### 8.3 Qualidade
- Código e identificadores em inglês; nomes que reflitam o domínio (`Demand`, `Team`, `Capacity`, `Forecast`, `Scenario`, `SafetyMargin`).
- Sem refatorações oportunistas fora do escopo da change.
- Sem segredos, credenciais ou dados pessoais no repositório.

---

## 9. Git: branches e commits

### 9.1 Branches
- **Semânticas, em inglês**, formato `type/short-description` em kebab-case: `docs/prd`, `docs/tdd`, `feat/forecast-engine`, `fix/margin-rounding`, `chore/pr-template`.
- Sempre criadas a partir da `main` atualizada. Uma branch por etapa documental ou por change do OpenSpec.
- Nunca trabalhe nem faça push direto na `main`.

### 9.2 Commits
- **Semânticos (Conventional Commits), em inglês**: `type(scope): description`, no imperativo, sem ponto final.
  - Ex.: `docs(prd): add vision and problem sections`, `feat(forecast): calculate end date with safety margin`, `test(forecast): cover multi-team parallelism`.
  - Tipos: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `build`, `ci`, `style`, `perf`.
- **Um commit por bloco lógico. Não economize commits.** Cada mudança coesa e independente vira um commit próprio: uma seção de documento, uma entidade, uma regra com seus testes, uma configuração. Nunca misture documentação, refatoração e funcionalidade sem relação no mesmo commit.
- Cada commit deve ser verificável; não commite código que quebre build ou testes.
- **Eu sempre faço os commits.** Ao fim de cada bloco lógico, mostre a lista de commits planejados (mensagem + arquivos de cada um) com os comandos `git add`/`git commit` prontos para copiar. Nunca execute `git commit` nem `git push`.

---

## 10. Pull Requests

### 10.1 Preparação
- Um PR = uma mudança coesa (normalmente uma change do OpenSpec ou uma etapa documental), de tamanho revisável.
- Origem: a feature branch. Destino: `main`.
- Antes de abrir: confira branch, commits e diff completo contra a `main`; verifique se não há segredos ou dados pessoais; consulte PRs existentes da branch. Se já houver PR para a mesma mudança, atualize-o em vez de abrir outro.
- Se `.github/pull_request_template.md` não existir, proponha criá-lo (em uma branch `chore/pr-template`) antes do primeiro PR.

### 10.2 Título
- Formato **`type(scope): summary`**, em inglês, sem ponto final, com os mesmos tipos dos commits. Ex.: `feat(forecast): add safety margin to delivery forecast`, `docs(prd): define product requirements`.
- O tipo reflete o objetivo principal do PR. Mudanças incompatíveis são sinalizadas na descrição com impacto e estratégia de migração.

### 10.3 Descrição (em português, usando o template)
Preencha todas as seções do template, sem deixar comentários de orientação nem remover seções para esconder pendências:
- **Resumo:** problema, motivação, solução, mudanças observáveis e o que ficou de fora.
- **Rastreabilidade:** links reais para `docs/PRD.md`, `docs/TDD.md` e a change em `openspec/changes/`; IDs `RF-*`, `RNF-*`, `DT-*` atendidos e fase do roadmap. Não declare cobertura de requisito que não foi implementado e verificado.
- **Validação:** comandos executados, cenários cobertos, resultados e limitações. Marque só o que foi realmente verificado; justifique o que não se aplica; diferencie verificação local de CI.
- **Riscos e pendências:** impactos, mudanças de contrato ou dados, dependências novas, rollback quando aplicável. Escreva "Nenhum" só quando realmente não houver.
- Mudanças de interface: incluir capturas de tela quando disponíveis.

### 10.4 Revisão, integração e encerramento
- Após abrir, confirme na plataforma a URL, a origem e o destino, e me informe o link e as verificações feitas. Se falhar, diga que falhou; não invente número nem URL.
- Corrija os comentários de revisão em commits pequenos e reexecute as verificações afetadas.
- **Abrir o PR não autoriza o merge.** Integre somente por meio do PR, com minha autorização, respeitando as regras do repositório. Nunca contorne proteções nem faça push direto na `main`.
- Após o merge: rode `/opsx:archive` para a change correspondente, atualize o "Estado atual" e não apague branches sem autorização.

---

## 11. Limites de autonomia

Pergunte antes (via `AskUserQuestion`) de:
- instalar, atualizar ou remover dependências;
- apagar arquivos ou pastas;
- abrir PR ou fazer merge (commit e push são sempre feitos por mim; entregue apenas os comandos);
- alterar regras invioláveis (§7.1), decisões já tomadas (§7.2) ou documentos já aprovados;
- mudar a stack, a estrutura de pastas ou as convenções deste arquivo.

Pode fazer sem perguntar: ler arquivos, buscar no repositório, rodar build/testes/lint, criar e editar arquivos dentro do escopo da etapa atual.

---

## 12. Prazo e gestão de escopo

- Entrega em **2026-11-28**. Ao concluir cada etapa, compare o andamento com o prazo e avise se os itens **Should** ou **Could** estiverem em risco, propondo o corte.
- Ordem de proteção do escopo: Must → Should → Could. Um item Must nunca é sacrificado sem minha decisão.

---

## 13. Estado atual

- [x] OpenSpec inicializado para o Claude Code
- [x] `CLAUDE.md` criado
- [ ] `.github/pull_request_template.md`
- [ ] PRD (`docs/PRD.md`), na branch `docs/prd`:
  - [x] 1 Visão geral · 2 Problema · 3 Personas · 4 Fluxo de uso (aprovadas em 2026-11-10)
  - [x] 5.1 Requisitos funcionais Must, RF-01 a RF-12 (aprovada em 2026-11-10)
  - [x] 5.2 Requisitos funcionais Should, RF-13 a RF-19 (aprovada em 2026-11-10)
  - [ ] 5.3 Could · 6 Requisitos não funcionais e regras invioláveis · 7 Métricas de sucesso · 8 Escopo e não objetivos · 9 Premissas, riscos e decisões delegadas ao TDD · 10 Marcos até 2026-11-28
- [ ] TDD (`docs/TDD.md`)
- [ ] Changes do OpenSpec, implementação e PRs, conforme o roadmap do TDD

**Desvio registrado (2026-11-10):** a branch `docs/prd` foi criada a partir da `chore/project-setup`, e não da `main`, porque a `main` ainda não contém este arquivo, a proposta nem o OpenSpec (decisão do usuário). A `chore/project-setup` deve ser integrada à `main` antes, e a `docs/prd` rebaseada sobre a `main` antes do seu PR.

**Próximo passo:** escrever a seção 5.3 (Could) do PRD. Já decidido para ela: previsto × realizado detalhado.

Mantenha esta seção atualizada ao concluir cada etapa.
