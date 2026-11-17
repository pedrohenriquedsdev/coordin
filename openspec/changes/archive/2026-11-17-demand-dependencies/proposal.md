# Proposal

## Why

Hoje o motor trata toda demanda como independente: uma demanda só espera pela data de início cadastrada e pela fila das pessoas. Mas o gestor sabe que, às vezes, uma demanda só pode começar depois que outra termina, e prever a entrega ignorando isso gera datas falsamente otimistas (RF-18). A change 9 (cenários de sequenciamento) também depende disto: o RF-18 exige que nenhum cenário viole uma dependência, e isso só é verificável se o motor já a respeitar.

## What Changes

- **Motor:** o `PlanningSnapshot` passa a trazer as dependências (cada demanda com a lista de seus pré-requisitos). A simulação só torna uma parte elegível a partir do dia útil seguinte ao `ForecastEndDate` **com margem** do pré-requisito (DT-23), mesmo que a data de início registrada seja anterior; cadeias (A → B → C) são respeitadas.
- **Motor:** um detector de ciclos por busca em profundidade (marcação em três estados) devolve o caminho do ciclo; o validador do snapshot rejeita ciclo e autodependência como defeito de quem montou o snapshot.
- **Motor:** a não previsibilidade se propaga. Uma demanda cujo pré-requisito (direto ou indireto) é não previsível fica não previsível com o novo motivo `PrerequisiteUnpredictable`, inclusive quando o pré-requisito estoura o horizonte de cinco anos. Pré-requisito já concluído conta como cumprido.
- **Motor:** a retirada de demandas que estouram o horizonte passa a escolher só entre as que não esperam nenhuma outra, e leva junto os dependentes (sem isso, uma dependente de maior prioridade seria apontada por engano).
- **API:** o agregado da demanda ganha `prerequisiteIds` (lista de ids) no corpo de `POST` e `PUT` e na resposta. Nova tabela `DemandDependencies` com chave composta e `CHECK` contra autodependência. Ciclo responde `409` com `code` `dependency-cycle` e mensagem em português com os títulos do ciclo; a exclusão de uma demanda que é pré-requisito de outra responde `409` com `demand-has-dependents`. O recálculo continua na mesma transação da escrita.
- **API:** o cronograma traz, em cada item, os pré-requisitos (id e título) e passa a devolver o motivo `PrerequisiteUnpredictable`. O cliente Angular é regenerado.
- **Tela:** o formulário de demanda ganha a escolha dos pré-requisitos (entre as demais demandas), com o erro de ciclo mostrado em palavras; o cronograma lista os pré-requisitos de cada demanda e escreve o novo motivo em português.
- **Documentos:** o TDD recebe o código `demand-has-dependents` (novo) no DT-18, o nome `prerequisiteIds` no contrato da demanda (5.3, no lugar de `prerequisiteDemandIds`) e `prerequisites` no exemplo do cronograma. `dependency-cycle`, o motivo `PrerequisiteUnpredictable` e a precedência na seção 6.3 já constavam. Ajuste de documento aprovado pelo autor em 2026-11-17.

Nenhuma mudança incompatível: o campo novo é opcional e a ausência dele equivale a nenhuma dependência.

## Capabilities

### New Capabilities

- `demand-dependencies`: cadastro das dependências entre demandas inteiras pelo agregado da demanda, rejeição de ciclos e autodependência, bloqueio da exclusão de pré-requisito e a leitura dos pré-requisitos (RF-18; DT-23).

### Modified Capabilities

- `forecast-predictability`: novo motivo `PrerequisiteUnpredictable`, na ordem de precedência, e propagação da não previsibilidade pelos dependentes.
- `delivery-forecast`: elegibilidade pelos pré-requisitos (dia útil seguinte ao fim com margem), cadeias, início previsto nunca anterior ao fim do pré-requisito e retirada correta de quem estoura o horizonte.
- `schedule-view`: cada item traz os pré-requisitos e o motivo novo.
- `demands-web`: escolha dos pré-requisitos no formulário e erro de ciclo.
- `schedule-web`: exibição dos pré-requisitos e do motivo novo em português.

## Impact

- **Código:** `src/Coordin.Engine` (`Planning/`, `Forecasting/`), `src/Coordin.Api` (`Features/Demands`, `Features/Planning`, migration `AddDemandDependencies`), `src/coordin-web` (formulário, cronograma, rótulos, cliente gerado).
- **API:** campo novo em `DemandRequest`/`DemandResponse` e em `ScheduleItemResponse`; dois códigos de erro `409`; valor novo no motivo de não previsibilidade.
- **Banco:** tabela `DemandDependencies`, com `ON DELETE RESTRICT` no pré-requisito e `CASCADE` na dependente.
- **Dependências de pacote:** nenhuma.
- **Roadmap:** linha 8 do TDD 9.1 (`demand-dependencies`, `feat/demand-dependencies`, Should, marco em 2026-11-22). **Requisitos:** RF-18. **Decisões:** DT-23 (também toca DT-18, DT-21 e DT-22). RNF-02 (determinismo) e RNF-08 (testes dos casos de borda: dependência em cadeia e ciclo) continuam valendo. As regras invioláveis 1, 2 e 3 não são afetadas: a margem só amplia, o sistema não reordena nada e a prioridade é preservada.

## Fora desta change

- Dependências entre **etapas** de uma demanda (o RF-18 diz que são entre demandas inteiras).
- Cenários de sequenciamento e a verificação de que nenhum cenário viola dependências (change 9; aqui só o motor passa a respeitá-las).
- Exportação do cronograma com os pré-requisitos (change 10).
- Arrastar para criar dependências ou visualização em grafo: a escolha é por lista no formulário.
- Reordenar demandas automaticamente por causa de uma dependência: a ordem do gestor é mantida (regra inviolável 2); a dependente só espera.
