# PRD — Coordin

- **Status:** aprovado (2026-11-10); ajustes aprovados em 2026-11-11 em RF-02, RF-03, RF-06, RF-10 e 8.1, decorrentes do TDD, e em RF-13, RF-17 e RF-23 (justificativa dos cenários e apresentação em gráficos)
- **Última atualização:** 2026-11-11
- **Fonte original:** [`docs/references/tcc-proposal.pdf`](references/tcc-proposal.pdf)

---

## 1. Visão geral

O **Coordin** é um sistema de planejamento e apoio à decisão para a gestão de desenvolvimento. Ele transforma uma lista de demandas priorizadas, o esforço estimado de cada uma e a capacidade dos times em um cronograma de entregas mais previsível. Além de calcular datas, compara sequências alternativas de execução, aponta possíveis ociosidades e sobrecargas e recomenda alternativas de organização.

> **Objetivo central:** reduzir o trabalho do gestor de desenvolvimento para encontrar ociosidade, sobrecarga e sequências de execução mais eficazes, dando a ele visibilidade sobre a capacidade real dos times. Com essa visibilidade, as previsões de entrega comunicadas ao gestor de produto passam a ser fundamentadas em dados, cálculos e alternativas descritas.

**Princípio central:** o gestor sempre decide. O sistema calcula, mostra o impacto de cada alternativa e recomenda, mas nunca substitui a decisão do gestor nem a prioridade combinada entre produto e desenvolvimento.

**Envolvidos** (detalhados na seção 3):

- **Gestor de desenvolvimento:** usuário único do sistema; organiza demandas, times e capacidade e toma as decisões de planejamento.
- **Gestor de produto:** recebe as previsões de entrega e define, junto com o gestor de desenvolvimento, as prioridades.
- **Times de desenvolvimento:** fornecem as estimativas de esforço.

**Contexto de uso:** um setor de desenvolvimento real, com 7 ou mais times atuando sobre demandas concorrentes.

**Resultados esperados:**

- Identificação antecipada de ociosidade, sobrecarga e oportunidades de realocação.
- Carga de trabalho mais equilibrada entre os times.
- Comparação de cenários para apoiar decisões de sequenciamento.
- Visão centralizada das demandas e da capacidade dos times.
- Melhor sincronização entre times que participam de uma mesma demanda.
- Maior previsibilidade das datas de entrega.
- Mais clareza na comunicação entre os gestores de desenvolvimento e de produto, como consequência dos itens anteriores.

---

## 2. Problema

O planejamento de entregas fica difícil quando há várias demandas concorrentes e diferentes times envolvidos. Nem toda demanda precisa de todos os times: algumas são executadas por um único time, enquanto outras dependem da atuação conjunta de vários. Ao mesmo tempo, um mesmo time pode atuar em diferentes demandas, dentro da sua capacidade.

**Situação atual:** hoje esse planejamento é feito de maneira manual, com ferramentas soltas, planilhas ou até mesmo apenas na cabeça do gestor. Não existe uma ferramenta voltada ao desenvolvimento para tratar esse problema.

**Dor central:** nesse cenário, encontrar ociosidades, equilibrar a carga entre os times e descobrir sequências de execução mais eficazes exige do gestor de desenvolvimento um trabalho manual grande. As dificuldades são:

1. **Ociosidade e sobrecarga difíceis de enxergar:** o trabalho fica distribuído de forma desigual, com times parados enquanto outros acumulam fila, e as oportunidades de realocação passam despercebidas.
2. **Comparação de alternativas difícil:** é difícil comparar sequências de execução e identificar uma organização mais eficaz em relação a datas e times.
3. **Impacto de novas demandas pouco claro:** é difícil entender como uma nova demanda afeta o cronograma já planejado e a carga de cada time.
4. **Falta de visão consolidada** sobre demandas, responsáveis, esforço e datas previstas.
5. **Previsões dependentes das estimativas dos times:** a data de entrega depende do esforço levantado pelos próprios times, e não apenas da prioridade da demanda. Essa estimativa pode não existir quando a demanda chega: inicialmente a demanda traz apenas sua prioridade, e as demais informações são complementadas ao longo do planejamento.

**Consequência:** sem essa análise, o gestor de desenvolvimento responde ao gestor de produto quando cada demanda será entregue com base em intuição, e a produtividade do setor fica abaixo do que a capacidade disponível dos times permitiria.

**Em síntese:** o desafio não é somente acompanhar tarefas, e sim dar ao gestor visibilidade sobre a capacidade dos times para equilibrar o trabalho e encontrar alternativas mais eficazes. Previsões confiáveis e uma comunicação clara com o produto decorrem disso.

---

## 3. Personas

O Coordin tem **um único usuário**: o gestor de desenvolvimento do setor. O gestor de produto e os times participam do processo, mas não acessam o sistema; a relação com eles passa pelo gestor de desenvolvimento.

| Papel | Usa o sistema? | Relação com o Coordin |
|---|---|---|
| Gestor de desenvolvimento | Sim — usuário único | Registra as informações, consulta previsões e relatórios e decide sobre os cenários |
| Gestor de produto | Não | Define prioridades com o gestor de desenvolvimento e recebe dele as previsões |
| Times de desenvolvimento | Não | Fornecem as estimativas de esforço, registradas pelo gestor de desenvolvimento |

