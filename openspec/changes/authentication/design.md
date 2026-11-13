# Design

## Context

Motivação e escopo estão em `proposal.md`; o comportamento exigido, nas specs `authentication` e `web-authentication`. O TDD já fecha o desenho de segurança (DT-28, DT-29, DT-30 e o fluxo da seção 7.1); este documento só decide **como encaixar isso no código da change 1**.

Estado atual do código:

- `Coordin.Api` tem `Program.cs` com a ordem de middlewares do D5 da change 1 (esquema/HTTPS → cabeçalhos de segurança → Problem Details → documentação → arquivos estáticos → endpoints → fallbacks), Problem Details em português (`ProblemTexts` cobre 400, 404, 409, 500 e 503), JSON em `camelCase`, encaminhamento de cabeçalhos do proxy e `UseHttpsRedirection`/HSTS só em produção.
- `CoordinDbContext` é um `DbContext` vazio, com a migration `InitialCreate` aplicada na inicialização.
- Os endpoints existentes são `GET /api/health`, o OpenAPI/Swagger (só em `Development`) e os `/api/_test/*` (só em `Testing`). Há ainda os fallbacks `/api/{**path}` (404) e o da SPA.
- O `appsettings.json` versionado não tem segredo, e um teste garante isso (ele recusa `Password`, `SigningKey` e `ConnectionStrings` em `appsettings*.json`).
- O Angular 22 é zoneless, tem a casca (`App`), a rota `''` (`Home`), o proxy do `ng serve` para `/api` e um cliente tipado gerado em `src/app/api/`. Ainda não há `provideHttpClient`.

## Goals / Non-Goals

**Goals:**

- Todo endpoint existente e futuro nasce protegido; as exceções são poucas, explícitas e testadas.
- Nenhum segredo, conta ou token em arquivo versionado, em log ou em armazenamento legível do navegador.
- Fluxo de sessão sem fricção para o gestor: renovação invisível, sem novo login a cada recarga.

**Non-Goals:**

- Papéis, múltiplos usuários, recuperação de senha por e-mail, troca de senha pela interface e troca de e-mail da conta.
- Limpeza periódica de refresh tokens expirados (volume mínimo com usuário único; ver Riscos).
- Mudar qualquer decisão do TDD. Se algo abaixo contrariar um DT-*, o certo é parar e perguntar ao autor.

## Decisions

### D1. Onde fica o código

- `Features/Auth/`: endpoints (`AuthEndpoints`), serviços (`TokenService`, `RefreshTokenService`), entidade `RefreshToken`, DTOs e a criação da conta (`ManagerAccountSeeder`).
- `Infrastructure/Auth/`: configuração (`AuthenticationSetup`: JwtBearer, autorização, Identity, limite de requisições) e as opções tipadas (`JwtOptions`, `ManagerOptions`).
- Segue a organização por feature e infraestrutura do D4 da change 1. Nada disso toca o `Coordin.Engine`, que continua puro (DT-02).

### D2. Identity sem a parte de cookie, UI ou endpoints prontos

Usar `AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<CoordinDbContext>()` e `UserManager`. `CoordinDbContext` passa a herdar de `IdentityDbContext<ApplicationUser>`.

- *Alternativa: `AddIdentity`/`SignInManager`:* traz o esquema de cookie e depende de `HttpContext`; não serve a uma API com JWT.
- *Alternativa: `MapIdentityApi`:* já descartada no DT-28 (token proprietário e cadastro público).
- O bloqueio é feito à mão com `UserManager`: `IsLockedOutAsync` antes de checar a senha, `CheckPasswordAsync`, `AccessFailedAsync` em caso de erro e `ResetAccessFailedCountAsync` em caso de acerto.
- Opções: `Lockout` com 5 tentativas e 15 minutos (valendo também para usuários novos); `Password.RequiredLength = 12` com as demais exigências de composição desligadas, porque o DT-28 fixa só o tamanho mínimo (suposição registrada).
- O hash da senha é o padrão do Identity (PBKDF2 com sal); nada a configurar.

### D3. Token de acesso: JWT próprio validado pelo JwtBearer

- Emissão com `JsonWebTokenHandler` (`Microsoft.IdentityModel.JsonWebTokens`, que já vem com o JwtBearer), HMAC-SHA256, claims `sub` (id do usuário) e `email`, `exp` em 15 minutos, `iss` e `aud` configurados.
- Validação: `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` ligados e `ClockSkew = TimeSpan.Zero`, para os 15 minutos serem exatos (o padrão de 5 minutos estenderia a validade).
- Configuração: `Jwt:Issuer` e `Jwt:Audience` ficam no `appsettings.json` (não são segredos); `Jwt:SigningKey` vem só de variável de ambiente ou user-secrets (DT-30). A chave é texto lido como bytes UTF-8, com no mínimo 32 bytes. As opções são validadas na inicialização (`ValidateOnStart`), então uma chave ausente ou curta impede a API de subir, com mensagem que nunca inclui o valor.
- O relógio de emissão é o `TimeProvider` do ASP.NET Core. Nos testes, o token expirado é fabricado com a mesma chave e um `exp` no passado, em vez de tentar adiantar o relógio do validador (menos acoplado à versão da biblioteca).

