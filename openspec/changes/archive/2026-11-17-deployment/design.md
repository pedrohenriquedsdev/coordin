# Design

## Context

- A imagem única (DT-01, change 1) já serve a API e o front na mesma origem; o Dockerfile já separa o build do Angular, o publish do .NET e a imagem final de runtime, e roda como usuário sem privilégios.
- A API já trata o proxy da hospedagem: `UseForwardedHeaders` com `X-Forwarded-Proto`, redirecionamento HTTPS e HSTS só em produção (change 1); o health check `GET /api/health` existe (change 1); as migrations rodam ao iniciar (DT-07).
- Os segredos já têm nomes fixos: `ConnectionStrings__Default`, `Jwt__SigningKey`, `Manager__Email`, `Manager__Password` (DT-29, DT-30).
- Documentação do Render, verificada pelo agente de documentação em 2026-11-16 (confirmar nas páginas ao configurar):
  - web services devem escutar em `0.0.0.0` na porta da variável `PORT`; o padrão é `10000`;
  - Docker é suportado com build a partir do Dockerfile do repositório;
  - PostgreSQL: versões 13 a 18 disponíveis, com 18 como padrão;
  - plano gratuito: o web service dorme após 15 minutos sem tráfego; o PostgreSQL gratuito expira 30 dias após a criação, com 14 dias de carência antes da exclusão; 750 horas gratuitas por workspace por mês.

## Goals / Non-Goals

**Goals:**
- A mesma imagem roda no Render sem alteração de código por ambiente.
- A implantação pode ser repetida (blueprint + roteiro), e trocar de plano é mudança de configuração, não de código.

**Non-Goals:**
- Escalar para mais de uma instância, backups automáticos, domínio próprio com certificado e monitoramento: dependem do plano pago e de decisão posterior do autor.
- Alterar qualquer regra de cálculo, endpoint ou tela.

## Decisions

### D1. A porta vem da variável `PORT`, lida no código

`Program.cs` chama uma função pura `ListenUrls.Resolve(string? port)` que devolve `http://+:{porta}`, com `8080` quando `PORT` não existe e erro de configuração quando o valor não é um inteiro entre 1 e 65535. Ela é chamada antes do `Build()`: `builder.WebHost.UseUrls(...)`. O Dockerfile deixa de definir `ASPNETCORE_URLS`.

*Alternativa:* deixar `ASPNETCORE_URLS=http://+:8080` e confiar na detecção automática da porta pelo Render. Descartada: a documentação diz que a leitura de `PORT` é a abordagem recomendada e que a detecção automática pode falhar (o deploy falha quando a porta não é detectada).

### D2. O blueprint descreve o ambiente; o painel guarda os segredos

`render.yaml` tem um serviço `type: web`, `runtime: docker`, `dockerfilePath: ./Dockerfile`, `healthCheckPath: /api/health`, `plan: free` (decisão do autor), e um banco `databases:` de PostgreSQL com `plan: free` e `postgresMajorVersion: "18"`. A `ConnectionStrings__Default` é ligada ao banco por `fromDatabase` (a URL interna). `Jwt__SigningKey` usa `generateValue: true` (o Render gera um valor aleatório), e `Manager__Email` e `Manager__Password` usam `sync: false` (o autor preenche no painel). Nenhum valor literal de segredo aparece no arquivo.

*Alternativa:* criar tudo pelo painel, sem arquivo. Descartada: não fica versionado e não se repete igual.

### D3. O roteiro é do autor; Claude não cria serviços

Criar a conta, os serviços e preencher os segredos no painel do Render são ações na conta do autor e envolvem credenciais; CLAUDE.md §6 e §11 pedem a autorização do autor. Claude escreve o roteiro (`docs/deployment.md`) e valida o que é validável sem a conta: a imagem constrói, a aplicação sobe com `PORT` definido, o health check responde, e o blueprint é um YAML válido com os campos exigidos.

### D4. Plano gratuito como ambiente de teste

Registrado no proposal: o banco expira em 30 dias e o serviço dorme. O roteiro inclui o aviso e o que trocar para o plano pago (`plan:` no `render.yaml`). A decisão de plano para a entrega final fica como pendência do autor antes de 2026-11-28.

## Risks / Trade-offs

- **[Risco]** O Render pode mudar a documentação ou os nomes dos campos do blueprint. → *Mitigação:* o roteiro manda conferir o campo na documentação ao criar; a validação local do YAML cobre a sintaxe, não o schema do Render.
- **[Risco]** O serviço gratuito dorme e a primeira requisição demora cerca de um minuto. → *Mitigação:* o roteiro avisa; a verificação ponta a ponta é feita depois de acordar o serviço.
- **[Risco]** Dados gratuitos expiram. → *Mitigação:* o ambiente é de teste; o roteiro registra o risco e a decisão de plano pago fica explícita.
- **[Trade-off]** Ler `PORT` no código adiciona uma função a testar, em troca de a imagem funcionar em qualquer hospedagem que defina `PORT` (o padrão da maioria).
