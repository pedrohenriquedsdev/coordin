# validation-web Specification

## Purpose
Dá ao gestor um assistente na tela para executar a validação por simulação do PRD 7.3 (carregar o conjunto fictício, registrar a linha de base, guardar o plano atual e comparar), com o resultado em gráficos acompanhados de tabela, sem que nada seja gravado pela comparação e com confirmação antes de qualquer carga que substitua os dados (RF-24; RNF-02; RNF-05; DT-36; DT-37).

## Requirements

### Requirement: Visibilidade condicionada à configuração
A área de validação SHALL aparecer no menu (grupo "Análise") e a rota `/validation` SHALL ser acessível apenas quando `GET /api/validation/status` responder com sucesso (RF-24). Quando a API responder 404 ou falhar, o item de menu SHALL NOT aparecer, e a rota SHALL mostrar a mensagem de que a área não está disponível neste ambiente.

#### Scenario: Área habilitada
- **WHEN** o gestor está autenticado e `GET /api/validation/status` responde com sucesso
- **THEN** o menu mostra o item "Validação" e a rota `/validation` abre o assistente

#### Scenario: Área desabilitada
- **WHEN** `GET /api/validation/status` responde 404
- **THEN** o item "Validação" não aparece no menu e a rota mostra "A validação não está disponível neste ambiente"

### Requirement: Carga do conjunto exige confirmação explícita
A etapa de carga SHALL informar que o carregamento substitui todo o planejamento (times, pessoas, demandas, previsões, linha de base e plano guardado) e SHALL enviar a carga somente depois que o gestor marcar a confirmação e acionar o botão (RF-24, passo 1). Sem a confirmação, nenhuma chamada de carga SHALL ser feita.

#### Scenario: Carga sem confirmação
- **WHEN** o gestor aciona "Carregar" sem marcar a confirmação
- **THEN** a tela não envia nenhuma requisição e mantém a etapa aberta com a mensagem de confirmação pendente

#### Scenario: Carga confirmada
- **WHEN** o gestor marca a confirmação e aciona "Carregar"
- **THEN** a tela envia a carga com a confirmação e, quando a API responder com sucesso, mostra quantos times, pessoas, ausências, feriados e demandas foram carregados

### Requirement: Linha de base completa antes de avançar
A etapa de linha de base SHALL mostrar uma data para cada demanda do conjunto e SHALL NOT permitir avançar enquanto alguma demanda estiver sem data (RF-24, passo 2). As datas SHALL ser tratadas como data de calendário, sem fuso.

#### Scenario: Linha de base incompleta
- **WHEN** o gestor tenta avançar com demandas sem data
- **THEN** a tela não avança e lista quantas demandas faltam

#### Scenario: Linha de base completa
- **WHEN** todas as demandas têm data e o gestor salva
- **THEN** a tela grava a linha de base e avança para a etapa do plano atual

### Requirement: Plano atual guardado uma vez
A etapa do plano atual SHALL oferecer uma única ação para guardar o planejamento do processo atual (RF-24, passo 3). Depois de guardado, a tela SHALL mostrar a data e a hora do registro e SHALL NOT oferecer nova gravação; se a API responder que o plano já existe, a tela SHALL mostrar essa informação sem tratá-la como falha.

#### Scenario: Plano guardado
- **WHEN** o gestor aciona "Guardar planejamento atual" e a API responde com sucesso
- **THEN** a tela mostra o momento do registro e não oferece a ação de novo

#### Scenario: Plano já guardado
- **WHEN** a API responde que o plano atual já existe
- **THEN** a tela mostra que o plano já está guardado e não mostra erro

### Requirement: Comparação com parâmetros informados e resultado reproduzível
A etapa de comparação SHALL pedir a faixa de variação do esforço (mínimo e máximo) e a semente, com os valores de 0,9, 1,1 e 20261126 como sugestão inicial, e SHALL enviá-los à avaliação (RF-24, passos 5 a 7; RNF-02). A tela SHALL NOT permitir valores fora da faixa aceita pela API (mínimo maior que zero e não maior que o máximo) sem mostrar o motivo. A tela SHALL deixar claro que a comparação não grava nada.

#### Scenario: Parâmetros inválidos
- **WHEN** o gestor informa mínimo maior que o máximo
- **THEN** a tela não envia a avaliação e mostra a mensagem do campo com o motivo

#### Scenario: Comparação repetida
- **WHEN** o gestor aciona a comparação duas vezes com os mesmos parâmetros
- **THEN** a tela mostra os mesmos valores nas duas vezes

### Requirement: Resultado em gráfico acompanhado de tabela
O resultado SHALL mostrar a eficiência dos dois planejamentos (ociosidade, dispersão da ocupação, sobrecargas e prazo do setor), os desvios por demanda e na média, com sinal e absolutos, e a checagem das 5 demandas mais prioritárias (RF-24; DT-36). Cada gráfico SHALL ter a tabela com os mesmos valores ao lado ou abaixo, e os valores das duas apresentações SHALL ser idênticos (RF-13, RF-17 e RF-23 aplicados à validação).

#### Scenario: Gráfico e tabela iguais
- **WHEN** a comparação termina com sucesso
- **THEN** cada gráfico de eficiência e de desvio aparece com uma tabela cujos valores são os mesmos do gráfico

#### Scenario: Checagem das prioritárias
- **WHEN** alguma das 5 demandas mais prioritárias piora com o Coordin
- **THEN** a tela mostra a quantidade de demandas que pioraram e destaca cada uma delas na tabela

### Requirement: Estados de carregamento e falha
Cada etapa SHALL mostrar "Carregando…" enquanto a API responde e uma mensagem de falha com a opção "Tentar de novo" quando a API falhar, sem perder os dados já informados pelo gestor nas etapas anteriores.

#### Scenario: Falha na comparação
- **WHEN** a API responde com erro na comparação
- **THEN** a tela mostra a mensagem de falha com "Tentar de novo" e mantém os parâmetros informados
