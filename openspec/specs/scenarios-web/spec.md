# scenarios-web Specification

## Purpose
Dá ao gestor a tela onde ele vê os cenários de sequenciamento recomendados, simula uma ordem própria, entende por que um cenário é melhor (gráfico sempre acompanhado de tabela) e decide aprovar ou rejeitar, sem que o sistema reordene nada sozinho (RF-13, RF-14, RF-15; DT-25, DT-36; regras invioláveis 2 e 3).

## Requirements

### Requirement: Tela de cenários protegida e no menu
A rota `/scenarios` SHALL exibir a tela "Cenários", protegida pela sessão (RNF-04), e o menu da casca SHALL oferecer o link "Cenários" (grupo "Planejamento") para quem tem sessão, destacando-o quando a tela está aberta. A tela SHALL mostrar "Carregando…" enquanto busca os dados e uma mensagem de falha com "Tentar de novo" quando a API falhar.

#### Scenario: Link com sessão
- **WHEN** o gestor está autenticado
- **THEN** o menu oferece o link "Cenários" e ele aparece destacado quando `/scenarios` está aberta

#### Scenario: Sem sessão
- **WHEN** alguém sem sessão abre `/scenarios`
- **THEN** é levado a `/login` guardando o destino, e nenhum dado de cenário é exibido

#### Scenario: Falha da API
- **WHEN** a busca de dados da tela falha
- **THEN** a tela mostra a mensagem de falha e o botão "Tentar de novo", que repete o pedido

### Requirement: Recomendações sob pedido
A tela SHALL oferecer o botão "Buscar recomendações" (RF-13), que pede `POST /api/scenarios/recommendations` e mostra o estado "Calculando…" enquanto espera (a busca leva alguns segundos); ela SHALL NOT buscar sozinha ao abrir. O botão SHALL ficar desabilitado enquanto uma busca, uma simulação ou uma aprovação está em andamento.

#### Scenario: Buscar com resultado
- **WHEN** o gestor aciona "Buscar recomendações" e a API devolve de 1 a 3 cenários
- **THEN** a tela mostra um cartão por cenário, na ordem recebida

#### Scenario: Nenhuma alternativa melhora
- **WHEN** a API devolve a lista vazia
- **THEN** a tela informa que nenhuma alternativa melhora o resultado atual, e nada é mostrado como cenário

#### Scenario: Ao abrir a tela
- **WHEN** o gestor abre `/scenarios`
- **THEN** nenhuma recomendação é pedida até ele acionar o botão

#### Scenario: Falha na busca
- **WHEN** a busca de recomendações falha
- **THEN** a tela mostra uma mensagem de falha com a opção de tentar de novo, e os cenários anteriores continuam na tela

### Requirement: Resumo e comparação de cada cenário
Cada cenário SHALL mostrar o valor do critério no cronograma atual e no cenário e a diferença (RF-13; DT-25), o prazo do setor com a diferença em dias e a tabela completa das demandas do cenário. Cada linha da tabela SHALL mostrar a **prioridade registrada** ao lado da **nova posição** de execução, a previsão atual e a nova previsão (com margem), e a diferença em dias em palavras ("3 dias de atraso", "2 dias antes", "no prazo"). Datas SHALL aparecer em `dd/MM/yyyy`, sem deslocamento de fuso.

#### Scenario: Resumo do cenário
- **WHEN** um cenário é exibido
- **THEN** o cartão mostra o critério atual, o do cenário e a diferença, e o prazo do setor com a diferença em dias

#### Scenario: Prioridade ao lado da posição
- **WHEN** a tabela do cenário é exibida
- **THEN** cada linha mostra a prioridade registrada e a nova posição de execução, de modo que o que muda em relação à prioridade fica visível

#### Scenario: Diferença em palavras
- **WHEN** uma demanda muda de data
- **THEN** a diferença aparece em palavras, e nunca só com sinal ou cor

#### Scenario: Prazo do setor sem data
- **WHEN** o cenário não tem prazo do setor (`sectorEndDate` nulo)
- **THEN** a tela mostra um travessão no lugar da data, sem inventar uma

### Requirement: Justificativa em gráfico e em tabela
Cada cenário SHALL mostrar por que é melhor com um gráfico de barras de `scoreContributionDelta` das demandas que mais contribuem, desenhado pelo `ChartComponent` (DT-36) e **acompanhado de uma tabela com os mesmos valores** (RF-13). O gráfico e a tabela de justificativa SHALL trazer no máximo as 10 demandas de maior contribuição absoluta, e a tabela SHALL incluir o peso e o sentido em texto ("melhora" ou "piora"), de modo que a cor nunca seja a única informação.

#### Scenario: Gráfico com tabela
- **WHEN** um cenário é exibido
- **THEN** o gráfico de justificativa aparece com uma tabela que traz os mesmos valores de cada barra

