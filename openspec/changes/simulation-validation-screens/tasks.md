## 1. Funções puras de apresentação

- [x] 1.1 Criar `validation/labels.ts` com a validação dos parâmetros (mínimo > 0, mínimo ≤ máximo), o texto dos códigos de erro da validação e a montagem das séries dos gráficos a partir do `ValidationResult`
- [x] 1.2 Testes de `labels.ts` (Vitest): parâmetros inválidos, cada código de erro com seu texto, séries com os mesmos valores da tabela, e prioritárias que pioram destacadas (RF-24; DT-36)

## 2. Estado do assistente e visibilidade

- [x] 2.1 Criar o store da validação com sinais: status do servidor, etapa atual derivada do status, parâmetros e datas digitados na memória, e estados de carregamento e falha (design, decisões 1 e 5)
- [x] 2.2 Testes do store: etapa derivada do status em cada combinação (conjunto não carregado, linha de base incompleta, plano guardado), e dados digitados preservados após falha (spec: estados de carregamento e falha)
- [x] 2.3 Mostrar o item "Validação" no menu da casca só quando o status responde com sucesso, e a mensagem de indisponibilidade na rota quando a API responde 404 (spec: visibilidade condicionada; design, decisão 2)
- [x] 2.4 Testes da casca: item aparece com sucesso, some com 404 e a rota mostra a mensagem de indisponibilidade

## 3. Etapas do assistente

- [x] 3.1 Etapa de carga: texto do que será substituído, caixa de confirmação, envio de `confirmReplaceAll: true` só com a caixa marcada e resumo da carga (spec: carga exige confirmação explícita; design, decisão 3)
- [x] 3.2 Etapa de linha de base: uma data por demanda na ordem de prioridade, contagem das que faltam, bloqueio de avanço e gravação por `PUT /api/validation/baseline` (spec: linha de base completa; datas de calendário sem fuso, design, decisão 6)
- [x] 3.3 Etapa do plano atual: ação única, momento do registro e tratamento de `validation-current-plan-exists` como informação (spec: plano atual guardado uma vez)
- [x] 3.4 Etapa de comparação: parâmetros com sugestão 0,9, 1,1 e 20261126, validação antes do envio, aviso de que nada é gravado e chamada de `POST /api/validation/evaluate` (spec: comparação com parâmetros informados)
- [x] 3.5 Testes de componentes das quatro etapas: caminho feliz, bloqueios (carga sem confirmação, linha de base incompleta, parâmetros inválidos) e falha com "Tentar de novo" sem perder os dados digitados
- [x] 3.6 Rota `/validation` protegida pela sessão, como as demais telas (RNF-04), com link de volta à casca

## 4. Resultado em gráficos e tabelas

- [ ] 4.1 Gráfico e tabela de eficiência dos dois planejamentos (ociosidade, sobrecargas e prazo do setor; dispersão e prazo do setor como valores isolados), usando `ChartComponent` (spec: resultado em gráfico acompanhado de tabela)
- [ ] 4.2 Gráfico e tabela de desvios: média com sinal e absoluta, e tabela por demanda com as duas previsões, a data real simulada e os dois desvios
- [ ] 4.3 Tabela da checagem das 5 demandas mais prioritárias, com a contagem das que pioram e destaque nas linhas
- [ ] 4.4 Layout em 360 px: tabela por demanda vira cartões, sem rolagem horizontal da página (RNF-03)
- [ ] 4.5 Testes de componentes do resultado: os valores do gráfico e da tabela são iguais, e a apresentação de cada seção aparece com os dados do `ValidationResult`

## 5. Verificação

- [ ] 5.1 Executar os comandos do TDD 8.6 em cópia limpa do branch: `npm ci`, `npm run lint`, `npm run format:check`, `npm test` nos fusos UTC, `TZ=Asia/Tokyo` e `TZ=America/Los_Angeles`, e `npm run build` com código 0 (informar as vulnerabilidades do `npm audit` como informativas)
- [ ] 5.2 Rodar `dotnet build Coordin.slnx` e `dotnet test Coordin.slnx` na mesma cópia para confirmar que nada da API ou do motor mudou
- [ ] 5.3 Verificação manual no Chrome headless, com API e PostgreSQL descartáveis, credenciais temporárias e `Validation__Enabled=true`, autorizada pelo autor: roteiro das quatro etapas, bloqueios, comparação repetida com valores idênticos, gráficos desenhados, tabelas com os mesmos valores, zero violações de CSP, 360 px e 1280 px sem rolagem horizontal, e a área escondida com a flag desligada
- [ ] 5.4 Rodar `openspec validate simulation-validation-screens --strict` e registrar os resultados no PR, com a diferença entre verificação local e CI
