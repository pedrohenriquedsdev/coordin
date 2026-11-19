import { describe, expect, it } from 'vitest';
import { ScheduleItem } from '../planning/planning';
import { TeamReport } from '../reports/team-report';
import {
  buildDayEvents,
  buildTimeline,
  teamOccupancy,
  weeklyDeliveries,
  weeklyLoad,
} from './home-data';

function item(overrides: Partial<ScheduleItem> = {}): ScheduleItem {
  return {
    demandId: 'd1',
    title: 'Checkout',
    priorityRank: 1,
    executionRank: 1,
    isOutOfPriorityOrder: false,
    teams: [],
    totalEstimatedEffortHours: 80,
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

const week = (weekStart: string, allocated: number, idle: number, overloaded = false) => ({
  weekStart,
  weekEnd: weekStart,
  capacityHours: allocated + idle,
  allocatedHours: allocated,
  occupancy: allocated + idle > 0 ? allocated / (allocated + idle) : null,
  idleHours: idle,
  isOverloaded: overloaded,
  overloadedDemands: [],
});

const report: TeamReport = {
  periodStart: '2026-12-07',
  periodEnd: '2026-12-20',
  teams: [
    {
      teamId: 'a',
      teamName: 'Web',
      unpredictableDemandCount: 0,
      weeks: [week('2026-12-07', 30, 10), week('2026-12-14', 40, 0, true)],
    },
    {
      teamId: 'b',
      teamName: 'App',
      unpredictableDemandCount: 0,
      weeks: [week('2026-12-07', 5, 15), week('2026-12-14', 10, 10)],
    },
  ],
};

describe('weeklyDeliveries', () => {
  it('counts open deliveries per week and separates the late ones', () => {
    // 2026-12-07 é uma segunda-feira.
    const result = weeklyDeliveries(
      [
        item({ forecastEndDate: '2026-12-08' }),
        item({ forecastEndDate: '2026-12-13', exceedsInformedEndDate: true }),
        item({ forecastEndDate: '2026-12-14' }),
        item({ forecastEndDate: '2026-12-09', completedOn: '2026-12-01' }),
        item({ forecastEndDate: null }),
      ],
      '2026-12-09',
      2,
    );

    expect(result.labels).toEqual(['07/12', '14/12']);
    expect(result.onTime).toEqual([1, 1]);
    expect(result.late).toEqual([1, 0]);
  });
});

describe('buildDayEvents', () => {
  it('places holidays, clipped absences and open deliveries on their days', () => {
    const days = buildDayEvents({
      holidays: [{ id: 'h', date: '2026-12-25', description: 'Natal' }],
      absences: [
        {
          id: 'a',
          personId: 'p',
          personName: 'Ana',
          teamId: 't',
          teamName: 'Web',
          type: 'Vacation',
          startDate: '2026-12-20',
          endDate: '2026-12-30',
        },
      ],
      items: [
        item({ forecastEndDate: '2026-12-25', exceedsInformedEndDate: true }),
        item({ forecastEndDate: '2026-12-26', completedOn: '2026-12-01' }),
      ],
      from: '2026-12-22',
      to: '2026-12-27',
    });

    const natal = days.get('2026-12-25') ?? [];
    expect(natal.map((event) => event.kind).sort()).toEqual(['absence', 'delivery', 'holiday']);
    expect(natal.find((event) => event.kind === 'delivery')?.late).toBe(true);
    expect(days.has('2026-12-21')).toBe(false); // antes do intervalo
    expect(days.has('2026-12-28')).toBe(false); // depois do intervalo
    expect(days.get('2026-12-26')?.some((event) => event.kind === 'delivery')).toBe(false); // concluída
    expect(days.get('2026-12-22')?.[0].detail).toBe('Férias · Web');
  });
});

describe('weeklyLoad', () => {
  it('sums every team for the sector and keeps the overload flag', () => {
    const sector = weeklyLoad(report, null);

    expect(sector).toHaveLength(2);
    expect(sector[0]).toMatchObject({
      label: '07/12',
      allocated: 35,
      idle: 25,
      capacity: 60,
      overloaded: false,
    });
    expect(sector[1].overloaded).toBe(true);
  });

  it('filters one team', () => {
    expect(weeklyLoad(report, 'b').map((w) => w.allocated)).toEqual([5, 10]);
  });
});

describe('teamOccupancy', () => {
  it('orders from the busiest team and counts overloaded weeks', () => {
    const result = teamOccupancy(report);

    expect(result.map((t) => t.teamName)).toEqual(['Web', 'App']);
    expect(result[0].overloadedWeeks).toBe(1);
    expect(result[0].occupancy).toBeCloseTo(70 / 80);
  });
});

describe('buildTimeline', () => {
  it('positions bars on one scale, with the margin after the work', () => {
    const timeline = buildTimeline(
      [
        item({
          demandId: 'x',
          executionRank: 2,
          forecastStartDate: '2026-12-11',
          endDateWithoutMargin: '2026-12-16',
          forecastEndDate: '2026-12-21',
        }),
        item({
          demandId: 'y',
          executionRank: 1,
          forecastStartDate: '2026-12-01',
          endDateWithoutMargin: '2026-12-08',
          forecastEndDate: '2026-12-11',
          exceedsInformedEndDate: true,
        }),
        item({
          demandId: 'z',
          isPredictable: false,
          forecastStartDate: null,
          forecastEndDate: null,
        }),
      ],
      '2026-12-05',
      8,
    );

    expect(timeline.rows.map((r) => r.id)).toEqual(['y', 'x']);
    expect(timeline.rows[0].leftPct).toBe(0);
    expect(timeline.rows[0].late).toBe(true);
    // 20 dias de escala: y vai do dia 0, trabalho até o dia 7, margem até o 10.
    expect(timeline.rows[0].workPct).toBeCloseTo(35);
    expect(timeline.rows[0].marginPct).toBeCloseTo(15);
    expect(timeline.todayPct).toBeCloseTo(20);
    expect(timeline.fromLabel).toBe('01/12');
    expect(timeline.toLabel).toBe('21/12');
  });

  it('is empty without predictable demands', () => {
    expect(buildTimeline([item({ isPredictable: false })], '2026-12-05', 8).rows).toEqual([]);
  });
});