### 3.1 Gestor de desenvolvimento (usuário)

Responsável pelo planejamento de entregas de todo o setor, com 7 ou mais times. Há um gestor para o setor, que mantém um planejamento único.

**Responsabilidades no processo:**

- Alinhar com o gestor de produto a lista de demandas e suas prioridades.
- Definir quais times atuarão em cada demanda, respeitando a capacidade de cada um.
- Registrar no sistema as demandas, as estimativas informadas pelos times, a alocação, a data de início e, quando aplicável, a data de fim.
- Avaliar o cronograma e os cenários alternativos, aprovando ou rejeitando as recomendações.
- Comunicar ao gestor de produto as previsões de entrega.

**Objetivos:**

- Perceber cedo ociosidade e sobrecarga nos times e encontrar times com capacidade para receber novas demandas ou apoiar outras equipes.
- Equilibrar a carga de trabalho entre os times.
- Comparar sequências de execução antes de decidir.
- Entender o impacto de uma nova demanda no cronograma já planejado.
- Ter uma visão consolidada de demandas, responsáveis, esforço e datas previstas.
- Responder ao gestor de produto quando cada demanda poderá ser entregue, com base na capacidade real dos times.

**Dores atuais:** encontrar ociosidade, equilibrar a carga e comparar alternativas de forma manual, com ferramentas soltas, planilhas ou de cabeça, sem uma visão consolidada (ver seção 2).

### 3.2 Gestor de produto (não usuário)

Define, junto com o gestor de desenvolvimento, a lista de demandas e suas prioridades. Não acessa o Coordin: recebe as previsões de entrega por meio do gestor de desenvolvimento.

**O que espera do processo:** saber quando cada demanda poderá ser entregue e ter a prioridade combinada preservada. Mudanças de sequência sugeridas pelo sistema são apenas recomendações, decididas pelo gestor de desenvolvimento.

### 3.3 Times de desenvolvimento (não usuários)

Executam as demandas. Uma demanda pode envolver um ou vários times, e um time pode atuar em diferentes demandas. Os times analisam o trabalho e informam o esforço necessário, em horas; o gestor de desenvolvimento registra essa estimativa no sistema. A estimativa pode chegar depois do cadastro da demanda.

Não acessam o Coordin, mas são diretamente afetados pelo planejamento: a distribuição de trabalho entre os times (ociosidade e sobrecarga) é uma das principais informações que o sistema expõe ao gestor.

---

## 4. Fluxo de uso

O fluxo começa no alinhamento entre produto e desenvolvimento e termina com a previsão e o acompanhamento das entregas. Todas as ações no sistema são feitas pelo gestor de desenvolvimento; as etapas que envolvem o gestor de produto ou os times acontecem fora do sistema.

```
Pré-requisito: times, pessoas e capacidade cadastrados
        │
1. Priorização → 2. Distribuição → 3. Estimativa → 4. Planejamento
        │
5. Cálculo → 6. Decisão → 7. Comunicação → 8. Acompanhamento
        │
        └── Replanejamento: qualquer mudança volta ao cálculo (4.2)
```

### 4.1 Etapas

**Pré-requisito: cadastro da capacidade.** O gestor cadastra os times, as pessoas de cada time e a capacidade disponível. Essa base é reaproveitada em todo o planejamento e atualizada quando a disponibilidade muda.

| # | Etapa | Quem | O que acontece | Resultado |
|---|---|---|---|---|
| 1 | Priorização | Gestor de desenvolvimento e gestor de produto (fora do sistema); gestor de desenvolvimento (no sistema) | Os gestores alinham a lista de demandas e suas prioridades. O gestor de desenvolvimento cadastra cada demanda com sua prioridade. | Demandas cadastradas e priorizadas, ainda sem estimativa |
| 2 | Distribuição | Gestor de desenvolvimento | Define quais times atuarão em cada demanda. Uma demanda pode envolver um ou vários times; um time pode atuar em várias demandas, respeitando sua capacidade. | Times associados a cada demanda |
| 3 | Estimativa | Times (fora do sistema); gestor de desenvolvimento (no sistema) | Os times analisam o trabalho e informam o esforço em horas. O gestor registra a estimativa. Enquanto ela não existir, a demanda permanece cadastrada como **não previsível**. | Esforço estimado registrado |
| 4 | Planejamento | Gestor de desenvolvimento | Registra a alocação (pessoas e times), a data de início e, quando necessário, uma data de fim informada por ele, além da previsão que o sistema calculará. | Demanda pronta para o cálculo |
| 5 | Cálculo | Sistema | Calcula a data prevista de conclusão de cada demanda considerando esforço, pessoas, times, disponibilidade, ordem de execução e margem de segurança. A margem só amplia o prazo, nunca o reduz, e adicionar um time não divide o prazo automaticamente. | Cronograma previsto |
| 6 | Decisão | Sistema e gestor de desenvolvimento | O sistema apresenta o cronograma, os cenários alternativos de sequência com seu impacto e o relatório por time (ocupação, ociosidade, sobrecarga). O gestor aprova ou rejeita cada recomendação; sem aprovação, nada muda. | Cronograma decidido pelo gestor |
| 7 | Comunicação | Gestor de desenvolvimento | Apresenta as previsões ao gestor de produto em reunião, usando as telas do sistema, e/ou exporta um arquivo com o cronograma para enviar a ele. | Gestor de produto informado das previsões |
| 8 | Acompanhamento | Gestor de desenvolvimento | Ao fim de cada demanda, registra a data real de conclusão. O sistema compara a data prevista com a realizada. | Desvio previsto × realizado por demanda |

