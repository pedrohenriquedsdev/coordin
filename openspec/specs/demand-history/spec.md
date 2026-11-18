# demand-history Specification

## Purpose
Guarda, sem possibilidade de edição ou exclusão, como a estimativa de esforço e a data prevista de cada demanda mudaram ao longo do tempo, com a causa de cada mudança, para que o gestor consiga consultar a evolução de cada demanda (RF-20, RF-21; DT-14).

## Requirements

### Requirement: Histórico de estimativas
O sistema SHALL registrar cada alteração na estimativa de esforço de um time em uma demanda, com o valor anterior, o novo valor, a data da alteração e um motivo opcional informado pelo gestor (RF-20). O histórico SHALL ser somente de inclusão: a API SHALL NOT oferecer edição nem exclusão de registros.

#### Scenario: Alteração de estimativa registrada
- **WHEN** o gestor altera de 40 para 60 horas a estimativa do time "Alfa" em uma demanda, informando o motivo "escopo maior"
- **THEN** o histórico da demanda ganha um registro com o time, 40, 60, a data da alteração e o motivo

#### Scenario: Motivo omitido
- **WHEN** o gestor altera uma estimativa sem informar motivo
- **THEN** o registro é gravado com o motivo vazio, sem erro

#### Scenario: Alteração que não muda o valor
- **WHEN** o gestor salva a demanda com a mesma estimativa que já estava gravada
- **THEN** nenhum registro é criado no histórico de estimativas

### Requirement: Histórico da data prevista
O sistema SHALL registrar cada mudança na data prevista de uma demanda, com a data anterior, a nova, a data da mudança e a causa que a provocou: estimativa, alocação, capacidade, ausência, dependência, cenário aprovado ou nova demanda (RF-21). A primeira previsão de uma demanda SHALL ser o primeiro registro do histórico.

#### Scenario: Primeira previsão
- **WHEN** uma demanda se torna previsível pela primeira vez
- **THEN** o histórico da data prevista ganha um primeiro registro, com a data anterior vazia e a causa correspondente

#### Scenario: Mudança causada por dependência
- **WHEN** a conclusão de um pré-requisito, ou uma mudança de dependência, altera a data prevista de uma demanda
- **THEN** o registro guarda a data anterior, a nova e a causa "dependência"

#### Scenario: Data prevista que não muda
- **WHEN** uma escrita não altera a data prevista de uma demanda
- **THEN** nenhum registro é criado para essa demanda

### Requirement: Consulta por demanda em ordem cronológica
A API SHALL devolver, para uma demanda, o histórico de estimativas e o histórico da data prevista em ordem cronológica (do mais antigo ao mais recente). Uma demanda inexistente SHALL responder 404.

#### Scenario: Consulta de uma demanda com histórico
- **WHEN** o gestor consulta o histórico de uma demanda com três mudanças de data prevista
- **THEN** a resposta traz os três registros do mais antigo ao mais recente

#### Scenario: Demanda inexistente
- **WHEN** o gestor consulta o histórico de um identificador que não existe
- **THEN** a API responde 404

### Requirement: Somente inclusão no banco
O banco SHALL recusar atualização das tabelas de histórico, e recusar exclusão de registro enquanto a demanda dele existir, de modo que um registro gravado não possa ser alterado nem apagado nem pela API nem por uma consulta direta (DT-14). A única exclusão permitida é a que vem da cascata da exclusão da própria demanda.

#### Scenario: Tentativa de alterar um registro
- **WHEN** uma consulta direta tenta alterar um registro do histórico da data prevista
- **THEN** o banco recusa a operação e o registro permanece igual

#### Scenario: Exclusão direta de um registro
- **WHEN** uma consulta direta tenta apagar um registro do histórico de uma demanda que existe
- **THEN** o banco recusa a operação

#### Scenario: Exclusão da demanda
- **WHEN** uma demanda é excluída
- **THEN** o histórico dela é removido junto, pela cascata, e nenhum outro registro é afetado
