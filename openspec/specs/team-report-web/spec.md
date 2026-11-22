# team-report-web Specification

## Purpose
Mostra ao gestor, por semana e por time, onde há capacidade livre e onde há trabalho esperando sem capacidade, em tabela e em gráfico, no computador e no celular (RF-17; RNF-03; T09, M03; DT-36).

## Requirements

### Requirement: Navegação e acesso
O menu da casca SHALL oferecer, para quem tem sessão, o link "Relatório por time" (grupo "Análise") (`/reports/teams`), destacando-o quando a tela está aberta; a rota SHALL exigir sessão (RNF-04).

#### Scenario: Link com sessão
- **WHEN** o gestor está autenticado
- **THEN** o menu oferece o link "Relatório por time" e o destaca na tela aberta

#### Scenario: Sem sessão
- **WHEN** alguém sem sessão abre `/reports/teams`
- **THEN** é levado a `/login` guardando o destino

### Requirement: Escolha do período
A tela SHALL oferecer as datas de início e de fim do período e SHALL abrir, por padrão, na segunda-feira da semana de hoje (em São Paulo) até oito semanas à frente. Datas vazias, fim anterior ao início ou mais de 52 semanas SHALL ser recusados na tela, sem chamar a API (RF-17; DT-24).

#### Scenario: Período padrão
- **WHEN** a tela é aberta em uma quarta-feira
- **THEN** o início é a segunda-feira daquela semana e o fim é o domingo da oitava semana

#### Scenario: Período inválido
- **WHEN** o fim é anterior ao início, ou o período passa de 52 semanas, ou uma data está vazia
- **THEN** a tela mostra a mensagem em português e não chama a API

### Requirement: Resumo por time
A tela SHALL listar os times, em ordem alfabética, com a capacidade total, as horas alocadas, a ocupação do período, as horas ociosas, a quantidade de semanas sobrecarregadas e a quantidade de demandas não previsíveis, de modo que o gestor veja quais times têm capacidade livre e quais estão sobrecarregados (RF-17).

#### Scenario: Lista de times
- **WHEN** o relatório chega
- **THEN** cada time aparece com os totais do período e o gestor escolhe um para ver o detalhe

#### Scenario: Ocupação do período
- **WHEN** a capacidade total é 160 h e as horas alocadas são 120 h
- **THEN** a ocupação do período aparece como "75%"

#### Scenario: Capacidade zero
- **WHEN** o time não tem capacidade no período
- **THEN** a ocupação aparece como "—"

#### Scenario: Não previsíveis
- **WHEN** o time tem demandas não previsíveis
- **THEN** a tela informa quantas são e que elas não entram nas horas alocadas

### Requirement: Detalhe semanal do time
Ao escolher um time, a tela SHALL mostrar uma tabela com uma linha por semana: período (`dd/MM` a `dd/MM`), capacidade, horas alocadas, ocupação, horas ociosas e, quando houver sobrecarga, o aviso "Sobrecarga" com os títulos das demandas afetadas (RF-17).

#### Scenario: Semana ociosa
- **WHEN** a capacidade é 40 h e as horas alocadas são 10 h
- **THEN** a linha mostra ocupação "25%" e "30 h" ociosas

#### Scenario: Semana sobrecarregada
- **WHEN** a API marca `isOverloaded`
- **THEN** a linha mostra "Sobrecarga" e os títulos das demandas afetadas, em texto

### Requirement: Gráfico com tabela
A tela SHALL desenhar, para o time escolhido, um gráfico de barras empilhadas por semana, com as horas alocadas e a ociosidade (que somam a capacidade) e as semanas sobrecarregadas destacadas; os mesmos valores SHALL estar sempre na tabela (RF-17; DT-36).

#### Scenario: Séries do gráfico
- **WHEN** o relatório do time chega
- **THEN** o gráfico tem uma barra por semana com as horas alocadas e as horas ociosas empilhadas, e a barra de uma semana sobrecarregada tem a cor de destaque

#### Scenario: Tabela sempre presente
- **WHEN** o gráfico é exibido
- **THEN** a tabela com os mesmos valores está na tela

### Requirement: Componente de gráfico reutilizável
O `ChartComponent` SHALL receber a configuração do gráfico por entrada, criar o gráfico no `<canvas>`, atualizá-lo quando a configuração muda e destruí-lo ao sair da tela, sem conter regra de negócio (DT-36).

#### Scenario: Cria, atualiza e destrói
- **WHEN** o componente recebe uma configuração, depois uma nova, e é removido
- **THEN** o gráfico é criado, atualizado e destruído, uma vez cada

### Requirement: Datas e percentuais em pt-BR
As datas SHALL aparecer em `dd/MM/yyyy` (ou `dd/MM` nas semanas), sem fuso, e os percentuais em pt-BR (RNF-07).

#### Scenario: Mesmo dia em qualquer fuso
- **WHEN** o relatório mostra a semana de "2026-12-28" com o navegador em `Asia/Tokyo` e `America/Los_Angeles`
- **THEN** a tela mostra 28/12 nos dois casos

### Requirement: Estados da tela
A tela SHALL mostrar "Carregando…" enquanto busca, a mensagem de que não há times (com o link para cadastrá-los) quando a lista é vazia, e uma falha com "Tentar de novo" quando a API não responde (RNF-07).

#### Scenario: Sem times
- **WHEN** a API devolve nenhum time
- **THEN** a tela explica que não há times e oferece o link para `/teams`

#### Scenario: Falha
- **WHEN** a API falha
- **THEN** a tela mostra a falha e o botão "Tentar de novo", que pede o relatório outra vez

### Requirement: Celular em cartões
No celular, a partir de 360 px, o resumo e o detalhe semanal SHALL aparecer em cartões, e o gráfico SHALL caber na largura, sem rolagem horizontal da página; no computador, em tabelas (RNF-03; M03).

#### Scenario: Computador
- **WHEN** a tela tem 1280 px de largura
- **THEN** o resumo e o detalhe aparecem como tabelas

#### Scenario: Celular
- **WHEN** a tela tem 360 px de largura
- **THEN** aparecem como cartões com os mesmos dados, o gráfico cabe na largura e a página não tem rolagem horizontal
