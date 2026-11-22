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

### Requirement: Apoios à digitação da margem
A tela SHALL oferecer valores rápidos (0, 10, 20 e 30), um controle deslizante de 0 a 100 e um exemplo ao vivo que mostra, em dias, o efeito da margem digitada sobre uma demanda de 20 dias; quando o valor digitado é inválido, o exemplo SHALL pedir uma margem válida. Esses apoios só preenchem o campo e não salvam nada (RF-09).

#### Scenario: Valor rápido
- **WHEN** o gestor aciona um dos valores rápidos
- **THEN** o campo passa a mostrar esse valor, sem chamar a API

#### Scenario: Exemplo ao vivo
- **WHEN** o gestor digita uma margem válida
- **THEN** o exemplo mostra os 20 dias da demanda e o total com a margem

### Requirement: Aparência
`/settings` SHALL oferecer a escolha entre o tema claro e o tema escuro, e a barra da casca SHALL ter um botão que alterna entre os dois. Sem escolha anterior, a interface SHALL seguir a preferência do sistema operacional. A escolha fica guardada só no navegador do gestor e não altera nenhum dado do planejamento.

#### Scenario: Escolher o tema escuro
- **WHEN** o gestor escolhe o tema escuro
- **THEN** a interface passa a usar o tema escuro e o botão de tema da barra passa a oferecer o tema claro

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
