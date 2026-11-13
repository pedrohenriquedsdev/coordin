# Proposal

## Why

Hoje qualquer pessoa que alcance a aplicação consegue chamar a API e abrir a SPA. O PRD exige que o acesso dependa do login do gestor e que, sem autenticação, nenhum dado seja exibido (RNF-04). Como todas as changes seguintes (3 a 13) criam endpoints e telas de negócio, a autenticação precisa existir antes delas: cada endpoint novo já nasce protegido por padrão, em vez de receber a proteção depois.

## What Changes

Linha do roadmap (TDD 9.1): change 2 · `authentication` · branch `feat/authentication` · Must · data alvo 2026-11-14.

- **API:** login do gestor (único usuário) com e-mail e senha, `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout` e `GET /api/auth/me`; token de acesso JWT de 15 minutos; refresh token em cookie `HttpOnly` de 7 dias, rotacionado a cada uso, com revogação de todos os tokens ao detectar o reuso de um token já trocado (DT-28).
- **Proteção por padrão:** todo endpoint da API passa a exigir o gestor autenticado, exceto `login`, `refresh`, `logout` e `health`. Endpoints futuros herdam a regra sem configuração extra.
- **Conta do gestor sem cadastro público:** criada na inicialização a partir de `Manager__Email` e `Manager__Password`; `Manager__ResetPassword=true` redefine a senha e revoga os refresh tokens (DT-29).
- **Defesas de login:** senha com no mínimo 12 caracteres, bloqueio de 15 minutos após 5 tentativas erradas e limite de 10 requisições por minuto por IP em `login` e `refresh` (DT-28).
- **Segredo de assinatura:** `Jwt__SigningKey` vem de variáveis de ambiente ou de `dotnet user-secrets`, nunca de arquivo versionado (DT-30; RNF-05).
- **Banco:** a migration `AddAuthentication` cria as tabelas do ASP.NET Core Identity e a tabela `RefreshToken` (TDD 4.2).
- **Front (protótipo T01):** tela de login em pt-BR, token de acesso só em memória, interceptor HTTP que envia o token e renova a sessão quando ele expira, guarda de rotas, restauração da sessão ao recarregar a página e botão "Sair" na casca. A página inicial deixa de ser pública.
- **Contrato:** o cliente tipado do Angular é gerado de novo a partir do OpenAPI para incluir os endpoints de autenticação (DT-19).
- **Pacotes novos (precisam da autorização do autor antes de instalar):** `Microsoft.AspNetCore.Identity.EntityFrameworkCore` e `Microsoft.AspNetCore.Authentication.JwtBearer`, ambos 10.0.15 (TDD 3). Nenhum pacote npm novo.

## Capabilities

### New Capabilities

- `authentication`: comportamento da API de acesso do gestor: login, renovação e encerramento de sessão, conta única criada por configuração, proteção de todos os endpoints por padrão, bloqueio por tentativas e limite de requisições.
- `web-authentication`: comportamento do front: tela de login, sessão mantida só em memória e renovada automaticamente, guarda de rotas e saída da sessão.

### Modified Capabilities

Nenhuma. As specs vigentes continuam verdadeiras: `GET /` ainda devolve o `index.html` (só os dados exigem login), `/api/rota-inexistente` ainda responde `404` e `GET /api/health` continua anônimo.

## Impact

- **Código:** `src/Coordin.Api` (nova feature `Auth` e configuração de autenticação, autorização e limite de requisições; `CoordinDbContext` passa a incluir o Identity), `src/coordin-web` (rotas, serviço de sessão, interceptor, guarda, página de login, cliente gerado) e `tests/Coordin.Api.Tests` (a fábrica de testes passa a fornecer chave e conta de teste).
- **API:** quatro endpoints novos sob `/api/auth`; todos os demais passam a responder `401` sem token.
- **Dados:** migration `AddAuthentication` (Identity + `RefreshToken`). Nenhuma tabela de domínio.
- **Configuração:** variáveis novas `Jwt__SigningKey`, `Manager__Email`, `Manager__Password` e `Manager__ResetPassword`. O README documenta como defini-las; nenhum valor entra no repositório.
- **Requisitos e decisões atendidos:** RNF-04 (acesso protegido, senhas nunca em texto puro) e RNF-05 (credenciais fora do repositório); DT-28, DT-29 e DT-30 (segredos); contratos de DT-16, DT-18 e DT-19 reaproveitados; riscos RT-07.

## Fora desta change

- Entidades de domínio, regras de cálculo e telas funcionais (changes 3 a 6).
- Cadastro, recuperação de senha por e-mail, troca de senha pela interface e papéis de usuário: o usuário é único (PRD 8.2); a senha só muda pela variável `Manager__ResetPassword`.
- Autenticação em dois fatores e login social.
- Escolha do provedor de hospedagem e implantação (change 7); aqui só se documenta quais variáveis a hospedagem precisará definir.
- Teste end-to-end de login e logout com Playwright (change 13).