### 4.2 Replanejamento

O planejamento é contínuo. O gestor volta ao cálculo (etapa 5) sempre que algo muda, por exemplo:

- chega uma nova demanda, e ele precisa ver o impacto dela no cronograma já planejado;
- uma estimativa é registrada, para uma demanda até então não previsível, ou alterada;
- a capacidade de um time ou pessoa muda;
- ele aprova um cenário com outra sequência de execução.

Em todos os casos, o sistema recalcula as previsões e mostra o impacto. Ele nunca reordena demandas sozinho: qualquer mudança de sequência é apresentada como cenário e só vale após aprovação do gestor de desenvolvimento.

---

## 5. Requisitos funcionais

Os requisitos são priorizados com **MoSCoW**:

- **Must:** indispensável para a entrega do TCC; sem ele o sistema não cumpre o objetivo central.
- **Should:** importante, entra se o prazo permitir depois dos Must.
- **Could:** desejável, só entra se sobrar tempo.

Cada requisito tem um ID `RF-*` e critérios de aceite verificáveis. O *como* (modelo de capacidade, cálculo, margem, paralelismo) é definido no TDD; os critérios abaixo descrevem apenas o comportamento esperado. Todo esforço e toda capacidade são expressos em horas.

### 5.1 Must

#### RF-01 — Cadastrar times

O gestor cadastra, edita e consulta os times do setor.

- É possível cadastrar 7 ou mais times.
- Cada time tem um nome que o identifica de forma única.

#### RF-02 — Cadastrar pessoas e sua capacidade

O gestor cadastra as pessoas, associa cada uma a um time e informa a capacidade disponível dela em horas por dia útil (segunda a sexta).

- Cada pessoa pertence a um único time.
- Não é possível salvar uma pessoa sem capacidade informada; capacidade zero é aceita e significa que a pessoa não contribui para as previsões.
- A capacidade de um time é derivada da capacidade das pessoas associadas a ele.
- Um time sem pessoas, ou cujas pessoas têm capacidade zero, é tratado como sem capacidade: as demandas que dependem só dele não recebem data prevista e o sistema indica o motivo.

#### RF-03 — Registrar ausências e feriados

O gestor registra ausências de uma pessoa (férias e outras ausências) com data de início e de fim, e cadastra os feriados do setor (datas sem expediente), válidos para todas as pessoas.

- Durante a ausência, a pessoa não contribui com capacidade.
- Em um feriado, nenhuma pessoa contribui com capacidade.
- Uma ausência ou um feriado no meio da execução de uma demanda empurra a data prevista para depois, na proporção da capacidade perdida.
- Registrar, alterar ou remover uma ausência ou um feriado recalcula as previsões afetadas.

#### RF-04 — Cadastrar demandas com prioridade

O gestor cadastra as demandas com sua prioridade, definida no alinhamento com o gestor de produto.

- A demanda pode ser salva **sem estimativa de esforço**.
- As demandas têm uma ordem de prioridade única e explícita, que o gestor pode alterar manualmente.
- A ordem de execução usada no cálculo da previsão é, por padrão, a ordem de prioridade. Ela só passa a diferir quando o gestor aprova um cenário (RF-15); mesmo assim, a prioridade registrada é preservada.

#### RF-05 — Alocar times e pessoas à demanda

O gestor define quais times atuarão em cada demanda e as pessoas alocadas.

- Uma demanda pode ter um ou vários times; um time pode estar em várias demandas.
- Quando um time está em várias demandas, sua capacidade é compartilhada entre elas respeitando a ordem de execução, sem nunca ultrapassar a capacidade disponível.

#### RF-06 — Registrar estimativa de esforço

O gestor registra o esforço estimado de cada demanda, em horas, por time alocado (a parte de cada time), conforme informado pelos times.

- O esforço total da demanda é a soma das partes dos times.
- Enquanto algum time alocado não tiver estimativa, a demanda é não previsível (RF-10).
- A estimativa pode ser registrada ou alterada a qualquer momento.
- Esforço deve ser maior que zero; zero ou valor negativo é rejeitado com mensagem clara.
- Registrar ou alterar a estimativa recalcula as previsões.

#### RF-07 — Registrar datas de início e de fim informada

O gestor registra a data de início planejada da demanda e, opcionalmente, uma data de fim informada por ele (prazo desejado ou combinado).

- A data de início é obrigatória para que a demanda receba previsão.
- A data de fim informada não altera o cálculo da previsão.

#### RF-08 — Calcular a data prevista de conclusão