### D4. Refresh token: rotação atômica e hash no banco

- Valor: 32 bytes de `RandomNumberGenerator` em base64url. No banco, só o hash SHA-256, com índice único em `TokenHash`.
- Entidade `RefreshToken` conforme o TDD 4.2/DT-28: `Id`, `UserId`, `TokenHash`, `ExpiresAt`, `RevokedAt`, `ReplacedById`. A migration é `AddAuthentication` (tabelas do Identity + `RefreshTokens`), na mesma convenção da `InitialCreate`.
- **Rotação** em uma transação: marcar o token atual como revogado com um `UPDATE ... WHERE RevokedAt IS NULL` (`ExecuteUpdateAsync`) e emitir o novo apontando `ReplacedById`. Se o `UPDATE` não afetar nenhuma linha, o token já foi usado: é tratado como reuso. Assim, duas chamadas simultâneas com o mesmo cookie nunca geram dois tokens válidos.
- **Reuso:** token encontrado, já substituído (`ReplacedById` preenchido) → revoga todos os tokens ativos do usuário e responde `401`. Token revogado só por `logout` (sem `ReplacedById`) → `401` sem revogar o resto: reapresentar um token de uma sessão que o próprio gestor encerrou não indica roubo (suposição registrada).
- **Cookie** `refresh_token`: `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/auth`, `Max-Age` de 7 dias. Para apagá-lo, emite-se um cookie expirado com os mesmos atributos (senão o navegador o mantém). O `Secure` é fixo (DT-28), inclusive em desenvolvimento: Chrome, Edge e Firefox aceitam cookie `Secure` em `http://localhost`; no Safari não (ver Riscos).
- O cookie nunca vai no corpo da resposta. No `refresh` sem cookie ou com valor desconhecido, a resposta é `401`, sem distinguir os casos.

### D5. Proteção por padrão com a política de fallback

`AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())`. Todo endpoint sem metadado de autorização passa a exigir o gestor; as exceções usam `.AllowAnonymous()` de forma explícita:

| Endpoint | Tratamento |
|---|---|
| `login`, `refresh`, `logout`, `GET /api/health` | `AllowAnonymous` |
| `GET /api/auth/me` | protegido (política padrão) |
| fallback `/api/{**path}` e fallback da SPA | `AllowAnonymous`; senão uma rota de API inexistente responderia `401` em vez de `404` (spec vigente) e a SPA não abriria |
| `MapOpenApi()` (só em `Development`) | `AllowAnonymous`; senão o Swagger UI e o `generate:api` quebram. O documento não contém dados do planejamento |
| grupo `/api/_test/*` (só em `Testing`) | `AllowAnonymous`, para os testes de erro e validação da change 1 continuarem valendo |
| `GET /api/_test/protected` (só em `Testing`) | **fora** do grupo, sem metadado: prova que o padrão exige login |

A Swagger UI e os arquivos estáticos não são endpoints e não passam pela política.

Ordem no pipeline (estende o D5 da change 1): `UseCoordinTransportSecurity` → `UseCoordinSecurityHeaders` → `UseCoordinProblemDetails` → documentação → arquivos estáticos → `UseRateLimiter` → `UseAuthentication` → `UseAuthorization` → endpoints.

### D6. Erros 401 e 429 em Problem Details

- O `JwtBearer` responde `401` com `WWW-Authenticate: Bearer` e corpo vazio; o `UseStatusCodePages` já configurado na change 1 transforma esse corpo vazio em Problem Details. Basta acrescentar 401, 403 e 429 aos textos em português de `ProblemTexts` (um teste confirma que o corpo é `application/problem+json`).
- O login inválido devolve `401` com `detail` "E-mail ou senha inválidos.", igual para e-mail inexistente, senha errada e conta bloqueada (sem revelar a situação da conta).
- O limite de requisições responde `429` com `Retry-After` e o mesmo formato (via `OnRejected`).

### D7. Limite de requisições

Rate limiter nativo (`AddRateLimiter`) com uma política `auth`, janela fixa de 10 requisições por minuto, particionada por `RemoteIpAddress`, aplicada com `.RequireRateLimiting("auth")` só em `login` e `refresh`. O IP real atrás da hospedagem vem do `UseForwardedHeaders` já configurado na change 1.

