# web-shell Specification

## Purpose
Entrega a interface web do Coordin pela própria API, na mesma origem, em português do Brasil e utilizável do computador ao celular. É a casca sobre a qual cada change seguinte acrescenta suas telas (DT-01, DT-08; RNF-03, RNF-07).

## Requirements

### Requirement: SPA servida pela API
A aplicação SHALL servir o build do front na raiz `/`, na mesma origem da API.

#### Scenario: Página inicial
- **WHEN** o cliente faz `GET /`
- **THEN** a resposta é `200` com o `index.html` da SPA

#### Scenario: Arquivos estáticos
- **WHEN** o cliente pede um arquivo do build referenciado pelo `index.html` (JavaScript ou CSS)
- **THEN** a resposta é `200` com o conteúdo do arquivo

### Requirement: Navegação direta por URL
Qualquer caminho fora de `/api` que não corresponda a um arquivo existente SHALL devolver o `index.html`, para que recarregar a página ou abrir um link direto funcione.

#### Scenario: Recarregar uma rota da SPA
- **WHEN** o cliente faz `GET /cronograma`
- **THEN** a resposta é `200` com o `index.html` da SPA

#### Scenario: Rota de API não cai na SPA
- **WHEN** o cliente faz `GET /api/rota-inexistente`
- **THEN** a resposta é `404` em Problem Details, e não o `index.html`

### Requirement: Interface em português do Brasil
A interface SHALL ser declarada em português do Brasil e formatar números e datas no padrão pt-BR, no fuso `America/Sao_Paulo` (RNF-07).

#### Scenario: Idioma do documento
- **WHEN** o `index.html` é carregado
- **THEN** o elemento `<html>` tem `lang="pt-BR"` e o título da página é "Coordin"

#### Scenario: Formatação pt-BR
- **WHEN** a interface formata o número `1234.5` e a data `2026-11-28`
- **THEN** exibe `1.234,5` e `28/11/2026`

### Requirement: Casca responsiva
A casca da aplicação SHALL exibir o nome "Coordin" e uma área de conteúdo utilizável de 360 px de largura até telas de computador, sem rolagem horizontal (RNF-03).

#### Scenario: Celular
- **WHEN** a página inicial é aberta em uma janela de 360 px de largura
- **THEN** o nome "Coordin" aparece e não há rolagem horizontal

#### Scenario: Computador
- **WHEN** a página inicial é aberta em uma janela de 1280 px de largura
- **THEN** o nome "Coordin" aparece e a área de conteúdo ocupa a largura disponível

### Requirement: Menu de navegação agrupado
Para quem tem sessão, a casca SHALL exibir uma barra no topo com a marca "Coordin" (link para `/inicio`), o link "Início" e três grupos: "Planejamento" (Cronograma, Demandas, Cenários), "Análise" (Relatório por time, Precisão e, só quando a área estiver disponível, Validação) e "Cadastros" (Times e pessoas, Calendário, Configurações). A tela atual, e o grupo que a contém, SHALL aparecer destacada. Sem sessão, a casca SHALL NOT exibir o menu (RNF-04).

#### Scenario: Menu no computador
- **WHEN** o gestor autenticado abre qualquer tela em uma janela de 1280 px
- **THEN** a barra mostra "Início" e os grupos "Planejamento", "Análise" e "Cadastros", e cada grupo abre a lista de suas telas

#### Scenario: Sem sessão
- **WHEN** a tela de login é exibida
- **THEN** não há menu de navegação

### Requirement: Menu recolhível no celular e controles da barra
No celular, os grupos do menu SHALL ficar em um painel recolhível aberto por um botão "Abrir menu", sem rolagem horizontal. A barra SHALL ter ainda um botão que alterna o tema claro e escuro e o botão "Sair" (RNF-03).

#### Scenario: Menu no celular
- **WHEN** o gestor autenticado abre qualquer tela em uma janela de 360 px
- **THEN** os grupos ficam recolhidos e abrem pelo botão "Abrir menu", sem rolagem horizontal
