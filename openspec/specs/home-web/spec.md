# home-web Specification

## Purpose
Dá ao gestor, ao entrar, uma visão geral do planejamento: números-chave, carga dos times, entregas previstas, calendário e atalhos para as demais áreas. É só leitura e usa os dados que o cronograma e o relatório por time já calculam (RF-11, RF-17; RNF-03).

## Requirements

### Requirement: Painel inicial protegido
A rota `/inicio` SHALL exibir o painel "Início", protegida pela sessão, e SHALL ser o destino do login quando não houver destino anterior (RNF-04).

#### Scenario: Entrar no sistema
- **WHEN** o gestor entra sem destino anterior
- **THEN** a interface abre `/inicio` e mostra o título "Início"

#### Scenario: Sem sessão
- **WHEN** alguém sem sessão abre `/inicio`
- **THEN** é levado a `/login` e nenhum dado do painel é exibido

### Requirement: Indicadores e ações rápidas
O painel SHALL mostrar os indicadores "Prazo do setor", "Demandas ativas", "Sem previsão" e "Passam do prazo informado", e os botões "Abrir cronograma" (`/`) e "Nova demanda" (`/demands/new`). Enquanto carrega, SHALL mostrar um aviso de carregamento.

#### Scenario: Indicadores
- **WHEN** o painel termina de carregar
- **THEN** os quatro indicadores aparecem com os valores do cronograma atual

#### Scenario: Ações rápidas
- **WHEN** o gestor aciona "Abrir cronograma" ou "Nova demanda"
- **THEN** a interface abre `/` ou `/demands/new`, respectivamente

### Requirement: Gráficos acompanhados de tabela
O painel SHALL mostrar a carga das próximas 8 semanas (horas alocadas e ociosas, com as semanas de sobrecarga destacadas e filtro por time ou pelo setor inteiro) e as entregas previstas por semana. Todo gráfico SHALL ter, ao lado, a opção "Ver os valores em tabela" com os mesmos valores (DT-36). Quando a carga ou o cronograma não puderem ser carregados, o painel SHALL mostrar uma mensagem em vez do gráfico, sem derrubar o resto da tela.

#### Scenario: Filtrar a carga por time
- **WHEN** o gestor escolhe um time no filtro da carga
- **THEN** o gráfico e a tabela passam a mostrar só esse time, e "Setor" volta a mostrar todos

#### Scenario: Tabela com os mesmos valores
- **WHEN** o gestor abre "Ver os valores em tabela"
- **THEN** a tabela mostra, por semana, capacidade, horas alocadas, ociosas e a situação ("Normal" ou "Sobrecarga")

#### Scenario: Falha parcial
- **WHEN** o relatório de carga falha e o cronograma carrega
- **THEN** o painel avisa que não foi possível carregar a carga dos times e continua mostrando o restante

### Requirement: Ocupação, calendário, linha do tempo e atalhos
O painel SHALL mostrar a ocupação por time com link para o relatório por time, um calendário do mês com feriados, ausências e entregas, a linha do tempo das próximas demandas em ordem de execução (com a margem em destaque no fim de cada barra) e, ao final, atalhos para as áreas do sistema.

#### Scenario: Atalhos
- **WHEN** o gestor usa um dos atalhos de "Áreas do sistema"
- **THEN** a interface abre a tela correspondente
