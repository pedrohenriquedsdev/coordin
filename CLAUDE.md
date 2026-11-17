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
- **A estrutura de pastas do código está definida no TDD (DT-04).** Não crie pastas fora dela nem proponha outra estrutura sem discutir comigo.

```
coordin/
├── CLAUDE.md                     # este arquivo
├── .claude/                      # comandos e skills do OpenSpec + skills generate-prd e generate-tdd
├── .config/                      # manifesto das ferramentas .NET locais (dotnet-ef)
├── .github/
│   ├── pull_request_template.md  # template obrigatório de PR
│   └── workflows/ci.yml          # CI: jobs dotnet e web (DT-33)
├── docs/
│   ├── PRD.md
│   ├── TDD.md
│   ├── design/                   # ERD, protótipos de telas e especificações consolidadas
│   └── references/
│       └── tcc-proposal.pdf
├── openspec/
│   ├── config.yaml               # contexto do projeto; aponta para PRD e TDD
│   ├── specs/                    # specs vigentes (fonte da verdade do comportamento)
│   └── changes/                  # changes em andamento e arquivadas
├── src/
│   ├── Coordin.Engine/           # biblioteca pura de cálculo (DT-02)
│   ├── Coordin.Api/              # ASP.NET Core: endpoints, auth, persistência, exportação
│   └── coordin-web/              # Angular SPA
├── tests/
│   ├── Coordin.Engine.Tests/     # testes de unidade do motor (obrigatórios)
│   └── Coordin.Api.Tests/        # testes de integração da API
├── Coordin.slnx                  # solução .NET
├── Directory.Build.props         # propriedades comuns dos projetos .NET
├── Directory.Packages.props      # versões centralizadas dos pacotes NuGet
├── global.json                   # versão fixa do SDK .NET
├── compose.yaml                  # PostgreSQL local; perfil `app` roda a imagem de produção
├── Dockerfile                    # imagem única: API + build do Angular
├── .dockerignore · .gitignore    # o que o Docker e o Git ignoram
├── .editorconfig                 # convenções de código e formatação
├── .gitattributes                # fim de linha sempre LF, em qualquer sistema (inclusive no Windows)
├── README.md                     # como rodar, testar e containerizar localmente
└── LICENSE
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
- Conduzido pela skill `/generate-prd [seção]` (`.claude/skills/generate-prd/`).
- Define **o quê e por quê**: problema, personas, fluxo de uso, requisitos com critérios de aceite verificáveis, métricas, escopo e não objetivos, premissas e riscos.
- **Sem solução técnica.** Algoritmos, stack e modelo de dados vão para o TDD. Os pontos em aberto da proposta entram como "decisões delegadas ao TDD".

### 4.2 TDD
- Conduzido pela skill `/generate-tdd [seção]` (`.claude/skills/generate-tdd/`).
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
  - **Must:** cadastros (times, pessoas, capacidade, demandas), cálculo de previsão com margem, cronograma, relatório por time (ocupação, ociosidade, sobrecarga; promovido em 2026-11-12).
  - **Should:** cenários de sequenciamento com aprovação do gestor.
  - **Could:** previsto × realizado detalhado.
  - Ajustes podem ser propostos, com justificativa.
- **Dor central (2026-11-12):** o trabalho manual do gestor de desenvolvimento para encontrar ociosidade, equilibrar a carga entre os times e descobrir sequências mais eficazes; previsões confiáveis e a comunicação com o gestor de produto são consequência.
- **Métrica principal de sucesso:** desvio entre a data prevista e a data realizada, acompanhada das métricas de eficiência do setor (horas ociosas, equilíbrio de ocupação, períodos de sobrecarga e prazo total; PRD 7.2).

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
- **Cerca de um commit por grupo de tasks, sempre com organização lógica** (regra do autor desde 2026-11-13, que substitui o antigo "um commit por bloco lógico, não economize commits", para reduzir os commits em cerca de metade). Junte no mesmo commit o código, os testes e a marcação do `tasks.md` do grupo; só separe blocos realmente independentes (por exemplo, uma troca de pacotes). Nunca misture documentação, refatoração e funcionalidade sem relação.
- Cada commit deve ser verificável; não commite código que quebre build ou testes. Um arquivo que importa algo entra no mesmo commit (ou depois) do que ele importa.
- **Eu sempre faço os commits.** Ao fim de cada grupo de tasks, mostre os comandos `git add`/`git commit` prontos para copiar (uma lista só, na ordem certa). Nunca execute `git commit` nem `git push`.

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
- [x] Skills `generate-prd` e `generate-tdd` criadas (branch `chore/prd-tdd-skills`, integrada à `docs/prd` por fast-forward)
- [x] `.github/pull_request_template.md` (PR #2)
- [x] Marco M1: `chore/project-setup` (#1), `chore/pr-template` (#2) e `docs/prd` (#3) integradas à `main` em 2026-11-11, com merge commit
- [x] PRD (`docs/PRD.md`), na branch `docs/prd`: seções 1 a 10 aprovadas em 2026-11-10 (RF-01 a RF-23, RNF-01 a RNF-08, marcos M0 a M8)
- [x] TDD (`docs/TDD.md`), na branch `docs/tdd`: seções 1 a 10 aprovadas em 2026-11-11 (M3 antecipado), DT-01 a DT-35; todos os pontos do §7.3 fechados. PRD ajustado em RF-02, RF-03 (feriados), RF-06 (esforço por time), RF-10 e 8.1. Integrado à `main` pelo PR #4 em 2026-11-11
- [x] Ajuste de gráficos e justificativa dos cenários, na branch `docs/decision-charts` (2026-11-11): PRD RF-13, RF-17 e RF-23 (todo gráfico acompanhado de tabela); TDD DT-36 (Chart.js 4.6.x com `ChartComponent` próprio), contrato de cenário com `score` e parcelas por demanda, RT-10. Mensageria avaliada e descartada: os gráficos são desenhados na tela a partir de dados já calculados. Integrado à `main` pelo PR #5 em 2026-11-11
- [x] Reenquadramento da dor central e validação por simulação, na branch `docs/prd-core-pain-reframe` (2026-11-12): PRD seções 1 a 3 (dor central), RF-17 promovido a Must com M4 em 2026-11-21, métricas de eficiência (7.2), validação por simulação (7.3, premissas, riscos, M2, M7) e RF-24 (5.4, apoio à validação, não cortado); TDD DT-37 (validação no produto com `Validation__Enabled` e confirmação), algoritmo 6.6, roadmap com 13 changes e RT-11. Integrado à `main` pelo PR #6 em 2026-11-12
- [x] Artefatos de design em `docs/design/` (ERD, protótipos de telas e especificações consolidadas), commitados direto na `main` em 2026-11-12 por decisão do autor
- [x] Hospedagem (DT-09) decidida em 2026-11-12, na branch `docs/hosting-decision`: container Docker em PaaS com PostgreSQL gerenciado, validado com o orientador; o provedor (Render, Railway ou Fly.io) é escolhido na change 7
- [x] `openspec/config.yaml` aponta para `docs/PRD.md` e `docs/TDD.md` (context + rules por artefato)
- [x] Change 1 `project-foundation` implementada na branch `chore/project-foundation` (2026-11-12): solução .NET com motor puro e teste de arquitetura, API com Problem Details, cabeçalhos de segurança e HTTPS em produção, PostgreSQL com migration inicial e `GET /api/health`, OpenAPI e Swagger só em desenvolvimento, SPA Angular 22 com Tailwind e pt-BR servida pela API, cliente tipado gerado, Dockerfile, README e workflow de CI. Verificado localmente: 44 testes .NET e 7 do front aprovados, lint, formatação e builds limpos, imagem Docker rodando. PR #8 integrado à `main` em 2026-11-12 com CI verde (jobs `dotnet` e `web`). O PR #9 (`chore/remove-env-example`) removeu o `.env.example` do repositório (o README explica como criar o `.env`). Change arquivada em `openspec/changes/archive/2026-11-12-project-foundation` e specs vigentes em `openspec/specs/` (`api-conventions`, `http-security`, `service-health`, `web-shell`). **Lições para as próximas changes:** (1) teste que usa data não pode depender do fuso da máquina (o CI roda em UTC): use instante fixo com `Z`; (2) data só com dia (prazo, entrega) não passa pelo `DatePipe` com `-0300`, pois pode deslocar um dia: tratar "data de calendário" separada de "instante" nas changes com telas de data (3 ou 5)
- [x] Change 2 `authentication` implementada na branch `feat/authentication` (2026-11-13): ASP.NET Core Identity + JWT de 15 min + refresh token em cookie `HttpOnly` com rotação e revogação em massa no reuso, bloqueio após 5 erros, limite de 10 requisições por minuto por IP em `login` e `refresh`, conta do gestor criada por `Manager__*`, todos os endpoints protegidos por padrão (política de fallback), e no Angular a tela de login, a sessão só em memória, o interceptor com renovação única, as guardas e a restauração da sessão ao recarregar. Verificada em uma cópia limpa do commit (127 testes .NET, 80 do front, lint, formatação e builds limpos) e em um Chrome real contra a API e a imagem Docker (56 verificações, 0 violações de CSP). PR #11 integrado à `main` em 2026-11-13 com CI verde (jobs `dotnet` e `web`). Change arquivada em `openspec/changes/archive/2026-11-13-authentication` e specs vigentes em `openspec/specs/` (`authentication`, `web-authentication`). **Lições para as próximas changes:** (1) endpoint novo já nasce protegido pela política de fallback; só `AllowAnonymous` explícito o libera; (2) o `docker compose` resolve `${VAR:?}` do arquivo inteiro, mesmo de perfis inativos: use `${VAR:-}` para variáveis só do perfil `app`; (3) o roteador do Angular resolve `redirectTo` antes das guardas; (4) `dotnet <dll>` precisa rodar da pasta `src/Coordin.Api`, senão não acha o `appsettings.json`
- [x] Marco M2 cumprido em 2026-11-13, na branch `docs/m2-simulation-definition`: conjunto fictício da simulação de 7 times, cerca de 35 pessoas e 30 demandas; faixa de variação do esforço de 0,9 a 1,1 (a confirmar com o gestor na revisão do conjunto, P-03); disponibilidade do gestor (P-05) confirmada pelo autor, sem plano B. PRD (R-04 e seção 7.3) e TDD (DT-37 e pendência 2 da seção 10) ajustados
- [x] Change 3 `capacity-registry` implementada na branch `feat/capacity-registry` (2026-11-13): motor `TeamCapacity` (soma da capacidade das pessoas), tabelas `Teams`, `People`, `Absences` e `Holidays` com a integridade garantida pelo banco (unicidade, `CHECK`, `RESTRICT`, `CASCADE`), 16 endpoints (times, pessoas, ausências e feriados, com `GET /api/absences` acrescentado ao TDD 5.2 e os códigos `team-name-taken` e `holiday-date-taken` ao DT-18), cliente gerado, e no Angular a navegação, a tela "Times e pessoas" e a tela "Calendário", com datas de calendário tratadas sem fuso. Verificada em testes (281 do .NET e 171 do front) e em um Chrome real contra a API real (37 verificações, 0 violações de CSP, datas iguais em 4 fusos, incluindo UTC+14). PR #15 integrado à `main` em 2026-11-13 com CI verde (jobs `dotnet` e `web`). Change arquivada em `openspec/changes/archive/2026-11-13-capacity-registry` e specs vigentes em `openspec/specs/` (`team-registry`, `people-registry`, `capacity-calendar`, `registry-web`). **Lições para as próximas changes:** (1) o banco decide a unicidade em corrida: grave e traduza o erro (`23505` em `409`), sem um "já existe?" antes; (2) no PostgreSQL, `ON DELETE RESTRICT` devolve `23001`, e não `23503`; (3) um `DateOnly` não anulável ausente no corpo vira `0001-01-01` sem erro: use `DateOnly?` com `[Required]`; (4) as funções geradas de `DELETE` pedem a resposta como texto, então o Problem Details de erro chega como string JSON (o `readProblem` já a lê); (5) uma data de calendário nunca passa por `Date` nem `DatePipe`: use `core/dates` e o `Clock` injetável, e teste em vários fusos; (6) em roteiros de navegador, espere a tela recarregar antes de conferir
- [x] Change 4 `forecast-engine` implementada na branch `feat/forecast-engine` (2026-11-13): motor puro em `Coordin.Engine` com o snapshot e o resultado imutáveis (`Planning/`, `Forecasting/`), validação do snapshot, calendário de dias úteis e capacidade por data (ausências e feriados), previsibilidade (`MissingStartDate`, `MissingEstimate`, `NoCapacity`), simulação dia a dia com fila por pessoa, margem percentual sobre a duração e `ForecastEngine.Calculate`; sem API, banco ou tela (o recálculo na escrita e as demandas chegam na change 5; as dependências, na change 8). Verificada localmente: 106 testes do motor (100% das linhas e 99,1% dos ramos do `Coordin.Engine`) e invariantes em 400 snapshots de semente fixa (margem nunca reduz o prazo, capacidade nunca ultrapassada, esforço consumido = estimado, resultado igual com listas embaralhadas, adicionar um time nunca antecipa o fim). PR #17 integrado à `main` em 2026-11-13 com CI verde (jobs `dotnet` e `web`). Change arquivada em `openspec/changes/archive/2026-11-13-forecast-engine` e specs vigentes em `openspec/specs/` (`daily-capacity`, `forecast-predictability`, `delivery-forecast`). **Lições para as próximas changes:** (1) um teste achou um erro de design: ao retirar demandas que estouram o horizonte, retire só a de maior prioridade e refaça a simulação, senão as de trás são descartadas por engano; (2) o analisador CA2208 recusa um `paramName` que não seja parâmetro do método (nos auxiliares de validação, lance a exceção sem nome); (3) o .NET não troca o fuso da máquina dentro de um teste: garanta o determinismo por construção (o motor só usa `DateOnly`) e por um teste que varre o código por `DateTime`, `TimeZoneInfo`, `Random` e `Guid.NewGuid`; (4) `record` com lista compara a lista por referência: compare snapshots serializados ou com `SequenceEqual`
- [x] Change 5 `demand-planning` (back-end; tela na 5b) implementada na branch `feat/demand-planning` (2026-11-16): migration `AddDemandPlanning` (`Demands`, `DemandTeams`, `DemandAssignments`, `Forecasts`, `PlanningSettings` com margem 20% e `FollowPriority`), API de demandas como agregado (CRUD, ordem de prioridade com duas fases de ranks, conclusão), `GET /api/schedule` com desvios (DT-27), `GET`/`PUT /api/settings`, recálculo na mesma transação de toda escrita que afeta o cálculo (`PlanningTransaction`, *advisory lock*), e regras nos cadastros existentes (pessoa, ausência, feriado e time). Cliente Angular regenerado; o gerador recebeu o OpenAPI exportado da fábrica de testes, e não a API de desenvolvimento. Verificada em cópia limpa: 106 testes do motor, 380 da API (com PostgreSQL real), 171 do front; lint, formatação, build e `openspec validate --strict` limpos. **PR e CI ainda a confirmar.** **Lições para as próximas changes:** (1) a `RESTRICT` de um time em `DemandTeams` tem o nome da FK no erro: é assim que se distingue `team-has-demands` de `team-has-people`; (2) `ExecuteUpdateAsync` ignora o *change tracker*, então use-o nos renumeros; (3) uma requisição de escrita sem corpo responde 404 em vez de 401 (o roteamento não escolhe a rota): teste de contrato deve enviar corpo; (4) o `ng-openapi-gen` deixa de gerar modelos aninhados quando `ignoreUnusedModels` está ativo, e a `rootUrl` vira a origem do arquivo local: passe `--ignoreUnusedModels false` e restaure `api-configuration.ts`
- [x] Change 5b `demand-screens` implementada na branch `feat/demand-screens` (2026-11-16): telas do planejamento no front. O cronograma é a tela inicial (tabela no computador, cartões a partir de 360 px, margem com e sem a data, motivos das não previsíveis, alertas e desvios em palavras); `/demands` com abas Ativas e Concluídas, Subir e Descer (a lista completa vai à API, pulando as concluídas), exclusão com confirmação e conclusão na própria linha; formulário de nova e edição de demanda (times com horas opcionais, pessoas dos times marcados); `/settings` com a margem de segurança. Sem arrastar (decisão do autor: evita `@angular/cdk`) e sem tela de detalhe. **Verificação:** cópia limpa com todos os comandos do TDD 8.6 (.NET: 106 do motor e 380 da API; front: 237 testes, também com `TZ=Asia/Tokyo` e `TZ=America/Los_Angeles`; lint, formatação, build e `openspec validate --strict` limpos) e roteiro manual no Chrome contra a API local e banco descartável: 11 de 11 verificações (login, cadastro, Subir, conclusão com desvio, margem, 1280 px com tabela e 360 px com cartões, ambos sem rolagem horizontal, 0 violações de CSP). PR #20 integrado à `main` em 2026-11-16 com CI verde (jobs `dotnet` e `web`). Change arquivada em `openspec/changes/archive/2026-11-16-demand-screens` e specs vigentes em `openspec/specs/` (`schedule-web`, `demands-web`, `settings-web`). **Lições para as próximas changes:** (1) `(ngSubmit)` só dispara com `FormsModule` ou `ReactiveFormsModule`; sem um dos dois, use `(submit)` com `preventDefault`; (2) a API aceita o build do Angular com a variável `ASPNETCORE_WEBROOT`, sem copiar nada para o repositório; (3) nos testes que pedem várias listas em sequência, a ordem das requisições importa: o store pede o segundo recurso só depois do primeiro (a edição pede a demanda depois de times e pessoas); (4) um estado de tela compartilhado (`Clock`, `Number(...)` nos números da API) deve virar função pura testada à parte, como em `planning/labels.ts`
- [x] Change 6 `team-report` implementada na branch `feat/team-report` (2026-11-16): relatório semanal por time (RF-17). Motor `Coordin.Engine/Reporting/TeamReport` (capacidade, horas alocadas, ocupação, ociosidade e sobrecarga por semana segunda a domingo; sobrecarga = toda a capacidade usada e parte elegível esperando, definição do autor em 2026-11-16); `GET /api/reports/teams?from=&to=` (datas obrigatórias, até 52 semanas, sem gravar nada); tela `/reports/teams` com resumo por time, detalhe semanal, `ChartComponent` sobre `chart.js` 4.6.1 (dependência autorizada, MIT) e tabela com os mesmos valores. **Verificação:** cópia limpa com os comandos do TDD 8.6 (.NET: 127 do motor, 397 da API com PostgreSQL real; front: 261 testes nos fusos UTC, Tóquio e Los Angeles; build, lint, formatação e `openspec validate --strict` limpos); cobertura do motor 100% das linhas e 99,4% dos ramos; roteiro no Chrome com dados reais: 13 de 13 verificações (link, resumo, gráfico desenhado com pixels, tabela de semanas com sobrecarga, 1280 px e 360 px sem rolagem horizontal, zero violações de CSP). PR #22 integrado à `main` em 2026-11-16 com CI verde (jobs `dotnet` e `web`). Change arquivada em `openspec/changes/archive/2026-11-16-team-report` e specs vigentes em `openspec/specs/` (`team-report-calculation`, `team-report-api`, `team-report-web`). **Lições para as próximas changes:** (1) `record` com lista compara por referência: compare serializado (já anotado na change 4); (2) o `vi` do Vitest não existe no build de produção: arquivo de apoio dos testes dentro de `src/app` não pode usar `vi`; (3) `vi.mock` de módulo é frágil quando vários arquivos de teste dividem o processo: injete a dependência por `InjectionToken` (como `CHART_FACTORY`) e troque no teste; (4) o `curl` no shell do Windows corrompe títulos com acentos no JSON: nas verificações manuais, use títulos sem acento ou envie o corpo de um arquivo UTF-8.
- [ ] Changes do OpenSpec, implementação e PRs, conforme o roadmap do TDD (9.1): 1 `project-foundation` · 2 `authentication` · 3 `capacity-registry` · 4 `forecast-engine` · 5 `demand-planning` · 6 `team-report` · 7 `deployment` (M4) · 8 `demand-dependencies` · 9 `sequencing-scenarios` · 10 `schedule-export` (M5) · 11 `simulation-validation` · 12 `delivery-insights` · 13 `e2e-smoke`

**Desvios registrados (2026-11-10 e 2026-11-11):** a `docs/prd` foi criada a partir da `chore/project-setup` porque a `main` ainda não tinha este arquivo; o PR #1 foi aberto antes do template de PR. Ambos registrados nas descrições dos PRs. PRs são integrados sempre com merge commit (`gh pr merge <n> --merge`), nunca squash ou rebase.

- [x] Change 7 `deployment` (M4) implementada na branch `chore/deployment` (PR #24, integrado à `main` com CI verde): listener pela variável `PORT` (`ListenUrls`, DT-09), `Dockerfile` ajustado, `render.yaml` (web service Docker, plano gratuito, região oregon, health check `/api/health`), roteiro em `docs/deployment.md` e spec `hosting-deployment`. Banco trocado para o **Neon** (plano gratuito, PostgreSQL 18) por causa da expiração de 30 dias do banco gratuito do Render: PR #25 integrado à `main` com CI verde. Deploy real feito pelo autor no Render com a string Npgsql do Neon e os segredos `Manager__*` preenchidos no painel; `GET /api/health` respondeu `{"status":"Healthy"}`. **Verificação ponta a ponta** (`docs/deployment.md`, seção f): feita pelo autor em 2026-11-17 no deploy real, que respondeu corretamente (relato do autor, sem detalhamento por passo). **Verificação local em cópia limpa** (2026-11-17, branch `docs/deployment-state`): `dotnet build` com 0 avisos e 0 erros, `dotnet format --verify-no-changes` limpo, 127 testes do motor e 406 da API aprovados (com PostgreSQL real), `openspec validate deployment --strict` válido; a imagem Docker e o `render.yaml` foram validados pelo próprio deploy no Render, sem novo `docker build` local. **Confirmado pelo autor em 2026-11-17:** Render e Neon não pediram cartão (até agora); a branch `production` do Neon está com expiração "Never"; o workspace do Render é o plano Hobby (US$ 0/mês, 5 GB de banda, 500 minutos de build), e o serviço segue no plano `free` (dorme após 15 minutos sem tráfego; o primeiro acesso leva cerca de 1 minuto). O RNF-06 fica atendido pelo banco do Neon. **Decisão pendente do autor (não bloqueante):** manter o serviço `free` e acordá-lo antes da banca/validação, ou subir para um plano pago só no mês da entrega. **Antes de 2026-11-28:** fazer um backup do banco do Neon. Change arquivada em `openspec/changes/archive/2026-11-17-deployment` e spec vigente em `openspec/specs/` (`hosting-deployment`). A change 5 `demand-planning` foi arquivada em 2026-11-17 (`openspec/changes/archive/2026-11-17-demand-planning`; 30 requisitos em 4 specs novas, `demand-registry`, `forecast-recalculation`, `planning-settings` e `schedule-view`, e 3 acrescentados às existentes), pois o arquivamento havia sido esquecido.

**Próximo passo:** abrir o PR do `docs/deployment-state` (arquivamento das changes 5 e 7 e atualização do estado; PR e merge só com autorização do autor e CI verde). Depois, changes 8 a 13. **Modelo:** o planejamento da change 7 foi feito no Haiku 5.5, fora da regra (planejar com modelo mais forte); a implementação também no Haiku. Para planejar as changes 8 a 13, usar modelo mais forte. **Risco de prazo:** o M4 é 2026-11-21 (faltam 4 dias a partir de 2026-11-17); as changes 8 a 13 seguem pendentes e a entrega do TCC é 2026-11-28. O conjunto fictício só é carregado quando a primeira versão estiver pronta (change 11). **Modelo:** o planejamento foi feito no Haiku 5.5, fora da regra (planejar com modelo mais forte); a implementação também no Haiku. **Risco de prazo:** o M4 é 2026-11-21, e a 7 é o último Must do caminho. Entrega do TCC em 2026-11-28; o conjunto fictício só é carregado quando a primeira versão estiver pronta (change 11).

Mantenha esta seção atualizada ao concluir cada etapa.
