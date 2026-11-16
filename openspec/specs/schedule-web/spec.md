# schedule-web Specification

## Purpose
Mostra ao gestor o cronograma consolidado das demandas, na ordem de execução, com as datas previstas, a margem separada, os alertas e o desvio das concluídas, legível no computador e no celular (RF-08, RF-09, RF-10, RF-11, RF-12; RNF-03; T02, M01).

## Requirements

### Requirement: O cronograma é a tela inicial
A rota `/` SHALL exibir o cronograma, protegida pela sessão, com o título "Cronograma" e os dados de `GET /api/schedule` (RF-11; RNF-04).

#### Scenario: Entrar no sistema
- **WHEN** o gestor entra e abre `/`
- **THEN** vê o título "Cronograma" e as demandas na ordem de execução

#### Scenario: Sem sessão
- **WHEN** alguém sem sessão abre `/`
- **THEN** é levado a `/login` e nenhum dado do cronograma é exibido

#### Scenario: Sem demandas
- **WHEN** ainda não há demandas
- **THEN** a tela explica que não há demandas e oferece o link "Cadastrar demanda" para `/demands/new`

#### Scenario: Carregando e falha
- **WHEN** os dados ainda não chegaram, ou a API falha
- **THEN** a tela mostra "Carregando…" ou uma mensagem de falha com o botão "Tentar de novo", que pede os dados outra vez

### Requirement: Dados de cada demanda
Cada demanda SHALL mostrar a prioridade e a ordem de execução, o título, os times com a estimativa de cada um, o esforço total em horas, a data de início, a previsão e a data de fim informada, sempre em `dd/MM/yyyy` (RF-11; RNF-07).

#### Scenario: Demanda previsível
- **WHEN** a demanda tem previsão
- **THEN** a linha mostra os times com horas (por exemplo "Web 80 h"), o esforço total, o início e as datas previstas

#### Scenario: Time sem estimativa
- **WHEN** um time da demanda ainda não estimou
- **THEN** a linha mostra esse time com "sem estimativa" e o esforço total soma só o que já foi estimado

### Requirement: A margem aparece separada
Para a demanda previsível, o cronograma SHALL mostrar o fim **sem margem** e o fim **com margem** como duas datas rotuladas, de modo que o gestor veja quanto da previsão vem da margem, e a data com margem nunca é anterior à data sem margem (RF-09; regra inviolável 1).

#### Scenario: Duas datas rotuladas
- **WHEN** o fim sem margem é 08/12/2026 e o fim com margem é 10/12/2026
- **THEN** a tela mostra "Sem margem: 08/12/2026" e "Com margem: 10/12/2026"

### Requirement: Demanda não previsível mostra o motivo
A demanda não previsível SHALL aparecer sem datas previstas e com o motivo em português (RF-10).

#### Scenario: Os três motivos
- **WHEN** o motivo é `MissingEstimate`, `MissingStartDate` ou `NoCapacity`
- **THEN** a tela mostra, respectivamente, "Falta a estimativa de algum time", "Sem data de início" e "Sem capacidade: falta pessoa alocada ou a capacidade é zero"

#### Scenario: Sem datas inventadas
- **WHEN** a demanda não é previsível
- **THEN** os campos de previsão mostram "—" e nenhuma data é exibida

### Requirement: Alertas
O cronograma SHALL sinalizar com texto (e não só com cor) a demanda cuja previsão com margem passa do fim informado, e a demanda cuja ordem de execução difere da prioridade (RF-11, RF-15; regras 2 e 3).

#### Scenario: Passa do prazo informado
- **WHEN** a API marca `exceedsInformedEndDate`
- **THEN** a demanda mostra o alerta "Passa do prazo informado"

#### Scenario: Ordem diferente da prioridade
- **WHEN** a API marca `isOutOfPriorityOrder`
- **THEN** a demanda mostra o aviso "Fora da ordem de prioridade"

#### Scenario: Sem alertas
- **WHEN** nenhuma das duas condições vale
- **THEN** nenhum alerta aparece na demanda

### Requirement: Demandas concluídas mostram o desvio
A demanda concluída SHALL mostrar a data real de conclusão e os dois desvios em dias corridos, contra a linha de base e contra a previsão vigente na conclusão, com o sinal e a palavra: "2 dias de atraso", "2 dias antes" ou "no prazo" (RF-12; DT-27).

#### Scenario: Concluída com atraso
- **WHEN** os desvios são 6 e 2
- **THEN** a tela mostra "6 dias de atraso" contra a primeira previsão e "2 dias de atraso" contra a previsão vigente

#### Scenario: Concluída antes
- **WHEN** o desvio é -2
- **THEN** a tela mostra "2 dias antes"

#### Scenario: No prazo e singular
- **WHEN** o desvio é 0 ou 1
- **THEN** a tela mostra "no prazo" ou "1 dia de atraso"

#### Scenario: Concluída sem previsão
- **WHEN** a demanda foi concluída sem nunca ter sido previsível
- **THEN** a tela mostra "Sem previsão para comparar" no lugar dos desvios

### Requirement: Datas de calendário sem deslocamento
Todas as datas do cronograma SHALL ser tratadas como datas de calendário, sem fuso, e SHALL aparecer iguais em qualquer fuso do navegador (RNF-07).

#### Scenario: Mesmo dia em qualquer fuso
- **WHEN** o cronograma mostra a data "2026-12-31" com o navegador em `America/Sao_Paulo`, `Asia/Tokyo` e `America/Los_Angeles`
- **THEN** a tela mostra 31/12/2026 nos três casos

### Requirement: Computador em tabela, celular em cartões
No computador o cronograma SHALL aparecer em uma tabela legível quando projetada; em telas estreitas, a partir de 360 px, SHALL aparecer em cartões, uma demanda por cartão, sem rolagem horizontal da página (RNF-03; M01).

#### Scenario: Computador
- **WHEN** a tela tem 1280 px de largura
- **THEN** o cronograma aparece como tabela, com as colunas de prioridade, demanda, times, esforço, início, previsão, prazo e situação

#### Scenario: Celular
- **WHEN** a tela tem 360 px de largura
- **THEN** cada demanda aparece em um cartão com os mesmos dados, e a página não tem rolagem horizontal

#### Scenario: Estrutura acessível
- **WHEN** o cronograma é lido por tecnologia assistiva
- **THEN** a tabela tem cabeçalhos de coluna e os cartões formam uma lista com título por demanda