#### Scenario: Muitas demandas
- **WHEN** o cenário tem mais de 10 demandas com contribuição diferente de zero
- **THEN** o gráfico mostra as 10 de maior contribuição absoluta, e a tabela de justificativa traz as mesmas 10, enquanto a tabela completa do cenário traz todas

#### Scenario: Gráfico sem cor como única pista
- **WHEN** a tabela de justificativa é exibida
- **THEN** cada linha diz "melhora", "piora" ou "neutra" em texto

#### Scenario: Soma das contribuições
- **WHEN** a justificativa é exibida
- **THEN** a diferença do critério mostrada no resumo é a soma de todas as contribuições do cenário, e a tela não recalcula nenhum valor

### Requirement: Simular uma ordem própria com Subir e Descer
A tela SHALL oferecer a lista das demandas **abertas** na ordem de execução vigente, com **Subir** e **Descer** (sem arrastar), e o botão "Simular", que envia a ordem para `POST /api/scenarios/simulate` e mostra o resultado **no mesmo formato** dos cenários recomendados (RF-14). A ordem enviada SHALL ser a **completa**, com as concluídas nas posições em que já estavam, e a simulação SHALL NOT gravar nada.

#### Scenario: Reordenar e simular
- **WHEN** o gestor sobe uma demanda duas posições e aciona "Simular"
- **THEN** a tela envia a ordem completa, mostra o resultado no formato de cenário e o cronograma vigente continua igual

#### Scenario: Concluídas ficam fora da lista
- **WHEN** há demandas concluídas
- **THEN** elas não aparecem na lista de reordenar, e a ordem enviada as mantém nas posições em que já estavam

#### Scenario: Sem alteração
- **WHEN** o gestor aciona "Simular" sem ter movido nada
- **THEN** o resultado mostra diferença zero em tudo, como a API devolve

#### Scenario: A lista de reordenar
- **WHEN** a lista é exibida
- **THEN** o primeiro item não tem "Subir", o último não tem "Descer" e há o botão "Restaurar a ordem atual"

#### Scenario: Restaurar
- **WHEN** o gestor aciona "Restaurar a ordem atual"
- **THEN** a lista volta à ordem de execução vigente e o resultado da simulação some da tela

#### Scenario: Ordem recusada
- **WHEN** a API recusa a ordem
- **THEN** a tela mostra a mensagem do problema em português e mantém a lista como estava

### Requirement: Aprovar ou rejeitar um cenário
Cada cenário, recomendado ou simulado, SHALL ter "Aprovar este cenário" e "Rejeitar" (RF-15). Aprovar SHALL pedir confirmação antes de qualquer chamada e, ao confirmar, chamar `POST /api/scenarios/apply` com a ordem do cenário. Rejeitar SHALL remover o cenário da tela sem chamar a API. Nenhum cenário SHALL ser aplicado sem o gestor aprovar.

#### Scenario: Aprovar com confirmação
- **WHEN** o gestor aciona "Aprovar este cenário" e confirma
- **THEN** a tela chama `apply` com a ordem do cenário, mostra que a ordem de execução foi alterada e a prioridade registrada se manteve, e limpa os cenários da tela

#### Scenario: Depois de aprovar
- **WHEN** a aprovação tem sucesso
- **THEN** a tela informa que a ordem de execução mudou e a prioridade registrada não, recarrega o cronograma vigente e limpa os cenários antigos da tela, que deixaram de valer

#### Scenario: Desistir na confirmação
- **WHEN** o gestor aciona "Aprovar este cenário" e cancela a confirmação
- **THEN** nenhuma chamada é feita e a tela continua como estava

#### Scenario: Rejeitar
- **WHEN** o gestor aciona "Rejeitar" em um cenário
- **THEN** o cenário some da tela, nenhuma chamada de escrita é feita e o cronograma não muda

#### Scenario: Falha ao aprovar
- **WHEN** a API recusa ou falha ao aprovar
- **THEN** a tela mostra a mensagem em português e mantém o cenário para nova tentativa

#### Scenario: Nada acontece sozinho
- **WHEN** a tela é aberta, uma busca termina ou uma simulação termina
- **THEN** nenhuma chamada a `apply` ou `reset-to-priority` é feita

### Requirement: Voltar à ordem de prioridade a partir da tela
Quando o modo de execução for `Custom`, a tela de cenários SHALL mostrar o aviso de que a ordem de execução difere da ordem de prioridade e o botão "Voltar à ordem de prioridade", com a mesma confirmação e o mesmo efeito do botão do cronograma (RF-15). No modo `FollowPriority`, o aviso e o botão SHALL NOT aparecer.

#### Scenario: Modo personalizado
- **WHEN** o modo é `Custom`
- **THEN** a tela mostra o aviso e o botão

#### Scenario: Modo padrão
- **WHEN** o modo é `FollowPriority`
- **THEN** a tela não mostra o aviso nem o botão
