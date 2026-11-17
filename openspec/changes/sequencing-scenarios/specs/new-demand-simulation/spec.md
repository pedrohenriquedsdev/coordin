# Spec Delta

## Purpose

Mostra ao gestor, antes de incluir uma nova demanda e na posição da prioridade que ele escolher, quais demandas ele deslocaria e em quantos dias, sem gravar nada, para decidir se confirma a inclusão exatamente ali ou descarta (RF-04, RF-16).

## ADDED Requirements

### Requirement: Simular a inclusão de uma nova demanda
`POST /api/demands/simulate` SHALL receber o mesmo corpo de `POST /api/demands`, inclusive o `position` opcional, aplicar as mesmas validações e calcular o cronograma sem e com a nova demanda posta nessa posição da ordem de prioridade e da de execução, sem gravar nada (RF-16; DT-17; DT-13).

#### Scenario: Nova demanda na frente desloca outras
- **WHEN** a nova demanda é simulada na posição 1 e divide uma pessoa com demandas existentes
- **THEN** a resposta traz cada uma dessas demandas com a data prevista atual, a nova e a diferença em dias

#### Scenario: Nada é gravado
- **WHEN** a simulação é chamada
- **THEN** nenhuma demanda, parte, alocação, dependência ou previsão é criada ou alterada, e nenhuma prioridade muda

### Requirement: Posição da nova demanda
O `position` SHALL ser um inteiro de 1 a `n + 1`, com `n` o número de demandas cadastradas, e, quando ausente, SHALL ser `n + 1` (o fim). A demanda existente que ocupa essa posição e as seguintes SHALL descer uma posição, nas duas ordens. A resposta SHALL trazer o `position` usado (RF-04, RF-16).

#### Scenario: Sem posição, no fim da fila
- **WHEN** o corpo não traz `position`
- **THEN** a nova demanda é simulada no fim das duas ordens, a resposta traz `position` igual a `n + 1`, e nenhuma demanda existente muda de data, porque a nova só usa as horas que sobram

#### Scenario: Posição no meio
- **WHEN** há quatro demandas e o `position` é 3
- **THEN** a nova demanda fica na terceira posição e as antigas terceira e quarta passam a quarta e quinta

#### Scenario: Posição fora do intervalo
- **WHEN** o `position` é 0, negativo ou maior que `n + 1`
- **THEN** a resposta é `400` com o erro no campo `position`, em português, e nada é calculado

### Requirement: Impacto por demanda e no setor
A resposta SHALL trazer a previsão da nova demanda (se é previsível, o motivo quando não for e as datas), a lista das demandas cuja `ForecastEndDate` muda, cada uma com `demandId`, `title`, a data atual, a nova e `deltaDays` em dias corridos, e `sectorEndDate` e `sectorDeltaDays` (RF-16; DT-27).

#### Scenario: Só entram as demandas que mudam
- **WHEN** a nova demanda não afeta a data de uma demanda existente
- **THEN** essa demanda não aparece na lista de mudanças

#### Scenario: Diferença em dias corridos
- **WHEN** uma demanda passa a terminar 4 dias corridos depois
- **THEN** o `deltaDays` dela é 4

### Requirement: Nova demanda não previsível não desloca ninguém
Uma nova demanda sem estimativa de algum time, sem data de início ou sem capacidade SHALL aparecer como não previsível, com o motivo, e a lista de mudanças SHALL ficar vazia, em qualquer posição (RF-10; regra inviolável 5).

#### Scenario: Sem estimativa
- **WHEN** o corpo traz um time sem estimativa e `position` 1
- **THEN** a nova demanda é não previsível com `MissingEstimate`, e nenhuma outra demanda muda

### Requirement: Confirmar na mesma posição ou descartar
Descartar a simulação SHALL não ter efeito algum. Confirmar SHALL ser o `POST /api/demands` com o mesmo corpo, que SHALL criar a demanda na mesma posição e produzir, para ela e para as deslocadas, as mesmas datas que a simulação mostrou, se nada mais mudou (RF-16; DT-17).

#### Scenario: A simulação antecipa o que a criação faz
- **WHEN** o gestor simula com `position` 2 e em seguida cria a mesma demanda com `position` 2, sem outras alterações
- **THEN** as datas gravadas da nova demanda e das deslocadas são as que a simulação mostrou

### Requirement: Validação como na criação
A simulação SHALL responder `400` com erros por campo para corpo inválido (título, times, esforço, pessoas, pré-requisitos inexistentes ou repetidos) e `409` com os mesmos códigos da criação, sem calcular nada (RF-16; DT-18).

#### Scenario: Título vazio
- **WHEN** o corpo não tem título
- **THEN** a resposta é `400` com o erro em `title`

#### Scenario: Pessoa de outro time
- **WHEN** uma pessoa alocada não é de um dos times da demanda
- **THEN** a resposta é `409` com `code` `person-not-in-demand-teams`

### Requirement: Rota protegida por login
`POST /api/demands/simulate` SHALL exigir o gestor autenticado, pela política padrão (RNF-04).

#### Scenario: Sem token
- **WHEN** a rota é chamada sem `Authorization`, com corpo
- **THEN** a resposta é `401` em Problem Details
