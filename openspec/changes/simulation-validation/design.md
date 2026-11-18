# Design

## Context

O TDD já fixa o desenho da validação (DT-37, seção 6.6, contrato em 5.3, tabelas em 4): um avaliador no motor, quatro rotas atrás de `Validation__Enabled`, duas tabelas novas e um conjunto embutido. Ver `proposal.md` para a motivação. Esta change implementa o back-end; o assistente na tela é a change 11b.

Fatos conferidos no código (não suposições):
- O `PlanningSnapshot` é um `record` imutável com listas de `TeamInput`, `PersonInput`, `AbsenceInput`, `HolidayInput`, `DemandInput` (com `Parts`), `AssignmentInput`, `DependencyInput`, o modo de ordem e o percentual da margem. Ele já cobre tudo que o avaliador precisa.
- `ForecastEngine.Calculate(snapshot)` devolve o `ForecastResult`; `TeamReport.Build(snapshot, result, from, to)` e `TeamReport.AlignedPeriod(from, to)` já existem e dão ociosidade, ocupação e sobrecarga por semana.
- O `PlanningTransaction.RunAsync` pega o lock do planejamento, executa uma escrita, recalcula e confirma, ou desfaz tudo. É o caminho certo para o carregamento do conjunto.
- A API tem `PlanningSnapshotLoader.LoadAsync(db, ct)` (lê o banco para um snapshot) e as entidades `Team`, `Person`, `Absence`, `Holiday`, `Demand`, `DemandTeam`, `DemandAssignment`, `DemandDependency`, `Forecast` e `PlanningSettings`.
- Há um teste de arquitetura do motor que varre o código por `DateTime`, `TimeZoneInfo`, `Random` e `Guid.NewGuid`: o avaliador novo entra nessa varredura sem trabalho extra.

## Goals / Non-Goals

**Goals:**
- Cálculo da validação 100% no motor, determinístico e testável sem banco (DT-02, RNF-02).
- Reutilizar a simulação, a margem, o relatório por time e o desvio, sem regra nova de previsão.
- Carregar o conjunto sem poder apagar o planejamento do gestor por engano (RT-11).

**Non-Goals:**
- Tela, gráficos e o assistente (change 11b); persistir o resultado; importar conjunto próprio; o questionário do gestor.

## Decisions

### D1. `ValidationEvaluator` em `Coordin.Engine/Validation/`

Entrada: `ValidationInput(PlanningSnapshot CurrentProcess, PlanningSnapshot WithCoordin, IReadOnlyDictionary<Guid, DateOnly> Baseline, ValidationParameters Parameters)`. Saída: `ValidationResult` (eficiência dos dois planejamentos, desvios, checagem das prioritárias). Funções pequenas e separadas (fatores, realizado simulado, eficiência, desvios, prioritárias), todas puras, para cada uma ter seus testes. **Alternativa:** uma classe grande. Descartada: cada regra seria difícil de testar à parte.

### D2. Fator por demanda com SplitMix64 e hash estável do id

`u` = (próximo valor do SplitMix64 `>> 11`) × 2⁻⁵³, em [0, 1). O estado inicial é `seed XOR hash64(demandId)`, com o hash FNV-1a de 64 bits sobre os 16 bytes do `Guid` em ordem fixa (`ToByteArray(bigEndian: true)`). O fator é `min + (max − min) × u`, arredondado a 6 casas; o esforço de cada parte é `esforço × fator`, arredondado a 2 casas (`MidpointRounding.AwayFromZero`), e nunca fica abaixo de 0,01. Não usa `System.Random` (a sequência para uma semente não é garantida entre versões do .NET). **Alternativa:** `Random(seed)`. Descartada pelo motivo acima.

### D3. Realizado simulado com margem zero

O planejamento do processo atual é simulado de novo com o esforço escalado e `SafetyMarginPercent = 0`; a `EndDateWithoutMargin` de cada demanda é a data real simulada. A margem zero só serve para **medir o real**: a previsão do processo atual (com margem) não é alterada, então a regra inviolável 1 não é tocada. Com os dois fatores iguais a 1, o resultado reproduz o `EndDateWithoutMargin` do processo atual (invariante testada).

### D4. Métricas de eficiência sobre um período comum

O período vai da menor data de início à maior `ForecastEndDate` entre os dois planejamentos, alinhado em semanas (`TeamReport.AlignedPeriod`). Para cada planejamento, com o `TeamReport.Build` sobre esse período:
- `idleHours`: soma da ociosidade de todos os times e semanas;
- `occupancySpreadPoints`: (maior − menor ocupação média dos times) × 100, em que a **ocupação média de um time é o total de horas alocadas dividido pelo total de capacidade no período** (média ponderada, e não a média das ocupações semanais); times sem capacidade no período ficam de fora;
- `overloadedPeriods`: pares time × semana com sobrecarga;
- `sectorEndDate`: a maior `ForecastEndDate` entre as demandas previsíveis.

O TDD 6.6 diz "ocupação média dos times" sem definir a média; a definição ponderada é minha e fica registrada aqui. **Alternativa:** média simples das ocupações semanais. Descartada: semanas de capacidade pequena pesariam como as grandes.

### D5. Desvio com sinal e absoluto

