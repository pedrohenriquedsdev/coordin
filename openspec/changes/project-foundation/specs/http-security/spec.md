# Spec Delta — http-security

## Purpose

Protege o tráfego HTTP do Coordin com cabeçalhos de segurança, HTTPS obrigatório em produção e origem única, como barreiras contra XSS, clickjacking e vazamento de dados pela rede (DT-01, DT-30; RNF-03, RNF-04, RNF-05).

## ADDED Requirements

### Requirement: Cabeçalhos de segurança em todas as respostas
Toda resposta da aplicação (API, SPA e arquivos estáticos) SHALL incluir `Content-Security-Policy` com `default-src 'self'`, `frame-ancestors 'none'` e sem `'unsafe-inline'` em `script-src`, além de `X-Content-Type-Options: nosniff` e `Referrer-Policy: no-referrer` (DT-30).

#### Scenario: Cabeçalhos na API
- **WHEN** o cliente faz `GET /api/health`
- **THEN** a resposta contém `Content-Security-Policy` com `default-src 'self'` e `frame-ancestors 'none'`, `X-Content-Type-Options: nosniff` e `Referrer-Policy: no-referrer`

#### Scenario: Cabeçalhos na SPA
- **WHEN** o cliente faz `GET /` e recebe o `index.html`
- **THEN** a resposta contém os mesmos cabeçalhos de segurança

#### Scenario: Cabeçalhos também em erros
- **WHEN** a resposta é um erro em Problem Details (`404` ou `500`)
- **THEN** ela contém os mesmos cabeçalhos de segurança

#### Scenario: SPA funciona sob a CSP
- **WHEN** o build de produção do front é servido com a CSP aplicada
- **THEN** a página carrega sem violações de CSP no console do navegador (sem scripts ou estilos inline bloqueados)

### Requirement: HTTPS obrigatório em produção
No ambiente de produção, a aplicação SHALL redirecionar requisições HTTP para HTTPS e enviar o cabeçalho `Strict-Transport-Security`. Atrás do proxy da hospedagem, ela SHALL reconhecer o esquema original pelos cabeçalhos encaminhados (DT-30, DT-09).

#### Scenario: HSTS em produção
- **WHEN** a aplicação roda em `Production` e recebe uma requisição HTTPS
- **THEN** a resposta contém `Strict-Transport-Security`

#### Scenario: Requisição encaminhada pelo proxy como HTTPS
- **WHEN** a aplicação roda em `Production` e recebe uma requisição HTTP com `X-Forwarded-Proto: https` vinda do proxy
- **THEN** ela trata a requisição como HTTPS e não redireciona em laço

#### Scenario: Sem HSTS em desenvolvimento
- **WHEN** a aplicação roda em `Development`
- **THEN** a resposta não contém `Strict-Transport-Security`

### Requirement: Origem única, sem CORS
A aplicação SHALL servir a API e a SPA na mesma origem e não SHALL habilitar CORS (DT-01, DT-30).

#### Scenario: Requisição de outra origem
- **WHEN** o cliente envia uma requisição com `Origin: https://outro-site.example`
- **THEN** a resposta não contém `Access-Control-Allow-Origin`

### Requirement: Nenhum segredo versionado
A configuração versionada SHALL NOT conter segredos: a string de conexão e as senhas vêm de variáveis de ambiente ou de segredos locais fora do repositório, e o repositório não traz arquivos de modelo com senhas (DT-30; RNF-05).

#### Scenario: Arquivos de configuração sem segredos
- **WHEN** se inspecionam os arquivos de configuração versionados da API
- **THEN** nenhum deles contém senha, chave ou string de conexão com credencial real

#### Scenario: Arquivo de ambiente ignorado
- **WHEN** existe um arquivo `.env` na raiz do repositório
- **THEN** o Git o ignora
