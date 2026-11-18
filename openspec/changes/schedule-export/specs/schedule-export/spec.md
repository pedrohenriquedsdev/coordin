# Spec Delta

## Purpose

Permite ao gestor levar o cronograma para o gestor de produto em uma planilha de uso comum, com as mesmas informações da tela, no formato brasileiro (RF-19; DT-20; RNF-07).

## ADDED Requirements

### Requirement: Exportar o cronograma em CSV
O sistema SHALL devolver o cronograma vigente em `GET /api/schedule/export` como arquivo CSV, com as mesmas informações do `GET /api/schedule`, incluindo a prioridade e a ordem de execução de cada demanda (RF-19; RF-11). A rota SHALL exigir o gestor autenticado (RNF-04) e SHALL NOT gravar nada.

#### Scenario: Exportar com demandas
- **WHEN** o gestor autenticado chama `GET /api/schedule/export` e há demandas
- **THEN** a resposta é `200` com `Content-Type` `text/csv; charset=utf-8`, uma linha de cabeçalho e uma linha por demanda, na ordem de execução vigente

#### Scenario: Exportar sem demandas
- **WHEN** não há demandas cadastradas
- **THEN** a resposta é `200` com apenas a linha de cabeçalho

#### Scenario: Sem sessão
- **WHEN** a chamada não traz o token do gestor
- **THEN** a resposta é `401` e nenhum conteúdo é devolvido

#### Scenario: Reflete o cronograma no momento da exportação
- **WHEN** o gestor muda a ordem de execução e exporta em seguida
- **THEN** o arquivo traz a ordem nova, e a exportação anterior não é alterada

### Requirement: Formato brasileiro do arquivo
O arquivo SHALL usar `;` como separador, codificação UTF-8 com BOM (para o Excel reconhecer os acentos), quebra de linha CRLF, datas `dd/MM/yyyy`, horas com vírgula decimal e cabeçalhos em português (DT-20; RNF-07). Campos sem valor SHALL aparecer vazios, e nunca como texto como "null".

#### Scenario: Abrir no Excel em português
- **WHEN** o arquivo é aberto no Excel em português
- **THEN** as colunas se separam, os acentos aparecem corretos e as datas e horas estão no formato brasileiro

#### Scenario: Hora com vírgula
- **WHEN** um time tem 12,5 horas estimadas
- **THEN** o arquivo mostra `12,5`, e não `12.5`

#### Scenario: Campo vazio
- **WHEN** uma demanda não tem data de conclusão
- **THEN** a célula correspondente fica vazia

#### Scenario: Campo com separador ou aspas
- **WHEN** um título contém `;`, aspas ou quebra de linha
- **THEN** o campo vai entre aspas, com as aspas internas duplicadas, e a linha continua a ter o mesmo número de colunas

### Requirement: Nome do arquivo com a data da exportação
O arquivo SHALL ser entregue com o nome `coordin-cronograma-AAAA-MM-DD.csv`, em que a data é a do dia da exportação no fuso `America/Sao_Paulo` (DT-20; RNF-07).

#### Scenario: Nome do arquivo
- **WHEN** a exportação é feita em 2026-11-24 (horário de Brasília)
- **THEN** o nome é `coordin-cronograma-2026-11-24.csv`

### Requirement: Texto digitado não vira fórmula
Os campos de texto que o gestor digitou (título da demanda e nome do time) SHALL, quando começarem com `=`, `+`, `-`, `@` ou tabulação, ser precedidos de uma apóstrofo, para que a planilha não os interprete como fórmula. Os valores gerados pelo sistema (números, datas e desvios negativos) SHALL NOT ser alterados.

#### Scenario: Título que parece fórmula
- **WHEN** uma demanda se chama `=SOMA(A1:A2)`
- **THEN** a célula mostra o texto `'=SOMA(A1:A2)`, e a planilha não calcula nada

#### Scenario: Desvio negativo
- **WHEN** uma demanda concluída antes do prazo tem desvio de -3 dias
- **THEN** a célula mostra `-3`, sem apóstrofo, porque o número é gerado pelo sistema

### Requirement: Colunas do cronograma
Cada linha SHALL trazer, nesta ordem: prioridade, ordem de execução, fora da ordem de prioridade (`Sim` ou `Não`), demanda, times com as horas de cada um, esforço total em horas, data de início, início previsto, fim sem margem, fim com margem, fim informado, passa do prazo informado (`Sim` ou `Não`), situação, concluída em, desvio contra a primeira previsão (dias), desvio contra a previsão vigente (dias) e depende de (RF-11; DT-27; RF-18).

#### Scenario: Demanda não previsível
- **WHEN** uma demanda não tem previsão
- **THEN** a situação traz o motivo em português (por exemplo, "Falta a estimativa de algum time") e as colunas de datas previstas ficam vazias

#### Scenario: Demanda concluída
- **WHEN** uma demanda está concluída
- **THEN** a situação indica "Concluída", e as colunas de desvio mostram os dias em relação à primeira previsão e à previsão vigente, com o sinal positivo para atraso

#### Scenario: Dependências
- **WHEN** uma demanda depende de outras
- **THEN** a coluna "depende de" traz os títulos dos pré-requisitos separados por vírgula, na ordem de prioridade deles

### Requirement: Exportação pela tela
A tela do cronograma SHALL oferecer o botão "Exportar planilha" (RF-19). O download SHALL usar a sessão do gestor e salvar o arquivo com o nome devolvido pela API. Em falha, a tela SHALL mostrar a mensagem em português e manter o cronograma na tela.

#### Scenario: Baixar o arquivo
- **WHEN** o gestor aciona "Exportar planilha"
- **THEN** o navegador baixa o arquivo `coordin-cronograma-AAAA-MM-DD.csv`

#### Scenario: Falha no download
- **WHEN** a exportação falha
- **THEN** a tela mostra a mensagem de falha e o cronograma continua como estava
