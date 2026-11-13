# Tasks — capacity-registry

Regras para todas as tasks:
- Nenhum pacote novo é necessário. Se surgir a necessidade de instalar algum, a task pausa e pede a autorização do autor (`CLAUDE.md` §11).
- Ao fim de cada grupo, o build e os testes (novos e antigos) passam, e o autor recebe **uma** lista de comandos de commit. **Regra do autor (2026-11-13): cerca de metade dos commits de antes**, ou seja, **aproximadamente um commit por grupo**, com as marcações deste `tasks.md` dentro do commit do código a que se referem (sem commit só de marcação). Cada commit continua lógico, compila e passa nos testes sozinho; só ficam separados os blocos realmente independentes.
- Nenhum dado real, segredo ou credencial entra no repositório, nem nos testes. As verificações manuais com credenciais usam banco descartável e credenciais temporárias, e só rodam com a autorização do autor.
- Cada task de API mantém verdes os testes das changes anteriores (em especial os de proteção por padrão e de documentação).

## 1. Motor e banco

- [x] 1.1 Criar `Coordin.Engine/Capacity/TeamCapacity.cs` com `DailyHours(IEnumerable<decimal>)` (soma da capacidade diária das pessoas do time) e os testes de unidade em `Coordin.Engine.Tests`; verificar com `dotnet test` os casos nenhuma pessoa (0), pessoas com capacidade 0, casas decimais (0,5 + 0,25) e várias pessoas (8 + 6 + 6 = 20), e que o teste de arquitetura do motor continua passando
- [x] 1.2 Criar as entidades `Team`, `Person`, `Absence` (com o enum `AbsenceType`) e `Holiday` com suas `IEntityTypeConfiguration` (D2), registrar `ApplyConfigurationsFromAssembly` no `CoordinDbContext`, gerar e revisar a migration `AddCapacityRegistry` (só criações) e ajustar os testes de migração para as três migrations; verificar com testes de persistência que **o banco** recusa: nome de time repetido em outra caixa, feriado em data repetida, capacidade fora de 0 a 24, ausência com fim antes do início e exclusão de time que tem pessoas (`RESTRICT`), e que excluir uma pessoa remove as ausências dela (`CASCADE`)

## 2. API: base compartilhada e times

- [x] 2.1 Acrescentar a base comum: `JsonStringEnumConverter` em `AddCoordinJson`, o atributo `[MaxDecimalPlaces]`, e um auxiliar que reconhece violação de índice único (`23505`), de chave estrangeira (`23503`, inserir com referência inexistente) e de `RESTRICT` (`23001`, excluir o que ainda é referenciado) em `DbUpdateException` e em `PostgresException`; verificar com testes de que um enum sai e entra como texto, de que `8,123` falha em `MaxDecimalPlaces(2)` e de que um corpo JSON com data inválida responde `400` em Problem Details (se vier `500`, mapear `BadHttpRequestException` para `400` nesta mesma task, D4)
- [x] 2.2 Implementar os endpoints de times (`GetTeams`, `CreateTeam`, `UpdateTeam`, `DeleteTeam`) com nome único sem diferenciar maiúsculas, `team-name-taken`, `team-has-people`, `404`, e `peopleCount` e `dailyCapacityHours` (soma por `TeamCapacity`, já ligada aqui); verificar com os cenários da spec `team-registry`: "Cadastrar um time", "Sete ou mais times", "Sem times cadastrados", "Nome repetido", "Nome vazio ou só com espaços", "Nome longo demais", "Espaços nas pontas", "Cadastros simultâneos com o mesmo nome" (exatamente um `201`), "Renomear", "Salvar o mesmo nome", "Nome de outro time", "Time sem pessoas", "Time com pessoas" (pessoas inseridas direto no banco no teste, pois o endpoint de pessoas é da 3.1), "Time inexistente" e "Sem token"

## 3. API: pessoas