O sistema calcula, para cada demanda previsível, a data prevista de conclusão a partir do esforço, da data de início, das pessoas e times alocados, da capacidade disponível (descontadas as ausências) e da ordem de execução.

- O cálculo é refeito sempre que um dado que o afeta muda (estimativa, alocação, capacidade, ausência, ordem ou data de início).
- Para os mesmos dados, o resultado é sempre o mesmo.
- Adicionar um time a uma demanda **não divide o prazo automaticamente** pelo número de times: a previsão considera a capacidade efetiva alocada.
- Uma demanda posterior na ordem de execução não toma capacidade de uma anterior no mesmo time.

#### RF-09 — Aplicar margem de segurança

O sistema aplica automaticamente uma margem de segurança à previsão.

- A data prevista com margem é sempre **igual ou posterior** à data calculada sem margem; a margem nunca reduz o prazo.
- O resultado da margem é arredondado para cima.
- O gestor vê a previsão com margem e consegue identificar quanto dela vem da margem.

#### RF-10 — Tratar demandas não previsíveis

Uma demanda sem estimativa de esforço de algum time alocado, sem data de início ou sem capacidade disponível é exibida como **não previsível**.

- Demandas não previsíveis aparecem no cronograma, sem data prevista, com o motivo indicado.
- Demandas não previsíveis não consomem capacidade dos times no cálculo das demais.
- Quando o dado faltante é registrado, a demanda passa a ser previsível e recebe data prevista.

#### RF-11 — Visualizar o cronograma

O gestor visualiza o cronograma consolidado de todas as demandas.

- Para cada demanda: prioridade, times alocados, esforço, data de início, data prevista (com margem), data de fim informada (se houver) e situação (previsível ou não previsível).
- As demandas aparecem na ordem de execução, com a prioridade visível ao lado.
- Quando a data prevista ultrapassa a data de fim informada, a demanda é sinalizada com um alerta.
- O cronograma pode ser apresentado em reunião ao gestor de produto, sem exportação.

#### RF-12 — Registrar a conclusão real e o desvio

O gestor registra a data real de conclusão de uma demanda.

- A data real de conclusão não pode ser anterior à data de início.
- O sistema guarda a **primeira previsão** calculada para a demanda (linha de base); replanejamentos posteriores não a alteram.
- Para cada demanda concluída, o sistema mostra a data real e dois desvios, em dias: em relação à primeira previsão e em relação à previsão vigente no momento da conclusão.

#### RF-17 — Relatório por time

O RF-17 passou de Should para Must em 2026-11-12, por atender diretamente à dor central (seção 2); o ID foi mantido para preservar as referências.

Para cada time, o sistema mostra por período a capacidade disponível (descontadas as ausências), as horas alocadas e a ocupação.

- **Ociosidade:** período em que o time tem capacidade sem trabalho alocado; o relatório indica quantas horas estão livres.
- **Sobrecarga:** período em que o trabalho na fila do time excede sua capacidade, empurrando entregas para depois; o relatório indica as demandas afetadas.
- O relatório permite identificar times com capacidade livre para receber novas demandas ou apoiar outros times.
- Demandas não previsíveis do time não entram nas horas alocadas, mas o relatório informa quantas existem.
- A capacidade disponível e as horas alocadas de cada time por período também são apresentadas em gráfico, com ociosidade e sobrecarga destacadas, e os mesmos valores ficam disponíveis em tabela.
- O tamanho do período (semana, mês) é decidido no TDD.

### 5.2 Should

O relatório de planejamento geral previsto na proposta (comparar sequências e seu impacto no cronograma do setor) é atendido pelos cenários (RF-13 a RF-15). O critério que define um cenário "melhor" (prazo total, entregas prioritárias, utilização ou combinação) é decidido no TDD.

#### RF-13 — Recomendar cenários de sequenciamento

O sistema analisa ordens de execução alternativas e recomenda as que melhoram o resultado geral do planejamento.

- Cada cenário mostra a sequência proposta e, para cada demanda, as novas datas de início e prevista, com a diferença em dias em relação ao cronograma atual.
- Cada cenário mostra também o efeito no conjunto do setor (por exemplo, a data de conclusão da última demanda).
- A prioridade registrada de cada demanda aparece ao lado da nova posição, deixando visível o que muda em relação a ela.
- Cada cenário mostra por que é melhor: o valor do critério de comparação (decidido no TDD) no cronograma atual e no cenário, e as demandas que mais contribuem para a diferença.
- A comparação entre o cronograma atual e o cenário também é apresentada em gráfico (por exemplo, a data prevista de cada demanda antes e depois), com os mesmos valores disponíveis em tabela.
- Quando nenhuma alternativa melhora o resultado, o sistema informa isso.
- Nenhum cenário é aplicado automaticamente.

#### RF-14 — Simular uma sequência proposta pelo gestor

O gestor monta uma ordem de execução alternativa (por exemplo, trocar a posição de duas demandas) e o sistema calcula o impacto.

- O resultado é apresentado no mesmo formato dos cenários recomendados (RF-13).
- A simulação não altera o cronograma vigente até ser aprovada (RF-15).

