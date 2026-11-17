import { ScenarioItem } from './scenarios';

export type MoveDirection = 'up' | 'down';

/** Quantas demandas o gráfico de justificativa mostra (D4): as que mais contribuem para a diferença do critério. */
export const CHART_LIMIT = 10;

/**
 * Monta a ordem de execução **completa** que a API exige (todas as demandas, uma vez cada, inclusive as concluídas).
 * O gestor só reordena as abertas: as posições que hoje têm uma demanda aberta recebem, na sequência, as demandas
 * da lista editada, e as posições das concluídas ficam como estão (elas não consomem capacidade).
 * A lista editada precisa ter as mesmas demandas abertas, em qualquer ordem.
 */
export function buildFullOrder(
  currentOrder: readonly string[],
  openIds: ReadonlySet<string>,
  editedOpen: readonly string[],
): string[] {
  let next = 0;
  return currentOrder.map((id) => (openIds.has(id) ? editedOpen[next++] : id));
}

/**
 * As demandas que mais contribuem para a diferença do critério: maior contribuição absoluta primeiro, e as de
 * contribuição zero ficam de fora. O desempate é pelo título e depois pelo id, para a lista nunca variar (D4).
 */
export function topContributions(
  items: readonly ScenarioItem[],
  limit: number = CHART_LIMIT,
): ScenarioItem[] {
  return items
    .filter((item) => item.scoreContributionDelta !== 0)
    .sort(
      (a, b) =>
        Math.abs(b.scoreContributionDelta) - Math.abs(a.scoreContributionDelta) ||
        a.title.localeCompare(b.title, 'pt-BR') ||
        a.demandId.localeCompare(b.demandId),
    )
    .slice(0, limit);
}

/** O sentido da contribuição em palavras, para a cor nunca ser a única pista: negativa diminui o critério (melhora). */
export function contributionLabel(value: number): string {
  if (value < 0) {
    return 'melhora';
  }

  return value > 0 ? 'piora' : 'neutra';
}

/** O critério em pt-BR, sem casas decimais (ele é uma soma de pesos por dias). */
export function scoreLabel(value: number): string {
  return value.toLocaleString('pt-BR', { maximumFractionDigits: 0 });
}

/**
 * Troca a demanda com a vizinha, para cima ou para baixo, e devolve uma lista nova. Nas pontas, ou com um id que
 * não está na lista, devolve uma cópia igual (os botões Subir e Descer nem aparecem nesses casos).
 */
export function moveInList(ids: readonly string[], id: string, direction: MoveDirection): string[] {
  const list = [...ids];
  const index = list.indexOf(id);
  const neighbor = direction === 'up' ? index - 1 : index + 1;
  if (index === -1 || neighbor < 0 || neighbor >= list.length) {
    return list;
  }

  [list[index], list[neighbor]] = [list[neighbor], list[index]];
  return list;
}
