# Design

## Context

- O `Coordin.Engine` hoje só tem `Capacity/TeamCapacity.cs` (change 3) e um teste de arquitetura que garante que o assembly não referencia nada além do runtime do .NET. Nenhum código chama o motor ainda; a API passa a montar o snapshot na change 5 (DT-02, DT-03).
- As regras vêm prontas do TDD: capacidade diária (6.2, DT-10), previsibilidade (6.3), simulação (DT-21), fila por pessoa (DT-12), esforço por time (DT-11), ordem de execução (DT-13), margem (DT-22). Este design só decide **como organizar o código** e fecha detalhes que o TDD deixa em aberto. Nada aqui contradiz um `DT-*`.
- Os identificadores das entidades já são `Guid` na API (change 3), então o motor usa `Guid` para `Id`.

## Goals / Non-Goals

**Goals:**
- Um ponto de entrada puro, `ForecastEngine.Calculate(PlanningSnapshot)`, que devolve o resultado completo sem efeitos colaterais.
- Código que um estudante consiga seguir: o algoritmo cabe em poucos métodos pequenos, cada um com seus testes.
- Testes que comprovam as regras invioláveis 1, 4 e 5 e o determinismo (RNF-02, RNF-08).

**Non-Goals:**
- Dependências entre demandas (change 8). O snapshot desta change não tem dependências, mas a simulação é escrita de modo que a elegibilidade de uma parte seja uma função isolada, onde a change 8 acrescenta a regra do pré-requisito.
- Otimização de desempenho além do razoável. O teste de 200 demandas fica para a change 9 (TDD 8.1); aqui o algoritmo só não pode ser ingênuo.
- Dado "hoje" no snapshot. A previsão é baseada no plano (DT-21): parte das datas de início, não de uma data de referência. A data de referência entra quando uma change precisar dela (alerta de data de fim, change 5).

## Decisions

### D1. Organização em três pastas, sem dependências

`Planning/` (entrada: o snapshot), `Capacity/` (calendário de dias úteis e capacidade por data, junto do `TeamCapacity` existente) e `Forecasting/` (simulação, margem e saída). Tudo em `Coordin.Engine`, sem pacotes novos; o teste de arquitetura existente continua sendo a prova de pureza.

*Alternativa:* um projeto por área. Descartada: o TDD (DT-04) define um só `Coordin.Engine`, e três projetos para algo desse tamanho só criariam cerimônia.

### D2. Entrada e saída como `record`s imutáveis (DT-02)

- **Entrada** (`Planning/`): `PlanningSnapshot` com `Teams`, `People` (`Id`, `TeamId`, `DailyHours`), `Absences` (`PersonId`, `StartDate`, `EndDate`), `Holidays` (`Date`), `Demands` (`Id`, `PriorityRank`, `ExecutionRank`, `StartDate?`, `IsCompleted`, e as partes por time com `TeamId` e `EstimatedEffortHours?`), `Assignments` (`DemandId`, `PersonId`), `ExecutionOrderMode` (`FollowPriority` ou `Custom`) e `SafetyMarginPercent`.
- **Saída** (`Forecasting/`): `ForecastResult` com a lista de `DemandForecast` (`DemandId`, situação `Completed`/`Predictable`/`Unpredictable`, motivo, `ForecastStartDate`, `EndDateWithoutMargin`, `MarginBusinessDays`, `ForecastEndDate`) e a lista de `ConsumedHours` (`PersonId`, `DemandId`, `TeamId`, `Date`, `Hours`). O consumo diário é o que o relatório por time (change 6) vai agregar.
- Datas são `DateOnly` e horas são `decimal`: sem fuso e sem erro de ponto flutuante nas somas de horas (0,1 + 0,2).
- Coleções expostas como `IReadOnlyList<T>`. O snapshot é validado no construtor do motor (D6), e o resultado vem ordenado de forma estável (D5).

*Alternativa:* classes mutáveis ou as entidades do EF Core. Descartadas pelo DT-02.

### D3. Calendário de trabalho separado da simulação

Um `WorkCalendar` (criado a partir dos feriados) responde `IsBusinessDay`, `NextBusinessDay` e `AddBusinessDays`. Uma `PersonCapacityCalendar` (criada a partir das pessoas e das ausências) responde `CapacityOf(person, date)` e `CapacityOf(team, date)`. Ausências são pré-processadas em um conjunto por pessoa, para a pergunta "está ausente nesta data?" ser instantânea.

*Por quê:* a mesma pergunta "quantas horas esta pessoa tem neste dia?" será feita pelo relatório por time (change 6) e pelos cenários (change 9); ter uma peça pequena, testada isoladamente, evita repetir a regra.

### D4. Simulação em duas fases: classificar, depois simular

