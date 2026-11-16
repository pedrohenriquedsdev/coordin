# Spec Delta

## ADDED Requirements

### Requirement: Ausências e feriados recalculam as previsões
Registrar, alterar ou remover uma ausência ou um feriado SHALL recalcular as previsões na mesma transação da escrita, empurrando as datas das demandas afetadas na proporção da capacidade perdida (RF-03; DT-03).

#### Scenario: Ausência no meio da execução
- **WHEN** a pessoa alocada a uma demanda previsível registra uma ausência de três dias úteis no meio da execução
- **THEN** a previsão guardada da demanda passa para depois, pelos dias de capacidade perdidos

#### Scenario: Remover a ausência
- **WHEN** o gestor remove essa ausência
- **THEN** a previsão volta à data anterior

#### Scenario: Feriado no meio da execução
- **WHEN** o gestor cadastra um feriado em um dia útil dentro da execução de uma demanda
- **THEN** a previsão guardada passa para depois e ninguém é contado como trabalhando no feriado

#### Scenario: Remover o feriado
- **WHEN** o gestor remove o feriado
- **THEN** a previsão volta à data anterior

#### Scenario: Escrita rejeitada
- **WHEN** o cadastro da ausência ou do feriado é rejeitado (dados inválidos ou data repetida)
- **THEN** as previsões guardadas não mudam
