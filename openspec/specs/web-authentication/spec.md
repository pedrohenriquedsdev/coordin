# web-authentication Specification

## Purpose
Define como a interface exige o login do gestor, mantém a sessão sem guardar o token no navegador e a renova sem interromper o trabalho. Implementa o protótipo T01 (Login) sobre a API de autenticação (DT-28; RNF-03, RNF-04, RNF-07).

## Requirements

### Requirement: Tela de login
A interface SHALL ter a tela de login em `/login`, em português do Brasil, com os campos "E-mail" e "Senha" e o botão "Entrar" (protótipo T01; RNF-07).

#### Scenario: Login com sucesso
- **WHEN** o gestor preenche e-mail e senha corretos e aciona "Entrar"
- **THEN** a interface o leva à página que ele tentava abrir ou, sem destino anterior, à página inicial

#### Scenario: Credenciais inválidas
- **WHEN** a API responde `401` ao login
- **THEN** a tela permanece em `/login` e mostra "E-mail ou senha inválidos. Após 5 tentativas erradas, o acesso é bloqueado por 15 minutos."

#### Scenario: Campos obrigatórios
- **WHEN** o gestor aciona "Entrar" com e-mail ou senha vazios
- **THEN** a tela indica o campo faltante e não chama a API

#### Scenario: Muitas tentativas
- **WHEN** a API responde `429` ao login
- **THEN** a tela informa, em português, que há tentativas demais e que se deve aguardar para tentar de novo

#### Scenario: Senha oculta
- **WHEN** a tela de login é exibida
- **THEN** o campo de senha oculta o texto digitado

### Requirement: Rotas protegidas por guarda
Toda rota da interface, exceto `/login`, SHALL exigir sessão ativa; sem sessão, a guarda SHALL redirecionar para `/login` guardando o destino pedido (RNF-04).

#### Scenario: Sem sessão
- **WHEN** alguém sem sessão abre a página inicial `/`
- **THEN** é redirecionado para `/login` e nenhum conteúdo da página inicial é exibido

#### Scenario: Retorno ao destino
- **WHEN** alguém sem sessão abre `/cronograma`, é levado ao login e entra com sucesso
- **THEN** a interface abre `/cronograma`

#### Scenario: Login já feito
- **WHEN** o gestor com sessão ativa abre `/login`
- **THEN** é redirecionado para a página inicial

### Requirement: Token de acesso só em memória
A interface SHALL manter o token de acesso apenas na memória da aplicação, sem gravá-lo em `localStorage`, `sessionStorage`, IndexedDB ou cookie legível por JavaScript (DT-28).

#### Scenario: Armazenamento do navegador
- **WHEN** o gestor faz login
- **THEN** nenhum armazenamento do navegador contém o token de acesso

### Requirement: Envio do token e renovação automática
Um interceptor HTTP SHALL enviar `Authorization: Bearer` nas chamadas à API e, ao receber `401` de um endpoint protegido, renovar a sessão uma única vez e repetir a chamada; várias chamadas que falham ao mesmo tempo SHALL compartilhar uma única renovação (DT-28).

#### Scenario: Token enviado
- **WHEN** a interface chama um endpoint protegido com sessão ativa
- **THEN** a requisição leva o cabeçalho `Authorization: Bearer <accessToken>`

#### Scenario: Token expirado
- **WHEN** um endpoint protegido responde `401` e a renovação funciona
- **THEN** a interface repete a chamada com o novo token e o gestor não percebe a interrupção

#### Scenario: Chamadas simultâneas
- **WHEN** três chamadas respondem `401` ao mesmo tempo
- **THEN** apenas uma chamada a `refresh` é feita e as três são repetidas com o novo token

#### Scenario: Renovação falha
- **WHEN** a chamada a `refresh` responde `401`
- **THEN** a sessão é encerrada, o token é descartado e o gestor é levado a `/login`

#### Scenario: Endpoints de autenticação
- **WHEN** a interface chama `login`, `refresh` ou `logout`
- **THEN** não tenta renovar a sessão nem repete a chamada em caso de `401`

### Requirement: Sessão restaurada ao abrir ou recarregar a página
Ao iniciar, a interface SHALL tentar renovar a sessão pelo cookie antes de decidir a rota, para que recarregar a página (que apaga o token em memória) não exija novo login enquanto o refresh token for válido (DT-28).

#### Scenario: Recarregar com sessão válida
- **WHEN** o gestor com sessão ativa recarrega a página
- **THEN** a interface renova o token e permanece na mesma rota, sem passar pela tela de login

#### Scenario: Abrir sem sessão válida
- **WHEN** a interface abre sem cookie válido
- **THEN** o gestor é levado a `/login`

### Requirement: Saída da sessão
A casca SHALL exibir o botão "Sair" quando houver sessão, e ao acioná-lo a interface SHALL chamar `logout`, descartar o token e levar o gestor a `/login` (protótipo T01).

#### Scenario: Sair
- **WHEN** o gestor aciona "Sair"
- **THEN** a interface chama `logout`, descarta o token em memória e abre `/login`
- **AND** voltar com o botão do navegador não exibe conteúdo protegido

#### Scenario: Botão oculto no login
- **WHEN** a tela `/login` é exibida
- **THEN** o botão "Sair" não aparece

### Requirement: Login utilizável do celular ao computador
A tela de login SHALL ser utilizável a partir de 360 px de largura, sem rolagem horizontal (RNF-03).

#### Scenario: Celular
- **WHEN** `/login` é aberta em uma janela de 360 px de largura
- **THEN** campos e botão aparecem por inteiro e não há rolagem horizontal

#### Scenario: Computador
- **WHEN** `/login` é aberta em uma janela de 1280 px de largura
- **THEN** o formulário aparece centralizado e legível
