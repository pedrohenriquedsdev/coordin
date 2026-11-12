# Design — project-foundation

## Context

O repositório só tem documentação (`docs/`, `openspec/`, `.github/`, `.claude/`). A arquitetura, a stack e as versões já estão decididas no TDD (DT-01 a DT-08, DT-15 a DT-19, DT-30, DT-31, DT-33; seção 3). Este design só fecha o que o TDD deixou para a implementação. A motivação está em `proposal.md` (Why), e o comportamento esperado nas specs `api-conventions`, `http-security`, `service-health` e `web-shell`.

Ambiente verificado em 2026-11-12: .NET SDK 10.0.403, Node 24.21.0, npm 11.19, Docker 29.8.2. No NuGet: `Microsoft.EntityFrameworkCore.Design`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` e `dotnet-ef` em 10.0.15.

## Goals / Non-Goals

**Goals:**
- `dotnet build`, `dotnet test`, `npm run lint`, `npm test` e `npm run build` passando localmente e no CI, já nesta change.
- Uma imagem Docker que sobe a API servindo a SPA e responde `GET /api/health` com o PostgreSQL do Compose.
- Uma estrutura interna da API que as próximas changes sigam sem rediscutir.

**Non-Goals:**
- Escolher o provedor da PaaS ou publicar a imagem (change 7).
- Convenção de nomes de tabelas e colunas do banco além do padrão do EF Core. Decide-se na change 3, com as primeiras entidades, se for preciso.
- Job de CI para construir a imagem Docker. O DT-33 define só os jobs `dotnet` e `web`; a imagem é verificada localmente nesta change (ver Open Questions).

## Decisions

### D1 — Versões centralizadas e SDK fixado
`global.json` fixa o SDK `10.0.403` com `rollForward: latestFeature`. `Directory.Build.props` liga `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors` e `AnalysisLevel` em todos os projetos. `Directory.Packages.props` (Central Package Management) concentra as versões NuGet do TDD 3.
- *Alternativa:* versões em cada `.csproj`. É mais simples, mas as versões divergem entre projetos e cada atualização (inclusive a do .NET 11, DT-05) tocaria vários arquivos.

### D2 — Projetos .NET e testes xUnit v3
- **Projetos:** `Coordin.Engine` (`classlib`, sem `PackageReference`) e `Coordin.Api` (`web`, Minimal APIs), ambos em `net10.0`.
- **Testes:** os `.csproj` de teste são escritos à mão, com `xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` e `coverlet.collector`, rodando no modo VSTest. Assim, o comando `dotnet test --collect:"XPlat Code Coverage"` do TDD 8.6 funciona sem configuração extra.
- *Alternativa:* instalar o pacote de templates do xUnit v3. Seria mais uma instalação global para gerar dois arquivos pequenos.
- *Alternativa:* usar o Microsoft Testing Platform. Ele muda a forma de coletar cobertura e os comandos do TDD 8.6.

### D3 — Pureza do motor garantida por teste
`Coordin.Engine.Tests` tem um teste de arquitetura: o assembly `Coordin.Engine` só pode referenciar assemblies do runtime (`System.*`, `netstandard`). Isso transforma a regra do DT-02 ("sem banco, HTTP ou I/O") numa verificação automática. Nesta change, o motor só tem um marcador de assembly e nenhuma regra.

### D4 — Organização interna da `Coordin.Api`
```
src/Coordin.Api/
├── Program.cs                      # composição: serviços e pipeline, nada de regra
├── Features/Health/                # um diretório por recurso (Teams, Demands, ...)
├── Infrastructure/
│   ├── Persistence/                # CoordinDbContext, Migrations/
│   └── Http/                       # ProblemDetails, SecurityHeaders, SPA fallback
└── wwwroot/                        # build do Angular (só na imagem; ignorado pelo Git)
```
Cada recurso expõe um método de extensão `MapXxxEndpoints(this IEndpointRouteBuilder)` com `MapGroup("/api/xxx")` (DT-06). `Program.cs` declara `public partial class Program` para a `WebApplicationFactory`.
- *Alternativa:* separar em camadas por projeto (Application, Infrastructure). Para um usuário e poucas dezenas de endpoints, as pastas já separam as responsabilidades; a regra de negócio fica no motor (DT-02).

### D5 — Pipeline HTTP, nesta ordem
1. `UseForwardedHeaders` (X-Forwarded-For/Proto; redes conhecidas limpas, porque o IP do proxy da PaaS não é fixo).
2. Produção: `UseHsts` e `UseHttpsRedirection`.
3. Middleware próprio de cabeçalhos de segurança (D7), antes de tudo o que gera resposta, para valer também em erros e arquivos estáticos.
4. `UseExceptionHandler` + `UseStatusCodePages`, ambos gerando Problem Details (D6).
5. `UseStaticFiles` (wwwroot).
6. Endpoints: grupos `/api/*`, `MapHealthChecks("/api/health")`, OpenAPI e Swagger UI só em desenvolvimento.
7. `MapFallback("/api/{**path}")` → `404` em Problem Details. Essa rota é mais específica que a da SPA e impede que uma rota de API inexistente devolva o `index.html`.
8. `MapFallbackToFile("index.html")` para as rotas da SPA.

### D6 — Problem Details em português
- **Base:** `AddProblemDetails` com `CustomizeProblemDetails`, que acrescenta `traceId` e preenche `detail` em português quando vazio, por status (`404` "O recurso solicitado não foi encontrado.", `500` "Ocorreu um erro inesperado. Tente novamente.").
- **Exceções:** o handler nunca expõe mensagem nem pilha da exceção.
- **Conflitos:** uma fábrica `ApiProblems.Conflict(code, detail)` gera `409` com a extensão `code` (DT-18).
- **Validação:** usa a validação nativa de Minimal APIs do .NET 10 (`AddValidation()`, Data Annotations), com mensagens em português nos atributos e nomes de campo em `camelCase`.
- **Ambiente `Testing`:** registra endpoints de diagnóstico em `/api/_test/` (exceção, conflito, validação). Eles existem só para provar os cenários da spec `api-conventions` antes de haver endpoints reais, e nunca são mapeados em `Development` ou `Production`.
- *Alternativa:* pacote de Problem Details de terceiros. O suporte nativo do ASP.NET Core 10 já cobre tudo.
- *Alternativa:* testar as fábricas só por unidade. Isso não prova o pipeline (handler, status pages, cabeçalhos).

### D7 — Cabeçalhos de segurança e CSP
Um middleware próprio de poucas linhas, sem pacote:
- **CSP:** `default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'`.
- **Demais cabeçalhos:** `X-Content-Type-Options: nosniff` e `Referrer-Policy: no-referrer`.
- **Estilos inline liberados:** o Angular injeta `<style>` dos componentes em tempo de execução. O DT-30 proíbe só *scripts* inline, e liberar estilos não abre execução de código. Para não haver script inline, o build desliga o `inlineCritical` (que usa um `onload` inline).
- **Swagger UI em desenvolvimento:** ele usa script inline, então só o caminho `/swagger` em `Development` recebe uma CSP relaxada.
- *Alternativa:* nonce por requisição. Exigiria reescrever o `index.html` a cada resposta, com um ganho pequeno, porque o risco que a CSP mais mitiga é o de scripts.

### D8 — Persistência e migrations
`CoordinDbContext` em `Infrastructure/Persistence`, registrado com `UseNpgsql(ConnectionStrings:Default)`. A migration `InitialCreate` é gerada pelo `dotnet-ef` (ferramenta local em `.config/dotnet-tools.json`) e fica vazia: ela só cria a tabela de histórico. Na inicialização, um passo antes do `app.Run` chama `Database.MigrateAsync()` (spec `service-health`).
- *Alternativa:* aplicar as migrations por um comando separado no deploy. É mais seguro com várias instâncias, mas o Coordin roda em uma instância só, e aplicar na inicialização elimina um passo manual na PaaS.

### D9 — Health check
`AddHealthChecks().AddDbContextCheck<CoordinDbContext>()` (pacote oficial `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`). O `/api/health` é um endpoint comum de Minimal API, anônimo, que consulta o `HealthCheckService` e devolve só `{"status":"..."}` com `200` (saudável) ou `503` (não saudável), com resposta tipada (`HealthResponse`).
- *Alternativa:* `AspNetCore.HealthChecks.NpgSql`. É um pacote comunitário que duplica a configuração da conexão, enquanto o check pelo `DbContext` reusa a mesma.
- *Alternativa descartada (ajustada na implementação, task 6.1):* `MapHealthChecks` com um `ResponseWriter`. Ele não entra no documento OpenAPI (o `paths` saía vazio), e o cliente Angular gerado (D12) nunca receberia o endpoint.

### D10 — Configuração e segredos
- `appsettings.json` não tem string de conexão.
- **Desenvolvimento:** `dotnet user-secrets` guarda `ConnectionStrings:Default`.
- **Compose:** `compose.yaml` sobe `postgres:18` na porta `5432` com volume nomeado, lendo `POSTGRES_PASSWORD` do `.env`. O repositório traz `.env.example` com um valor de exemplo explícito.
- **Produção:** variáveis de ambiente (`ConnectionStrings__Default`).
- **`.gitignore`:** cobre `.env`, `bin/`, `obj/`, `node_modules/`, `dist/`, `src/Coordin.Api/wwwroot/`, `TestResults/`, `coverage/` e `Claude outputs/`.

### D11 — Front Angular
- **Geração:** `npx @angular/cli@22.2.6 new coordin-web` em `src/`, com roteamento, CSS, sem SSR e sem commit automático do CLI. O padrão do Angular 22 já traz componentes standalone, sem zone.js, e Vitest.
- **Tailwind 4:** `@tailwindcss/postcss` com `.postcssrc.json` e `@import "tailwindcss"` no `styles.css`, conforme o guia oficial do Tailwind para Angular.
- **pt-BR:** `registerLocaleData(localePt)`, `LOCALE_ID = 'pt-BR'` e `DATE_PIPE_DEFAULT_OPTIONS` com `timezone: '-0300'`. O `DatePipe` aceita só deslocamentos, não nomes IANA, e `America/Sao_Paulo` não tem horário de verão desde 2019. Se isso mudar, um único provider é ajustado.
- **Desenvolvimento:** `ng serve` usa `proxy.conf.json` para encaminhar `/api` à API local, então o navegador vê uma origem só, como em produção.
- **Lint e formatação:** `ng add angular-eslint` (ESLint) e Prettier 3 com `eslint-config-prettier`, sem conflito de regras.
- **Casca:** cabeçalho com "Coordin" e um `<main>` com `router-outlet`, estilizados com Tailwind. A página inicial é um placeholder em pt-BR.

### D12 — Cliente gerado
`ng-openapi-gen` lê o documento `/openapi/v1.json` da API em execução local e gera o cliente em `src/coordin-web/src/app/api/`, **versionado no Git**. O script `npm run generate:api` refaz a geração. O CI não precisa subir a API para compilar o front, e uma mudança de contrato aparece no diff do PR.
- *Alternativa:* gerar o documento no build (`Microsoft.Extensions.ApiDescription.Server`). Seria mais um pacote e uma etapa no build .NET, por um ganho pequeno com um único autor.

### D13 — Testes de integração
`tests/Coordin.Api.Tests` tem uma fixture com `PostgreSqlContainer` (imagem `postgres:18`, a mesma versão principal do Compose e da produção) e uma `CoordinApiFactory : WebApplicationFactory<Program>`. A factory substitui a string de conexão, define o ambiente (`Testing` por padrão, `Production` nos testes de HSTS e de documentação) e aponta o web root para uma pasta temporária com um `index.html` falso, para testar a SPA sem depender do build do Angular. Cada classe de teste tem um banco próprio (TDD 8.3).

### D14 — Imagem Docker
`Dockerfile` multiestágio:
1. `node:24` executa `npm ci` e `npm run build`;
2. `mcr.microsoft.com/dotnet/sdk:10.0` executa `dotnet publish -c Release` e copia o `dist/coordin-web/browser` para o `wwwroot` publicado;
3. `mcr.microsoft.com/dotnet/aspnet:10.0` roda com o usuário não root da imagem (`$APP_UID`), na porta `8080`.

Um `.dockerignore` exclui `bin/`, `obj/`, `node_modules/`, `.git` e `.env`. O `compose.yaml` ganha um perfil opcional `app` para subir a imagem junto do banco, o que permite verificar ponta a ponta localmente.

### D15 — CI
`.github/workflows/ci.yml` roda em `pull_request` e em `push` para a `main`, com `concurrency` cancelando execuções antigas da mesma branch. Tem dois jobs em `ubuntu-latest`:
- **`dotnet`:** `setup-dotnet` lendo o `global.json`, `dotnet format --verify-no-changes`, build e testes com cobertura (o Docker do runner atende os Testcontainers). O resumo da cobertura é publicado como artefato.
- **`web`:** `setup-node` 24 com cache do npm, `npm ci`, `lint`, `test` e `build` em `src/coordin-web`.

As versões das actions (`checkout`, `setup-dotnet`, `setup-node`, `upload-artifact`) são verificadas nas releases oficiais durante a implementação, fechando a pendência 3 do TDD (10.2).

## Risks / Trade-offs

- **[Change grande para um dia (alvo 11-13)]** → As tasks seguem uma ordem em que cada grupo termina compilando e testado. Se o dia acabar, a change pode fechar sem a imagem Docker, que só será usada na change 7, mas não sem o CI.
- **[CSP quebrar o build de produção do Angular]** → `inlineCritical` desligado e uma verificação manual no navegador, com o console aberto, sobre a imagem Docker (cenário "SPA funciona sob a CSP").
- **[`ForwardedHeaders` aceitar qualquer proxy]** → Necessário porque o IP do proxy da PaaS não é fixo. O impacto é limitado: o esquema e o IP só são usados para o redirecionamento HTTPS e para os logs. Revisar na change 7 com a documentação do provedor escolhido.
- **[Endpoints `/api/_test/*` no assembly de produção]** → Mapeados só quando o ambiente é `Testing`. Um teste garante que `Production` responde `404` para eles.
- **[Testcontainers exige Docker]** (RT-06) → Docker Desktop local e runners Linux do GitHub Actions.
- **[Fuso fixo `-0300` no `DatePipe`]** → Limitação documentada; um único provider muda se o Brasil voltar a adotar horário de verão.
- **[Versões novas: Angular 22, TypeScript 6.0, Tailwind 4.3]** (RT-05) → `package-lock.json` versionado, `npm ci` no CI e sem atualização automática.

## Migration Plan

Não há dados nem deploy anteriores. O rollback é reverter o merge commit. O banco local é descartável (`docker compose down -v`).

## Open Questions

- **Job de CI para construir a imagem Docker:** pode entrar na change 7, junto do deploy, sem mudar as specs nem as tasks desta change.
