# Proposal

## Why

O cronograma é a única saída que o Coordin tem para o gestor de produto (PRD 3, "a única saída é a exportação em planilha", RF-19). Hoje o gestor precisa copiar a tela para levar a informação, e o PRD pede um arquivo que abra em planilhas de uso comum, com as mesmas informações do cronograma. A change entrega a exportação. Ela é o marco M5 (Should, 2026-11-24).

## What Changes

- **Endpoint `GET /api/schedule/export`** (novo): devolve o cronograma vigente como CSV, com as mesmas informações do `GET /api/schedule`, incluindo prioridade e ordem de execução (RF-19, RF-11).
- **Formato do arquivo (DT-20):** separador `;`, codificação UTF-8 com BOM, quebra de linha CRLF, datas `dd/MM/yyyy`, horas com vírgula decimal, cabeçalhos em português e nome `coordin-cronograma-AAAA-MM-DD.csv`, com a data do dia da exportação no fuso `America/Sao_Paulo`.
- **Leitura compartilhada:** a montagem do cronograma sai do endpoint `GET /api/schedule` para uma função própria, usada pelos dois. A exportação não tem regra de previsão: lê as previsões gravadas, como o cronograma (DT-03).
- **Segurança do conteúdo:** títulos e nomes de time (texto digitado pelo gestor) que começam com `=`, `+`, `-`, `@` ou tabulação são neutralizados, para não virar fórmula no Excel. Os números gerados pelo sistema (como desvios negativos) não são afetados.
- **Tela:** botão **Exportar planilha** no cronograma. O download é feito com a sessão do gestor (o cabeçalho de autorização), e não por um link simples.
- **Cliente Angular regenerado** a partir do OpenAPI (procedimento da change 5).
- Nenhuma mudança no motor, no banco, nas migrations nem nas previsões.

Roadmap (TDD 9.1): change 10, `schedule-export`, branch `feat/schedule-export`, Should, data alvo 2026-11-24 (M5).
Atende: **RF-19** (arquivo com as informações do cronograma, incluindo prioridade e ordem de execução; abre em planilhas de uso comum; reflete o cronograma vigente no momento da exportação); **RF-11** (mesmas colunas do cronograma); **RNF-07** (pt-BR, datas e horas em formato brasileiro, fuso `America/Sao_Paulo`); **DT-20**, **DT-03**.

## Capabilities

### New Capabilities

- `schedule-export`: o endpoint de exportação, o formato do arquivo (separador, codificação, datas, horas, colunas) e a neutralização de fórmulas.

### Modified Capabilities

- `schedule-web`: o cronograma ganha o botão "Exportar planilha" (um requisito acrescentado).

## Impact

- **Código:** `src/Coordin.Api` (`Features/Planning/`: leitura compartilhada do cronograma, formatador CSV puro e o endpoint novo); `src/coordin-web` (botão e download no cronograma, cliente regenerado). Testes em `tests/Coordin.Api.Tests` e no front.
- **Contratos:** uma rota nova (`GET /api/schedule/export`, `text/csv`). Nenhuma rota existente muda.
- **Dependências:** nenhuma. O CSV é gerado à mão, como o DT-20 decidiu (sem XLSX, que exigiria biblioteca).
- **Documentos:** `CLAUDE.md` §13 ao final (inclui a correção do estado do PR #32, que já está integrado).

**Fora desta change:**
- **XLSX e PDF:** o DT-20 escolheu CSV; formatação visual fica de fora.
- **Exportação agendada ou por e-mail:** o PRD não pede; o gestor baixa quando precisa.
- **Exportar o histórico ou o previsto × realizado:** são as changes 11 e 12; esta exporta só o cronograma atual.
- **Importação de planilha:** não está no escopo do PRD.

**Risco de prazo:** pequeno, é a menor change restante. O ponto de atenção é o download pelo navegador com o cabeçalho de autorização: se o `blob:` for bloqueado pela política de segurança (CSP), a solução vai para o design e não muda o escopo.