O limite é configurável (`Auth:RateLimit:PermitLimit`, padrão 10). A fábrica dos testes eleva o valor para os testes de integração poderem fazer muitos logins; só o teste do limite usa o 10 real. Isso evita testes lentos ou instáveis por causa do limite.

### D8. Conta do gestor na inicialização

`ManagerAccountSeeder` roda logo depois de `MigrateCoordinDatabaseAsync` e antes de a API aceitar requisições:

1. Com usuário existente e sem `Manager:ResetPassword`: não faz nada (a senha da variável é ignorada).
2. Sem usuário e com `Manager:Email` e `Manager:Password`: cria a conta (e-mail confirmado, sem papéis). Se o Identity rejeitar a senha, a inicialização falha com a lista de requisitos violados, sem repetir a senha.
3. Com `Manager:ResetPassword=true` e usuário existente: troca a senha pela da variável, zera bloqueio e tentativas e revoga todos os refresh tokens. Só a senha muda (o e-mail da conta não muda; suposição registrada).
4. Sem usuário e sem as variáveis: a API sobe e registra um aviso (ela ainda serve `health`; ninguém consegue entrar até a configuração ser feita).

Os nomes `Manager__Email` etc. vêm do DT-29; no código são lidos como `Manager:Email` (a convenção de dois sublinhados do .NET).

### D9. Contrato

DTOs (`LoginRequest` com `[Required]`, `AccessTokenResponse`, `CurrentUserResponse`) e `WithName` estáveis (`Login`, `Refresh`, `Logout`, `GetCurrentUser`), marcados com a tag `Auth` e `Produces`/`ProducesProblem`, para o `ng-openapi-gen` gerar um serviço tipado. Depois da API pronta, o cliente é regenerado com `npm run generate:api` e commitado, como no D12 da change 1.

### D10. Front: sessão em um serviço, interceptor funcional e guarda

Em `src/app/core/auth/`:

- **`SessionService`:** guarda o token em um `signal` privado (somente leitura para fora) e expõe `isAuthenticated` (computed). Métodos `login`, `refresh`, `logout`, `clear`. Usa o cliente gerado para chamar a API. Nada vai para `localStorage`/`sessionStorage`.
- **Renovação única:** `refresh()` mantém a `Observable` da renovação em andamento e devolve a mesma a quem pedir enquanto ela não termina. É o que impede N chamadas simultâneas de provocar a detecção de reuso do D4.
- **`authInterceptor`** (`HttpInterceptorFn`, registrado com `provideHttpClient(withInterceptors([...]))`): só toca em URLs que começam com `/api/`; ignora `/api/auth/login`, `refresh` e `logout`; anexa o `Bearer`; em `401` de um endpoint protegido chama `refresh()` e repete a requisição uma vez; se a renovação falha, chama `clear()` e navega para `/login`.
- **`authGuard`** (`CanActivateFn`): com sessão, libera; sem sessão, `router.createUrlTree(['/login'], { queryParams: { returnUrl } })`. Uma guarda inversa em `/login` manda quem já tem sessão para a página inicial.
- **`provideAppInitializer`:** antes de rotear, tenta `refresh()` e ignora a falha (sem cookie, a guarda cuida do redirecionamento). É assim que recarregar a página mantém a sessão.
- **`returnUrl`** só é aceito se for um caminho interno (começa com `/` e não com `//`), para o login não virar um redirecionamento aberto para outro site.
- **Tela `/login`:** componente standalone com `ReactiveFormsModule` (já faz parte do Angular, sem pacote novo), Tailwind, `type="password"`, mensagens em português (protótipo T01), botão desabilitado durante a chamada.
- **Casca:** o `App` mostra o botão "Sair" apenas com sessão. As rotas ficam: `/login` (pública) e `''` com `canActivate: [authGuard]`.
- O `locale` e o fuso da change 1 não mudam. O CSP já permite `connect-src 'self'`, então o front fala com a API na mesma origem sem ajuste.

### D11. Testes

- **API (integração, PostgreSQL real via Testcontainers):** a `CoordinApiFactory` passa a fornecer, em memória, uma `Jwt:SigningKey` aleatória por execução, `Manager:Email` e `Manager:Password` de teste (valores fictícios, nunca versionados como segredo real) e o limite de requisições elevado. Um auxiliar de teste faz login e devolve o cliente autenticado. Casos: todos os cenários das specs, mais a prova de que `Set-Cookie` tem os atributos exigidos. Como o cookie é `Secure` e o teste fala em `http`, os testes leem o `Set-Cookie` e reenviam o `Cookie` manualmente, sem o `CookieContainer`.
- **Bloqueio por tempo:** o fim dos 15 minutos é provado ajustando `LockoutEnd` para o passado direto no banco (o Identity calcula o prazo com o relógio do sistema; a confirmar na implementação).
- **Front (Vitest):** `SessionService` (token só em memória, renovação única compartilhada), interceptor (`HttpTestingController`: anexa token, repete após `401`, não repete em `/api/auth/*`, `401` simultâneos → um `refresh`), guarda (com e sem sessão, `returnUrl` interno vs. externo), tela de login (campos obrigatórios, erro `401`, `429`) e a casca (botão "Sair").
- **Verificação manual** no fim: a cadeia real (`docker compose` + API + `ng serve`) com login, recarga da página, expiração do token e logout, e as larguras de 360 px e 1280 px da tela de login.

