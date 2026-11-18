# Tasks — schedule-export

Regras para todas as tasks:
- **Sem dependência nova** (NuGet ou npm): o CSV é gerado à mão, como o DT-20 decidiu. Qualquer pacote novo exige pausa e a autorização do autor (`CLAUDE.md` §11).
- Padrões a copiar: montagem e rotas em `Features/Planning/ScheduleEndpoints.cs` e `ScheduleContracts.cs`; testes de contrato em `tests/Coordin.Api.Tests/Http/`, no estilo de `TeamReportContractTests`; testes de integração com PostgreSQL real, no estilo de `ScheduleEndpointTests`; `TimeProvider` injetado (como em `TokenService`); store e tela no estilo de `schedule/schedule.store.ts` e `schedule-page.ts`.
- Comentários e textos ao usuário em português (pt-BR); identificadores em inglês. **Os arquivos de `src/coordin-web/src/app/api/` são gerados: não os edite à mão**; só o gerador os muda.
- Ao fim de cada grupo: `dotnet build Coordin.slnx` com 0 avisos, `dotnet format Coordin.slnx --verify-no-changes`, e os testes do escopo verdes; no front, `npm run format:check`, `npm run lint`, `npm test -- --watch=false` e `npm run build`.
- Marque cada task ao concluí-la, no mesmo commit do código e dos testes do grupo.

## 1. Leitura compartilhada do cronograma (RF-11; DT-03)

- [x] 1.1 Extrair a montagem de `GetScheduleAsync` (em `ScheduleEndpoints.cs`) para `ScheduleReader.ReadAsync(CoordinDbContext db, CancellationToken cancellationToken)` em `Features/Planning/ScheduleReader.cs`, devolvendo `ScheduleResponse`. Nenhuma regra muda: é só mover o código. O `GET /api/schedule` passa a chamar `ScheduleReader.ReadAsync`.
- [x] 1.2 Rodar os testes existentes do cronograma (`ScheduleEndpointTests`, contrato do cronograma): tudo continua verde, sem alterar as asserções. Se algum teste pedir mudança, parar e perguntar ao autor.

## 2. Formatador CSV puro (RF-19; DT-20; RNF-07)

- [x] 2.1 Criar `Features/Planning/ScheduleCsv.cs` com `string Build(ScheduleResponse response)` (CRLF, cabeçalho em português, colunas da seção "Colunas do cronograma" da spec) e `byte[] ToBytes(string csv)` (UTF-8 com BOM `EF BB BF`). Textos de motivos e situações conforme D3 do design.
- [x] 2.2 Criar `Features/Planning/ScheduleCsvText.cs` (ou dentro de `ScheduleCsv`) com o escapamento RFC 4180 (`;`, aspas, CR, LF) e a neutralização de fórmulas (D4): só nos campos de texto digitado (título, nome de time, lista de dependências), com apóstrofo antes de `=`, `+`, `-`, `@` ou tabulação.
- [x] 2.3 Criar `tests/Coordin.Api.Tests/Planning/ScheduleCsvTests.cs` (unitário, sem servidor): cabeçalho e ordem das colunas; BOM na codificação; CRLF; campo com `;`, aspas e quebra de linha entre aspas, com o mesmo número de colunas; título `=SOMA(A1:A2)` neutralizado; desvio `-3` sem apóstrofo; hora `12,5`; datas `dd/MM/yyyy`; campo vazio (sem `null`); demanda não previsível com o motivo em português; demanda concluída com desvios; dependências em lista; lista vazia (só o cabeçalho); `Sim`/`Não` nos booleanos.
- [x] 2.4 Teste de texto fixo: cada motivo e situação tem o texto esperado (fixa D3, para uma divergência com o front aparecer no teste).

## 3. Endpoint de exportação (RF-19; RNF-04)

