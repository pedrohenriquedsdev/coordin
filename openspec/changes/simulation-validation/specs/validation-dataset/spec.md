# Spec Delta

## Purpose

Define o conjunto fictício da simulação, embutido na API, que serve de base para a validação do TCC sem nenhum dado real (PRD 7.3, M2; RNF-05; DT-37).

## ADDED Requirements

### Requirement: Conjunto fictício embutido e sem dado real
A API SHALL embutir o conjunto como recurso JSON versionado, com 7 times, entre 30 e 40 pessoas, 30 demandas, ausências e feriados. O conjunto SHALL NOT conter nome, e-mail, título ou data de nenhuma pessoa, equipe ou projeto reais (RNF-05).

#### Scenario: Tamanho do conjunto
- **WHEN** o recurso é lido
- **THEN** ele tem 7 times, de 30 a 40 pessoas e 30 demandas

#### Scenario: Nada de dado real
- **WHEN** o conjunto é revisado
- **THEN** os nomes são genéricos, e nenhum e-mail ou identificador de pessoa real aparece

### Requirement: Conjunto válido e previsível
O conjunto SHALL formar um planejamento válido para o motor (sem ciclo de dependência, sem pessoa fora do seu time, capacidades de 0 a 24 horas) e SHALL ser **previsível em todas as demandas**: cada demanda tem data de início, estimativa em todas as partes e ao menos uma pessoa alocada em cada time envolvido.

#### Scenario: Snapshot válido
- **WHEN** o conjunto vira um planejamento para o motor
- **THEN** o validador do motor o aceita

#### Scenario: Todas previsíveis
- **WHEN** o motor calcula as previsões do conjunto
- **THEN** nenhuma demanda fica sem previsão

#### Scenario: Sem ciclo
- **WHEN** as dependências do conjunto são verificadas
- **THEN** não há ciclo

### Requirement: Alocação inicial e prioridades definidas
O conjunto SHALL trazer a ordem de prioridade (1 a 30, sem repetição) e a alocação inicial de pessoas a cada demanda, como o processo atual as faria, sem concluir nenhuma demanda. O conjunto SHALL NOT ser ajustado depois de se conhecer o resultado da avaliação.

#### Scenario: Prioridades únicas
- **WHEN** as prioridades do conjunto são lidas
- **THEN** são os números de 1 a 30, uma vez cada

#### Scenario: Nenhuma demanda concluída
- **WHEN** o conjunto é carregado
- **THEN** nenhuma demanda está concluída e o modo de execução é o de prioridade

### Requirement: Conjunto reprodutível
O conteúdo do conjunto SHALL ser fixo: o recurso embutido é um arquivo versionado, e o mesmo arquivo SHALL produzir sempre os mesmos dados no banco, exceto os identificadores, que são gerados no carregamento (a conta do gestor não faz parte do conjunto).

#### Scenario: Dois carregamentos
- **WHEN** o conjunto é carregado duas vezes
- **THEN** os títulos, estimativas, datas e alocações são os mesmos, e o cronograma resultante tem as mesmas datas
