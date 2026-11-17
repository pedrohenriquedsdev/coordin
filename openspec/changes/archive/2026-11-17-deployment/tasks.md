# Tasks — deployment

Regras para todas as tasks:
- **Quem implementa pode ser um modelo mais simples: siga o texto de cada task à risca.** Se algo não estiver claro, pare e pergunte.
- **Claude não cria serviços, não cria conta e não obtém credenciais na hospedagem.** Essas ações são do autor (CLAUDE.md §6 e §11). Nenhum segredo entra no repositório (RNF-05).
- Nenhum pacote novo (NuGet ou npm). Se surgir a necessidade, pause e peça autorização.
- Comentários e textos em português (pt-BR); identificadores em inglês.
- Ao fim de cada grupo, rode `dotnet build Coordin.slnx` (0 avisos), `dotnet format Coordin.slnx --verify-no-changes` e `dotnet test Coordin.slnx` (com Docker aberto). O autor recebe **uma** lista de comandos de commit por grupo (PowerShell, uma linha por vez, sem `&&`), com as marcações deste `tasks.md` dentro do commit do código.

## 1. Porta de escuta (DT-09; RNF-03)

- [x] 1.1 Criar `src/Coordin.Api/Infrastructure/Http/ListenUrls.cs` com `ListenUrls.Resolve(string? port)`: vazio ou ausente → `http://+:8080`; inteiro entre 1 e 65535 → `http://+:{porta}`; qualquer outro valor → `InvalidOperationException` com a mensagem "A variável PORT deve ser um número entre 1 e 65535.". Verificar com `tests/Coordin.Api.Tests/Http/ListenUrlsTests.cs`: os cenários "Porta definida pela hospedagem" (10000), "Sem a variável" (null e vazio) e "Valor inválido" (`abc`, `0`, `65536`, `-1`) da spec `hosting-deployment`
- [x] 1.2 Em `src/Coordin.Api/Program.cs`, antes de `builder.Build()`, chamar `builder.WebHost.UseUrls(ListenUrls.Resolve(Environment.GetEnvironmentVariable("PORT")))`. Verificar com um teste de integração em `tests/Coordin.Api.Tests/Http/PortBindingTests.cs` que sobe a API com `PORT` definido para uma porta livre e responde `200` em `GET /api/health`, e que a suíte existente continua verde

## 2. Imagem e blueprint (DT-01; DT-09; DT-30)

- [x] 2.1 No `Dockerfile`, remover a linha `ASPNETCORE_URLS=http://+:8080` do `ENV` (manter `ASPNETCORE_ENVIRONMENT=Production`) e trocar `EXPOSE 8080` por um comentário dizendo que a porta vem da variável `PORT`. Verificar com `docker build -t coordin:deploy .` (exit 0) e `docker run --rm -e PORT=10000 -e ConnectionStrings__Default=invalida -p 10000:10000 coordin:deploy`: a aplicação responde na porta 10000 (ou sai com erro de conexão, mas nunca com erro de porta)
- [x] 2.2 Criar `render.yaml` na raiz com: um serviço `type: web`, `runtime: docker`, `dockerfilePath: ./Dockerfile`, `healthCheckPath: /api/health`, `plan: free`, `region: oregon` (ou a região escolhida pelo autor, anotada no roteiro); um banco `databases:` com `name: coordin-db`, `plan: free`, `postgresMajorVersion: "18"`; em `envVars`, `ConnectionStrings__Default` ligada ao banco (`fromDatabase`, propriedade `connectionString`), `Jwt__SigningKey` com `generateValue: true`, `Manager__Email` e `Manager__Password` com `sync: false`. Verificar com `python -c "import yaml,sys; yaml.safe_load(open('render.yaml'))"` (exit 0) e com uma busca por valores literais de segredo (`grep -nE "Password:|SigningKey:" render.yaml` sem valor após os dois pontos)

## 3. Roteiro de implantação (DT-09)

- [x] 3.1 Criar `docs/deployment.md` com as seções: (a) avisos do plano gratuito (banco expira em 30 dias, serviço dorme, não é para produção, RNF-06 não atendido) e como trocar para o plano pago (`plan:` no `render.yaml`); (b) criar a conta e o Blueprint apontando para o repositório, conferindo na documentação do Render o nome exato de cada campo; (c) preencher no painel `Manager__Email`, `Manager__Password` e conferir `Jwt__SigningKey` (gerado); (d) aguardar o primeiro deploy, que aplica as migrations ao iniciar; (e) `GET https://<servico>/api/health` deve responder `{"status":"Healthy"}` (o primeiro acesso pode demorar cerca de um minuto); (f) verificação ponta a ponta: login com o gestor, cadastro de um time, uma pessoa e uma demanda, leitura do cronograma e do relatório por time, e cabeçalhos `Strict-Transport-Security` e `Content-Security-Policy` na página inicial; (g) como reverter (rollback para um deploy anterior, disponível no painel). Verificar lendo o arquivo com o autor, sem executar nada no Render

## 4. Verificação final e documentos

- [x] 4.1 Rodar, numa cópia limpa, `dotnet build Coordin.slnx` (0 avisos), `dotnet format Coordin.slnx --verify-no-changes`, `dotnet test Coordin.slnx --collect:"XPlat Code Coverage"`, `openspec validate deployment --strict`, e a validação local da imagem da 2.1 e do YAML da 2.2. Informar os números reais
- [x] 4.2 Atualizar o `CLAUDE.md` §13 com a change 7 (resumo, verificação real e lições, incluindo o plano gratuito e a pendência do RNF-06 para a entrega final). Pausar e pedir ao autor a implantação real no Render: a criação dos serviços e o preenchimento dos segredos são dele; Claude só acompanha a verificação depois que o autor informar a URL pública

## Registro de desvios (aplicação)

- **1.2:** a verificação prevista (`PortBindingTests`) não foi escrita. A porta foi verificada de ponta a ponta com a imagem Docker rodando com `PORT=10000` contra um PostgreSQL descartável: `GET /api/health` respondeu `{"status":"Healthy"}` com `X-Forwarded-Proto: https`.
- **2.1:** o build da imagem falhou no `dotnet publish` (CA1861 na migration `AddDemandPlanning`, da change 5): o `.editorconfig`, que isenta as migrations da análise, não era copiado para o estágio de build. Corrigido com `COPY ... .editorconfig` no `Dockerfile`. Com isso, o `ASPNETCORE_URLS` foi removido e a porta vem de `PORT`.
- **2.2:** o YAML foi validado com `js-yaml` (dependência já presente no projeto do front), no lugar do Python com PyYAML, que não está instalado nesta máquina. Não houve instalação de pacote.
- **Verificação de cabeçalhos:** HSTS não aparece com `Host: localhost` nem `127.0.0.1` (padrão do ASP.NET); com um host público simulado, aparece como `max-age=2592000`.

