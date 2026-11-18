# Proposal

## Why

As regras de negócio já são cobertas no motor e na API, mas nenhum teste automatizado confirma que o produto funciona **de ponta a ponta no navegador**: a API servindo o Angular, o login, o cadastro até a previsão e o cronograma legível no celular (RNF-03). Hoje essa confirmação é manual, repetida a cada change. O TDD (DT-32) prevê três cenários de smoke com Playwright, só depois do marco M4, se couberem no prazo. M4 já passou, o prazo de entrega é 2026-11-28, e esta é a última change do roadmap (TDD 9.1, linha 13).

## What Changes

- **Três cenários de smoke com Playwright 1.65.x**, os do TDD 8.4:
  1. login e logout;
  2. cadastrar time, pessoa e demanda com estimativa e ver a data prevista no cronograma;
  3. consultar o cronograma em viewport de 360 px, sem rolagem horizontal da página (RNF-03).
- **Um comando só**: `npx playwright test` (em `src/coordin-web`) sobe uma pilha descartável (PostgreSQL em container, API servindo o build do Angular), roda os cenários e derruba tudo, sem tocar no banco de desenvolvimento do autor.
- **Chrome já instalado** (canal `chrome` do Playwright), sem baixar navegador.
- **Credenciais temporárias**: o e-mail e a senha do gestor são gerados a cada execução e nunca vão para o repositório.
- **Roteiro no README** para rodar o smoke localmente, com os pré-requisitos (Docker, Chrome, .NET).
- **Dependência nova**, autorizada pelo autor em 2026-11-18: `@playwright/test` como `devDependency` do front.

**Fora desta change:**
- Rodar o smoke no CI: decisão do autor de manter o CI com os jobs `dotnet` e `web` e rodar o smoke só localmente. Fica registrado como risco.
- Qualquer mudança de código da aplicação, da API ou do motor.
- Cobrir cenários além dos três (cenários, relatórios, validação, exportação): o E2E só confirma que as peças estão ligadas (DT-32).
- Baixar navegadores do Playwright (`npx playwright install`).

## Capabilities

### New Capabilities

- `e2e-smoke`: a suíte de smoke ponta a ponta: o que ela cobre (login e logout, do cadastro à previsão, e o cronograma no celular), como ela se isola (pilha descartável, credenciais temporárias, sem tocar no banco do autor) e o comando que a roda (RNF-03; DT-32).

### Modified Capabilities

Nenhuma. Nenhum requisito do produto muda; a change só acrescenta uma verificação automatizada.

## Impact

- **Front (`src/coordin-web`)**: `package.json` e `package-lock.json` (nova `devDependency`), `playwright.config.ts`, a pasta `e2e/` (cenários e o script que sobe a pilha) e entradas no `.gitignore` para os relatórios do Playwright.
- **README**: seção nova para rodar o smoke.
- **API, motor, banco e CI**: nenhuma mudança.
- **Pasta nova**: `src/coordin-web/e2e/`. O TDD (DT-04) não define uma pasta para os testes ponta a ponta; esta fica dentro de `src/coordin-web`, que já existe. Premissa registrada no design, para o autor vetar.
- **Prazo**: Could, com 2026-11-25 como data indicada no roadmap; sem dependência de nenhuma outra change.
