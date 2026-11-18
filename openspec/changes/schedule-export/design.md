# Design

## Context

O cronograma é montado pelo `GET /api/schedule` (`ScheduleEndpoints.cs`), que só lê as previsões gravadas e monta a resposta `ScheduleResponse` com os itens na ordem de execução (DT-03). A exportação precisa das mesmas informações. A regra de formato vem do DT-20 (`;`, UTF-8 com BOM, `dd/MM/yyyy`, vírgula decimal, nome com a data). Ver `proposal.md` para a motivação.

Fatos conferidos no código (não suposições):
- O `TimeProvider` já está registrado na API (`TokenService`, `RefreshTokenService`), então a data do nome do arquivo pode vir de um relógio injetado, testável.
- A resposta do cronograma (`ScheduleItemResponse`) já traz todas as colunas do RF-11 e os desvios (DT-27), mas com datas como `DateOnly` e motivos como códigos (`MissingEstimate`), e não em português.
- O cliente Angular é gerado a partir do OpenAPI; os arquivos de `src/coordin-web/src/app/api/` são "DO NOT EDIT" e só mudam pelo gerador.

## Goals / Non-Goals

**Goals:**
- Mesmas informações do cronograma, sem duplicar a regra de montagem.
- Arquivo que abre no Excel em português sem ajuste manual.
- Formatação em código puro, testável sem servidor nem banco.

**Non-Goals:**
- XLSX, PDF, agendamento, envio por e-mail, exportação do histórico (changes 11 e 12).
- Alterar o cálculo, o banco ou as previsões.

## Decisions

### D1. Leitura compartilhada do cronograma

Extrair a montagem de `GetScheduleAsync` para `ScheduleReader.ReadAsync(db, cancellationToken)`, que devolve `ScheduleResponse`. O `GET /api/schedule` passa a chamar essa função e a exportação também. **Alternativa:** a exportação consultar o banco por conta própria. Descartada: duplicaria a regra de ordem, de desvio e de alerta, e a planilha poderia divergir da tela. Um teste de contrato existente do cronograma continua garantindo que a resposta não mudou.

### D2. Formatador CSV puro, separado do endpoint

`ScheduleCsv.Build(ScheduleResponse response, IReadOnlyDictionary<..> labels?)` devolve `string` (com CRLF), e a conversão para bytes com BOM fica em um método à parte. O formatador não lê relógio nem banco. As etiquetas em português ficam no próprio formatador. **Alternativa:** formatar no front, a partir do JSON do cronograma. Descartada: o gestor baixa o arquivo sem depender da tela, e a regra de formato ficaria no navegador, longe dos testes do servidor.

### D3. Motivos e situações em português, duplicados do front (com risco aceito)

A API devolve códigos (`MissingEstimate`); o front tem o texto em `planning/labels.ts`. O CSV precisa do texto no servidor. Os quatro motivos e as situações ("Concluída", "Previsível") são repetidos em `ScheduleCsv`. **Risco:** as duas listas podem divergir. **Mitigação:** um teste no formatador fixa cada texto, e a verificação de navegador confere a coluna contra a tela. Se o número de textos crescer, a próxima change pode centralizar os rótulos (fora desta).

### D4. Escapamento e neutralização de fórmulas

- Campo com `;`, `"`, CR ou LF vai entre aspas, com `"` duplicada (RFC 4180).
- Campo de **texto digitado** (título, nome de time, lista de dependências) que começa com `=`, `+`, `-`, `@` ou tabulação ganha um apóstrofo antes. Só esses campos: os números gerados pelo sistema (como `-3` de desvio) nunca recebem apóstrofo, porque são gerados aqui e não digitados.
- Ao escrever, o número usa vírgula decimal (`12,5`) e o inteiro não tem separador de milhar.

**Alternativa:** ignorar o problema. Descartada: título digitado pelo gestor (ou por um time) com `=` viraria fórmula ao abrir o arquivo.

### D5. Codificação, quebra de linha e nome

UTF-8 com BOM (`EF BB BF`) e CRLF, como o DT-20 pede. O `Content-Type` é `text/csv; charset=utf-8` e o `Content-Disposition` é `attachment; filename="coordin-cronograma-AAAA-MM-DD.csv"`. A data vem de `TimeProvider.GetUtcNow()` convertido para `America/Sao_Paulo`. Em ambiente sem o fuso IANA, a API usa a data de Brasília fixa (`UTC-3`, sem horário de verão desde 2019); o teste fixa essa conversão com um instante conhecido.

### D6. Tela: download com a sessão, sem link simples

Um `<a href="/api/schedule/export">` não envia o token, que vive só em memória (change 2). O `ScheduleStore` pede o arquivo com `HttpClient` (o interceptor já põe o cabeçalho), com `responseType: 'blob'`, cria uma URL de objeto (`URL.createObjectURL`), dispara um `<a download="...">` e libera a URL. O nome vem do cabeçalho `Content-Disposition`; se ele faltar, usa `coordin-cronograma.csv`. **Risco de CSP:** o download por `blob:` pode ser bloqueado pela política da API. A verificação no navegador confirma; se bloquear, a alternativa é aceitar a URL de objeto só para o `download` e não para navegação.

### D7. Cliente regenerado com o procedimento da change 5

O endpoint novo entra no OpenAPI, e o cliente é regenerado com o procedimento já conhecido (exportar o OpenAPI pela fábrica de testes, `ng-openapi-gen --ignoreUnusedModels false`, restaurar `api-configuration.ts`, pasta `api` em LF). A função gerada para `text/csv` devolve um `Blob`; a tela usa esse retorno.

## Risks / Trade-offs

- **Divergência de textos** entre o CSV e a tela (D3). Mitigado por teste e verificação.
- **CSP bloqueando o download** (D6). Verificado no navegador antes do PR; a correção fica dentro desta change.
- **Excel e separador:** em configurações de Excel com separador `,`, o arquivo abre em uma coluna. O DT-20 escolheu `;` para o português; o gestor de produto pode usar outra configuração. Registrado para a validação.
- **Cronograma muda entre a leitura e o download:** a exportação lê uma única vez, então o arquivo é consistente consigo mesmo; a data no nome é a do momento da leitura.

## Decisões delegadas ao autor

Nenhuma aberta. As escolhas de formato são as do DT-20 já aprovado. A neutralização de fórmulas (D4) é uma decisão minha de segurança, registrada para a revisão.
