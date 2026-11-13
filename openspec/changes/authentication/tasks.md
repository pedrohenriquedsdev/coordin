# Tasks — authentication

Regras para todas as tasks:
- Toda instalação de pacote ou ferramenta só acontece depois da autorização do autor (`CLAUDE.md` §11). As versões são as do TDD 3, conferidas nas fontes oficiais antes de usar.
- Ao fim de cada grupo, o build e os testes (novos e antigos) passam, e o autor recebe os comandos de commit (um commit por bloco lógico). Cada commit compila e passa nos testes por conta própria.
- Nenhum valor de segredo, senha ou e-mail real entra no repositório, nem em testes versionados.

## 1. Pacotes, opções e base dos testes

- [x] 1.1 Pedir a autorização do autor e conferir nas fontes oficiais (NuGet) as versões de `Microsoft.AspNetCore.Identity.EntityFrameworkCore` e `Microsoft.AspNetCore.Authentication.JwtBearer` (TDD indica 10.0.15); adicionar ao `Directory.Packages.props` e ao `Coordin.Api.csproj`; verificar com `dotnet restore` e `dotnet build Coordin.slnx` sem avisos
- [x] 1.2 Criar `JwtOptions` (`Issuer`, `Audience`, `SigningKey`) e `ManagerOptions` (`Email`, `Password`, `ResetPassword`) com validação na inicialização (chave com no mínimo 32 bytes); colocar só `Jwt:Issuer` e `Jwt:Audience` no `appsettings.json`; verificar com testes dos cenários "Chave ausente ou curta" (a API não inicia e a mensagem não contém o valor) e "Nenhum segredo versionado" (o teste existente de `appsettings*.json` continua passando)
- [x] 1.3 Ajustar a `CoordinApiFactory` para fornecer, em memória, `Jwt:SigningKey` aleatória por execução, `Manager:Email` e `Manager:Password` fictícios e o limite de requisições elevado, e aceitar sobrescritas de configuração por teste; verificar que os 44 testes da change 1 seguem passando

## 2. Persistência: Identity e refresh token

- [x] 2.1 Criar `ApplicationUser`, a entidade `RefreshToken` (`Id`, `UserId`, `TokenHash` com índice único, `ExpiresAt`, `RevokedAt`, `ReplacedById`) e passar o `CoordinDbContext` a `IdentityDbContext<ApplicationUser>`; configurar o Identity em `AddIdentityCore` (bloqueio de 5 tentativas por 15 minutos, senha com no mínimo 12 caracteres); verificar com `dotnet build`
- [x] 2.2 Gerar a migration `AddAuthentication` com `dotnet tool run dotnet-ef`, revisar o SQL gerado e ajustar os testes de migração (`MigrationOnStartupTests`) para as duas migrations; verificar com os testes "Banco vazio" e "Banco já atualizado" e com a existência das tabelas de usuário e `RefreshTokens` no banco de teste

## 3. Conta do gestor

- [x] 3.1 Implementar o `ManagerAccountSeeder` (D8) e chamá-lo depois da migração; verificar com testes dos cenários "Primeira inicialização" (login possível), "Conta já existente" (senha inalterada), "Redefinição de senha" (nova senha vale, bloqueio zerado, refresh tokens revogados), "Senha curta na configuração" (falha sem repetir a senha) e "Configuração ausente" (API sobe e registra aviso)

## 4. Token de acesso e proteção por padrão

- [x] 4.1 Configurar o JwtBearer (validação de emissor, audiência, assinatura e validade, sem tolerância de relógio), a política de fallback `RequireAuthenticatedUser`, `UseAuthentication`/`UseAuthorization` na ordem do D5 e o `TokenService` que emite o JWT de 15 minutos; verificar com testes de unidade/integração dos cenários "Token válido", "Token expirado", "Token adulterado ou de outro emissor" (tokens fabricados com a mesma chave)
- [x] 4.2 Marcar `AllowAnonymous` em `GET /api/health`, nos fallbacks de `/api` e da SPA, no `MapOpenApi()` e no grupo `/api/_test/*`; criar `GET /api/_test/protected` (só em `Testing`, sem metadado); acrescentar 401, 403 e 429 ao `ProblemTexts`; verificar com os cenários "Endpoint de negócio sem token", "com token inválido", "Saúde continua pública", "Rota inexistente continua 404", "Formato do 401" (Problem Details em português e `WWW-Authenticate: Bearer`) e com a suíte da change 1 inteira passando (erros, validação, Swagger em `Development`, SPA)

## 5. Login, bloqueio e usuário autenticado

- [x] 5.1 Implementar `POST /api/auth/login` (DTOs com `[Required]`, mensagem genérica, cookie `refresh_token` com os atributos do D4) e o `RefreshTokenService` na parte de emissão; criar o auxiliar de teste que faz login e devolve um cliente autenticado (movido da 1.3, pois depende deste endpoint); verificar com os cenários "Credenciais válidas" (inclui `Set-Cookie` com `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/auth`, 7 dias, e o token ausente do corpo), "Senha incorreta", "E-mail inexistente" e "Campos ausentes"
- [x] 5.2 Implementar o bloqueio no login (D2); verificar com os cenários "Quinta tentativa errada", "Fim do bloqueio" (ajustando `LockoutEnd` no banco) e "Acerto zera as tentativas"
- [x] 5.3 Implementar `GET /api/auth/me`; verificar com os cenários "Com token válido" (só `email`, sem hash nem campos internos) e "Sem token"; e com o cenário "Não existe cadastro público" (`POST /api/auth/register` responde `404`)

## 6. Refresh, rotação, reuso e logout

