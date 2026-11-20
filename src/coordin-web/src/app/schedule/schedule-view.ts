import { ScheduleItem } from '../planning/planning';

/*
 * Resumo e filtros da tela do cronograma. Funções puras: o cronograma já vem calculado da API (DT-03); aqui
 * só se contam e se escolhem demandas para mostrar. Datas de calendário nunca passam por `Date` (RNF-07).
 */

export type ScheduleFilter = 'all' | 'predictable' | 'unpredictable' | 'late' | 'completed';

export interface ScheduleSummary {
  readonly activeCount: number;
  readonly completedCount: number;
  readonly unpredictableCount: number;
  readonly lateCount: number;
  /** A maior data de fim, com margem, entre as demandas abertas previsíveis (a entrega do setor). */
  readonly sectorEnd: string | null;
  /** Soma do esforço estimado das demandas abertas, em horas. */
  readonly openEffortHours: number;
}

const isOpen = (item: ScheduleItem) => item.completedOn === null;

export function summarizeSchedule(items: readonly ScheduleItem[]): ScheduleSummary {
  const open = items.filter(isOpen);
  const ends = open
    .map((item) => item.forecastEndDate)
    .filter((date): date is string => date !== null);

  return {
    activeCount: open.length,
    completedCount: items.length - open.length,
    unpredictableCount: open.filter((item) => !item.isPredictable).length,
    lateCount: open.filter((item) => item.exceedsInformedEndDate).length,
    sectorEnd: ends.length === 0 ? null : ends.reduce((a, b) => (b > a ? b : a)),
    openEffortHours: open.reduce((sum, item) => sum + item.totalEstimatedEffortHours, 0),
  };
}

/** Texto sem acento e em minúsculas, para a busca ignorar acentos e caixa. */
export function normalizeText(value: string): string {
  return value
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .toLowerCase()
    .trim();
}

function matches(item: ScheduleItem, filter: ScheduleFilter): boolean {
  switch (filter) {
    case 'predictable':
      return isOpen(item) && item.isPredictable;
    case 'unpredictable':
      return isOpen(item) && !item.isPredictable;
    case 'late':
      return isOpen(item) && item.exceedsInformedEndDate;
    case 'completed':
      return !isOpen(item);
    default:
      return true;
  }
}

/** Quantas demandas há em cada filtro (para mostrar nos botões de filtro). */
export function countByFilter(items: readonly ScheduleItem[]): Record<ScheduleFilter, number> {
  const filters: ScheduleFilter[] = ['all', 'predictable', 'unpredictable', 'late', 'completed'];
  return Object.fromEntries(
    filters.map((filter) => [filter, items.filter((item) => matches(item, filter)).length]),
  ) as Record<ScheduleFilter, number>;
}

/** As demandas do filtro cujo título contém a busca (vazia = todas). A ordem do cronograma é mantida. */
export function filterItems(
  items: readonly ScheduleItem[],
  filter: ScheduleFilter,
  query: string,
): ScheduleItem[] {
  const needle = normalizeText(query);
  return items.filter(
    (item) =>
      matches(item, filter) && (needle === '' || normalizeText(item.title).includes(needle)),
  );
}
