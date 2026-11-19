import { describe, expect, it } from 'vitest';
import { ScheduleItem } from '../planning/planning';
import { countByFilter, filterItems, normalizeText, summarizeSchedule } from './schedule-view';

function item(overrides: Partial<ScheduleItem> = {}): ScheduleItem {
  return {
    demandId: 'd',
    title: 'Checkout',
    priorityRank: 1,
    executionRank: 1,
    isOutOfPriorityOrder: false,
    teams: [],
    totalEstimatedEffortHours: 10,
    startDate: '2026-12-01',
    isPredictable: true,
    unpredictableReason: null,
    forecastStartDate: '2026-12-01',
    endDateWithoutMargin: '2026-12-08',
    forecastEndDate: '2026-12-10',
    informedEndDate: null,
    exceedsInformedEndDate: false,
    completedOn: null,
    baselineEndDate: null,
    baselineDeviationDays: null,
    forecastDeviationDays: null,
    prerequisites: [],
    ...overrides,
  };
}

const items = [
  item({ demandId: '1', title: 'Autenticação social' }),
  item({
    demandId: '2',
    title: 'Relatório de performance',
    forecastEndDate: '2026-12-20',
    exceedsInformedEndDate: true,
    totalEstimatedEffortHours: 30,
  }),
  item({ demandId: '3', title: 'Sem estimativa', isPredictable: false, forecastEndDate: null }),
  item({
    demandId: '4',
    title: 'Já entregue',
    completedOn: '2026-11-01',
    forecastEndDate: '2026-11-01',
    totalEstimatedEffortHours: 99,
  }),
];

describe('summarizeSchedule', () => {
  it('counts only open demands and picks the latest forecast end', () => {
    const summary = summarizeSchedule(items);

    expect(summary).toMatchObject({
      activeCount: 3,
      completedCount: 1,
      unpredictableCount: 1,
      lateCount: 1,
      sectorEnd: '2026-12-20',
      openEffortHours: 50,
    });
  });

  it('has no sector end without forecasts', () => {
    expect(
      summarizeSchedule([item({ isPredictable: false, forecastEndDate: null })]).sectorEnd,
    ).toBeNull();
    expect(summarizeSchedule([]).activeCount).toBe(0);
  });
});

describe('filters', () => {
  it('counts each filter', () => {
    expect(countByFilter(items)).toEqual({
      all: 4,
      predictable: 2,
      unpredictable: 1,
      late: 1,
      completed: 1,
    });
  });

  it('filters by situation and keeps the schedule order', () => {
    expect(filterItems(items, 'predictable', '').map((i) => i.demandId)).toEqual(['1', '2']);
    expect(filterItems(items, 'completed', '').map((i) => i.demandId)).toEqual(['4']);
    expect(filterItems(items, 'all', '')).toHaveLength(4);
  });

  it('searches the title ignoring accents and case', () => {
    expect(filterItems(items, 'all', 'AUTENTICACAO').map((i) => i.demandId)).toEqual(['1']);
    expect(filterItems(items, 'late', 'relatorio')).toHaveLength(1);
    expect(filterItems(items, 'late', 'checkout')).toHaveLength(0);
  });

  it('normalizes text', () => {
    expect(normalizeText('  Ação  ')).toBe('acao');
  });
});
