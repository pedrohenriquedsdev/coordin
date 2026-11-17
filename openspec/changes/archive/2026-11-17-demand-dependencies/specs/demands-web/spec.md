# Spec Delta

## ADDED Requirements

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