- [x] 6.1 Implementar `POST /api/auth/refresh` com a rotação atômica do D4; verificar com os cenários "Refresh token válido", "Token já trocado não vale", "Sem cookie ou token desconhecido" e "Refresh token expirado" (cookie apagado)
- [x] 6.2 Implementar a detecção de reuso (revoga todos os tokens do usuário) e `POST /api/auth/logout` (anônimo, idempotente, apaga o cookie); verificar com os cenários "Reuso detectado" (o token mais novo também deixa de valer), "Logout com sessão ativa" e "Logout sem sessão", e com um teste de duas chamadas simultâneas ao `refresh` com o mesmo cookie (só uma tem sucesso)
- [x] 6.3 Verificar o cenário "Banco sem texto puro" (senha e refresh token não aparecem em nenhuma coluna) e "Resposta de erro sem segredos" (corpo e logs da falha de login não contêm a senha enviada)

## 7. Limite de requisições

- [x] 7.1 Configurar o rate limiter (`auth`, 10 por minuto por IP, limite configurável) e aplicá-lo só a `login` e `refresh`, com `429` em Problem Details e `Retry-After`; verificar com os cenários "Limite excedido" (a 11ª chamada) e "Outros endpoints não sofrem o limite"

## 8. Contrato e cliente gerado

- [x] 8.1 Completar os metadados OpenAPI dos endpoints de autenticação (`WithName`, tag `Auth`, `Produces`, `ProducesProblem`); verificar que `/openapi/v1.json` em `Development` contém os quatro caminhos e continua acessível sem login
- [x] 8.2 Rodar `npm run generate:api` com a API local e commitar o cliente regenerado; verificar que o serviço de autenticação foi gerado e que `npm run build` e `npm run lint` passam

## 9. Front: sessão e interceptor

- [x] 9.1 Criar o `SessionService` (token só em um signal, `login`, `refresh` com renovação única compartilhada, `logout`, `clear`) e registrar `provideHttpClient`; verificar com testes Vitest dos cenários "Token de acesso só em memória" (nada em `localStorage`/`sessionStorage`) e de que duas chamadas simultâneas a `refresh()` fazem uma única requisição
- [x] 9.2 Criar o `authInterceptor` (anexa `Bearer` só em `/api/`, ignora `/api/auth/login|refresh|logout`, repete uma vez após `401`, encerra a sessão se a renovação falhar); verificar com testes com `HttpTestingController` dos cenários "Token enviado", "Token expirado", "Chamadas simultâneas" (três `401`, um `refresh`), "Renovação falha" e "Endpoints de autenticação"

## 10. Front: telas, guarda e casca

- [x] 10.1 Criar o `authGuard`, a guarda inversa de `/login`, as rotas (`/login` pública; `''` protegida) e o `provideAppInitializer` que restaura a sessão; verificar com testes dos cenários "Sem sessão", "Retorno ao destino" (e rejeição de `returnUrl` externo, como `//outro-site`), "Login já feito", "Recarregar com sessão válida" e "Abrir sem sessão válida"
- [x] 10.2 Criar a tela `/login` (E-mail, Senha oculta, "Entrar", validação de campos, mensagens do protótipo T01 para `401` e `429`, botão desabilitado durante a chamada) e o botão "Sair" na casca; verificar com testes dos cenários "Login com sucesso", "Credenciais inválidas", "Campos obrigatórios", "Muitas tentativas", "Senha oculta", "Sair" e "Botão oculto no login"
- [x] 10.3 Verificar manualmente, com a API local e `ng serve`, os cenários "Celular" e "Computador" da tela de login (360 px e 1280 px, sem rolagem horizontal, medido por `scrollWidth`) e o fluxo completo: login, recarregar a página sem novo login, esperar o token de 15 minutos expirar (ou fabricar um token curto em ambiente de teste), e sair

## 11. Infra e documentação

- [x] 11.1 Ajustar o perfil `app` do `compose.yaml` para repassar `Jwt__SigningKey`, `Manager__Email` e `Manager__Password` do `.env` (com padrão vazio `${VAR:-}`: o Compose resolve as variáveis do arquivo inteiro, e `${VAR:?}` quebraria o `docker compose up -d` comum, só com o banco); verificar que `docker compose --profile app up` sobe a imagem, que `GET /api/health` responde `200` sem login, que `GET /api/auth/me` sem token responde `401` e que o login funciona pela SPA servida pela API
- [x] 11.2 Documentar no `README.md` a configuração da autenticação (variáveis, como gerar uma chave aleatória com um comando, `dotnet user-secrets`, redefinição de senha e o aviso do `Secure` no Safari); verificar seguindo os passos a partir de um clone limpo, sem nenhum arquivo-modelo de `.env`

## 12. Verificação final

- [x] 12.1 Rodar todos os comandos do TDD 8.6 que se aplicam (`dotnet build Coordin.slnx`, `dotnet format Coordin.slnx --verify-no-changes`, `dotnet test Coordin.slnx --collect:"XPlat Code Coverage"`, `npm ci`, `npm run lint`, `npm test`, `npm run build`) e `openspec validate authentication --strict`; revisar o diff completo contra a `main` procurando segredos, senhas e e-mails reais; registrar os resultados reais na descrição do PR, separando local de CI

## Workflow follow-up

- Atualizar o `CLAUDE.md` §13 (change 2 concluída; próximo passo: change 3 `capacity-registry`).
- Abrir o PR `feat/authentication` → `main` com o template, após autorização do autor; integrar com `gh pr merge <n> --merge`.
- Após o merge, rodar `/opsx:archive authentication` e conferir as specs em `openspec/specs/`.
