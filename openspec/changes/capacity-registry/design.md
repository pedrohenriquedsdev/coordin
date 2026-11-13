# Design

## Context

Motivação e escopo estão em `proposal.md`; o comportamento exigido, nas quatro specs (`team-registry`, `people-registry`, `capacity-calendar` e `registry-web`). O TDD já fecha o modelo (seção 4.2, DT-10, DT-15), os endpoints (5.2) e os erros (DT-18); este documento decide **como encaixar isso no código existente**.

Estado atual:

- `Coordin.Api` tem a organização por feature de `Features/Auth` e a configuração comum em `Infrastructure/Http` (JSON em `camelCase`, Problem Details em português, `ApiProblems.Conflict(code, detail)` para o `409` com `code`, validação nativa por atributos com `AddValidation`). Toda rota sem regra própria já exige o gestor (política de fallback da change 2).
- `CoordinDbContext` herda de `IdentityDbContext`, tem `RefreshTokens` e duas migrations (`InitialCreate`, `AddAuthentication`) aplicadas na inicialização.
- `Coordin.Engine` é uma biblioteca pura e vazia (só o marcador de assembly), com um teste de arquitetura que a proíbe de referenciar qualquer coisa fora do runtime.
- O Angular 22 é zoneless, com `core/auth`, `login`, `home`, o cliente gerado em `src/app/api` (funções que devolvem `Observable`) e `provideApiConfiguration('')`. O `DatePipe` está configurado com o deslocamento `-0300` para **instantes**.
- Os protótipos T07 (Times e pessoas) e T08 (Ausências e feriados, menu "Calendário") estão em `docs/design/coordin-prototypes.excalidraw`.

## Goals / Non-Goals

**Goals:**

- Cadastros completos de times, pessoas, ausências e feriados, com as regras de integridade garantidas pelo **banco** (não só pela aplicação), porque as changes 4 a 6 vão confiar nelas.
- Datas de calendário que nunca mudam de dia, em nenhuma camada (lição da change 1).
- Telas fiéis aos protótipos T07 e T08, utilizáveis no computador.

**Non-Goals:**

- Qualquer cálculo de previsão, calendário de capacidade por data ou recálculo na escrita (change 4 em diante).
- Interface para celular, importação em massa, histórico de alterações e jornadas irregulares.
- Reorganizar o que já existe: as entidades e a configuração do EF de `RefreshToken` ficam como estão.

## Decisions

### D1. Onde fica o código

- **API:** `Features/Teams`, `Features/People` e `Features/Calendar` (ausências e feriados), cada uma com entidade, contratos (records) e endpoints, como `Features/Auth`. O mapeamento de cada entidade ao banco fica em uma classe `IEntityTypeConfiguration` na própria feature, carregada por `ApplyConfigurationsFromAssembly` no `CoordinDbContext`.
- **Motor:** `Coordin.Engine/Capacity/TeamCapacity.cs`.
- **Front:** `core/dates` (datas de calendário), `core/problems` (leitura de erros da API), `teams/` e `calendar/` (as telas) e a navegação na casca (`app.html`).

### D2. Modelo no banco: a integridade mora no banco

Seguindo o TDD 4.2 e o DT-15 (`Guid` v7 gerado pela aplicação com `Guid.CreateVersion7()`, `DateOnly` como `date`, horas como `numeric(5,2)`):

| Tabela | Colunas e regras |
|---|---|
| `Teams` | `Id`, `Name varchar(100)`, `NormalizedName varchar(100)` com **índice único** |
| `People` | `Id`, `Name varchar(100)`, `TeamId` (FK para `Teams`, **`RESTRICT`**), `DailyCapacityHours numeric(5,2)` com restrição `CHECK (0..24)` |
| `Absences` | `Id`, `PersonId` (FK para `People`, **`CASCADE`**), `Type` (texto: `Vacation` ou `Other`), `StartDate date`, `EndDate date` com `CHECK (EndDate >= StartDate)` e índice em `PersonId` |
| `Holidays` | `Id`, `Date date` com **índice único**, `Description varchar(200)` opcional |

