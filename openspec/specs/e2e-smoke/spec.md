# e2e-smoke Specification

## Purpose
Confirma, no navegador e de ponta a ponta, que as peças do produto estão ligadas: a API servindo o Angular, o login, o cadastro até a previsão e o cronograma legível no celular. A suíte é um smoke de três cenários e roda em uma pilha descartável, sem tocar nos dados do autor (RNF-03; DT-32).

## Requirements

### Requirement: Login e logout
O smoke SHALL cobrir o login do gestor com as credenciais da execução e o logout, confirmando que depois do logout nenhuma tela protegida é mostrada (RNF-04).

#### Scenario: Login com credenciais corretas
- **WHEN** o gestor informa o e-mail e a senha corretos na tela de login
- **THEN** a tela inicial (o cronograma) é mostrada e o menu aparece

#### Scenario: Logout encerra a sessão
- **WHEN** o gestor aciona "Sair" e depois tenta abrir uma tela protegida
- **THEN** ele é levado à tela de login e nenhum dado é exibido

### Requirement: Do cadastro à previsão
O smoke SHALL cobrir o caminho principal do gestor: cadastrar um time, uma pessoa do time e uma demanda com estimativa e início, e ver a data prevista da demanda no cronograma. O teste SHALL confirmar que há uma data prevista e que a demanda não aparece como sem previsão; o valor exato da data é coberto pelos testes do motor e da API.

#### Scenario: Demanda cadastrada com previsão
- **WHEN** o gestor cadastra um time, uma pessoa com capacidade e uma demanda com início, estimativa e a pessoa alocada
- **THEN** o cronograma mostra a demanda com uma data de fim prevista no formato dd/MM/aaaa

#### Scenario: Nomes únicos por execução
- **WHEN** o smoke roda mais de uma vez contra o mesmo banco
- **THEN** cada execução usa nomes próprios e não depende de dados deixados por outra

### Requirement: Cronograma no celular
O smoke SHALL abrir o cronograma em viewport de 360 px de largura e confirmar que a página não tem rolagem horizontal (RNF-03).

#### Scenario: Sem rolagem horizontal em 360 px
- **WHEN** o gestor abre o cronograma com pelo menos uma demanda em viewport de 360 px
- **THEN** a largura do conteúdo da página não excede a largura da janela

### Requirement: Pilha descartável e isolada
A execução do smoke SHALL subir um PostgreSQL novo e a API, com porta e senha aleatórias a cada vez, e SHALL derrubar tudo ao terminar, inclusive quando um teste falha ou a execução é interrompida. O smoke SHALL NOT ler, usar nem alterar o `.env`, o banco de desenvolvimento ou qualquer segredo do autor.

#### Scenario: Execução limpa
- **WHEN** o comando do smoke termina, com sucesso ou com falha
- **THEN** não resta container, processo da API nem arquivo com credenciais da execução

#### Scenario: Credenciais fora do repositório
- **WHEN** o repositório é inspecionado depois da execução
- **THEN** nenhum arquivo versionado contém a senha do banco nem a do gestor da execução

### Requirement: Um comando e pré-requisitos claros
O smoke SHALL rodar com `npx playwright test` na pasta `src/coordin-web`. Quando faltar um pré-requisito (Docker em execução, Chrome instalado ou o .NET), o comando SHALL falhar cedo, com uma mensagem que diga o que falta, em vez de um erro obscuro do navegador. O README SHALL listar os pré-requisitos e o comando.

#### Scenario: Docker parado
- **WHEN** o comando é executado com o Docker parado
- **THEN** a execução falha antes de abrir o navegador, com uma mensagem que diz que o Docker é necessário

#### Scenario: Roteiro no README
- **WHEN** o autor abre o README
- **THEN** ele encontra os pré-requisitos e o comando para rodar o smoke