- [x] 3.1 Acrescentar `MapGet("/export", ExportScheduleAsync)` em `ScheduleEndpoints.cs` (nome `ExportSchedule`, tag `Schedule`, `Produces` `text/csv`). A resposta é `Results.File(bytes, "text/csv; charset=utf-8", "coordin-cronograma-AAAA-MM-DD.csv")`, com o nome calculado pelo `TimeProvider` no fuso `America/Sao_Paulo` (D5).
- [x] 3.2 Criar `Features/Planning/ScheduleExportName.cs` (pura): `string For(DateTimeOffset utcNow)`, que converte para `America/Sao_Paulo` (com a conversão fixa de UTC-3 se o fuso IANA não existir). Teste unitário: 2026-11-24T02:30Z é 2026-11-23 em Brasília; 2026-11-24T15:00Z é 2026-11-24.
- [x] 3.3 Criar `tests/Coordin.Api.Tests/Planning/ScheduleExportTests.cs` (integração com PostgreSQL real): `200` com `text/csv; charset=utf-8`, BOM, cabeçalho, uma linha por demanda na ordem de execução, e a alteração da ordem aparece na exportação seguinte; sem demandas, só o cabeçalho; nome do arquivo com a data do relógio injetado; `401` sem token (não devolve conteúdo); nada é gravado (contagem de linhas nas tabelas não muda).
- [x] 3.4 Acrescentar no teste de contrato do OpenAPI (`Http/`) a operação `ExportSchedule` com a tag `Schedule` e a rota `401` sem token, no estilo dos contratos existentes.

## 4. Cliente Angular regenerado e tela (RF-19; schedule-web)

- [x] 4.1 Regenerar o cliente Angular com o procedimento da change 5: exportar o OpenAPI pela fábrica de testes (não pela API de desenvolvimento), rodar `ng-openapi-gen --ignoreUnusedModels false`, restaurar `api-configuration.ts`, converter a pasta `api` para LF. Confirmar que a função de exportação devolve `Blob`. Não editar os arquivos gerados à mão.
- [x] 4.2 Criar em `schedule/schedule-export.ts` a função pura `fileNameFrom(contentDisposition: string | null): string` (lê o `filename` do cabeçalho; sem cabeçalho, `coordin-cronograma.csv`) e a função `downloadBlob(blob, name)` que cria a URL de objeto, dispara o `<a download>` e libera a URL. Testes com `TZ` fixo.
- [x] 4.3 Acrescentar em `schedule.store.ts` o método `exportCsv(): void` (usa a função gerada com `responseType: 'blob'`, o estado `exporting` (`signal<boolean>`) e a mensagem de falha em `signal<string | null>`). Teste com `HttpTestingController`: pedido à rota certa, download disparado, falha com a mensagem e `exporting` voltando a `false`.
- [x] 4.4 Acrescentar em `schedule-page.html` o botão "Exportar planilha" acima da lista (desabilitado enquanto `exporting()`), e a mensagem de falha em `role="alert"`. Testes em `schedule-page.spec.ts`: botão visível com o cronograma carregado, desabilitado durante o download, falha mantém o cronograma.

## 5. Verificação final e documentos

- [ ] 5.1 Cópia limpa do commit com os comandos do TDD 8.6 (`dotnet build`, `dotnet format`, `dotnet test` com `Category!=Performance` e cobertura, `Category=Performance`; front com `npm ci`, `format:check`, `lint`, testes nos fusos UTC, Tóquio e Los Angeles, `build` e `npm audit` informativo); `openspec validate schedule-export --strict`. Registrar os números reais.
- [ ] 5.2 Verificação no navegador (headless, API e PostgreSQL descartáveis, credenciais temporárias, autorização do autor): baixar o arquivo pelo botão, conferir o BOM, o `;`, a data no nome, o título com `=` neutralizado, a ordem depois de uma mudança, o cabeçalho de `Content-Security-Policy` sem violação de `blob:`, e a largura de 360 e 1280 px sem rolagem horizontal. Limpar banco, credenciais e processos.
- [ ] 5.3 Atualizar `CLAUDE.md` §13 com a change 10 (resumo, verificação, lições e "Próximo passo"), incluindo a correção do estado do PR #32 (arquivamento da 9b já integrado à `main`). Entregar a lista final de commits (o autor commita; PR e merge só com a autorização dele e CI verde).
