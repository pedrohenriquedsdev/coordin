# Spec Delta

## Purpose

Define quantas horas cada pessoa e cada time podem trabalhar em uma data, considerando dias úteis, feriados e ausências. É a base de toda previsão de entrega (RF-02, RF-03, RF-08; DT-10).

## ADDED Requirements

### Requirement: Dia útil
O motor SHALL tratar como dia útil toda data de segunda a sexta-feira que não seja feriado do setor (RF-03; DT-10).

#### Scenario: Dia de semana comum
- **WHEN** a data é uma quarta-feira sem feriado
- **THEN** ela é um dia útil

#### Scenario: Fim de semana
- **WHEN** a data é um sábado ou um domingo
- **THEN** ela não é um dia útil

#### Scenario: Feriado em dia de semana
- **WHEN** a data é uma sexta-feira cadastrada como feriado
- **THEN** ela não é um dia útil

### Requirement: Capacidade de uma pessoa em uma data
A capacidade de uma pessoa em uma data SHALL ser igual à sua capacidade diária se a data for dia útil e a pessoa não estiver em ausência nela; caso contrário, SHALL ser zero (RF-02, RF-03; DT-10).

#### Scenario: Dia útil sem ausência
- **WHEN** a pessoa tem 6 h por dia útil e a data é um dia útil sem ausência dela
- **THEN** a capacidade da pessoa na data é 6 h

#### Scenario: Ausência cobre a data
- **WHEN** a pessoa tem uma ausência de 10 a 14 do mês e a data é o dia 12, um dia útil
- **THEN** a capacidade da pessoa na data é 0 h

#### Scenario: Limites da ausência
- **WHEN** a ausência vai do dia 10 ao dia 14, inclusive
- **THEN** a pessoa tem capacidade zero nos dias 10 e 14 e capacidade normal nos dias 9 e 15 (se forem dias úteis)

#### Scenario: Ausência de outra pessoa
- **WHEN** só a pessoa B tem ausência na data
- **THEN** a capacidade da pessoa A na data não muda

#### Scenario: Capacidade zero cadastrada
- **WHEN** a pessoa tem capacidade diária zero
- **THEN** sua capacidade é zero em qualquer data

### Requirement: Capacidade de um time em uma data
A capacidade de um time em uma data SHALL ser a soma das capacidades, naquela data, das pessoas que pertencem a ele (RF-02; DT-10).

#### Scenario: Uma pessoa ausente
- **WHEN** o time tem duas pessoas de 8 h e uma delas está em ausência na data
- **THEN** a capacidade do time na data é 8 h

#### Scenario: Time sem pessoas
- **WHEN** o time não tem pessoas
- **THEN** sua capacidade é zero em qualquer data

### Requirement: Avançar em dias úteis
O motor SHALL permitir encontrar o dia útil seguinte a uma data e avançar uma data por um número de dias úteis, pulando fins de semana e feriados (RF-03, RF-09; DT-22).

#### Scenario: Pular o fim de semana
- **WHEN** se avança 1 dia útil a partir de uma sexta-feira sem feriado na segunda seguinte
- **THEN** o resultado é a segunda-feira

#### Scenario: Pular um feriado
- **WHEN** se avança 2 dias úteis a partir de uma quarta-feira e a quinta-feira é feriado
- **THEN** o resultado é a segunda-feira

#### Scenario: Avançar zero dias
- **WHEN** se avança 0 dias úteis a partir de uma data
- **THEN** o resultado é a própria data

### Requirement: Datas de calendário sem fuso
Todas as datas do motor SHALL ser datas de calendário, sem horário nem fuso, e nenhum resultado SHALL depender do relógio ou do fuso da máquina (RNF-02; DT-02).

#### Scenario: Mesmo resultado em qualquer fuso
- **WHEN** o mesmo cálculo é executado em máquinas com fusos diferentes
- **THEN** as datas resultantes são idênticas
