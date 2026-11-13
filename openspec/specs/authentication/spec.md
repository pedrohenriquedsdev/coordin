# authentication Specification

## Purpose
Garante que só o gestor, identificado por e-mail e senha, acesse os dados da API. Define o login, a renovação e o encerramento da sessão, a conta única criada por configuração e a proteção de todos os endpoints por padrão (DT-28, DT-29; RNF-04, RNF-05).

## Requirements

### Requirement: Login com e-mail e senha
A API SHALL expor `POST /api/auth/login`, anônimo, que recebe `email` e `password` e, com credenciais válidas, responde `200` com um `accessToken` JWT de 15 minutos e define o cookie `refresh_token` (RNF-04; DT-28).

#### Scenario: Credenciais válidas
- **WHEN** o gestor envia o e-mail e a senha corretos
- **THEN** a resposta é `200` com `accessToken` no corpo
- **AND** o cookie `refresh_token` é definido com `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/auth` e validade de 7 dias
- **AND** o refresh token não aparece no corpo da resposta

#### Scenario: Senha incorreta
- **WHEN** o gestor envia o e-mail correto com a senha errada
- **THEN** a resposta é `401` em Problem Details com a mensagem "E-mail ou senha inválidos." em português e sem cookie

#### Scenario: E-mail inexistente
- **WHEN** alguém envia um e-mail que não pertence a nenhuma conta
- **THEN** a resposta é idêntica à de senha incorreta, sem revelar se o e-mail existe

#### Scenario: Campos ausentes
- **WHEN** o corpo não traz `email` ou `password`
- **THEN** a resposta é `400` em Problem Details com o erro no campo correspondente, em `camelCase`

### Requirement: Conteúdo e validade do token de acesso
O `accessToken` SHALL ser um JWT assinado com HMAC-SHA256, com emissor e audiência validados, claims `sub` e `email`, e validade de 15 minutos (DT-28).

#### Scenario: Token válido
- **WHEN** uma requisição a um endpoint protegido traz `Authorization: Bearer <accessToken>` dentro dos 15 minutos
- **THEN** a resposta não é `401`

#### Scenario: Token expirado
- **WHEN** a requisição traz um token com mais de 15 minutos
- **THEN** a resposta é `401`

#### Scenario: Token adulterado ou de outro emissor
- **WHEN** a requisição traz um token com assinatura inválida, emissor ou audiência diferentes dos configurados
- **THEN** a resposta é `401`

### Requirement: Bloqueio por tentativas erradas
A conta SHALL ser bloqueada por 15 minutos após 5 tentativas de login erradas consecutivas, e durante o bloqueio o login SHALL falhar mesmo com a senha correta (DT-28).

#### Scenario: Quinta tentativa errada
- **WHEN** o gestor erra a senha 5 vezes seguidas
- **THEN** a sexta tentativa, mesmo com a senha correta, responde `401` com a mesma mensagem genérica

#### Scenario: Fim do bloqueio
- **WHEN** passam 15 minutos desde o bloqueio
- **THEN** o login com a senha correta volta a responder `200`

#### Scenario: Acerto zera as tentativas
- **WHEN** o gestor erra a senha 4 vezes e depois acerta
- **THEN** o login responde `200` e a contagem de erros recomeça do zero

### Requirement: Renovação da sessão com rotação do refresh token
A API SHALL expor `POST /api/auth/refresh`, anônimo, que valida o cookie `refresh_token`, responde `200` com um novo `accessToken` e substitui o cookie por um novo refresh token; o token usado deixa de valer (DT-28).

#### Scenario: Refresh token válido
- **WHEN** o cliente chama `refresh` com o cookie válido
- **THEN** a resposta é `200` com um novo `accessToken` e um novo cookie `refresh_token` com as mesmas restrições do login

#### Scenario: Token já trocado não vale
- **WHEN** o cliente chama `refresh` de novo com o cookie anterior, já substituído
- **THEN** a resposta é `401`

#### Scenario: Sem cookie ou token desconhecido
- **WHEN** o cliente chama `refresh` sem o cookie, ou com um valor que não existe no banco
- **THEN** a resposta é `401` em Problem Details

#### Scenario: Refresh token expirado
- **WHEN** o cliente chama `refresh` com um cookie emitido há mais de 7 dias
- **THEN** a resposta é `401` e o cookie é apagado

### Requirement: Reuso de refresh token revoga a sessão inteira
Quando um refresh token já trocado for apresentado de novo, a API SHALL revogar todos os refresh tokens do usuário, tratando o reuso como suspeita de roubo (DT-28; RT-07).

#### Scenario: Reuso detectado
- **WHEN** um refresh token já substituído é reapresentado
- **THEN** a resposta é `401`
- **AND** o refresh token mais novo, emitido na troca legítima, também deixa de valer

### Requirement: Encerramento da sessão
A API SHALL expor `POST /api/auth/logout`, anônimo, que revoga o refresh token do cookie e apaga o cookie; ele SHALL funcionar mesmo com o token de acesso expirado e ser idempotente (DT-28).

#### Scenario: Logout com sessão ativa
- **WHEN** o cliente chama `logout` com o cookie válido
- **THEN** a resposta é `204`, o cookie é apagado e o refresh token deixa de valer em `refresh`

#### Scenario: Logout sem sessão
- **WHEN** o cliente chama `logout` sem cookie ou com um token desconhecido
- **THEN** a resposta é `204` e nada é alterado

### Requirement: Dados do usuário autenticado
A API SHALL expor `GET /api/auth/me`, protegido, que devolve o e-mail do gestor autenticado e nenhum outro dado da conta (DT-28; RNF-05).

