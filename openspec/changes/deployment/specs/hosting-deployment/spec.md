# Spec Delta

## Purpose

Coloca o Coordin no ar na hospedagem escolhida (DT-09): a API escuta na porta que a hospedagem define, lê a configuração de variáveis de ambiente, responde ao health check, aplica as migrations ao iniciar e é implantada a partir de um blueprint versionado, sem segredos no repositório (RNF-03; RNF-05; DT-30).

## ADDED Requirements

### Requirement: Porta de escuta do ambiente
A API SHALL escutar em todas as interfaces (`0.0.0.0`) na porta indicada pela variável `PORT`. Sem a variável, SHALL usar a porta `8080`. Um valor inválido de `PORT` SHALL ser recusado na inicialização (DT-09; RNF-03).

#### Scenario: Porta definida pela hospedagem
- **WHEN** a variável `PORT` vale `10000`
- **THEN** a API escuta em `http://+:10000`

#### Scenario: Sem a variável
- **WHEN** a variável `PORT` não existe
- **THEN** a API escuta em `http://+:8080`

#### Scenario: Valor inválido
- **WHEN** `PORT` não é um número entre 1 e 65535
- **THEN** a aplicação não inicia e informa o erro na configuração

### Requirement: Configuração só por variáveis de ambiente
A imagem e o repositório SHALL NOT conter a string de conexão, a chave de assinatura nem a senha do gestor. Esses valores SHALL vir de variáveis de ambiente na hospedagem (DT-30; RNF-05).

#### Scenario: Imagem sem segredos
- **WHEN** a imagem é inspecionada
- **THEN** não há `ConnectionStrings__Default`, `Jwt__SigningKey` nem `Manager__Password` gravados nela

#### Scenario: Blueprint sem segredos
- **WHEN** o arquivo `render.yaml` é lido
- **THEN** os segredos aparecem como preenchidos no painel (`sync: false` ou valor gerado pela hospedagem), e nunca com um valor literal

### Requirement: Health check da hospedagem
`GET /api/health` SHALL responder `200` quando o banco aceita conexões, e a hospedagem SHALL usar esse caminho como health check do serviço (RNF-06; DT-09).

#### Scenario: Serviço saudável
- **WHEN** a aplicação e o banco estão no ar
- **THEN** `GET /api/health` responde `200` com `{"status":"Healthy"}`

#### Scenario: Banco fora
- **WHEN** o banco não aceita conexões
- **THEN** `GET /api/health` responde `503` sem detalhes internos

### Requirement: Migrations ao iniciar
A aplicação SHALL aplicar as migrations pendentes antes de aceitar requisições, para que um deploy novo no PostgreSQL gerenciado chegue ao esquema esperado sem passo manual (DT-07).

#### Scenario: Banco vazio no primeiro deploy
- **WHEN** a aplicação inicia com um banco sem tabelas
- **THEN** todas as migrations são aplicadas antes da primeira requisição

### Requirement: Acesso pela rede com HTTPS
Na hospedagem, o TLS SHALL ser terminado pela plataforma; a aplicação SHALL reconhecer o esquema original pelo cabeçalho `X-Forwarded-Proto`, redirecionar HTTP para HTTPS e enviar HSTS em produção (DT-30).

#### Scenario: Requisição HTTP
- **WHEN** a requisição chega com `X-Forwarded-Proto: http`
- **THEN** a resposta é um redirecionamento para `https`

#### Scenario: Cabeçalhos de segurança
- **WHEN** a página inicial é servida em produção
- **THEN** a resposta traz `Content-Security-Policy`, `Strict-Transport-Security` e `X-Content-Type-Options: nosniff`

### Requirement: Blueprint reproduzível
O repositório SHALL conter `render.yaml` com um web service Docker, o caminho de health check, a região, e um banco PostgreSQL 18 cuja string de conexão é ligada ao serviço, e SHALL conter `docs/deployment.md` com o roteiro para criar os serviços a partir dele (DT-09).

#### Scenario: Criar a partir do blueprint
- **WHEN** o autor cria um Blueprint no Render apontando para o repositório
- **THEN** o web service e o banco são criados, e o autor preenche no painel os segredos marcados

### Requirement: Roteiro de verificação em produção
O roteiro SHALL terminar com a verificação ponta a ponta: login com o gestor, cadastro de um time, uma demanda e a leitura do cronograma e do relatório, e a conferência dos cabeçalhos HTTPS (RNF-03).

#### Scenario: Verificação concluída
- **WHEN** o roteiro é seguido do início ao fim
- **THEN** o autor confirma cada passo com o resultado esperado descrito no roteiro
