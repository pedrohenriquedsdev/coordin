# Proposal

## Why

Os requisitos Must só servem ao gestor se estiverem no ar: o acesso pelo computador e pelo celular exige o sistema acessível pela rede (RNF-03; PRD R-02), e o marco M4 (2026-11-21) fecha os Must acessíveis pela rede. A DT-09 decidiu uma imagem Docker em PaaS com PostgreSQL gerenciado, e o autor escolheu o **Render** em 2026-11-16. Esta change coloca a imagem única do DT-01 no Render, com o banco gerenciado, e deixa um roteiro para repetir a implantação.

## What Changes

- **Porta do ambiente:** a API passa a escutar na porta da variável `PORT`, que o Render define (padrão 10000 no Render; 8080 quando a variável não existe). A porta fixa `8080` do Dockerfile sai.
- **Dockerfile:** deixa de fixar `ASPNETCORE_URLS`; a porta vem do código.
- **Blueprint `render.yaml`:** descreve o web service (Docker, caminho de health check `/api/health`, região) e o PostgreSQL 18, com a string de conexão ligada ao banco, e os segredos (`Jwt__SigningKey`, `Manager__Email`, `Manager__Password`) marcados para serem preenchidos no painel, nunca gravados no repositório (DT-30).
- **Roteiro de implantação** (`docs/deployment.md`): passo a passo para criar os serviços no Render a partir do blueprint, preencher os segredos, aplicar as migrations, conferir `/api/health`, abrir o login e fazer o roteiro de verificação de ponta a ponta.
- **Plano gratuito (decisão do autor, 2026-11-16):** o ambiente é de **testes**. A documentação oficial diz que o PostgreSQL gratuito expira em 30 dias e que o web service gratuito dorme após 15 minutos sem tráfego e não deve ser usado em produção. Por isso o roteiro registra essas limitações e o que muda para o plano pago. O RNF-06 (dados salvos) **não é atendido** no plano gratuito; isso fica como pendência explícita para a entrega final.

Roadmap (TDD 9.1): change 7, `deployment`, branch `chore/deployment`, Must, data alvo 2026-11-21 (M4).
Atende: **DT-09** (hospedagem), **DT-30** (segredos e transporte), **DT-01** (imagem única), **DT-07** (migrations na inicialização); **RNF-03** (acesso pela rede), **RNF-06** (parcial: depende do plano); **RNF-05** (nenhum segredo no repositório).

## Capabilities

### New Capabilities

- `hosting-deployment`: a API escuta na porta do ambiente, lê a configuração de variáveis de ambiente, responde ao health check da hospedagem, aplica as migrations na inicialização e é implantada por um blueprint reproduzível.

### Modified Capabilities

Nenhuma. O health check (`service-health`) e o transporte HTTPS (`http-security`) continuam como estão; esta change só os usa na hospedagem.

## Impact

- **Código:** `src/Coordin.Api/Program.cs` (leitura de `PORT`) e uma função pura para resolver a URL de escuta, com testes; `Dockerfile`.
- **Infraestrutura (arquivos novos):** `render.yaml` na raiz e `docs/deployment.md`.
- **Não muda:** API, banco, migrations, front, pacotes NuGet e npm, CI. Nenhuma credencial entra no repositório.
- **Ações fora do repositório (do autor, não de Claude):** criar a conta e os serviços no Render, preencher os segredos no painel, e apontar o domínio. Claude não cria serviços nem obtém credenciais (CLAUDE.md §6 e §11).
- **Risco de prazo:** o M4 é 2026-11-21, e esta é a última change Must. A parte de código é pequena; o trabalho restante é de configuração no painel, feito pelo autor.
- **Risco de dados:** no plano gratuito, o banco expira em 30 dias e os dados podem ser apagados depois de mais 14 dias de carência. Antes da entrega final (2026-11-28), a decisão de plano pago precisa ser tomada.