#### RF-15 — Aprovar ou rejeitar um cenário

O gestor aprova ou rejeita cada cenário, recomendado ou simulado.

- Ao aprovar, a ordem de execução passa a ser a do cenário e as previsões são recalculadas; a prioridade registrada não muda.
- Ao rejeitar, nada muda no cronograma.
- O cronograma indica quando a ordem de execução difere da ordem de prioridade e quais demandas foram deslocadas.
- O gestor pode voltar a ordem de execução para a ordem de prioridade a qualquer momento.

#### RF-16 — Simular o impacto de uma nova demanda

Antes de incluir uma nova demanda no planejamento, o gestor simula seu efeito no cronograma.

- O sistema mostra quais demandas têm a data prevista alterada e em quantos dias.
- O gestor confirma a inclusão, que passa a valer no cronograma, ou descarta a simulação, sem nenhum efeito.

#### RF-18 — Dependências entre demandas

O gestor registra que uma demanda só pode começar depois que outra terminar.

- A demanda dependente nunca tem início previsto antes da conclusão prevista da demanda de que depende, mesmo que sua data de início registrada seja anterior.
- Dependências em cadeia (A → B → C) são respeitadas.
- Uma dependência que forme ciclo (A → B → A, direta ou indiretamente) é rejeitada com mensagem clara.
- Se a demanda de que se depende for não previsível, a dependente também fica não previsível, com o motivo indicado.
- Cenários (RF-13 e RF-14) nunca violam dependências.
- Dependências são entre demandas inteiras, não entre etapas de uma demanda.

#### RF-19 — Exportar o cronograma em planilha

O gestor exporta o cronograma para um arquivo de planilha, para enviar ao gestor de produto.

- O arquivo contém, por demanda, as mesmas informações do cronograma (RF-11), incluindo prioridade e ordem de execução.
- O arquivo abre em ferramentas de planilha de uso comum.
- O conteúdo reflete o cronograma vigente no momento da exportação.

### 5.3 Could

Detalham a comparação entre previsto e realizado, além do mínimo garantido pelo RF-12. Também atendem à identificação de possíveis gaps de produtividade por time prevista na proposta, entendidos aqui como diferenças recorrentes entre o estimado e o realizado, sem métricas de produtividade individual.

#### RF-20 — Histórico de estimativas

O sistema registra cada alteração na estimativa de esforço de uma demanda.

- Cada registro guarda o valor anterior, o novo valor e a data da alteração.
- O gestor pode informar um motivo opcional para a alteração.
- O histórico não pode ser editado nem apagado; o gestor o consulta por demanda.

#### RF-21 — Histórico da data prevista

O sistema registra a evolução da data prevista de cada demanda ao longo do tempo.

- Cada vez que a data prevista de uma demanda muda, o sistema guarda a data anterior, a nova, quando mudou e o evento que causou a mudança (estimativa, alocação, capacidade, ausência, dependência, cenário aprovado ou nova demanda).
- O primeiro registro é a primeira previsão (linha de base do RF-12).
- O gestor consulta o histórico por demanda, em ordem cronológica.

#### RF-22 — Registrar o esforço real

Ao registrar a conclusão de uma demanda (RF-12), o gestor pode informar as horas realmente gastas.

- O esforço real é opcional: a conclusão pode ser registrada sem ele.
- Quando informado, deve ser maior que zero.
- O sistema mostra o esforço estimado (o vigente na conclusão), o real e a diferença, em horas e em percentual.
- O esforço real é registrado para a demanda como um todo, não por time.

#### RF-23 — Indicadores de precisão

O sistema apresenta indicadores de precisão calculados sobre as demandas concluídas, para o setor e para cada time.

- **Desvio médio de prazo**, em dias, em relação à primeira previsão e em relação à previsão vigente na conclusão.
- **Percentual de demandas concluídas até a data prevista**, considerando a primeira previsão.
- **Diferença média entre esforço estimado e real**, em percentual, apenas para as demandas com esforço real registrado (RF-22).
- Os indicadores também são apresentados em gráfico, permitindo comparar os times entre si e com o setor.
- O gestor filtra os indicadores por período de conclusão.
- Uma demanda com vários times entra nos indicadores de cada time envolvido.
- Times com diferença recorrente entre estimado e realizado ficam evidenciados na comparação entre times.

---

## 6. Requisitos não funcionais e regras invioláveis

### 6.1 Regras invioláveis

Valem para todo o sistema. Nenhum requisito, cenário ou decisão técnica pode contrariá-las; qualquer mudança exige revisão e aprovação deste PRD.

| # | Regra | Onde se aplica |
|---|---|---|
| 1 | A margem de segurança nunca reduz o prazo: ela só pode ampliar a previsão. | RF-09 |
| 2 | O sistema nunca reordena demandas sozinho. Mudança de sequência é sempre um cenário apresentado com seu impacto, aguardando aprovação ou rejeição do gestor. | RF-13, RF-14, RF-15 |
| 3 | A prioridade definida entre produto e desenvolvimento é preservada; alternativas aparecem como recomendação, nunca como substituição silenciosa. | RF-04, RF-13, RF-15 |
| 4 | Adicionar um time não divide o prazo automaticamente pela metade: a previsão considera a capacidade efetiva. | RF-05, RF-08 |
| 5 | O esforço pode não existir quando a demanda é cadastrada: o sistema aceita demandas sem estimativa e as trata como não previsíveis até que a estimativa chegue. | RF-04, RF-10 |