- [x] 3.1 Implementar os endpoints de pessoas (`GetPeople`, `CreatePerson`, `UpdatePerson`, `DeletePerson`) com `[MaxDecimalPlaces]`, faixa 0 a 24, `teamId` existente (`400` em `teamId`), nomes com `Trim()` e a capacidade do time calculada por `TeamCapacity`; verificar com os cenários da spec `people-registry` e os de capacidade da `team-registry`: "Cadastrar uma pessoa", "Pessoas com o mesmo nome", "Consultar as pessoas" (ordem por time e nome), "Sem capacidade informada", "Capacidade zero", "Limites da faixa", "Fora da faixa", "Casas decimais demais", "Sem time", "Time inexistente", "Nome inválido", "Mudar a capacidade", "Mudar de time", "Pessoa inexistente", "Excluir uma pessoa com ausências", e "Soma das pessoas", "Pessoa com capacidade zero", "Time cujas pessoas têm capacidade zero" e "A capacidade acompanha as mudanças"; e o `401` sem token

## 4. API: ausências e feriados

- [x] 4.1 Implementar os endpoints de ausências (`GetPersonAbsences`, `GetAbsences`, `CreateAbsence`, `UpdateAbsence`, `DeleteAbsence`) com a resposta única da D6, `endDate >= startDate` (`IValidatableObject`) e sem campo de motivo; verificar com os cenários da spec `capacity-calendar`: "Registrar férias", "Pessoa inexistente", "Ausências sobrepostas", "Campo de motivo ignorado", "Fim antes do início", "Ausência de um dia", "Tipo desconhecido ou campos faltando", "Data em formato inválido", "Ausências de uma pessoa", "Ausências de todos", "Pessoa inexistente na consulta", "Sem ausências", "Alterar o período", "Período inválido na edição", "Excluir", "Ausência inexistente" e o `401` sem token
- [x] 4.2 Implementar os endpoints de feriados (`GetHolidays`, `CreateHoliday`, `DeleteHoliday`) com `holiday-date-taken` e descrição opcional; verificar com os cenários da spec `capacity-calendar`: "Cadastrar um feriado", "Feriado sem descrição", "Listar por data", "Remover", "Dados inválidos", "Data repetida", "Cadastros simultâneos na mesma data" (exatamente um `201`), "Ida e volta" (`2026-12-31` volta `2026-12-31`, sem nenhum fuso aplicado) e o `401` sem token
- [x] 4.3 Verificar o contrato: o documento OpenAPI de `Development` lista as 16 operações com os `operationId` da D6, tag por recurso e o enum `type` como texto, é lido sem login, e todas as rotas novas respondem `401` sem token (teste parametrizado sobre a lista de rotas)

## 5. Cliente gerado e documentos

- [x] 5.1 Rodar `npm run generate:api` com a API local em `Development` (chave JWT temporária só no ambiente do processo, como na change 2, e a API encerrada ao final) e commitar o cliente regenerado; verificar que as funções e os modelos de times, pessoas, ausências e feriados foram gerados e que `npm run build` e `npm run lint` passam
- [x] 5.2 Registrar no TDD 5.2 o `GET /api/absences` (acréscimo aprovado pelo autor em 2026-11-13) e, no DT-18, os códigos `team-name-taken` e `holiday-date-taken`; verificar que a tabela e o DT-18 citam os acréscimos e a spec `capacity-calendar`

## 6. Front: base e navegação

- [x] 6.1 Criar `core/dates` (`formatCalendarDate`, `CalendarDatePipe`, `formatPeriod`, `today()` com relógio injetável, D8) e os testes; verificar com Vitest que `2026-12-31` vira `31/12/2026` e `formatPeriod` dá "04–15/01" e "21/12–04/01", e que **os mesmos testes passam com `TZ=UTC`, `TZ=America/Sao_Paulo` e `TZ=Asia/Tokyo`** (cenário "Exibição" da spec `registry-web`)
- [x] 6.2 Criar `core/problems` (leitura de Problem Details: `status`, `code`, erros por campo), a navegação na casca com os links "Times e pessoas" e "Calendário" só com sessão, e as rotas protegidas `/teams` e `/calendar` com telas provisórias; verificar com testes dos cenários "Links com sessão" e "Sem sessão" (redirecionamento a `/login` guardando o destino, e sem links na tela de login) e do utilitário de erros (código, erro por campo, falha de rede)

## 7. Front: Times e pessoas