- **Nome do time sem diferenciar maiúsculas:** o TDD pede unicidade "sem diferenciar maiúsculas". `NormalizedName` guarda `Name.Trim().ToUpperInvariant()`, e o índice único é sobre ele. Alternativas: índice funcional `lower("Name")` (exige SQL na migration, e o EF não o modela) e a extensão `citext` (exige habilitar uma extensão no PostgreSQL gerenciado da hospedagem, DT-09). Escolhi a coluna normalizada por ser portável e simples de testar. Limitação aceita: acentos contam como diferentes ("Mobile" e "Móbile").
- **Tipo da ausência:** guardado como texto (`HasConversion<string>()`), legível em consultas e estável se um valor novo for acrescentado.
- A migration é `AddCapacityRegistry`, gerada com o `dotnet-ef` e revisada, na mesma convenção das anteriores.

### D3. Unicidade e corrida: o banco decide, a aplicação traduz

Uma consulta prévia ("já existe?") dá o caminho feliz, mas não protege duas requisições simultâneas. Por isso a violação do índice único (`PostgresException` com `SqlState` `23505`, dentro de `DbUpdateException`) é capturada e traduzida para o mesmo `409` (`team-name-taken` ou `holiday-date-taken`). É o mesmo cuidado da rotação do refresh token na change 2, com o mesmo tipo de teste (duas chamadas simultâneas, exatamente uma vence). Do mesmo modo, ao excluir um time, a violação da chave estrangeira vira `team-has-people`, caso uma pessoa seja adicionada entre a consulta e a exclusão. Com `ON DELETE RESTRICT` o PostgreSQL devolve `23001` (`restrict_violation`), e não `23503` (`foreign_key_violation`), que é o código de *inserir* uma pessoa com time inexistente (descoberto nos testes de persistência da task 1.2); o auxiliar da task 2.1 reconhece os dois.

### D4. Validação de entrada

- Contratos como `record` com atributos (`[Required]`, `[StringLength]`, `[Range]`), validados pelo `AddValidation` que já existe; os nomes dos campos nos erros saem em `camelCase` (`errors.dailyCapacityHours`).
- **Casas decimais:** um atributo próprio `[MaxDecimalPlaces(2)]` (um `ValidationAttribute`), porque `Range` não o faz. A capacidade é `decimal?` com `[Required]`, para "não informada" ser distinguível de zero (RF-02).
- **Período da ausência:** `endDate >= startDate` por `IValidatableObject`, com o erro no campo `endDate`.
- **Nome:** `Trim()` antes de validar e guardar; vazio ou só espaços é erro em `name`.
- **Referência a time inexistente** no cadastro e na edição de pessoa é `400` com o erro em `teamId` (o corpo está errado), e não `404` (o recurso da URL existe). Já `404` é para o `id` da URL.
- **Enum em JSON:** `JsonStringEnumConverter` registrado em `AddCoordinJson`, para `type` ir e voltar como `"Vacation"`/`"Other"`, e o OpenAPI descrever os valores. Isso vale para toda a API daqui em diante, o que é intencional.
- **A verificar na primeira task de API:** um corpo JSON inválido (por exemplo, `31/12/2026` em um `DateOnly`) deve virar `400`. A expectativa é que o ASP.NET converta o `BadHttpRequestException` em `400` mesmo com o `UseExceptionHandler` da change 1. Se vier `500`, a correção é um tratador que mapeia esse tipo de exceção para `400` em Problem Details, dentro da mesma task.

### D5. Capacidade do time no motor (DT-02)

`TeamCapacity.DailyHours(IEnumerable<decimal>)` devolve a soma da capacidade diária das pessoas e vive em `Coordin.Engine`, com testes de unidade (nenhuma pessoa, zeros, casas decimais, várias pessoas). A API carrega as pessoas de cada time e chama o motor; **não** faz `SUM` no SQL, porque o cálculo da capacidade é regra do motor, e o volume (7 a 10 times, dezenas de pessoas) é desprezível. A change 4 estende essa área com a capacidade por data (calendário, ausências e feriados, DT-10).

- *Alternativa:* `SUM` no banco. Mais curto, mas espalha a regra de capacidade por fora do motor, e a change 4 teria de reconciliar as duas versões.

### D6. Contratos da API