### 6.2 Requisitos não funcionais

#### RNF-01 — Desempenho

- Com até **15 times, 150 pessoas e 200 demandas**, o cronograma recalculado após qualquer alteração aparece para o gestor em até **2 segundos**.
- Nesse mesmo volume, os cenários recomendados (RF-13) são apresentados em até **10 segundos**.

#### RNF-02 — Determinismo do cálculo

- Para os mesmos dados de entrada, previsões, cenários e indicadores são sempre os mesmos, independentemente de quando ou quantas vezes o cálculo é executado.

#### RNF-03 — Acesso pelo navegador, no computador e no celular

- O sistema é usado pelo navegador, em computador e em celular, sem instalação.
- **No computador**, todas as funcionalidades estão disponíveis.
- **No celular**, o gestor consulta o cronograma, as previsões e os relatórios (RF-11, RF-12, RF-17 e RF-23) em telas a partir de 360 px de largura, sem rolagem horizontal da página. Cadastros, simulações e aprovação de cenários são feitos no computador.
- O cronograma é legível quando projetado em reunião.

#### RNF-04 — Acesso protegido

- O acesso exige login do gestor; sem autenticação, nenhum dado é exibido.
- Senhas nunca são armazenadas nem exibidas em texto puro.

#### RNF-05 — Dados pessoais mínimos

- Das pessoas, o sistema guarda apenas o necessário para o planejamento: nome, time, capacidade e ausências (tipo e período), sem motivo detalhado da ausência.
- Nenhum dado real do setor ou credencial é versionado no repositório do projeto.

#### RNF-06 — Persistência

- Toda alteração confirmada pelo gestor permanece salva entre sessões e dispositivos; fechar o navegador não perde dados.

#### RNF-07 — Idioma e unidades

- A interface é em português do Brasil.
- Esforço e capacidade são sempre exibidos em horas; datas consideram o fuso `America/Sao_Paulo`.

#### RNF-08 — Verificabilidade das regras de cálculo

- Os critérios de aceite dos requisitos de cálculo (RF-02, RF-03, RF-05, RF-08 a RF-10, RF-12 e RF-18) e as regras invioláveis são verificados por testes automatizados, incluindo casos de borda: esforço ausente, time sem capacidade, ausência no meio do período, vários times na mesma demanda, dependência em cadeia, ciclo de dependência e margem arredondada para cima.

---

## 7. Métricas de sucesso

### 7.1 Métrica principal: desvio entre previsto e realizado

- **Definição:** para cada demanda concluída, desvio = data real de conclusão − data prevista, em dias. Valor positivo indica atraso; negativo, entrega antecipada.
- **Leituras:** em relação à primeira previsão (linha de base da demanda) e à previsão vigente na conclusão (RF-12). A primeira mostra a qualidade da previsão original; a segunda, o efeito dos replanejamentos.
- **Agregação:** desvio médio e percentual de demandas concluídas até a data prevista, no setor e por time (RF-23).
- **Meta:** não há meta numérica, porque o setor não tem medição atual do atraso. O sucesso é o desvio do Coordin ser **menor que a linha de base** do processo atual (7.2).

### 7.2 Validação no TCC com demandas históricas

O prazo do TCC não permite acompanhar demandas reais do início à conclusão com o sistema. A métrica principal é demonstrada com demandas já concluídas no setor:

1. Selecionar demandas concluídas, com seus dados **anonimizados** (sem nomes reais de pessoas, times ou clientes).
2. Cadastrar os times, as pessoas, a capacidade e as ausências do período, além das estimativas e datas de início que existiam quando cada demanda começou.
3. Calcular a previsão do Coordin para cada demanda e comparar com a data real de conclusão: este é o **desvio do Coordin**.
4. Comparar com a **linha de base**: o desvio entre a data real e a data que foi comunicada na época pelo processo manual, para as demandas em que esse registro existir.
5. Considerar a validação bem-sucedida se o desvio médio do Coordin for menor que o da linha de base.

A existência de registros das datas comunicadas na época ainda não foi confirmada (premissa na seção 9). **Plano B:** sem esses registros, a validação apresenta o desvio do Coordin nas demandas históricas sem comparação com a linha de base, e o resultado é avaliado junto com o gestor de desenvolvimento.

### 7.3 Métricas secundárias

| Métrica | Como medir | Resultado esperado relacionado | Requisitos |
|---|---|---|---|
| Demandas previsíveis | Percentual de demandas cadastradas que têm data prevista | Visão centralizada das demandas e da capacidade | RF-10, RF-11 |
| Ociosidade e sobrecarga detectadas | Horas ociosas e períodos de sobrecarga identificados por time | Identificação antecipada de ociosidade, sobrecarga e realocação | RF-17 |
| Cenários avaliados | Quantidade de cenários recomendados ou simulados, e quantos foram aprovados ou rejeitados | Comparação de cenários para apoiar o sequenciamento | RF-13 a RF-15 |
| Avaliação do gestor | Questionário curto respondido pelo gestor de desenvolvimento após usar o sistema na validação (clareza das previsões, utilidade dos cenários e relatórios, apoio à comunicação com o gestor de produto) | Previsibilidade e comunicação mais clara entre os gestores | — |