### D12. Infra e documentação

- `compose.yaml`, perfil `app`: repassa `Jwt__SigningKey`, `Manager__Email` e `Manager__Password` a partir do `.env` ignorado pelo Git (mesmo padrão `${VAR:?mensagem}` da senha do banco). A imagem não muda.
- `README`: seção "Configurar a autenticação" com as variáveis, como gerar uma chave aleatória com um comando e como definir tudo por `dotnet user-secrets`; e o aviso do `Secure` no Safari. Nenhum valor real e nenhum arquivo-modelo de `.env` entram no repositório.
- CI: não precisa de segredos, porque os testes geram a chave em memória.

### Suposições registradas (detalhes que o TDD não fixa)

1. Só o tamanho mínimo da senha é exigido (12); letras, números e símbolos não são obrigatórios.
2. Falha de login por bloqueio responde igual à senha errada.
3. Redefinição de senha também zera o bloqueio e as tentativas.
4. Redefinição troca só a senha, nunca o e-mail.
5. Token revogado só por `logout`, se reapresentado, não revoga os demais.
6. Sem `Manager__*` e sem usuários, a API sobe com aviso (em vez de falhar); com `Jwt__SigningKey` ausente ou curta, a API não sobe.
7. `logout` é anônimo e idempotente, para funcionar com o token de acesso já expirado.

Nenhuma delas contradiz um DT-*; se o autor discordar de alguma, o ajuste acontece antes do `apply`.

## Risks / Trade-offs

- **Condição de corrida entre abas:** duas abas abertas podem chamar `refresh` quase juntas com o mesmo cookie; a segunda veria um token já trocado e derrubaria a sessão (regra de reuso do DT-28). → A renovação única do D10 cobre uma aba; entre abas é um risco aceito (usuário único). Se incomodar, a evolução natural é uma pequena janela de tolerância, que exigiria mudar o DT-28.
- **Cookie `Secure` no Safari em `http://localhost`:** o navegador não o aceita no desenvolvimento local. → Documentado no README; Chrome, Edge e Firefox funcionam. Produção é HTTPS (DT-30).
- **Política de fallback quebrando endpoints existentes:** `_test`, OpenAPI e fallbacks passariam a responder `401`. → `AllowAnonymous` explícito (D5) e os testes da change 1 continuam rodando como prova.
- **Limite por IP atrás da hospedagem:** se o proxy não repassar `X-Forwarded-For` corretamente, todos compartilhariam um IP. → Já tratado no `UseForwardedHeaders` da change 1; a validação em produção fica na change 7.
- **Bloqueio como vetor de negação de serviço:** quem souber o e-mail do gestor pode bloqueá-lo por 15 minutos com 5 tentativas erradas. → Inerente ao DT-28; o limite de 10 por minuto não impede; é um risco aceito para um sistema interno de usuário único.
- **Refresh tokens expirados acumulam na tabela:** não há limpeza. → Com um único usuário e 7 dias de validade o volume é desprezível; não-objetivo desta change.
- **Enumeração por tempo de resposta:** o login de um e-mail inexistente responde mais rápido que o de um existente (não há hash a conferir). → Risco baixo para um único usuário, não tratado aqui.
- **Falha na implementação do JWT/refresh (RT-07):** → testes de integração de cada cenário das specs e CSP da change 1.
- **Pacotes novos:** dois pacotes NuGet exigem autorização do autor antes da instalação (CLAUDE.md §11). → Task 1.1 pede a autorização e confere as versões nas fontes oficiais.

## Migration Plan

1. Autorizar e adicionar os pacotes; criar a migration `AddAuthentication` (a API a aplica na inicialização, como a `InitialCreate`).
2. Em cada ambiente, definir `Jwt__SigningKey`, `Manager__Email` e `Manager__Password` **antes** de subir esta versão (sem a chave a API não inicia). No banco local, basta o `.env` e os user-secrets (README).
3. Para trocar a senha depois: definir `Manager__Password` novo e `Manager__ResetPassword=true`, reiniciar e remover o sinalizador.
4. **Rollback:** reverter o merge commit. O banco local é descartável (`docker compose down -v`); em um banco já migrado, remover a migration exige o `dotnet ef database update InitialCreate` antes do revert.