#### Scenario: Com token válido
- **WHEN** o cliente chama `GET /api/auth/me` com um token de acesso válido
- **THEN** a resposta é `200` com o `email` do gestor, sem hash de senha nem outros campos internos

#### Scenario: Sem token
- **WHEN** o cliente chama `GET /api/auth/me` sem `Authorization`
- **THEN** a resposta é `401`

### Requirement: Todos os endpoints protegidos por padrão
Todo endpoint da API SHALL exigir o gestor autenticado, exceto `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout` e `GET /api/health`, de modo que endpoints futuros nasçam protegidos sem configuração extra (RNF-04; DT-28).

#### Scenario: Endpoint de negócio sem token
- **WHEN** o cliente chama um endpoint protegido sem `Authorization`
- **THEN** a resposta é `401` em Problem Details, sem nenhum dado

#### Scenario: Endpoint de negócio com token inválido
- **WHEN** o cliente chama um endpoint protegido com um token malformado
- **THEN** a resposta é `401`

#### Scenario: Saúde continua pública
- **WHEN** o cliente chama `GET /api/health` sem `Authorization`
- **THEN** a resposta não é `401`

#### Scenario: Rota inexistente continua 404
- **WHEN** um cliente sem token chama `GET /api/rota-inexistente`
- **THEN** a resposta é `404` em Problem Details, e não `401`

#### Scenario: Não existe cadastro público
- **WHEN** o cliente chama `POST /api/auth/register` ou qualquer outro caminho de cadastro
- **THEN** a resposta é `404`

### Requirement: Erros de autenticação em Problem Details
As respostas `401` e `429` SHALL seguir o formato Problem Details em português, sem expor detalhes internos, e a `401` SHALL trazer o cabeçalho `WWW-Authenticate: Bearer` (DT-18; RNF-07).

#### Scenario: Formato do 401
- **WHEN** a API responde `401`
- **THEN** o corpo é `application/problem+json` com `status`, `title` e `detail` em português e `traceId`, sem nome de classe, pilha ou valor de configuração

### Requirement: Limite de requisições em login e refresh
A API SHALL aceitar no máximo 10 requisições por minuto por IP em `login` e `refresh`, respondendo `429` com `Retry-After` quando o limite for excedido (DT-28).

#### Scenario: Limite excedido
- **WHEN** o mesmo IP faz 11 chamadas a `login` em um minuto
- **THEN** a 11ª responde `429` em Problem Details com o cabeçalho `Retry-After`

#### Scenario: Outros endpoints não sofrem o limite
- **WHEN** o mesmo IP chama `GET /api/health` mais de 10 vezes em um minuto
- **THEN** todas as respostas são `200`

### Requirement: Conta do gestor criada por configuração
Na inicialização, se não existir nenhum usuário, a API SHALL criar a conta do gestor a partir de `Manager__Email` e `Manager__Password`; se já existir, SHALL manter a conta como está, a menos que `Manager__ResetPassword=true` (DT-29; RNF-04, RNF-05).

#### Scenario: Primeira inicialização
- **WHEN** a API inicia sem usuários e com `Manager__Email` e `Manager__Password` definidos
- **THEN** a conta é criada e o gestor consegue fazer login com essas credenciais

#### Scenario: Conta já existente
- **WHEN** a API inicia com um usuário existente e `Manager__Password` diferente da senha atual, sem o sinalizador de redefinição
- **THEN** a senha da conta não muda

#### Scenario: Redefinição de senha
- **WHEN** a API inicia com `Manager__ResetPassword=true`
- **THEN** a senha passa a ser a de `Manager__Password` e todos os refresh tokens do usuário são revogados

#### Scenario: Senha curta na configuração
- **WHEN** `Manager__Password` tem menos de 12 caracteres
- **THEN** a inicialização falha com uma mensagem clara que não inclui a senha

#### Scenario: Configuração ausente
- **WHEN** a API inicia sem usuários e sem `Manager__Email` e `Manager__Password`
- **THEN** a API inicia, registra um aviso no log e nenhum login é possível

### Requirement: Segredo de assinatura obrigatório e fora do repositório
A API SHALL exigir `Jwt__SigningKey` com no mínimo 32 bytes, vinda de variável de ambiente ou de `dotnet user-secrets`, e SHALL se recusar a iniciar sem ela; nenhum arquivo versionado contém a chave, senha ou conta (DT-30; RNF-05).

#### Scenario: Chave ausente ou curta
- **WHEN** a API inicia sem `Jwt__SigningKey` ou com menos de 32 bytes
- **THEN** a inicialização falha com uma mensagem que não inclui o valor da chave

#### Scenario: Nenhum segredo versionado
- **WHEN** se inspecionam os arquivos de configuração versionados
- **THEN** nenhum contém `SigningKey`, senha ou e-mail do gestor

### Requirement: Senhas e tokens nunca em texto puro
O sistema SHALL guardar a senha apenas como hash do Identity e o refresh token apenas como hash SHA-256, e SHALL NOT escrever senhas ou tokens em respostas de erro nem em logs (RNF-04).

#### Scenario: Banco sem texto puro
- **WHEN** se inspecionam as tabelas de usuário e de refresh token após um login
- **THEN** a senha e o valor do refresh token não aparecem em texto puro em nenhuma coluna

#### Scenario: Resposta de erro sem segredos
- **WHEN** o login falha
- **THEN** o corpo da resposta e os logs da requisição não contêm a senha enviada