Métricas que dependem de requisitos Should ou Could só são medidas se esses requisitos forem entregues.

---

## 8. Escopo e não objetivos

### 8.1 No escopo

| Prioridade | Requisitos | O que cobre |
|---|---|---|
| Must | RF-01 a RF-12 e RF-17 | Cadastros de times, pessoas, capacidade, ausências, feriados e demandas; alocação; estimativas; cálculo da previsão com margem de segurança; demandas não previsíveis; cronograma; conclusão real e desvio; relatório por time com ociosidade e sobrecarga |
| Should | RF-13 a RF-16, RF-18 e RF-19 | Cenários de sequenciamento (recomendados e simulados) com aprovação do gestor; simulação de nova demanda; dependências entre demandas; exportação em planilha |
| Could | RF-20 a RF-23 | Histórico de estimativas e da data prevista; esforço real; indicadores de precisão |
| Não funcionais | RNF-01 a RNF-08 | Valem para tudo o que for entregue |

O escopo é protegido na ordem **Must → Should → Could**. Se o prazo exigir cortes, eles começam pelos Could, são propostos com justificativa e dependem de aprovação; nenhum Must é cortado sem decisão explícita.

### 8.2 Não objetivos

O Coordin **não** vai:

- **Dar acesso ao gestor de produto nem aos times.** O gestor de desenvolvimento é o único usuário (seção 3).
- **Ter vários gestores ou perfis de acesso.** Há um único planejamento, mantido por um gestor.
- **Atender vários setores ou organizações.** O sistema cobre um único setor de desenvolvimento.
- **Permitir cadastros, simulações ou aprovações pelo celular.** No celular, apenas consulta (RNF-03).
- **Integrar-se a ferramentas externas** (planilhas, Jira, Azure DevOps ou similares), nem importar dados delas. Os dados são cadastrados no Coordin; a exportação em planilha (RF-19) é o único canal de saída.
- **Substituir a gestão de tarefas do dia a dia** dos times (quadros, subtarefas, status diário). O Coordin planeja demandas, não acompanha tarefas.
- **Enviar notificações** (e-mail, push ou mensagens). O gestor consulta o sistema; alertas aparecem apenas na tela (por exemplo, RF-11).
- **Controlar apontamento de horas.** Existe apenas o esforço real total, opcional, informado na conclusão (RF-22).
- **Medir produtividade individual.** Indicadores são por time e por setor (RF-23).
- **Reordenar demandas automaticamente.** Toda mudança de sequência é um cenário que depende de aprovação (regra inviolável 2).
- **Representar etapas dentro de uma demanda** nem dependências entre etapas (RF-18).
- **Dividir uma pessoa entre vários times** (RF-02).

---

## 9. Premissas, riscos e decisões delegadas ao TDD

### 9.1 Premissas

| ID | Premissa | Se não se confirmar |
|---|---|---|
| P-01 | O sistema é usado em um setor de desenvolvimento real, com 7 ou mais times, cujas estimativas são feitas em horas. | Revisar volume (RNF-01) e unidade de esforço. |
| P-02 | O gestor de desenvolvimento tem as informações de capacidade (pessoas, horas disponíveis e ausências) para cadastrar. | As previsões ficam não previsíveis ou imprecisas; reduzir a validação ao que houver de dado. |
| P-03 | Existem demandas concluídas com estimativa, data de início e data real de conclusão para a validação (7.2). | A métrica principal não pode ser demonstrada com dados reais; usar dados de demonstração e deixar a limitação explícita no TCC. |
| P-04 | Existem registros das datas comunicadas ao produto na época dessas demandas (linha de base). **A confirmar.** | Aplicar o plano B do 7.2: desvio do Coordin sem comparação com a linha de base. |
| P-05 | O gestor de desenvolvimento está disponível para usar o sistema na validação e responder ao questionário (7.3). | A avaliação do gestor deixa de ser medida. |

### 9.2 Riscos

