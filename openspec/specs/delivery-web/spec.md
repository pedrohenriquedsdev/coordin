# delivery-web Specification

## Purpose
Dá ao gestor duas telas para consultar a entrega: o detalhe de uma demanda com a evolução da estimativa e da data prevista, e a página de indicadores de precisão com filtro, gráfico e tabela, no computador e no celular (RF-20, RF-21, RF-23; DT-36; RNF-03).

## Requirements

### Requirement: Detalhe da demanda com os históricos
A rota `/demands/:id` SHALL mostrar os dados da demanda e, em duas listas separadas, o histórico de estimativas e o histórico da data prevista, em ordem cronológica (RF-20, RF-21). A tela SHALL mostrar "Carregando…" enquanto busca os dados, e uma falha com "Tentar de novo". Uma demanda inexistente SHALL mostrar uma mensagem própria, sem erro genérico.

#### Scenario: Demanda com histórico
- **WHEN** o gestor abre o detalhe de uma demanda com alterações de estimativa e de data
- **THEN** a tela mostra as duas listas, cada registro com a data, o valor anterior, o novo valor e, na data prevista, a causa

#### Scenario: Demanda inexistente
- **WHEN** o gestor abre o detalhe de um identificador que não existe
- **THEN** a tela mostra "Demanda não encontrada" e um link de volta às demandas

### Requirement: Link para o detalhe
A lista de demandas SHALL oferecer, em cada demanda, um link para o detalhe em `/demands/:id`.

#### Scenario: Abrir o detalhe pela lista
- **WHEN** o gestor aciona o link de uma demanda na lista
- **THEN** a tela abre o detalhe dessa demanda

### Requirement: Página de indicadores
A rota `/reports/accuracy` SHALL mostrar os indicadores de precisão do setor e de cada time, com filtro de período de conclusão, um gráfico por time com o setor e uma tabela com os mesmos valores (RF-23; DT-36). Os valores da tabela SHALL ser iguais aos do gráfico.

#### Scenario: Gráfico e tabela iguais
- **WHEN** o gestor consulta os indicadores de um período
- **THEN** cada valor plotado no gráfico aparece na tabela com o mesmo número

#### Scenario: Período sem demandas concluídas
- **WHEN** o período não tem demandas concluídas
- **THEN** a tela diz que não há demandas concluídas no período, sem gráfico vazio

#### Scenario: Período invertido
- **WHEN** o gestor informa uma data inicial posterior à final
- **THEN** a tela mostra o motivo e não envia a consulta

### Requirement: Demandas de fora dos indicadores
A página SHALL informar quantas demandas do período ficaram de fora de cada indicador, e por quê (sem linha de base, sem esforço real, ou concluídas antes desta change).

#### Scenario: Demandas de fora
- **WHEN** a API informa que duas demandas não têm esforço real
- **THEN** a página mostra "2 demandas sem esforço real ficaram de fora deste indicador"

### Requirement: Layout em 360 px
Em 360 px de largura, a página de indicadores e o detalhe da demanda SHALL caber sem rolagem horizontal da página; as tabelas largas SHALL virar cartões (RNF-03).

#### Scenario: Celular
- **WHEN** a tela é aberta em 360 px
- **THEN** a página não rola na horizontal e as tabelas aparecem como cartões
