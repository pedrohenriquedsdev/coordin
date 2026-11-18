# Proposal

## Why

A validação por simulação (RF-24, PRD 7.3) já existe na API e no motor, mas o gestor ainda não consegue executá-la pela interface: carregar o conjunto fictício, registrar a linha de base, guardar o plano atual e comparar os resultados. Sem a tela, a validação só é acessível por chamadas HTTP, o que não atende ao objetivo do TCC de o gestor operar o sistema sozinho e ver o resultado em gráficos. A change fecha a linha 11b do roadmap (TDD 9.1) e precisa existir antes da validação ser executada (M7, 2026-11-26).

## What Changes

- Nova tela `/validation` com um assistente de quatro etapas, na ordem do PRD 7.3:
  1. **Carregar o conjunto fictício**, com confirmação explícita que informa o que será substituído (times, pessoas, demandas, previsões, linha de base e plano guardado); o gestor precisa marcar a confirmação, e a tela só envia `confirmReplaceAll: true` depois disso.
  2. **Registrar a linha de base**: uma data por demanda do conjunto, com validação de que todas estão preenchidas antes de avançar.
  3. **Guardar o plano atual**: uma ação única; depois de guardado, a tela mostra que o plano não muda mais e oferece só a visualização.
  4. **Comparar**: parâmetros de faixa de variação do esforço (mínimo e máximo) e semente, com a execução da avaliação e a apresentação do resultado.
- Resultado da comparação em **gráficos pelo `ChartComponent`** (DT-36) e **tabela com os mesmos valores**, cobrindo: eficiência dos dois planejamentos (ociosidade, dispersão da ocupação, sobrecargas e prazo do setor), desvios por demanda e na média (com sinal e absolutos) e a checagem das 5 demandas mais prioritárias.
- A área de validação só aparece no menu quando `GET /api/validation/status` responde com sucesso; com a flag `Validation__Enabled` desligada, a rota não existe e a tela não é acessível (RF-24).
- Avisos de regra do produto na tela: a carga substitui todo o planejamento, e a avaliação não grava nada.
- **Fora desta change**: qualquer mudança na API ou no motor (ambos já estão arquivados e verificados); persistência do resultado (o resultado é recalculado a cada pedido, DT-37); o questionário do gestor (PRD 7.4, respondido fora do sistema); a change 12 `delivery-insights`.

## Capabilities

### New Capabilities

- `validation-web`: a tela do assistente de validação (carregar, linha de base, plano atual e comparação), com gráficos acompanhados de tabela, visibilidade condicionada à configuração e confirmações contra perda de dados (RF-24 na tela; RNF-03; DT-36).

### Modified Capabilities

Nenhuma. O comportamento da API (`validation-api`), do conjunto (`validation-dataset`) e da avaliação (`validation-evaluation`) não muda; a change só consome esses contratos.

## Impact

- **Front-end (`src/coordin-web`)**: nova rota `/validation` em `app.routes.ts`, nova entrada de menu condicionada ao status da validação, componentes do assistente e do resultado, e uso do `ChartComponent` existente.
- **Cliente gerado (`src/coordin-web/src/app/api/`)**: nenhum, porque as funções de validação já existem desde a change 11. Se o front precisar de algum campo novo, a mudança volta para a API e para o PRD/TDD, o que exige aprovação.
- **API e motor**: nenhuma mudança.
- **Dependências**: nenhuma nova; `chart.js` já está autorizado (change 6).
- **Testes**: testes de componentes e da lógica do assistente no Vitest; verificação manual no navegador contra a API real com banco descartável, com credenciais temporárias autorizadas pelo autor, como nas changes 9b e 10.
- **Prazo**: entra antes da execução da validação (M7, 2026-11-26) e depois da change 11 integrada (PR #35 e arquivamento PR #36).
