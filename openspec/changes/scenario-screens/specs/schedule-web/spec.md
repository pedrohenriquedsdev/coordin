# Spec Delta

## ADDED Requirements

### Requirement: Aviso do modo personalizado e volta à prioridade
Quando `GET /api/schedule` devolver `executionOrderMode` igual a `Custom`, o cronograma SHALL mostrar o aviso "A ordem de execução difere da ordem de prioridade" e o botão "Voltar à ordem de prioridade" (RF-15), que pede confirmação antes de chamar `POST /api/scenarios/reset-to-priority`. A tela SHALL NOT reordenar nada sozinha (regra inviolável 2).

#### Scenario: Modo personalizado
- **WHEN** o modo de execução é `Custom`
- **THEN** o cronograma mostra o aviso e o botão "Voltar à ordem de prioridade"

#### Scenario: Modo padrão
- **WHEN** o modo de execução é `FollowPriority`
- **THEN** o cronograma não mostra o aviso nem o botão

#### Scenario: Voltar com confirmação
- **WHEN** o gestor aciona o botão e confirma
- **THEN** a tela chama `reset-to-priority`, recarrega o cronograma, que passa a seguir a prioridade, e o aviso desaparece

#### Scenario: Desistir na confirmação
- **WHEN** o gestor aciona o botão e cancela
- **THEN** nenhuma chamada é feita

#### Scenario: Falha ao voltar
- **WHEN** a chamada falha
- **THEN** a tela mostra a mensagem de falha em português, mantém o aviso e o botão e o cronograma continua como estava
