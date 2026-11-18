## Context

O produto roda como uma imagem única: a API ASP.NET Core serve o build do Angular (`ASPNETCORE_WEBROOT`) e responde `GET /api/health`. Os testes de integração da API usam Testcontainers (DT-31), que não existe fora do .NET, e as verificações manuais das últimas changes subiram uma pilha descartável à mão (PostgreSQL em container, API, Chrome pelo protocolo de depuração). O front não tem nenhum teste de navegador, e o `package.json` não declara o Playwright. O CI atual (DT-33) tem os jobs `dotnet` e `web`. Ver proposal.md, "Why" e "What Changes".

## Goals / Non-Goals

**Goals:**
- Repetir, com um comando, a verificação que hoje é manual: login, do cadastro à previsão, e o cronograma em 360 px.
- Isolamento total: nunca tocar no banco de desenvolvimento, no `.env` nem nos segredos do autor.
- Falhar cedo e claro quando faltar Docker, Chrome ou .NET.

**Non-Goals:**
- Rodar no CI (decisão do autor).
- Cobrir mais que os três cenários do TDD 8.4.
- Reproduzir no Playwright o que o Vitest, o motor e a API já testam.

## Decisions

**1. Pasta `src/coordin-web/e2e/` e `playwright.config.ts` na raiz do front.** O comando `npx playwright test` do TDD 8.6 roda onde estão as dependências do Node. O TDD (DT-04) não define uma pasta para o E2E; a escolhida fica dentro de `src/coordin-web`, que já existe, e não cria nada na raiz do repositório. *Premissa a vetar pelo autor.* *Alternativa:* uma pasta `e2e/` na raiz, com o próprio `package.json`. Rejeitada: duplicaria o `package-lock.json` e criaria uma pasta que o TDD não prevê.

**2. A pilha sobe por um script do Playwright (`webServer`).** O `playwright.config.ts` declara um `webServer` cujo comando é `e2e/stack.mjs`. O script: (a) confere os pré-requisitos (Docker respondendo, Chrome instalado, `dotnet` no PATH) e para com uma mensagem clara; (b) sobe um PostgreSQL 18 em container, com senha aleatória e uma porta livre escolhida pelo script; (c) constrói o front (`npm run build`) para o `dist` nunca ficar velho; (d) sobe a API por `dotnet run` a partir de `src/Coordin.Api`, apontando `ASPNETCORE_WEBROOT` para o `dist` e passando a connection string, a chave do JWT e o gestor por variáveis de ambiente; (e) espera `GET /api/health`; (f) ao receber o sinal de encerramento, mata a árvore de processos da API e remove o container. Verificado na tarefa 1.4: no Windows, o Playwright derruba a árvore da pilha à força, e isso não dispara o tratamento de sinal do script; o container fica de pé. Por isso há também um `globalTeardown` (`e2e/teardown.mjs`), que remove os containers `coordin-e2e-*` com a pilha ainda no ar, antes de o `webServer` ser encerrado.

**3. Credenciais geradas na configuração, estáveis nos workers.** `playwright.config.ts` faz `process.env.E2E_MANAGER_PASSWORD ??= <aleatória>`; os workers herdam o ambiente do processo principal, então todos veem o mesmo valor. A senha tem 12 caracteres ou mais (regra do gestor). Nada é gravado em arquivo.

**4. Chrome do sistema (`channel: 'chrome'`).** Não há download de navegador. Dois projetos do Playwright: `desktop` (1280×720) e `mobile` (360×800, com emulação de toque). O cenário de 360 px roda no projeto `mobile`; os outros dois, no `desktop`.

**5. Seletores acessíveis.** Os testes usam `getByRole`, `getByLabel` e `getByText`, nunca classes CSS, para não quebrar quando o Tailwind mudar.

**6. Independência por nomes únicos, não por banco limpo.** O banco é novo a cada execução, mas os três cenários não dependem da ordem: cada um cria seus próprios nomes com um sufixo aleatório. O cenário do celular cria sua demanda pela API (o `request` do Playwright), porque no celular o produto só consulta (PRD, RNF-03), e a tela de cadastro não é o assunto dele.

**7. A data prevista é conferida pelo formato, não pelo valor.** O teste exige uma data `dd/MM/aaaa` na linha da demanda e a ausência de "Sem previsão". O valor exato depende da margem, do calendário e da capacidade, já cobertos pelos testes do motor e da API; repeti-lo aqui só tornaria o smoke frágil.

**8. Sem recarregar a página durante um teste.** A renovação de sessão tem limite de 10 por minuto (lição da change 8): os testes navegam pelo menu e só recarregam quando o cenário exige.

## Risks / Trade-offs

- **Só local, sem CI.** Uma regressão de ponta a ponta só aparece quando o autor roda o smoke. Mitigação: o roteiro de verificação manual continua na descrição dos PRs com mudança de tela, e o README descreve o comando.
- **Docker e Chrome na máquina.** Sem eles o smoke não roda. Mitigação: a verificação de pré-requisitos falha cedo com uma mensagem que diz o que falta.
- **Partida a frio.** A primeira execução compila a API e o front (de dezenas de segundos a alguns minutos). Mitigação: `webServer.timeout` generoso, e o script informa o que está fazendo.
- **Processo órfão.** Se o script for morto à força, a API e o container podem ficar vivos. Mitigação: o container tem nome com a porta aleatória; o script remove os restos de execuções anteriores antes de subir; a verificação final confere que nada ficou.
- **Porta ocupada.** A configuração sorteia a porta da API na faixa 40000 a 59999, porque o `baseURL` é preciso antes de o script rodar; o script usa uma porta livre para o PostgreSQL e, se a da API estiver ocupada, falha com uma mensagem que diz isso. Mitigação: rodar de novo sorteia outra porta.
- **Tamanho do `package-lock.json`.** A dependência acrescenta linhas ao lock; é esperado e entra no mesmo commit da instalação.
