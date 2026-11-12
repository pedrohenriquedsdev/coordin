# Spec Delta — api-conventions

## Purpose

Define as convenções comuns a todos os endpoints da API do Coordin: prefixo, formato do JSON, formato único de erro e onde a documentação da API fica disponível. Toda change seguinte herda essas regras (TDD 5.1; DT-06, DT-16, DT-18, DT-19).

## ADDED Requirements

### Requirement: Endpoints sob o prefixo /api
A API SHALL expor todos os seus endpoints sob o caminho `/api`, sem número de versão na URL (DT-16).

#### Scenario: Endpoint de saúde sob /api
- **WHEN** o cliente faz `GET /api/health`
- **THEN** a API responde pelo endpoint de saúde, e não pela SPA

#### Scenario: Rota de API inexistente
- **WHEN** o cliente faz `GET /api/rota-inexistente`
- **THEN** a resposta tem status `404` e corpo em Problem Details, e não o `index.html` da SPA

### Requirement: JSON em camelCase
A API SHALL serializar os corpos JSON com nomes de propriedades em `camelCase` e datas de planejamento no formato `yyyy-MM-dd` (TDD 5.1, DT-15).

#### Scenario: Propriedades em camelCase
- **WHEN** um endpoint devolve um objeto cuja propriedade no código é `DatabaseStatus`
- **THEN** o JSON contém a propriedade `databaseStatus`

### Requirement: Erros em Problem Details
Toda resposta de erro (status 4xx e 5xx) da API SHALL usar o formato Problem Details (RFC 9457), com `Content-Type: application/problem+json` e os campos `type`, `title`, `status` e `detail`, com `detail` em português (DT-06, DT-18; RNF-07).

#### Scenario: Recurso não encontrado
- **WHEN** o cliente chama uma rota de API inexistente
- **THEN** a resposta é `404` com `Content-Type: application/problem+json` e os campos `title`, `status` e `detail` preenchidos

#### Scenario: Erro inesperado no servidor
- **WHEN** um endpoint lança uma exceção não tratada
- **THEN** a resposta é `500` em Problem Details, com `detail` em português e sem pilha de chamadas, nome de classe ou mensagem interna da exceção

### Requirement: Código estável nos conflitos de regra de negócio
Respostas `409` de conflito de regra de negócio SHALL trazer a extensão `code`, um identificador estável em kebab-case que o front usa para reagir ao erro (DT-18).

#### Scenario: Conflito com código
- **WHEN** um endpoint sinaliza um conflito de regra de negócio com o código `example-conflict`
- **THEN** a resposta é `409` em Problem Details com `code` igual a `example-conflict` e `detail` em português

### Requirement: Erros de validação por campo
Respostas `400` de validação de entrada SHALL trazer a extensão `errors`, com as mensagens em português agrupadas pelo nome do campo em `camelCase` (DT-18).

#### Scenario: Campo inválido
- **WHEN** um endpoint rejeita uma entrada em que o campo `name` está vazio
- **THEN** a resposta é `400` em Problem Details com `errors.name` contendo ao menos uma mensagem em português

### Requirement: Documentação da API só em desenvolvimento
O documento OpenAPI (`/openapi/v1.json`) e a documentação interativa (`/swagger`) SHALL estar disponíveis apenas no ambiente de desenvolvimento (DT-19).

#### Scenario: Documentação em desenvolvimento
- **WHEN** a aplicação roda no ambiente `Development` e o cliente faz `GET /openapi/v1.json`
- **THEN** a resposta é `200` com um documento OpenAPI que inclui o endpoint `/api/health`

#### Scenario: Documentação bloqueada em produção
- **WHEN** a aplicação roda no ambiente `Production` e o cliente faz `GET /openapi/v1.json` ou `GET /swagger`
- **THEN** nenhum documento da API é devolvido
