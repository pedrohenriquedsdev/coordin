# Implantação: Render (aplicação) e Neon (banco)

Roteiro para colocar o Coordin no ar sem custo. A aplicação roda no **Render** (web service Docker, plano gratuito) e o banco PostgreSQL fica no **Neon** (plano gratuito). O arquivo `render.yaml` da raiz descreve o web service.

## a) Limites do plano gratuito

- **Render (web service):** o serviço dorme após 15 minutos sem tráfego. A primeira requisição depois disso leva cerca de um minuto. Não deve ser usado para produção.
- **Neon (banco):** a computação suspende após 5 minutos sem uso e volta sozinha na próxima consulta; os dados não são apagados. Limite de 1 GB por projeto. A documentação lida não menciona expiração de banco inativo, mas **confira no cadastro e nos termos** antes de depender disso.
- **Cartão de crédito:** não consegui confirmar nas documentações se o cadastro gratuito do Render ou do Neon pede cartão. Se algum deles pedir, pare e me avise antes de continuar.
- **RNF-06 (dados salvos):** atendido no banco do Neon, enquanto o plano gratuito estiver ativo. Antes da entrega final (2026-11-28), confirme que o projeto continua ativo e faça um backup do banco.

## b) Criar o banco no Neon

1. Crie uma conta em `neon.com` (ou entre na sua).
2. Crie um **projeto** com o nome `coordin`, na região mais próxima do Brasil disponível no painel.
3. Abra a tela de conexão do projeto e copie a string de conexão do PostgreSQL. Ela tem o formato `postgresql://usuario:senha@host/banco?sslmode=require`.
4. Monte a string no formato que o Coordin usa (`Host=...;Database=...;Username=...;Password=...;SSL Mode=Require`), trocando os valores da sua string. Guarde essa versão para o passo c.

Não coloque essa string em nenhum arquivo do repositório.

## c) Criar o web service no Render

1. Crie ou entre na sua conta no Render.
2. Crie um **Blueprint** apontando para o repositório `pedrohenriquedsdev/coordin`, branch `main`.
3. Confira na documentação do Render o nome exato de cada campo antes de confirmar.
4. O Render mostra o web service `coordin` para criar. Não há banco no Render: é o Neon.
5. Antes do primeiro deploy, preencha no painel do web service:
   - `ConnectionStrings__Default`: a string montada no passo b.4;
   - `Manager__Email`: o e-mail do gestor, que será a conta de acesso;
   - `Manager__Password`: a senha do gestor (use uma senha forte e guarde-a em um gerenciador de senhas).
   - `Jwt__SigningKey` já vem gerada pelo Render; não precisa preencher.

A região `oregon` está no arquivo como padrão. Se preferir outra, altere `region:` no `render.yaml` antes de criar o Blueprint.

## d) Aguardar o primeiro deploy

O Render constrói a imagem a partir do `Dockerfile` e sobe o serviço. Na inicialização, a API aplica as migrations pendentes ao banco do Neon (DT-07), então o primeiro deploy cria as tabelas sem passo manual. Acompanhe o log do serviço no painel.

## e) Conferir o health check

Abra no navegador ou por `curl`:

```
https://<endereco-do-servico>/api/health
```

Resposta esperada: `{"status":"Healthy"}`. O primeiro acesso pode demorar cerca de um minuto, por causa do plano gratuito.

Se a resposta for `503`, o banco não está acessível: confira se o projeto do Neon está ativo e se `ConnectionStrings__Default` está correta no painel do Render.

## f) Verificação ponta a ponta

Com o serviço acordado, siga este roteiro no navegador (computador e celular):

1. **Login** com o e-mail e a senha do gestor. Esperado: entra na tela inicial.
2. **Times e pessoas**: cadastre um time e uma pessoa com capacidade diária. Esperado: aparecem na lista.
3. **Demandas**: cadastre uma demanda com estimativa e um time. Esperado: aparece no cronograma com uma data prevista.
4. **Cronograma**: a tela inicial mostra a demanda com a previsão e a margem de segurança. Esperado: a data prevista não é anterior à calculada sem a margem (regra 1 do `CLAUDE.md` §7.1).
5. **Relatório por time**: abra `Relatório por time` e escolha o período. Esperado: a tabela e o gráfico mostram ocupação e ociosidade, com os mesmos valores.
6. **Cabeçalhos HTTPS**: na página inicial, confira pelas ferramentas do navegador (aba de rede) que a resposta traz `Strict-Transport-Security` e `Content-Security-Policy`.
7. **Celular**: abra o endereço em um celular. Esperado: a tela cabe na largura, sem rolagem horizontal.

Se algum passo falhar, anote o passo e a mensagem exibida antes de mexer em qualquer coisa.

## g) Reverter um deploy

No painel do web service, abra o histórico de deploys e escolha um deploy anterior para voltar. O banco não é revertido por esse procedimento: mudanças de esquema já aplicadas pelas migrations continuam.
