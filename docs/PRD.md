# PRD — Coordin

- **Status:** em elaboração — seções 1 e 2 aprovadas
- **Última atualização:** 2026-11-10
- **Fonte original:** [`docs/references/tcc-proposal.pdf`](references/tcc-proposal.pdf)

---

## 1. Visão geral

O **Coordin** é um sistema de planejamento e apoio à decisão para a gestão de desenvolvimento. Ele transforma uma lista de demandas priorizadas, o esforço estimado de cada uma e a capacidade dos times em um cronograma de entregas mais previsível. Além de calcular datas, compara sequências alternativas de execução, aponta possíveis ociosidades e sobrecargas e recomenda alternativas de organização.

> **Objetivo central:** dar ao gestor de desenvolvimento visibilidade suficiente para responder ao gestor de produto quando cada demanda poderá ser entregue, considerando a capacidade real dos times.

**Princípio central:** o gestor sempre decide. O sistema calcula, mostra o impacto de cada alternativa e recomenda, mas nunca substitui a decisão do gestor nem a prioridade combinada entre produto e desenvolvimento.

**Envolvidos** (detalhados na seção 3):

- **Gestor de desenvolvimento:** usuário principal; organiza demandas, times e capacidade e toma as decisões de planejamento.
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
