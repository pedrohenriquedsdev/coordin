# PRD — Coordin

- **Status:** em elaboração — seções 1 a 4 e 5.1 (Must) aprovadas
- **Última atualização:** 2026-11-10
- **Fonte original:** [`docs/references/tcc-proposal.pdf`](references/tcc-proposal.pdf)

---

## 1. Visão geral

O **Coordin** é um sistema de planejamento e apoio à decisão para a gestão de desenvolvimento. Ele transforma uma lista de demandas priorizadas, o esforço estimado de cada uma e a capacidade dos times em um cronograma de entregas mais previsível. Além de calcular datas, compara sequências alternativas de execução, aponta possíveis ociosidades e sobrecargas e recomenda alternativas de organização.

> **Objetivo central:** dar ao gestor de desenvolvimento visibilidade suficiente para responder ao gestor de produto quando cada demanda poderá ser entregue, considerando a capacidade real dos times.

**Princípio central:** o gestor sempre decide. O sistema calcula, mostra o impacto de cada alternativa e recomenda, mas nunca substitui a decisão do gestor nem a prioridade combinada entre produto e desenvolvimento.

**Envolvidos** (detalhados na seção 3):

- **Gestor de desenvolvimento:** usuário único do sistema; organiza demandas, times e capacidade e toma as decisões de planejamento.
- **Gestor de produto:** recebe as previsões de entrega e define, junto com o gestor de desenvolvimento, as prioridades.
- **Times de desenvolvimento:** fornecem as estimativas de esforço.

**Contexto de uso:** um setor de desenvolvimento real, com 7 ou mais times atuando sobre demandas concorrentes.

**Resultados esperados:**

- Maior previsibilidade das datas de entrega.
- Visão centralizada das demandas e da capacidade dos times.
- Melhor sincronização entre times que participam de uma mesma demanda.
- Identificação antecipada de ociosidade, sobrecarga e oportunidades de realocação.
- Comparação de cenários para apoiar decisões de sequenciamento.
- Mais clareza na comunicação entre os gestores de desenvolvimento e de produto.

---

## 2. Problema

O planejamento de entregas fica difícil quando há várias demandas concorrentes e diferentes times envolvidos. Nem toda demanda precisa de todos os times: algumas são executadas por um único time, enquanto outras dependem da atuação conjunta de vários. Ao mesmo tempo, um mesmo time pode atuar em diferentes demandas, dentro da sua capacidade.

**Situação atual:** hoje esse planejamento é feito de maneira manual, com ferramentas soltas, planilhas ou até mesmo apenas na cabeça do gestor. Não existe uma ferramenta voltada ao desenvolvimento para tratar esse problema.

Nesse cenário, o gestor de desenvolvimento precisa responder ao gestor de produto quando cada demanda será entregue, mas enfrenta as seguintes dificuldades:

1. **Falta de visão consolidada** sobre demandas, responsáveis, esforço e datas previstas.
2. **Distribuição desigual de trabalho**, com possibilidade de ociosidade em alguns times e sobrecarga em outros.
3. **Impacto de novas demandas pouco claro:** é difícil entender como uma nova demanda afeta o cronograma já planejado.
4. **Comparação de alternativas difícil:** é difícil comparar sequências de execução e identificar uma organização mais eficiente.
5. **Previsões dependentes das estimativas dos times:** a data de entrega depende do esforço levantado pelos próprios times, e não apenas da prioridade da demanda. Essa estimativa pode não existir quando a demanda chega: inicialmente a demanda traz apenas sua prioridade, e as demais informações são complementadas ao longo do planejamento.

**Em síntese:** o desafio não é somente acompanhar tarefas, e sim coordenar a capacidade de vários times para prever e melhorar as entregas do setor.

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

- Responder ao gestor de produto quando cada demanda poderá ser entregue, com base na capacidade real dos times.
- Ter uma visão consolidada de demandas, responsáveis, esforço e datas previstas.
- Perceber cedo ociosidade e sobrecarga nos times e encontrar times com capacidade para receber novas demandas ou apoiar outras equipes.
- Entender o impacto de uma nova demanda no cronograma já planejado.
- Comparar sequências de execução antes de decidir.