- [x] 7.1 Implementar a tela "Times e pessoas": lista de times com "N pessoas · X h/dia", detalhe com "Capacidade do time", "+ Novo time", renomear e excluir com confirmação, mensagens de `team-name-taken` e `team-has-people` e os estados de carregando, vazio e falha; verificar com testes dos cenários "Lista", "Capacidade do time", "Sem times", "Novo time", "Nome repetido" (sem perder o digitado), "Nome obrigatório", "Excluir time com pessoas", "Excluir time sem pessoas" e "Falha da API"
- [x] 7.2 Implementar a tabela de pessoas do time: adicionar, editar (nome, capacidade e time), excluir com confirmação, "não contribui" para capacidade 0 e validação no cliente de capacidade obrigatória e de 0 a 24; verificar com testes dos cenários "Adicionar pessoa", "Capacidade obrigatória e na faixa", "Capacidade zero", "Mudar de time" e "Excluir pessoa"

## 8. Front: Calendário

- [ ] 8.1 Implementar a seção de ausências: tabela (pessoa, time, tipo, início, fim), filtro "Todos os times" feito no navegador, formulário sem campo de motivo, "O fim não pode ser anterior ao início", editar e excluir com confirmação e datas por `CalendarDatePipe`; verificar com testes dos cenários "Lista e filtro", "Registrar ausência", "Fim antes do início", "Sem campo de motivo", "Editar e excluir" e "Envio" (o valor `2026-12-31` chega à API sem conversão, nos três fusos)
- [ ] 8.2 Implementar a seção de feriados: lista por data, adicionar (data e descrição opcional), remover com confirmação, mensagem de `holiday-date-taken` e "Lista vazia"; verificar com testes dos cenários "Adicionar feriado", "Data repetida", "Remover feriado" e "Lista vazia"
- [ ] 8.3 **Candidata a corte (1).** Acrescentar a coluna "Ausências" na tabela de pessoas com a próxima ausência (`formatPeriod`, "—" quando não há, `today()` em São Paulo); verificar com testes dos cenários "Com ausência futura", "Período que cruza meses" e "Ausência já terminada" (com um relógio simulado). Se for cortada, ajustar a spec `registry-web` antes do PR

## 9. Verificação manual

- [ ] 9.1 Com a autorização do autor, subir a API real servindo o build de produção (banco descartável e credenciais temporárias, como na change 2) e, num Chrome real guiado pelo protocolo de depuração, percorrer: cadastrar 8 times e as pessoas, erro de nome repetido, exclusão bloqueada de time com pessoas, mover uma pessoa de time, ausências e feriados, e a data `31/12/2026` exibida igual com o navegador emulando `America/Sao_Paulo`, `Asia/Tokyo` e `America/Los_Angeles`; verificar também 1280 px sem rolagem horizontal nas duas telas, **0 violações de CSP** e a limpeza completa do que foi criado

## 10. Documentos e verificação final

- [ ] 10.1 Atualizar o `CLAUDE.md`: §3 (incluir o `.gitattributes` na árvore), §9.2 (trocar "Um commit por bloco lógico. Não economize commits." pela regra de cerca de um commit por grupo de tasks, com as marcações do `tasks.md` dentro do commit do código) e §13 (change 3 implementada, com as lições novas, se houver, e o próximo passo: change 4); verificar que a árvore do §3 coincide com o repositório
- [ ] 10.2 Rodar todos os comandos do TDD 8.6 que se aplicam (`dotnet build Coordin.slnx`, `dotnet format Coordin.slnx --verify-no-changes`, `dotnet test Coordin.slnx --collect:"XPlat Code Coverage"`, `npm ci`, `npm run lint`, `npm test`, `npm run build`) numa cópia limpa do commit, mais `npm run format:check` e `openspec validate capacity-registry --strict`; revisar o diff completo contra a `main` procurando segredos, senhas e e-mails reais; registrar os resultados reais na descrição do PR, separando local de CI

## Workflow follow-up

- Abrir o PR `feat/capacity-registry` → `main` com o template, após autorização do autor; integrar com `gh pr merge <n> --merge`, também com a autorização explícita dele.
- Após o merge, rodar `/opsx:archive capacity-registry` em uma branch `chore/archive-capacity-registry` e conferir as quatro specs novas em `openspec/specs/`.
- Atualizar o §13 do `CLAUDE.md` com o archive e iniciar a change 4 `forecast-engine`.
