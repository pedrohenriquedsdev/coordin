# demands-web Specification

## Purpose
Permite ao gestor cadastrar e manter as demandas pelo navegador: lista com a ordem de prioridade, formulário com times, estimativas e pessoas, exclusão e registro da conclusão real (RF-04 a RF-07, RF-12; T03, T04, T06).

## Requirements

### Requirement: Navegação de planejamento
Para quem tem sessão, a casca SHALL exibir os links "Cronograma" (`/`), "Demandas" (`/demands`), "Times e pessoas" (`/teams`), "Calendário" (`/calendar`) e "Configurações" (`/settings`), destacando a tela atual; todas as rotas novas SHALL exigir sessão (RNF-04).

#### Scenario: Links com sessão
- **WHEN** o gestor está autenticado
- **THEN** a casca mostra os cinco links, e o da tela aberta aparece destacado

#### Scenario: Sem sessão
- **WHEN** alguém sem sessão abre `/demands`, `/demands/new` ou `/settings`
- **THEN** é levado a `/login` guardando o destino, e a tela de login não mostra os links

### Requirement: Lista de demandas ativas na ordem de prioridade
`/demands` SHALL listar as demandas não concluídas na ordem de prioridade, com o número da prioridade, o título, os times com estimativa, o início e a situação (previsível ou o motivo), e SHALL oferecer "+ Nova demanda" (RF-04; T03).

#### Scenario: Lista
- **WHEN** a tela é aberta com três demandas
- **THEN** elas aparecem na ordem de prioridade, numeradas, cada uma com suas ações

#### Scenario: Sem demandas
- **WHEN** ainda não há demandas
- **THEN** a tela explica que não há demandas e oferece "+ Nova demanda"

#### Scenario: Carregando e falha
- **WHEN** os dados ainda não chegaram, ou a API falha
- **THEN** a tela mostra "Carregando…" ou a falha com "Tentar de novo"

### Requirement: Aba de concluídas somente de leitura
A tela SHALL ter as abas "Ativas" e "Concluídas". Na aba "Concluídas" as demandas SHALL aparecer só para leitura, com a data de conclusão e os desvios, sem editar, excluir, reordenar ou concluir (RF-12; TDD 4.4).

#### Scenario: Concluídas
- **WHEN** o gestor abre a aba "Concluídas"
- **THEN** vê as demandas concluídas com a data real e os desvios, sem botões de edição ou exclusão

#### Scenario: Ativas não mostram concluídas
- **WHEN** há demandas ativas e concluídas
- **THEN** a aba "Ativas" lista só as ativas

### Requirement: Reordenar a prioridade com Subir e Descer
Cada demanda ativa SHALL ter os botões "Subir" e "Descer", que trocam a posição com a ativa vizinha e salvam a lista **completa** dos ids em `PUT /api/demands/priority-order`. A primeira não tem "Subir" ativo e a última não tem "Descer" ativo. O sistema NUNCA SHALL reordenar sozinho (regras invioláveis 2 e 3; DT-13).

#### Scenario: Subir
- **WHEN** o gestor clica em "Subir" na segunda demanda
- **THEN** a API recebe a lista com a segunda e a primeira trocadas e a tela mostra a nova ordem

#### Scenario: Extremos
- **WHEN** a demanda é a primeira ou a última
- **THEN** o botão "Subir" (primeira) ou "Descer" (última) fica desabilitado

#### Scenario: Concluídas no meio da lista
- **WHEN** há demandas concluídas com prioridade entre duas ativas e o gestor sobe uma ativa
- **THEN** a lista enviada à API troca só as duas ativas e mantém as concluídas nas suas posições

#### Scenario: Erro ao reordenar
- **WHEN** a API recusa a reordenação
- **THEN** a tela mostra a mensagem em português e mantém a ordem anterior

### Requirement: Excluir uma demanda com confirmação
Cada demanda ativa SHALL ter "Excluir", que pede confirmação antes de chamar a API; demandas concluídas não têm esse botão (TDD 4.4; DT-18).

#### Scenario: Confirmar
- **WHEN** o gestor clica em "Excluir" e confirma
- **THEN** a demanda sai da lista e a lista é recarregada

#### Scenario: Cancelar
- **WHEN** o gestor clica em "Excluir" e cancela
- **THEN** nada é chamado e a demanda continua na lista

#### Scenario: Recusa da API
- **WHEN** a API responde `409`
- **THEN** a tela mostra a mensagem em português

### Requirement: Formulário de demanda
`/demands/new` e `/demands/:id/edit` SHALL oferecer o título (obrigatório, até 200 caracteres), a data de início, o fim informado, os times envolvidos, o esforço estimado em horas de cada time e as pessoas alocadas, e salvar o agregado inteiro (RF-04 a RF-07; DT-17).

#### Scenario: Cadastrar com tudo
- **WHEN** o gestor preenche título, datas, dois times com horas e uma pessoa de cada time e salva
- **THEN** a API recebe o agregado, o gestor volta à lista e a demanda aparece no fim da ordem de prioridade

#### Scenario: Salvar sem estimativa
- **WHEN** o gestor escolhe um time e deixa o esforço em branco
- **THEN** a demanda é salva com a estimativa nula e passa a aparecer como não previsível (regra inviolável 5)

#### Scenario: Título obrigatório
- **WHEN** o gestor tenta salvar sem título
- **THEN** a tela indica o campo e não chama a API

