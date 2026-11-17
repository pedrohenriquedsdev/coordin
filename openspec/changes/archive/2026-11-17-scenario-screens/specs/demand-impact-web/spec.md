# Spec Delta

## Purpose

Deixa o gestor ver, no próprio formulário de nova demanda, quais demandas mudam de data se ela entrar numa certa posição, antes de decidir criá-la (RF-16), sem gravar nada na simulação.

## ADDED Requirements

### Requirement: Posição opcional na criação
O formulário de **nova** demanda (`/demands/new`) SHALL ter o campo opcional "Posição", um inteiro de 1 a n + 1, em que n é o número de demandas existentes, com o texto de ajuda de que vazio significa "no fim da fila". O campo SHALL NOT aparecer na edição. Quando preenchido, a posição SHALL ser enviada como `position` na criação e na simulação; vazio, o campo SHALL NOT ser enviado.

#### Scenario: Criar na posição
- **WHEN** o gestor preenche a posição 1 e salva
- **THEN** a demanda é criada com `position: 1`

#### Scenario: Posição vazia
- **WHEN** o gestor deixa a posição vazia e salva
- **THEN** o corpo enviado não contém `position`, e a demanda entra no fim

#### Scenario: Posição inválida
- **WHEN** a posição digitada não é um inteiro entre 1 e n + 1
- **THEN** a tela mostra o limite válido em português e não envia nada

#### Scenario: Edição
- **WHEN** o gestor edita uma demanda existente
- **THEN** o campo "Posição" e o botão "Simular impacto" não aparecem

### Requirement: Simular o impacto antes de criar
O formulário de nova demanda SHALL ter o botão "Simular impacto" (RF-16), que valida o formulário como o "Salvar", envia o mesmo corpo para `POST /api/demands/simulate` e mostra o resultado **sem criar nada**: a previsão da própria demanda, a posição usada, o prazo do setor e as demandas **existentes** que mudam de data, com a diferença em dias em palavras.

#### Scenario: Demandas deslocadas
- **WHEN** o gestor simula a posição 1 e a API devolve três demandas deslocadas
- **THEN** a tela lista as três com a data atual, a nova data e a diferença em palavras, e mostra o prazo do setor

#### Scenario: Nenhuma deslocada
- **WHEN** a API devolve a lista de mudanças vazia
- **THEN** a tela informa que nenhuma demanda existente muda de data

#### Scenario: Nova demanda não previsível
- **WHEN** a API devolve `isPredictable` falso
- **THEN** a tela mostra o motivo em português (o mesmo texto do cronograma) e não mostra datas inventadas

#### Scenario: A simulação não grava
- **WHEN** o gestor aciona "Simular impacto"
- **THEN** nenhuma chamada de criação é feita e o cronograma não muda

#### Scenario: Formulário inválido
- **WHEN** o título está vazio ou um esforço é inválido
- **THEN** a tela mostra a mesma mensagem do "Salvar" e não chama a API

#### Scenario: Erro da API
- **WHEN** a API recusa a simulação
- **THEN** a tela mostra a mensagem do problema em português e o formulário continua preenchido

### Requirement: Confirmar ou descartar a simulação
Depois de uma simulação, "Salvar demanda" SHALL criar a demanda com o **mesmo corpo** simulado (mesma posição), e descartar SHALL ser não salvar: sair da tela ou acionar "Descartar simulação", que apaga só o resultado da tela e mantém o formulário. Se o gestor **alterar qualquer campo** depois de simular, o resultado SHALL ser marcado como desatualizado ("Os dados mudaram desde a simulação; simule de novo") e SHALL NOT ser apresentado como o impacto do que será salvo.

#### Scenario: Confirmar
- **WHEN** o gestor simulou e aciona "Salvar demanda"
- **THEN** a demanda é criada com o mesmo corpo e a mesma posição da simulação

#### Scenario: Descartar
- **WHEN** o gestor aciona "Descartar simulação"
- **THEN** o resultado some, o formulário continua preenchido e nada é gravado

#### Scenario: Dados alterados depois de simular
- **WHEN** o gestor muda um campo depois de simular
- **THEN** o resultado passa a mostrar o aviso de desatualizado até uma nova simulação
