# Implantação no Render

Roteiro para colocar o Coordin no ar na hospedagem escolhida (DT-09): um web service Docker e um PostgreSQL gerenciado, ambos no Render, a partir do arquivo `render.yaml` da raiz do repositório.

## a) Antes de começar: plano gratuito é ambiente de teste

O `render.yaml` usa o **plano gratuito** (decisão do autor, 2026-11-16). Pela documentação oficial do Render:

- o **PostgreSQL gratuito expira 30 dias** após a criação; depois há 14 dias de carência, e então o banco e os dados são apagados;
- o **web service gratuito dorme** após 15 minutos sem tráfego; a primeira requisição depois disso leva cerca de um minuto;
- o serviço gratuito **não deve ser usado para produção**.

Consequência: o RNF-06 (dados salvos entre sessões) **não é atendido** neste ambiente. Ele serve para testes e para a demonstração. Antes da entrega final (2026-11-28), é preciso decidir o plano pago.

Para trocar de plano, altere `plan: free` para o plano pago desejado nos dois blocos (`services` e `databases`) do `render.yaml`, depois faça um novo deploy. Confira os nomes e valores de plano na página de planos do Render no momento da troca.

## b) Criar os serviços a partir do blueprint

1. Crie ou entre na sua conta no Render.
2. Crie um **Blueprint** apontando para o repositório `pedrohenriquedsdev/coordin` e a branch `main`.
3. Confira na documentação do Render o nome exato de cada campo do `render.yaml` antes de confirmar (o Render pode mudar a interface e os nomes).
4. O Render mostra os dois recursos a criar: o web service `coordin` e o banco `coordin-db`.

A região `oregon` está no arquivo como padrão. Se preferir outra, altere `region:` no `render.yaml` antes de criar o Blueprint.

## c) Preencher os segredos no painel

O repositório **não** contém senhas. Antes do primeiro deploy, preencha no painel do web service:

- `Manager__Email`: e-mail do gestor, que será a conta de acesso;
- `Manager__Password`: senha do gestor (use uma senha forte e guarde-a em um gerenciador de senhas);
- `Jwt__SigningKey`: já vem gerada pelo Render (`generateValue: true`); não precisa preencher.

`ConnectionStrings__Default` é ligada automaticamente ao banco `coordin-db`.

## d) Aguardar o primeiro deploy

O Render constrói a imagem a partir do `Dockerfile` e sobe o serviço. Na inicialização, a API aplica as migrations pendentes ao banco (DT-07), então o primeiro deploy cria as tabelas sem nenhum passo manual. Acompanhe o log do serviço no painel até aparecer a linha de inicialização da aplicação.

## e) Conferir o health check

Abra no navegador ou por `curl`:

```
https://<endereco-do-servico>/api/health
```

Resposta esperada: `{"status":"Healthy"}`. O primeiro acesso pode demorar cerca de um minuto, por causa do plano gratuito.

Se a resposta for `503`, o banco não está acessível: confira no painel se o `coordin-db` está ativo e se a variável `ConnectionStrings__Default` está ligada a ele.

## f) Verificação ponta a ponta

Com o serviço acordado, siga este roteiro no navegador (computador e celular):

1. **Login** com o e-mail e a senha do gestor. Esperado: entra na tela inicial.
2. **Times e pessoas**: cadastre um time e uma pessoa com capacidade diária. Esperado: aparecem na lista.
3. **Demandas**: cadastre uma demanda com estimativa e um time. Esperado: aparece no cronograma com uma data prevista.
4. **Cronograma**: a tela inicial mostra a demanda com a previsão e a margem de segurança. Esperado: a data prevista não é anterior à calculada sem a margem (regra 1 do `CLAUDE.md` §7.1).
5. **Relatório por time**: abra `Relatório por time` e escolha o período. Esperado: a tabela e o gráfico mostram ocupação e ociosidade, e a tabela tem os mesmos valores do gráfico.
6. **Cabeçalhos HTTPS**: na página inicial, confira pelas ferramentas do navegador (aba de rede) que a resposta traz `Strict-Transport-Security` e `Content-Security-Policy`. Esperado: ambos presentes.
7. **Celular**: abra o endereço em um celular. Esperado: a tela cabe na largura, sem rolagem horizontal.

Se algum passo falhar, anote o passo e a mensagem exibida antes de mexer em qualquer coisa.

## g) Reverter um deploy

No painel do web service, abra o histórico de deploys e escolha um deploy anterior para voltar. O Render mantém os deploys recentes para rollback. O banco não é revertido por esse procedimento: mudanças de esquema já aplicadas pelas migrations continuam.