1. **Classificar** (`PredictabilityClassifier`): para cada demanda, concluída, `MissingStartDate`, `MissingEstimate`, `NoCapacity` ou previsível, nessa ordem de precedência (spec `forecast-predictability`). É uma função pura sobre o snapshot, sem datas de simulação.
2. **Simular** (`ForecastSimulator`): só com as demandas previsíveis. Para cada dia útil, a partir da menor data de início, para cada pessoa (ordenada por `Id`), entrega as horas da capacidade do dia à primeira parte elegível da fila dela; a sobra vai para a próxima (DT-21). Uma parte é elegível se a data de início da demanda já chegou e ainda há esforço restante; a regra de elegibilidade fica em um método só, onde a change 8 acrescentará "pré-requisitos terminados".
3. **Margem** (`SafetyMargin`): função pura que, dado o início, o fim sem margem, o percentual e o calendário, devolve os dias de margem e a data final (DT-22).

A fila de cada pessoa é montada **uma vez** antes do laço dos dias: as partes das demandas a que ela está alocada, ordenadas por ranking (de prioridade ou de execução, conforme o modo) e, no empate, por `Id` da demanda (DT-13). Assim cada dia custa só o que cada pessoa realmente consome.

*Alternativa:* fórmula fechada (esforço ÷ capacidade). Descartada no DT-21.

### D5. Ordenação e determinismo

Toda ordenação usa chave total (ranking, depois `Id`), nunca a ordem de chegada das listas. O resultado sai ordenado: demandas pela mesma ordem da fila (ranking, `Id`) e consumo por data, pessoa e demanda. Nenhum `Dictionary` é percorrido sem ordenar antes. Nenhum uso de `DateTime.Now`, `Random` ou `Guid.NewGuid` no motor (o teste de determinismo embaralha as listas e compara os resultados).

### D6. Entradas inválidas viram erro de argumento, não previsão errada

O motor valida o snapshot ao receber: capacidade negativa, esforço informado menor ou igual a zero, margem negativa, `Id`s duplicados, alocação a pessoa ou demanda inexistente, alocação de uma pessoa cujo time não é um dos times da demanda, ausência com fim antes do início. A API já valida o mesmo nas bordas (change 3), então estes erros indicam um defeito de quem monta o snapshot; falhar alto evita uma previsão silenciosamente errada.

### D7. Demanda que estoura o horizonte: refazer a simulação sem ela

Se, ao chegar a 5 anos, alguma parte não terminou, as demandas dela viram `NoCapacity` e a simulação é **refeita sem elas**, para que horas "gastas" por uma demanda sem previsão não atrasem as demais (RF-10, regra 5). Retirar uma demanda só libera capacidade, então a segunda passada não gera novas falhas e basta uma repetição.

### D8. Testes: construtor de cenários e gerador de semente fixa

- `PlanningSnapshotBuilder` (somente nos testes): API fluente para montar cenários legíveis (`.WithTeam(...)`, `.WithPerson(...)`, `.WithDemand(...)`), no padrão Arrange–Act–Assert e nomes `Metodo_Situacao_ResultadoEsperado` (TDD 8.2).
- `RandomPlanningGenerator(seed)` (somente nos testes, DT-34): gera snapshots com times, pessoas, ausências, feriados, demandas parcialmente sem dados e partes em vários times. Os invariantes rodam em centenas de snapshots por execução, sempre os mesmos: margem nunca reduz o prazo; consumo nunca passa da capacidade; esforço consumido = esforço estimado; determinismo com listas embaralhadas; demanda não previsível nunca consome horas; e, para a regra 4, adicionar um time a uma demanda nunca antecipa o fim para menos que o da parte mais lenta.

## Risks / Trade-offs

- **[Risco]** Pessoas processadas em ordem de `Id` dentro do dia fazem com que, quando duas pessoas dividem uma parte, a de menor `Id` seja a primeira a dar horas a ela, o que pode mudar a que demandas sobram horas das outras. → *Mitigação:* o resultado é determinístico (D5), cumpre o RF-08 (nenhuma demanda posterior toma capacidade de uma anterior) e o fim de cada demanda continua dependendo só da capacidade de cada time; documentado no teste "Fila da pessoa" com duas pessoas.
- **[Risco]** A previsão é baseada no plano, não no que já foi feito (DT-21). Uma demanda em andamento com data de início no passado sai com datas no passado. → *Mitigação:* é a decisão do DT-21; o replanejamento e a conclusão real corrigem. A tela de cronograma (change 5) deve mostrar isso sem alarme falso.
- **[Risco]** Erros em combinações raras (fila, ausências, vários times) (RT-03 do TDD). → *Mitigação:* os casos da seção 8.2 viram testes escritos à mão e os invariantes rodam em dados gerados.
- **[Trade-off]** Validar o snapshot por exceção (D6) em vez de devolver uma lista de erros: mais simples e honesto para uso interno; a API não expõe isso ao usuário.
- **[Trade-off]** `decimal` em vez de `double` para horas: um pouco mais lento, sem erro de arredondamento nas somas; o volume (até 200 demandas) não pede mais que isso.
