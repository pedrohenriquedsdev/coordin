# settings-web Specification

## Purpose
Permite ao gestor ver e ajustar a margem de segurança aplicada às previsões, sempre sem reduzir o prazo (RF-09; DT-22).

## Requirements

### Requirement: Ver e alterar a margem
`/settings` SHALL mostrar a margem vigente de `GET /api/settings`, em percentual, e salvá-la com `PUT /api/settings` (RF-09; DT-22).

#### Scenario: Valor atual
- **WHEN** a tela é aberta
- **THEN** o campo mostra a margem vigente (por exemplo, 20)

#### Scenario: Salvar
- **WHEN** o gestor informa 30 e salva
- **THEN** a API recebe 30, a tela confirma "Margem salva." e o cronograma, ao ser aberto, mostra as previsões recalculadas

#### Scenario: Margem zero
- **WHEN** o gestor informa 0 e salva
- **THEN** a API recebe 0 e a tela confirma

### Requirement: Validação do percentual
A tela SHALL aceitar só valores de 0 a 1000 com até duas casas decimais e SHALL mostrar a mensagem do servidor quando ele recusar (RF-09; regra inviolável 1).

#### Scenario: Valor inválido
- **WHEN** o valor é negativo, maior que 1000, tem mais de duas casas ou está vazio
- **THEN** a tela mostra "A margem deve estar entre 0% e 1000%, com no máximo 2 casas decimais." e não chama a API

#### Scenario: Erro da API
- **WHEN** a API recusa o valor
- **THEN** a tela mostra a mensagem em português e mantém o que foi digitado

### Requirement: Carregamento e falha
A tela SHALL mostrar "Carregando…" enquanto busca a margem e uma mensagem de falha com "Tentar de novo" se a API falhar (RNF-07).

#### Scenario: Falha ao carregar
- **WHEN** a API falha ao devolver as configurações
- **THEN** a tela mostra a falha e o botão "Tentar de novo"