Além da média com sinal do TDD (positivo é atraso), o resultado traz a **média dos valores absolutos**, porque, com sinal, +10 e −10 se anulam e "o desvio do Coordin é menor que o da linha de base" (PRD 7.1) ficaria enganoso. O PRD define o desvio com sinal; o valor absoluto é um acréscimo que não contradiz a definição. **Isso muda o contrato do TDD 5.3** (campos `averageBaselineAbsoluteDeviationDays` e `averageCoordinAbsoluteDeviationDays`) e é proposto na última task, com a aprovação do autor.

### D6. Checagem das 5 demandas mais prioritárias

Dentre as demandas previsíveis nos dois planejamentos, as 5 de menor `PriorityRank` entram em `priorityCheck.items` com a previsão do processo atual, a do Coordin e a diferença em dias corridos. `worsenedCount` conta as de diferença positiva. O número 5 é uma constante do motor (`PriorityCheckSize`), simples de mudar. **Isso também muda o contrato do TDD 5.3** (decisão do autor em 2026-11-18, na pergunta desta change) e é proposto na última task.

### D7. Tabelas `ValidationBaseline` e `ValidationCurrentPlan`

`ValidationBaseline`: `DemandId` (chave e FK com `CASCADE`) e `BaselineDate`. `ValidationCurrentPlan`: registro único (`Id` fixo `1` com `CHECK`), `SnapshotJson` (`jsonb`) e `CapturedAt` (instante, `timestamptz`). Migration `AddValidation`; o teste que fixa a lista de migrations é atualizado. O JSON do snapshot usa `System.Text.Json` com opções fixas (enum como texto, `DateOnly` ISO) e um teste de ida e volta, para o retrato guardado reproduzir o mesmo cálculo.

### D8. Habilitação: mapear ou não mapear as rotas

`ValidationOptions` (`Validation__Enabled`, padrão `false`) lido na inicialização. `MapValidationEndpoints` só é chamado quando está ligada, então as rotas não existem e respondem `404`, **inclusive o `status`**, que é como o Angular saberá se mostra o menu (change 11b). **Alternativa:** mapear sempre e devolver `404` por um filtro. Descartada: o OpenAPI e o cliente listariam rotas que não deveriam existir em produção.

### D9. O carregamento é uma transação única, pelo `PlanningTransaction`

`POST /dataset` roda dentro do `PlanningTransaction.RunAsync` (lock, escrita e recálculo, tudo ou nada). A escrita apaga, em ordem segura para as chaves (dependências, alocações, partes, previsões, demandas, ausências, pessoas, times, feriados, linha de base e plano guardado) e recria a partir do recurso embutido, na ordem de prioridade. **Não toca** na conta do gestor nem nos refresh tokens (tabelas do Identity e `RefreshTokens` ficam fora). A margem passa a ser a do conjunto e o modo de execução volta a prioridade. A confirmação (`confirmReplaceAll`) é checada **antes** de abrir a transação.

### D10. O conjunto vem de um gerador descartável, e o arquivo é o que vale

Por decisão do autor (2026-11-18), eu gero o conteúdo e ele revisa. O gerador é um script **fora do repositório** (a estrutura de pastas do DT-04 não prevê uma pasta de ferramentas), executado uma vez com semente fixa; **o JSON gerado é o artefato versionado** e o recurso embutido. O conjunto reflete um setor típico, com alocação inicial concentrada como o gestor faria hoje, **sem ser ajustado depois de se conhecer o resultado** (evita ajustar o experimento ao resultado). Uma pausa para a revisão do autor fica nas tasks, antes de o carregador existir.

### D11. Guardar o plano atual na ordem de prioridade, uma vez

O snapshot guardado é o do banco no momento da chamada, com o modo de ordem forçado a prioridade (PRD 7.3, passo 3: "na ordem de prioridade e com a alocação inicial"). Uma segunda chamada responde `409` com `validation-current-plan-exists`, código **novo** em relação ao DT-18 (proposto na última task).

### D12. Erros e validação

`400` para `confirmReplaceAll` ausente ou falso, fatores inválidos, datas inválidas e demanda desconhecida; `409` com `validation-current-plan-missing`, `validation-baseline-incomplete` e `validation-current-plan-exists`. Mensagens em português. O cliente Angular é regenerado pelo procedimento já conhecido.

## Risks / Trade-offs

- **RT-11, apagar o planejamento do gestor.** Mitigado pela flag desligada por padrão, pela confirmação explícita, pela transação única e pelos testes de integração. Ligar a flag em produção é decisão do autor, só durante a validação.
- **Conteúdo do conjunto.** Um conjunto mal montado invalida a leitura do resultado. Mitigação: a revisão do autor antes do carregador, a revisão do gestor depois (P-03), a regra de não ajustar o conjunto ao resultado e a limitação já declarada no PRD 7.3.
- **Média ponderada da ocupação** (D4) é uma interpretação. Registrada aqui; se o autor preferir a média simples, é uma troca pequena.
- **O arredondamento do esforço** (D2) muda levemente o realizado; é determinístico e testado.
- **Tamanho da change.** É a maior das restantes; as tasks são fatiadas em grupos pequenos, com testes em cada um.

## Decisões delegadas ao autor

Aprovadas em 2026-11-18: o conjunto é gerado por mim e revisado por ele; a checagem das mais prioritárias entra no resultado; a change se divide em 11 e 11b. Aguardam a aprovação desta proposta: a **média ponderada** da ocupação (D4), a **média absoluta** do desvio (D5) e o código `validation-current-plan-exists` (D11), todos refletidos no TDD só com o aval dele.
