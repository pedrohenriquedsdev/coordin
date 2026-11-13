# Proposal

## Why

O Coordin transforma esforço e capacidade em previsões de entrega, mas hoje o sistema não sabe quem trabalha, em que time, com quantas horas por dia, nem quando essas pessoas estão ausentes. Sem esse cadastro não há capacidade, e sem capacidade o motor de previsão (change 4) e as demandas (change 5) não têm sobre o que calcular. É também a primeira tela de negócio do gestor, e a primeira vez que a proteção por padrão da change 2 protege dados de verdade.

## What Changes

Linha do roadmap (TDD 9.1): change 3 · `capacity-registry` · branch `feat/capacity-registry` · "Times, pessoas com capacidade diária, ausências e feriados (API e telas)" · RF-01, RF-02, RF-03; DT-10 · Must · data alvo 2026-11-15.

- **Times (RF-01):** cadastrar, editar, consultar e excluir times. O nome é único sem diferenciar maiúsculas. Um time com pessoas não pode ser excluído. A resposta traz a quantidade de pessoas e a capacidade diária do time, derivada da capacidade das pessoas.
- **Pessoas e capacidade (RF-02):** cadastrar, editar (inclusive mudar de time), consultar e excluir pessoas. Cada pessoa pertence a um único time e tem capacidade em horas por dia útil, de 0 a 24, obrigatória (zero é aceito e significa que a pessoa não contribui). Excluir uma pessoa remove as ausências dela.
- **Ausências e feriados (RF-03):** registrar, editar, consultar e excluir ausências de uma pessoa (férias ou outra, só tipo e período, sem motivo) e cadastrar e remover os feriados do setor (data única, descrição opcional).
- **Contrato novo:** `GET /api/absences`, que lista as ausências de todas as pessoas. O TDD 5.2 só previa a listagem por pessoa, mas a tela do protótipo T08 mostra todas de uma vez com filtro por time. O acréscimo foi aprovado pelo autor em 2026-11-13 e é registrado na tabela 5.2 do TDD dentro desta change.
- **Erros novos (DT-18):** `409` com `team-has-people` (já previsto no DT-18), e dois códigos de mesmo estilo: `team-name-taken` e `holiday-date-taken`.
- **Motor (DT-02):** a regra "a capacidade de um time é a soma da capacidade das pessoas" fica em `Coordin.Engine`, pura e testada. A change 4 a estende com o calendário.
- **Banco:** migration `AddCapacityRegistry` com `Teams`, `People`, `Absences` e `Holidays`, com as regras de integridade do TDD 4.2 garantidas pelo próprio banco (unicidade, chaves estrangeiras e faixas de valores).
- **Front (protótipos T07 e T08):** navegação entre as telas, tela "Times e pessoas" (lista de times e pessoas do time) e tela "Calendário" (ausências e feriados), com datas de calendário tratadas sem fuso, para não "andarem" um dia (lição da change 1).
- **Contrato gerado:** o cliente tipado do Angular é regenerado a partir do OpenAPI.
- **Pacotes novos:** nenhum (EF Core, Npgsql e formulários reativos do Angular já fazem parte do projeto).

## Capabilities

### New Capabilities

- `team-registry`: cadastro de times (nome único, exclusão protegida) e a capacidade diária derivada do time.
- `people-registry`: cadastro de pessoas, cada uma em um único time, com capacidade em horas por dia útil.
- `capacity-calendar`: ausências por pessoa e feriados do setor, as datas que tiram capacidade.
- `registry-web`: telas "Times e pessoas" e "Calendário" e a navegação entre elas.

### Modified Capabilities

Nenhuma. As specs vigentes continuam verdadeiras: os endpoints novos herdam a proteção por padrão da spec `authentication` (uma exigência por spec nova garante isso), e a navegação é aditiva à casca da spec `web-shell`.

## Impact

- **Código:** `src/Coordin.Engine` (capacidade do time), `src/Coordin.Api` (features de times, pessoas e calendário, configuração do JSON para enums, migration), `src/coordin-web` (navegação, rotas, telas, utilitário de datas de calendário, cliente gerado) e os projetos de teste.
- **API:** 16 operações novas sob `/api/teams`, `/api/people`, `/api/absences` e `/api/holidays`; todas exigem o gestor autenticado.
- **Dados:** quatro tabelas novas; nenhuma tabela existente muda.
- **Documentos:** TDD 5.2 (acréscimo de `GET /api/absences`) e `CLAUDE.md` (§3 e §13), com a aprovação do autor.
- **Requisitos e decisões atendidos:** RF-01, RF-02 e RF-03 (cadastros); RNF-05 (só tipo e período, nunca o motivo de uma ausência) e RNF-07 (pt-BR e datas `dd/MM/yyyy`); DT-10, DT-15, DT-16, DT-18 e DT-19; DT-02 (regra de capacidade no motor).

## Fora desta change

- **Previsão e recálculo.** O RF-03 diz que registrar, alterar ou remover uma ausência ou um feriado recalcula as previsões afetadas. Ainda não existem previsões: o motor chega na change 4 e as demandas na change 5, que passam a recalcular na escrita (DT-03). Esta change só guarda os dados.
- **Demandas, alocações e estimativas** (changes 4 e 5). Por isso o `409 person-has-open-assignments` do DT-18 e a regra "o time da pessoa precisa estar entre os times da demanda" ao mudar uma pessoa de time ficam para a change 5.
- **Relatório de ocupação** (change 6) e **conjunto fictício da simulação**, que entra só na change 11 (RF-24).
- **Cadastros no celular:** o PRD (RNF-03) manda os cadastros para o computador; as telas são pensadas para a largura de computador.
- Importação em massa, histórico de alterações dos cadastros, jornadas irregulares (horas por dia da semana) e mais de um usuário.
