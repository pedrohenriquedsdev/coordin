# registry-web Specification

## Purpose
Entrega ao gestor as telas de cadastro de capacidade: "Times e pessoas" (protótipo T07) e "Calendário", com ausências e feriados (protótipo T08), e a navegação entre elas. Os cadastros são feitos no computador (RNF-03); as datas são exibidas em pt-BR e nunca deslocadas (RNF-07).

## Requirements

### Requirement: Navegação entre as telas de cadastro
A casca SHALL exibir, para quem tem sessão, os links "Times e pessoas" (`/teams`) e "Calendário" (`/calendar`), destacando a tela atual; as duas rotas SHALL exigir sessão (RNF-04).

#### Scenario: Links com sessão
- **WHEN** o gestor está autenticado
- **THEN** a casca mostra os links "Times e pessoas" e "Calendário", e o da tela aberta aparece destacado

#### Scenario: Sem sessão
- **WHEN** alguém sem sessão abre `/teams` ou `/calendar`
- **THEN** é levado a `/login` guardando o destino, e a tela de login não mostra os links

### Requirement: Lista de times com a capacidade de cada um
A tela "Times e pessoas" SHALL listar os times com a quantidade de pessoas e a capacidade em horas por dia útil, e mostrar o time escolhido com as pessoas dele (RF-01, RF-02; T07).

#### Scenario: Lista
- **WHEN** a tela é aberta
- **THEN** cada time aparece com um texto como "4 pessoas · 22 h/dia", e o gestor escolhe um para ver o detalhe

#### Scenario: Capacidade do time
- **WHEN** um time é escolhido
- **THEN** o detalhe mostra "Capacidade do time: 22 h/dia útil", somando as pessoas

#### Scenario: Sem times
- **WHEN** ainda não há times
- **THEN** a tela explica que ainda não há times e oferece "+ Novo time"

### Requirement: Cadastrar, renomear e excluir times
O gestor SHALL cadastrar um time por "+ Novo time", renomeá-lo e excluí-lo; nome repetido e exclusão de time com pessoas SHALL exibir mensagens claras em português (RF-01; DT-18).

#### Scenario: Novo time
- **WHEN** o gestor informa um nome e salva
- **THEN** o time aparece na lista e fica escolhido

#### Scenario: Nome repetido
- **WHEN** a API responde `team-name-taken`
- **THEN** a tela mostra que já existe um time com esse nome, sem perder o que foi digitado

#### Scenario: Nome obrigatório
- **WHEN** o gestor tenta salvar sem nome
- **THEN** a tela indica o campo e não chama a API

#### Scenario: Excluir time com pessoas
- **WHEN** o time escolhido tem pessoas
- **THEN** "Excluir time" fica indisponível, com a mensagem "Indisponível: o time tem pessoas. Remova ou mova as pessoas antes."

#### Scenario: Excluir time sem pessoas
- **WHEN** o gestor confirma a exclusão de um time sem pessoas
- **THEN** o time sai da lista

### Requirement: Pessoas do time e capacidade
No detalhe do time, o gestor SHALL ver, adicionar, editar (inclusive mudar de time) e excluir as pessoas, informando nome e capacidade em horas por dia útil; capacidade zero SHALL aparecer como "não contribui" (RF-02; T07).

#### Scenario: Adicionar pessoa
- **WHEN** o gestor informa nome e horas por dia e salva
- **THEN** a pessoa aparece na tabela do time, e a capacidade do time é atualizada

#### Scenario: Capacidade obrigatória e na faixa
- **WHEN** o gestor salva sem horas, ou com horas fora de 0 a 24
- **THEN** a tela indica o campo e não chama a API

#### Scenario: Capacidade zero
- **WHEN** uma pessoa tem capacidade 0
- **THEN** a tabela mostra "não contribui" no lugar da capacidade

#### Scenario: Mudar de time
- **WHEN** o gestor muda o time de uma pessoa em "Editar"
- **THEN** ela sai do time atual e entra no escolhido, e os dois times refletem a nova capacidade

#### Scenario: Excluir pessoa
- **WHEN** o gestor confirma a exclusão
- **THEN** a pessoa e as ausências dela saem das telas