Operações e `operationId` estáveis (para o cliente gerado): `GetTeams`, `CreateTeam`, `UpdateTeam`, `DeleteTeam`, `GetPeople`, `CreatePerson`, `UpdatePerson`, `DeletePerson`, `GetPersonAbsences`, `GetAbsences`, `CreateAbsence`, `UpdateAbsence`, `DeleteAbsence`, `GetHolidays`, `CreateHoliday` e `DeleteHoliday`. Uma tag por recurso, `Produces`/`ProducesProblem` completos. As respostas:

- **Time:** `id`, `name`, `peopleCount`, `dailyCapacityHours`.
- **Pessoa:** `id`, `name`, `teamId`, `teamName`, `dailyCapacityHours`.
- **Ausência** (a mesma forma nas duas consultas): `id`, `personId`, `personName`, `teamId`, `teamName`, `type`, `startDate`, `endDate`. Uma forma só evita dois tipos quase iguais e dá à tela do calendário tudo de que precisa em uma chamada.
- **Feriado:** `id`, `date`, `description` (nulo quando não informada).

`201` com `Location` no formato `/api/<recurso>/{id}` (TDD 5.1). Listas devolvidas inteiras e ordenadas no servidor (DT-16). Nenhum endpoint declara regra de autorização: a política de fallback os protege (D5 da change 2), e uma spec de cada recurso prova o `401`.

Os dois códigos `409` novos (`team-name-taken` e `holiday-date-taken`) seguem o estilo de DT-18, que já os dá como exemplos abertos ("por exemplo"); a task de documentação os registra no TDD junto com o acréscimo de `GET /api/absences`.

### D7. Front: estrutura, estado e erros

- **Estado:** cada tela tem um serviço da feature com `signal`s (lista, carregando, erro) que chama as **funções geradas** do cliente (`getTeams`, `createTeam`…), como o `SessionService` fez. Depois de cada escrita bem-sucedida, a lista é recarregada (TDD 5.1: "o front recarrega depois de uma escrita"), em vez de remendar o estado local, o que evita divergência com o servidor e é simples de testar.
- **Formulários:** `ReactiveFormsModule`, como o login, com validação no cliente (campo obrigatório, faixa de 0 a 24, fim não anterior ao início) e **sem** repetir regra de negócio: o servidor continua sendo a autoridade, e a tela só evita idas inúteis.
- **Erros da API:** um utilitário `core/problems` lê o Problem Details (`status`, `code`, `errors` por campo) e as telas mapeiam `team-name-taken`, `team-has-people` e `holiday-date-taken` para mensagens em português; erros de campo (`400`) vão para o campo certo; falhas de rede e `5xx` viram "tentar de novo", sem perder o que foi digitado.
- **Confirmação de exclusão:** um passo de confirmação na própria tela ("Excluir? Confirmar / Cancelar"), e não o `confirm()` do navegador, que é difícil de testar e destoa do visual.

### D8. Datas de calendário sem fuso (lição da change 1)

Uma data de calendário (`2026-12-31`) **nunca** passa por `Date`, `DatePipe` ou `new Date(...)`, que a leem como meia-noite em algum fuso e podem deslocá-la um dia. Em `core/dates`:

- `formatCalendarDate('2026-12-31')` → `31/12/2026` e `CalendarDatePipe`, por troca de partes da string, sem fuso. Um `<input type="date">` já entrega `yyyy-MM-dd`, e esse valor vai à API como está.
- `formatPeriod(start, end)` produz "04–15/01" (mesmo mês) ou "21/12–04/01" (meses diferentes), usado na coluna "próxima ausência".
- `today()` devolve a data de hoje em São Paulo como `yyyy-MM-dd`, a partir de um relógio injetável (para os testes controlarem "hoje"), usando o deslocamento fixo `-03:00` já adotado no projeto (RNF-07).
- **Prova:** testes executados com `TZ=UTC`, `America/Sao_Paulo` e `Asia/Tokyo` (como se fez para corrigir o teste da change 1) e a mesma verificação no teste de integração da API ("ida e volta", D15 do TDD).

### D9. Navegação

