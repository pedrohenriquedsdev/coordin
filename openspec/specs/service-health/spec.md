# service-health Specification

## Purpose
Permite verificar, sem login, se a aplicação está no ar e consegue falar com o banco. A hospedagem usa essa verificação para saber se a aplicação está saudável (DT-09), e ela também serve como prova de ponta a ponta nos testes (RNF-06).

## Requirements

### Requirement: Verificação de saúde pública
A API SHALL expor `GET /api/health`, acessível sem autenticação, que responde `200` quando a aplicação está no ar e o banco aceita conexões.

#### Scenario: Aplicação e banco saudáveis
- **WHEN** o banco está acessível e o cliente faz `GET /api/health` sem credenciais
- **THEN** a resposta é `200` com `{"status":"Healthy"}`

#### Scenario: Banco indisponível
- **WHEN** o banco não aceita conexões e o cliente faz `GET /api/health`
- **THEN** a resposta é `503` com `{"status":"Unhealthy"}`

### Requirement: Saúde sem vazamento de detalhes
A resposta da verificação de saúde SHALL conter apenas o estado geral, sem string de conexão, nome do servidor, versão, mensagem de exceção ou tempo de cada verificação (DT-30; RNF-05).

#### Scenario: Falha sem detalhes internos
- **WHEN** o banco está indisponível e o cliente faz `GET /api/health`
- **THEN** o corpo contém apenas o campo `status` e nenhum texto da exceção ou dado da conexão

### Requirement: Esquema do banco atualizado na inicialização
Ao iniciar, a aplicação SHALL aplicar as migrations pendentes antes de aceitar requisições, para que o banco esteja sempre na versão esperada pelo código (DT-07; RNF-06).

#### Scenario: Banco vazio
- **WHEN** a aplicação inicia apontando para um banco PostgreSQL vazio
- **THEN** a tabela de histórico de migrations registra a migration inicial e `GET /api/health` responde `200`

#### Scenario: Banco já atualizado
- **WHEN** a aplicação reinicia apontando para um banco que já tem todas as migrations
- **THEN** nenhuma migration é reaplicada e a aplicação inicia normalmente
