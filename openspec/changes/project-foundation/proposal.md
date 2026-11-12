# Proposta — project-foundation

## Why

Nenhuma linha de código existe ainda, e todas as 12 changes seguintes precisam da mesma base: solução .NET, front Angular, banco, formato de erro, segurança HTTP, imagem Docker e CI. Fazer essa base primeiro, numa change própria, faz com que cada change de funcionalidade já nasça verificada automaticamente e na estrutura aprovada no TDD.

**Roadmap:** TDD 9.1, linha 1 — `project-foundation`, branch `chore/project-foundation`, Must, data alvo 2026-11-13.
**Atende:** DT-01 a DT-08, DT-15, DT-16, DT-18, DT-19, DT-30, DT-33; RNF-07 (e prepara RNF-03, RNF-04, RNF-05, RNF-06 e RNF-08).

## What Changes

- **Estrutura do monorepo** conforme o DT-04: `Coordin.slnx`, `src/Coordin.Engine`, `src/Coordin.Api`, `src/coordin-web`, `tests/Coordin.Engine.Tests`, `tests/Coordin.Api.Tests`, mais `.gitignore`, `.editorconfig` e `.env.example`.
- **`Coordin.Engine`** vazio de regras, mas já com a restrição de não ter pacotes externos nem referência à API (DT-02), e um teste que garante isso.
- **`Coordin.Api`** (ASP.NET Core 10, Minimal APIs):
  - endpoints sob `/api`, JSON em `camelCase`;
  - erros em Problem Details com extensão `code` (DT-06, DT-18);
  - documento OpenAPI e Swagger UI **só em desenvolvimento** (DT-19);
  - cabeçalhos de segurança, HTTPS e HSTS em produção (DT-30);
  - serve o build do Angular na mesma origem, com fallback das rotas da SPA para o `index.html` (DT-01);
  - EF Core 10 + Npgsql com a migration inicial `InitialCreate` (vazia), aplicada na inicialização (DT-07);
  - **novo:** `GET /api/health`, anônimo, que confere a conexão com o banco. É um acréscimo ao TDD 5.2, aprovado pelo autor em 2026-11-12 e registrado no TDD nesta change.
- **`coordin-web`** (Angular 22, TypeScript 6.0, Tailwind 4): casca da aplicação em pt-BR (`lang="pt-BR"`, locale `pt-BR`, fuso `America/Sao_Paulo`), ESLint, Prettier, Vitest e a geração do cliente com `ng-openapi-gen` (DT-08, DT-19).
- **Ambiente local:** `compose.yaml` com PostgreSQL 18, com a senha lida de um `.env` ignorado pelo Git (DT-30).
- **`Dockerfile`** multiestágio que gera uma imagem única com a API e o build do Angular (DT-01). Ele serve à PaaS da change 7 (DT-09).
- **CI** em `.github/workflows/ci.yml`, com os jobs `dotnet` e `web`, em `pull_request` e `push` para a `main` (DT-33).
- **Testes:** projetos xUnit v3 com uma base de testes de integração usando `WebApplicationFactory` e Testcontainers (DT-31).

## Capabilities

### New Capabilities

- `api-conventions`: base `/api`, JSON em `camelCase`, formato único de erro (Problem Details com `code` e `detail` em português) e documentação da API restrita ao desenvolvimento.
- `http-security`: cabeçalhos de segurança, HTTPS com HSTS em produção e origem única, sem CORS.
- `service-health`: verificação pública de que a aplicação e o banco estão no ar, usada pelos testes e pela hospedagem.
- `web-shell`: a SPA servida pela própria API na mesma origem, com navegação direta por URL e interface em pt-BR.

### Modified Capabilities

Nenhuma (não há specs vigentes).

## Fora desta change

- Qualquer entidade de domínio, regra de cálculo ou tela funcional (changes 3 a 6).
- Autenticação, Identity, JWT e a conta do gestor (change 2). Até lá, nenhum endpoint de negócio existe, então não há nada a proteger.
- Escolha do provedor e implantação (change 7).
- Chart.js (change 6), Playwright (change 13) e o teste de desempenho (change 9).
- Atualização para o .NET 11 (DT-05).

## Impact

- **Código novo:** todo o conteúdo de `src/`, `tests/`, `Coordin.slnx`, `compose.yaml`, `Dockerfile`, `.github/workflows/ci.yml`, `.gitignore`, `.editorconfig` e `.env.example`.
- **Documentação:** TDD 5.2 ganha a linha do `GET /api/health`; `CLAUDE.md` §3 passa a refletir a estrutura de pastas e o §13 é atualizado.
- **Dependências novas** (versões do TDD 3, instaladas com autorização do autor, `CLAUDE.md` §11):
  - **NuGet:** `Microsoft.EntityFrameworkCore.Design` 10.0.x, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.x, `Microsoft.AspNetCore.OpenApi` 10.0.x, `Swashbuckle.AspNetCore.SwaggerUI` 11.0.x, `xunit.v3` 3.2.x, `Microsoft.AspNetCore.Mvc.Testing` 10.0.x, `Testcontainers.PostgreSql` 4.16.x, `coverlet.collector` 10.x e `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` 10.0.15 (pacote oficial da Microsoft; ver design);
  - **npm:** Angular 22.2.x, TypeScript 6.0.x, Tailwind CSS 4.3.x, angular-eslint 22.x, Prettier 3.x, Vitest 5.x, ng-openapi-gen 1.1.x;
  - **ferramenta local:** `dotnet-ef` 10.0.15, como ferramenta local do repositório (`.config/dotnet-tools.json`).
- **Sistemas:** GitHub Actions passa a rodar em todo PR. Docker passa a ser necessário para os testes de integração (RT-06).