### Requirement: Ausências das pessoas
A tela "Calendário" SHALL listar as ausências de todas as pessoas (pessoa, time, tipo, início e fim), filtrar por time e permitir registrar, editar e excluir ausências, com o tipo "Férias" ou "Outra" e sem campo de motivo (RF-03, RNF-05; T08).

#### Scenario: Lista e filtro
- **WHEN** o gestor escolhe um time no filtro "Todos os times"
- **THEN** a tabela mostra só as ausências das pessoas desse time

#### Scenario: Registrar ausência
- **WHEN** o gestor escolhe a pessoa, o tipo e o período e salva
- **THEN** a ausência aparece na tabela

#### Scenario: Fim antes do início
- **WHEN** o fim é anterior ao início
- **THEN** a tela indica "O fim não pode ser anterior ao início" e não chama a API

#### Scenario: Sem campo de motivo
- **WHEN** o formulário de ausência é exibido
- **THEN** ele pede apenas pessoa, tipo, início e fim

#### Scenario: Editar e excluir
- **WHEN** o gestor edita o período ou confirma a exclusão de uma ausência
- **THEN** a tabela reflete a mudança

### Requirement: Feriados do setor
A tela "Calendário" SHALL listar os feriados por data e permitir adicionar (data e descrição opcional) e remover; uma data repetida SHALL exibir mensagem clara (RF-03; T08).

#### Scenario: Adicionar feriado
- **WHEN** o gestor informa a data e, se quiser, a descrição e salva
- **THEN** o feriado aparece na lista, ordenado por data

#### Scenario: Data repetida
- **WHEN** a API responde `holiday-date-taken`
- **THEN** a tela mostra que essa data já é feriado

#### Scenario: Remover feriado
- **WHEN** o gestor confirma a remoção
- **THEN** o feriado sai da lista

### Requirement: Datas de calendário em pt-BR, sem deslocamento
As telas SHALL exibir datas de calendário como `dd/MM/yyyy` sem aplicar fuso, e enviar à API o valor `yyyy-MM-dd` escolhido sem convertê-lo, de modo que o dia nunca mude conforme o fuso do navegador (DT-15; RNF-07).

#### Scenario: Exibição
- **WHEN** uma ausência vai de 2026-12-31 a 2027-01-02
- **THEN** as telas mostram 31/12/2026 e 02/01/2027, em qualquer fuso do navegador

#### Scenario: Envio
- **WHEN** o gestor escolhe 31/12/2026 em um campo de data
- **THEN** a API recebe `2026-12-31`

### Requirement: Carregamento, vazio e falha
As telas SHALL indicar que estão carregando, explicar quando uma lista está vazia e mostrar uma mensagem em português quando a API falhar, sem perder o que o gestor digitou (RNF-07).

#### Scenario: Lista vazia
- **WHEN** não há ausências ou feriados
- **THEN** a tela diz que ainda não há registros e mostra como adicionar

#### Scenario: Falha da API
- **WHEN** a API não responde ou responde com erro inesperado
- **THEN** a tela mostra uma mensagem em português e permite tentar de novo

### Requirement: Telas para o computador
As telas de cadastro SHALL ser utilizáveis em larguras de computador, a partir de 1024 px, sem rolagem horizontal da página (RNF-03; os cadastros não são exigidos no celular).

#### Scenario: Computador
- **WHEN** "Times e pessoas" e "Calendário" são abertas em uma janela de 1280 px
- **THEN** o conteúdo cabe na largura e não há rolagem horizontal

### Requirement: Próxima ausência de cada pessoa
A tabela de pessoas do time SHALL mostrar a próxima ausência de cada uma, a primeira que ainda não terminou em relação à data de hoje no fuso de São Paulo, ou "—" quando não houver (protótipo T07).

#### Scenario: Com ausência futura
- **WHEN** uma pessoa tem férias de 2027-01-04 a 2027-01-15 e hoje é anterior a 2027-01-15
- **THEN** a coluna mostra "Férias 04–15/01"

#### Scenario: Período que cruza meses
- **WHEN** uma pessoa tem uma ausência do tipo `Other` de 2026-12-21 a 2027-01-04
- **THEN** a coluna mostra "Outra 21/12–04/01"

#### Scenario: Ausência já terminada
- **WHEN** todas as ausências da pessoa terminaram antes de hoje
- **THEN** a coluna mostra "—"