| ID | Risco | Impacto | Mitigação |
|---|---|---|---|
| R-01 | **Prazo:** 23 requisitos funcionais, TDD e implementação até 2026-11-28. | Should e Could não entregues. | Proteger Must → Should → Could (seção 8); revisar o andamento ao fim de cada etapa e propor cortes cedo. |
| R-02 | **Hospedagem:** o uso no computador e no celular (RNF-03) exige que o sistema esteja acessível pela rede. | Sem hospedagem, o celular não acessa os dados; custo e configuração extras. | Decidir hospedagem e implantação no TDD, cedo no roadmap. |
| R-03 | **Dados históricos insuficientes** para a validação (P-03, P-04). | Métrica principal sem demonstração com dados reais ou sem linha de base. | Verificar os dados disponíveis antes da implementação; plano B do 7.2. |
| R-04 | **Cadastro manual do histórico:** sem importação (seção 8), as demandas da validação são digitadas uma a uma. | Horas de cadastro perto do prazo. | Limitar a validação a uma amostra de demandas definida antes; reservar tempo nos marcos (seção 10). |
| R-05 | **Agregação da métrica principal depende do RF-23 (Could).** | Sem média e % no prazo calculados pelo sistema. | Calcular a partir da exportação (RF-19) ou manualmente na validação. |
| R-06 | **Complexidade do cálculo:** ausências, vários times por demanda, dependências e busca de cenários com até 200 demandas. | Erros de previsão ou cenários acima do tempo do RNF-01. | Motor de cálculo isolado e testado (RNF-08); método de busca de cenários definido no TDD respeitando o RNF-01. |
| R-07 | **Exposição de dados reais** do setor na validação ou no repositório. | Problema de confidencialidade e de dados pessoais. | Anonimizar os dados da validação; nada real versionado (RNF-05). |
| R-08 | **Telas no celular:** o cronograma tem muitas informações para telas de 360 px. | Retrabalho de interface. | Celular só para consulta (RNF-03); definir no TDD a apresentação compacta. |

### 9.3 Decisões delegadas ao TDD

O PRD define o comportamento; as decisões abaixo definem **como** atendê-lo e devem ser fechadas no TDD, cada uma como `DT-*`.

| # | Decisão | Requisitos afetados |
|---|---|---|
| 1 | **Modelo de capacidade:** como representar horas disponíveis por pessoa e por time ao longo do tempo, ausências e calendário (dias úteis, fins de semana e feriados). | RF-02, RF-03, RF-08, RF-17 |
| 2 | **Paralelismo:** como estimar a duração quando vários times atuam na mesma demanda, sem presumir divisão exata do prazo. | RF-05, RF-08 |
| 3 | **Dependências:** como representá-las e detectar ciclos. | RF-18 |
| 4 | **Margem de segurança:** se é percentual, fixa ou configurável, em que etapa do cálculo é aplicada e em que unidade é arredondada para cima. | RF-09 |
| 5 | **Critério de otimização** dos cenários: prazo total, entregas prioritárias, utilização dos times ou combinação, e o método de busca. | RF-13 |
| 6 | **Registro de mudanças** de estimativa e de previsão, e comparação previsto × realizado. | RF-12, RF-20, RF-21, RF-23 |
| 7 | **Tamanho do período** do relatório por time (semana, mês). | RF-17 |
| 8 | **Formato do arquivo** exportado (por exemplo, CSV ou XLSX). | RF-19 |
| 9 | **Stack, autenticação e hospedagem**, incluindo a confirmação da preferência por C#/.NET. | RNF-01, RNF-03, RNF-04, RNF-06 |

---

## 10. Marcos até 2026-11-28

Em 2026-11-28 tudo precisa estar pronto: software funcionando, documentação (PRD, TDD, specs) e apresentação. Não há entregas intermediárias fixas; os marcos abaixo são o planejamento interno. Dedicação prevista: até cerca de 10 horas por dia, aproximadamente 180 horas no total, sem folga para imprevistos.

| # | Marco | Data limite | Pronto quando |
|---|---|---|---|
| M0 | PRD aprovado | 2026-11-10 | Seções 1 a 10 aprovadas |
| M1 | Repositório integrado | 2026-11-11 | Template de PR criado; `chore/project-setup` e `docs/prd` integradas à `main` por PR |
| M2 | Dados da validação verificados | 2026-11-13 | Confirmado se existem demandas históricas e registros das datas comunicadas (P-03, P-04); amostra da validação definida (R-04) |
| M3 | TDD aprovado | 2026-11-13 | Decisões da seção 9.3 fechadas como `DT-*`; roadmap de changes definido; hospedagem decidida (R-02) |
| M4 | Must entregues | 2026-11-21 | RF-01 a RF-12 e RF-17 implementados, testados (RNF-08) e acessíveis pela rede (RNF-03) |
| M5 | Should entregues | 2026-11-24 | RF-13 a RF-16, RF-18 e RF-19 implementados e testados |
| M6 | Could e congelamento | 2026-11-25 | Could que couberem no prazo implementados; a partir daqui, só correções |
| M7 | Validação executada | 2026-11-26 | Histórico cadastrado, desvios medidos (seção 7) e questionário respondido pelo gestor |
| M8 | Entrega pronta | 2026-11-27 | Documentação revisada, apresentação pronta e ensaiada |
| — | Entrega do TCC | 2026-11-28 | — |

### 10.1 Pontos de decisão de corte

- **Em 2026-11-21 (M4):** se os Must não estiverem completos, os Could saem do escopo e os Should são reavaliados um a um.
- **Em 2026-11-24 (M5):** se os Should não estiverem completos, os Could saem do escopo e os Should restantes são reavaliados.
- **A validação (M7) e a entrega (M8) não são cortadas:** se necessário, a implementação de Should e Could cede tempo a elas.
- Todo corte é proposto com justificativa e decidido pelo autor do TCC, nunca aplicado silenciosamente.