Links "Times e pessoas" e "Calendário" no cabeçalho da casca, visíveis só com sessão, com a página atual destacada (`routerLinkActive`) e `aria-current`. O protótipo mostra uma **barra lateral** com todos os itens do produto, mas hoje só existem duas telas; uma barra lateral com itens mortos seria pior que um cabeçalho curto. Quando as telas de consulta (cronograma, relatório) existirem, a navegação migra para a barra lateral: ela é um único componente, e a troca é pequena. As rotas ficam em inglês (`/teams`, `/calendar`), como a API e as pastas; os textos visíveis, em português.

### D10. Testes

- **Motor:** testes de unidade de `TeamCapacity` (nenhuma pessoa, zeros, decimais, soma).
- **API (PostgreSQL real via Testcontainers):** um conjunto por spec, com as regras de integridade verificadas **pelo banco** (índice único, `CHECK`, `RESTRICT`, `CASCADE`), as corridas de unicidade e o `401` de cada rota. O teste de migrations passa a esperar três migrations.
- **Front (Vitest):** utilitário de datas nos três fusos, serviços e componentes de cada tela, mensagens de erro e navegação.
- **Verificação manual** no fim, com um Chrome real contra a API real, como na change 2 (banco descartável e credenciais temporárias, mediante autorização do autor).

### Suposições registradas (detalhes que o TDD e o PRD não fixam)

1. Nomes de time e de pessoa: até 100 caracteres, sem espaços nas pontas; descrição de feriado: até 200.
2. Capacidade com até duas casas decimais (consistente com "horas com duas casas", DT-15).
3. Pessoas com o mesmo nome são permitidas; ausências sobrepostas da mesma pessoa também (a capacidade é zero em qualquer dia coberto).
4. `GET /api/people` e `GET /api/absences` não têm filtros nem paginação (DT-16): o filtro por time da tela é feito no navegador.
5. Referência a time inexistente no corpo é `400` em `teamId`.
6. Mudar uma pessoa de time não é bloqueado por nada nesta change; a regra "o time da pessoa precisa estar entre os times da demanda" entra com as demandas (change 5).
7. Navegação por cabeçalho agora, barra lateral quando houver mais telas (D9).

Nenhuma contradiz um DT-*; se o autor discordar de alguma, o ajuste acontece antes do `apply`.

## Risks / Trade-offs

- **Recálculo na escrita (DT-03):** o RF-03 exige recalcular previsões ao mudar ausências e feriados, e o DT-03 manda fazê-lo na mesma transação. Aqui não há previsão, então as escritas são "só gravar". → As escritas ficam em manipuladores pequenos, com um único `SaveChanges` por operação, para a change 4/5 acrescentar o recálculo sem reestruturar; essa mudança é tarefa explícita das changes seguintes.
- **Mudar de time com alocações (change 5):** a edição de pessoa aceita qualquer time hoje; quando as alocações existirem, a mesma operação precisará recusar movimentos que as invalidem. → Registrado no proposal como fora de escopo; a change 5 revisita o `PUT /api/people/{id}`.
- **Acentos na unicidade do nome:** "Mobile" e "Móbile" são nomes diferentes (D2). → Risco baixo para um setor com 7 a 10 times que o próprio gestor digita.
- **Datas de calendário:** o erro clássico de um dia a menos. → D8 e os testes em três fusos, em todas as camadas.
- **Volume de trabalho:** é a maior change até aqui (modelo, 16 operações, duas telas). → As tasks separam o indispensável do que pode esperar; os candidatos a corte, em ordem, são a coluna "próxima ausência", o filtro por time do calendário e o acabamento da confirmação de exclusão. Cortar a coluna exige ajustar a spec `registry-web`.
- **Cadastro só no computador (RNF-03):** as telas não são otimizadas para 360 px. → É o que o PRD manda; a navegação e a página inicial continuam utilizáveis no celular.
- **Enum global em JSON:** `JsonStringEnumConverter` vale para toda a API. → É o desejado; o OpenAPI passa a descrever enums como texto, e o cliente gerado reflete isso.

## Migration Plan

1. Gerar `AddCapacityRegistry` com o `dotnet-ef` e revisar o SQL (só criações; nenhuma tabela existente muda). A API a aplica na inicialização, como as anteriores.
2. Nenhuma variável de ambiente nova e nenhum pacote novo.
3. **Rollback:** reverter o merge commit. Em um banco já migrado, rodar antes `dotnet ef database update AddAuthentication`; o banco local é descartável (`docker compose down -v`).
