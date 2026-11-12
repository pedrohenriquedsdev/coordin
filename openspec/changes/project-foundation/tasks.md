# Tasks — project-foundation

Regras para todas as tasks:
- Toda instalação de pacote, ferramenta ou template é feita só depois da autorização do autor (`CLAUDE.md` §11). As versões são as do TDD 3 e as conferidas no design.
- Ao fim de cada grupo, o build e os testes já existentes passam, e o autor recebe os comandos de commit (um commit por bloco lógico).

## 1. Base do repositório

- [x] 1.1 Criar `.gitignore` (D10), `.editorconfig` (convenções C#, TypeScript, JSON, YAML e Markdown) e `global.json` (SDK `10.0.403`, `rollForward: latestFeature`); verificar que `git status` não lista `Claude outputs/` e que `dotnet --version` dentro do repositório responde `10.0.403`
- [x] 1.2 Criar `Directory.Build.props` (Nullable, ImplicitUsings, TreatWarningsAsErrors, AnalysisLevel) e `Directory.Packages.props` com as versões NuGet do TDD 3 e do design; verificar que o arquivo de versões é aceito por `dotnet restore` na task 2.1
- [x] 1.3 Criar a solução `Coordin.slnx` e a ferramenta local `dotnet-ef` 10.0.15 (`.config/dotnet-tools.json`); verificar com `dotnet tool run dotnet-ef --version`
- [ ] 1.4 Atualizar o `CLAUDE.md` §3 com a estrutura de pastas do DT-04 (agora existente); verificar que a árvore do §3 coincide com o repositório

## 2. Motor de cálculo (projeto vazio e puro)

- [x] 2.1 Criar `src/Coordin.Engine` (`classlib`, `net10.0`, sem `PackageReference`) e `tests/Coordin.Engine.Tests` (`.csproj` escrito à mão com xUnit v3, VSTest e coverlet; D2), ambos na solução; verificar com `dotnet build Coordin.slnx`
- [x] 2.2 Escrever o teste de arquitetura `EngineAssembly_References_OnlyRuntimeAssemblies` (D3) e um marcador de assembly no motor; verificar que `dotnet test` passa e que o teste falha ao referenciar temporariamente um pacote externo

## 3. API: esqueleto, Problem Details e base de testes de integração

- [x] 3.1 Criar `src/Coordin.Api` (Minimal APIs, `net10.0`) com a organização do D4 (`Features/`, `Infrastructure/Http`, `Infrastructure/Persistence`) e `public partial class Program`, referenciando o `Coordin.Engine`; verificar com `dotnet build` e `dotnet run` respondendo em `http://localhost`
- [x] 3.2 Criar `tests/Coordin.Api.Tests` com a fixture do Testcontainers (`postgres:18`) e a `CoordinApiFactory` (D13: string de conexão, ambiente, web root temporário com `index.html` falso); verificar com um teste de fumaça que sobe a factory
- [x] 3.3 Configurar Problem Details em português (D6: `AddProblemDetails`, `UseExceptionHandler`, `UseStatusCodePages`, `traceId`, fábrica `ApiProblems.Conflict`) e a validação nativa (`AddValidation`); verificar com os testes dos cenários `404`, `500` sem detalhes internos, `409` com `code` e `400` com `errors.name` (spec `api-conventions`), usando os endpoints `/api/_test/*` do ambiente `Testing`
- [x] 3.4 Garantir o JSON em `camelCase` e o prefixo `/api` com o fallback `MapFallback("/api/{**path}")` → `404` (D5, passo 7); verificar com os testes "Propriedades em camelCase" e "Rota de API inexistente" e com o teste de que `/api/_test/*` responde `404` em `Production`

## 4. Segurança HTTP

- [ ] 4.1 Implementar o middleware de cabeçalhos de segurança com a CSP do D7 (relaxada só em `/swagger` em `Development`), na posição do D5; verificar com os testes "Cabeçalhos na API", "Cabeçalhos na SPA" e "Cabeçalhos também em erros" (spec `http-security`)
- [ ] 4.2 Configurar `UseForwardedHeaders`, `UseHsts` e `UseHttpsRedirection` só em produção (D5); verificar com os testes "HSTS em produção", "Requisição encaminhada pelo proxy como HTTPS" e "Sem HSTS em desenvolvimento"
- [ ] 4.3 Verificar a ausência de CORS com o teste "Requisição de outra origem" (sem `Access-Control-Allow-Origin`)

## 5. Persistência, migration inicial e saúde

- [ ] 5.1 Criar `compose.yaml` (`postgres:18`, porta `5432`, volume nomeado, senha do `.env`) e `.env.example` com valores de exemplo; configurar `ConnectionStrings:Default` por `dotnet user-secrets` (D10); verificar com `docker compose up -d` e `docker compose ps` mostrando o banco saudável
- [ ] 5.2 Criar `CoordinDbContext`, gerar a migration vazia `InitialCreate` com o `dotnet-ef` e aplicá-la na inicialização (D8); verificar com os testes "Banco vazio" e "Banco já atualizado" (spec `service-health`)
- [ ] 5.3 Mapear `GET /api/health` anônimo com o check do `DbContext` e a resposta só com `status` (D9); verificar com os testes "Aplicação e banco saudáveis", "Banco indisponível" (container parado ou string de conexão inválida) e "Falha sem detalhes internos"
- [ ] 5.4 Registrar o `GET /api/health` na tabela do TDD 5.2 (acréscimo aprovado pelo autor em 2026-11-12); verificar que o TDD cita o endpoint e a spec `service-health`
- [ ] 5.5 Verificar a spec "Nenhum segredo versionado" com um teste que lê os `appsettings*.json` versionados e não encontra `Password=` nem `ConnectionStrings` com valor, e confirmar que `git check-ignore .env` retorna o arquivo

## 6. Documentação da API (OpenAPI e Swagger UI)

- [ ] 6.1 Adicionar `Microsoft.AspNetCore.OpenApi` (`/openapi/v1.json`) e `Swashbuckle.AspNetCore.SwaggerUI` (`/swagger`) só em `Development` (DT-19); verificar com os testes "Documentação em desenvolvimento" (documento contém `/api/health`) e "Documentação bloqueada em produção"

## 7. Front Angular

- [ ] 7.1 Gerar `src/coordin-web` com o Angular CLI 22.2.6 (roteamento, CSS, sem SSR; D11); verificar com `npm run build` e `npm test` padrão passando
- [ ] 7.2 Instalar e configurar o Tailwind CSS 4 (`@tailwindcss/postcss`, `.postcssrc.json`, `@import "tailwindcss"`); verificar que uma classe utilitária aparece no CSS do build
- [ ] 7.3 Configurar ESLint (`angular-eslint`) e Prettier 3 com `eslint-config-prettier` e os scripts `lint` e `format`; verificar com `npm run lint` sem erros
- [ ] 7.4 Configurar pt-BR (`lang="pt-BR"`, título "Coordin", `registerLocaleData`, `LOCALE_ID`, `DATE_PIPE_DEFAULT_OPTIONS` com `-0300`) e desligar o `inlineCritical` no build de produção (D7); verificar com teste Vitest do cenário "Formatação pt-BR" (`1.234,5` e `28/11/2026`)
- [ ] 7.5 Criar a casca responsiva (cabeçalho "Coordin", `<main>` com `router-outlet`, página inicial placeholder em pt-BR) com Tailwind; verificar com teste Vitest de que o componente renderiza "Coordin" e manualmente em 360 px e 1280 px sem rolagem horizontal (spec `web-shell`)
- [ ] 7.6 Criar `proxy.conf.json` encaminhando `/api` para a API local e ligá-lo ao `ng serve`; verificar que `http://localhost:4200/api/health` responde pela API

## 8. SPA servida pela API

- [ ] 8.1 Adicionar `UseStaticFiles` e `MapFallbackToFile("index.html")` depois do fallback de `/api` (D5); verificar com os testes "Página inicial", "Arquivos estáticos", "Recarregar uma rota da SPA" e "Rota de API não cai na SPA" (spec `web-shell`)

## 9. Cliente gerado

- [ ] 9.1 Instalar o `ng-openapi-gen` 1.1.x, criar a configuração e o script `generate:api` (D12) e gerar o cliente em `src/app/api/` a partir da API local; verificar que o serviço do endpoint de saúde foi gerado e que `npm run build` e `npm run lint` passam com o código gerado (excluído do lint se necessário)

## 10. Imagem Docker

- [ ] 10.1 Criar o `Dockerfile` multiestágio (D14) e o `.dockerignore`; verificar com `docker build -t coordin:local .` concluído sem erros
- [ ] 10.2 Adicionar o perfil `app` ao `compose.yaml` e subir a imagem com o banco; verificar que `GET http://localhost:8080/api/health` responde `200`, que `/` abre a casca e que o console do navegador não mostra violações de CSP (cenário "SPA funciona sob a CSP")
- [ ] 10.3 Escrever no `README.md` como rodar localmente (Compose, user-secrets, API, `ng serve`, testes e imagem); verificar seguindo os passos a partir de um clone limpo

## 11. CI

- [ ] 11.1 Verificar as versões atuais das actions `checkout`, `setup-dotnet`, `setup-node` e `upload-artifact` nas releases oficiais e registrar no PR (pendência 3 do TDD 10.2)
- [ ] 11.2 Criar `.github/workflows/ci.yml` com os jobs `dotnet` e `web` (D15); verificar que o workflow passa no PR da change

## 12. Verificação final

- [ ] 12.1 Rodar todos os comandos do TDD 8.6 que se aplicam (`dotnet build Coordin.slnx`, `dotnet format Coordin.slnx --verify-no-changes`, `dotnet test Coordin.slnx --collect:"XPlat Code Coverage"`, `npm ci`, `npm run lint`, `npm test`, `npm run build`) e `openspec validate project-foundation`; registrar os resultados reais na descrição do PR, separando local de CI

## Workflow follow-up

- Atualizar o `CLAUDE.md` §13 (change 1 concluída; próximo passo: change 2 `authentication`).
- Abrir o PR `chore/project-foundation` → `main` com o template, após autorização do autor; integrar com `gh pr merge <n> --merge`.
- Após o merge, rodar `/opsx:archive project-foundation` e conferir as specs em `openspec/specs/`.