**Dores atuais:** planeja de forma manual, com ferramentas soltas, planilhas ou de cabeça, sem uma visão consolidada (ver seção 2).

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

O gestor cadastra as pessoas, associa cada uma a um time e informa a capacidade disponível dela em horas.

- Cada pessoa pertence a um único time.
- Não é possível salvar uma pessoa sem capacidade informada; capacidade zero é aceita e significa que a pessoa não contribui para as previsões.
- A capacidade de um time é derivada da capacidade das pessoas associadas a ele.
- Um time sem pessoas, ou cujas pessoas têm capacidade zero, é tratado como sem capacidade: as demandas que dependem só dele não recebem data prevista e o sistema indica o motivo.

#### RF-03 — Registrar ausências

O gestor registra ausências de uma pessoa (férias e outras ausências) com data de início e de fim.

- Durante a ausência, a pessoa não contribui com capacidade.
- Uma ausência no meio da execução de uma demanda empurra a data prevista para depois, na proporção da capacidade perdida.
- Registrar, alterar ou remover uma ausência recalcula as previsões afetadas.

#### RF-04 — Cadastrar demandas com prioridade

O gestor cadastra as demandas com sua prioridade, definida no alinhamento com o gestor de produto.

- A demanda pode ser salva **sem estimativa de esforço**.
- As demandas têm uma ordem de prioridade única e explícita, que o gestor pode alterar manualmente.
- A ordem de prioridade é a ordem de execução usada no cálculo da previsão.

#### RF-05 — Alocar times e pessoas à demanda

O gestor define quais times atuarão em cada demanda e as pessoas alocadas.

- Uma demanda pode ter um ou vários times; um time pode estar em várias demandas.
- Quando um time está em várias demandas, sua capacidade é compartilhada entre elas respeitando a ordem de prioridade, sem nunca ultrapassar a capacidade disponível.

#### RF-06 — Registrar estimativa de esforço

O gestor registra o esforço estimado de cada demanda, em horas, conforme informado pelos times.

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
- Uma demanda de menor prioridade não toma capacidade de uma de maior prioridade no mesmo time.

#### RF-09 — Aplicar margem de segurança

O sistema aplica automaticamente uma margem de segurança à previsão.

- A data prevista com margem é sempre **igual ou posterior** à data calculada sem margem; a margem nunca reduz o prazo.
- O resultado da margem é arredondado para cima.
- O gestor vê a previsão com margem e consegue identificar quanto dela vem da margem.

#### RF-10 — Tratar demandas não previsíveis

Uma demanda sem estimativa de esforço, sem data de início ou sem capacidade disponível é exibida como **não previsível**.

- Demandas não previsíveis aparecem no cronograma, sem data prevista, com o motivo indicado.
- Demandas não previsíveis não consomem capacidade dos times no cálculo das demais.
- Quando o dado faltante é registrado, a demanda passa a ser previsível e recebe data prevista.

#### RF-11 — Visualizar o cronograma

O gestor visualiza o cronograma consolidado de todas as demandas.

- Para cada demanda: prioridade, times alocados, esforço, data de início, data prevista (com margem), data de fim informada (se houver) e situação (previsível ou não previsível).
- As demandas aparecem na ordem de prioridade.
- Quando a data prevista ultrapassa a data de fim informada, a demanda é sinalizada com um alerta.
- O cronograma pode ser apresentado em reunião ao gestor de produto, sem exportação.

#### RF-12 — Registrar a conclusão real e o desvio

O gestor registra a data real de conclusão de uma demanda.

- A data real de conclusão não pode ser anterior à data de início.
- O sistema guarda a **primeira previsão** calculada para a demanda (linha de base); replanejamentos posteriores não a alteram.
- Para cada demanda concluída, o sistema mostra a data real e dois desvios, em dias: em relação à primeira previsão e em relação à previsão vigente no momento da conclusão.