#### Scenario: Esforço inválido
- **WHEN** o esforço é zero, negativo ou tem mais de duas casas decimais
- **THEN** a tela mostra "O esforço deve ser maior que zero, com no máximo 2 casas decimais." e não chama a API

#### Scenario: Erro da API
- **WHEN** a API responde `400` ou `409`
- **THEN** a tela mostra a mensagem em português e mantém o que foi digitado

### Requirement: Pessoas só dos times escolhidos
O formulário SHALL oferecer para alocação apenas as pessoas dos times escolhidos, e SHALL remover da alocação as pessoas de um time que seja desmarcado (RF-05; DT-12).

#### Scenario: Escolher um time
- **WHEN** o gestor marca o time "Web"
- **THEN** as pessoas do time "Web" aparecem para alocação e as dos outros times não

#### Scenario: Desmarcar um time
- **WHEN** o gestor desmarca um time que tinha pessoas alocadas
- **THEN** essas pessoas deixam de estar alocadas e saem da lista

### Requirement: Editar uma demanda
`/demands/:id/edit` SHALL carregar os valores atuais e salvar com `PUT /api/demands/{id}`. Uma demanda concluída não SHALL ser editável (RF-05, RF-06, RF-07; DT-18).

#### Scenario: Valores atuais
- **WHEN** o gestor abre a edição de uma demanda
- **THEN** o formulário vem preenchido com título, datas, times, horas e pessoas

#### Scenario: Registrar a estimativa que faltava
- **WHEN** o gestor preenche a estimativa de um time e salva
- **THEN** a API recebe a alteração e a lista mostra a demanda como previsível

#### Scenario: Demanda concluída
- **WHEN** alguém abre a edição de uma demanda concluída
- **THEN** a tela informa que ela não pode mais ser alterada, sem oferecer o formulário

#### Scenario: Demanda inexistente
- **WHEN** a demanda não existe
- **THEN** a tela informa que não encontrou a demanda e oferece voltar à lista

### Requirement: Registrar a conclusão
Cada demanda ativa SHALL ter "Concluir", que pede a data real (com o dia de hoje, em São Paulo, como valor inicial) e chama `PUT /api/demands/{id}/completion`. Uma data anterior ao início SHALL ser recusada (RF-12; T06).

#### Scenario: Concluir
- **WHEN** o gestor informa a data e confirma
- **THEN** a demanda passa para a aba "Concluídas" com os desvios

#### Scenario: Antes do início
- **WHEN** a data é anterior ao início da demanda
- **THEN** a tela mostra "A data de conclusão não pode ser anterior ao início da demanda." e não chama a API

#### Scenario: Cancelar
- **WHEN** o gestor cancela
- **THEN** nada é chamado e a demanda continua ativa

#### Scenario: Recusa da API
- **WHEN** a API recusa a conclusão
- **THEN** a tela mostra a mensagem em português e mantém o que foi digitado

#### Scenario: Data de calendário sem fuso
- **WHEN** o gestor conclui com "31/12/2026"
- **THEN** a API recebe `2026-12-31`, em qualquer fuso do navegador

### Requirement: Telas para o computador
As telas de cadastro de demandas SHALL ser feitas para o computador, utilizáveis a partir de 1024 px; o celular é para consulta do cronograma (RNF-03).

#### Scenario: Largura do computador
- **WHEN** a tela tem 1280 px de largura
- **THEN** a lista e o formulário cabem sem rolagem horizontal da página

### Requirement: Escolher os pré-requisitos no formulário
O formulário de demanda SHALL oferecer "Depende de", uma lista com as demais demandas, ativas e concluídas (as concluídas marcadas como tal), para o gestor marcar os pré-requisitos, e SHALL enviá-los em `prerequisiteIds` junto com o agregado. A demanda em edição NÃO SHALL aparecer na própria lista (RF-18; DT-17, DT-23).

#### Scenario: Marcar pré-requisitos
- **WHEN** o gestor marca duas demandas em "Depende de" e salva
- **THEN** a API recebe `prerequisiteIds` com os ids das duas e o gestor volta à lista

#### Scenario: Editar mostra o que já estava marcado
- **WHEN** o gestor abre a edição de uma demanda que depende de A
- **THEN** A aparece marcada em "Depende de" e a própria demanda não aparece na lista

#### Scenario: Sem pré-requisitos
- **WHEN** o gestor não marca nenhuma demanda
- **THEN** a demanda é salva sem pré-requisitos e a tela não pede nada além do que já era obrigatório

#### Scenario: Nenhuma outra demanda
- **WHEN** não há outra demanda cadastrada
- **THEN** a seção mostra que não há demandas para escolher e não bloqueia o salvamento

### Requirement: Ciclo de dependências explicado em palavras
Quando a API responde `409` com `dependency-cycle` ao salvar, a tela SHALL mostrar a mensagem em português com os títulos do ciclo, manter o que foi digitado e as marcações, e NÃO SHALL sair do formulário (RF-18; DT-18).

#### Scenario: Salvar com ciclo
- **WHEN** o gestor faz A depender de B, e B já depende de A, e salva
- **THEN** a tela mostra a mensagem do ciclo, como "A → B → A", e os campos continuam preenchidos

#### Scenario: Corrigir e salvar de novo
- **WHEN** o gestor desmarca o pré-requisito que fechava o ciclo e salva
- **THEN** a demanda é salva e o gestor volta à lista
