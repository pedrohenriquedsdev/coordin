import { AbsenceResponse } from '../api/models/absence-response';
import { HolidayResponse } from '../api/models/holiday-response';
import {
  addDays,
  daysBetween,
  formatShortCalendarDate,
  mondayOf,
} from '../core/dates/calendar-date';
import { ScheduleItem } from '../planning/planning';
import { ReportTeam, summarizeTeam, TeamReport } from '../reports/team-report';

/*
 * Cálculos do painel inicial. São funções puras: os números e as datas já vêm prontos da API (DT-03);
 * aqui só se agrupam, se somam e se posicionam para desenhar. Datas de calendário nunca passam por
 * `Date` do navegador (RNF-07): tudo usa os auxiliares de `core/dates`.
 */

export type DayEventKind = 'holiday' | 'absence' | 'delivery';

export interface DayEvent {
  readonly kind: DayEventKind;
  readonly title: string;
  readonly detail: string;
  /** Uma entrega que passa do prazo informado (sempre dita em palavras também). */
  readonly late: boolean;
}

function isOpen(item: ScheduleItem): boolean {
  return item.completedOn === null;
}

/** Os eventos de cada dia entre `from` e `to` (inclusive): feriados, ausências e entregas previstas. */
export function buildDayEvents(input: {
  readonly holidays: readonly HolidayResponse[];
  readonly absences: readonly AbsenceResponse[];
  readonly items: readonly ScheduleItem[];
  readonly from: string;
  readonly to: string;
}): Map<string, DayEvent[]> {
  const days = new Map<string, DayEvent[]>();
  const add = (date: string, event: DayEvent) => {
    if (date < input.from || date > input.to) {
      return;
    }
    const list = days.get(date) ?? [];
    list.push(event);
    days.set(date, list);
  };

  for (const holiday of input.holidays) {
    add(holiday.date, {
      kind: 'holiday',
      title: holiday.description?.trim() || 'Feriado',
      detail: 'Feriado do setor: ninguém contribui com capacidade',
      late: false,
    });
  }

  for (const absence of input.absences) {
    const start = absence.startDate < input.from ? input.from : absence.startDate;
    const end = absence.endDate > input.to ? input.to : absence.endDate;
    for (let date = start; date <= end; date = addDays(date, 1)) {
      add(date, {
        kind: 'absence',
        title: absence.personName,
        detail: `${absence.type === 'Vacation' ? 'Férias' : 'Ausência'} · ${absence.teamName}`,
        late: false,
      });
    }
  }

  for (const item of input.items.filter(isOpen)) {
    if (item.forecastEndDate !== null) {
      add(item.forecastEndDate, {
        kind: 'delivery',
        title: item.title,
        detail: item.exceedsInformedEndDate
          ? 'Entrega prevista, com margem · passa do prazo informado'
          : 'Entrega prevista, com margem',
        late: item.exceedsInformedEndDate,
      });
    }
  }

  return days;
}

export interface WeeklyDeliveries {
  readonly labels: string[];
  readonly onTime: number[];
  readonly late: number[];
}

/** Quantas demandas abertas têm a entrega prevista em cada uma das próximas `weeks` semanas. */
export function weeklyDeliveries(
  items: readonly ScheduleItem[],
  today: string,
  weeks: number,
): WeeklyDeliveries {
  const first = mondayOf(today);
  const result: WeeklyDeliveries = { labels: [], onTime: [], late: [] };

  for (let index = 0; index < weeks; index++) {
    const start = addDays(first, index * 7);
    const end = addDays(start, 6);
    const inWeek = items.filter(
      (item) =>
        isOpen(item) &&
        item.forecastEndDate !== null &&
        item.forecastEndDate >= start &&
        item.forecastEndDate <= end,
    );
    result.labels.push(formatShortCalendarDate(start));
    result.late.push(inWeek.filter((item) => item.exceedsInformedEndDate).length);
    result.onTime.push(inWeek.filter((item) => !item.exceedsInformedEndDate).length);
  }

  return result;
}

export interface LoadWeek {
  readonly label: string;
  readonly allocated: number;
  readonly idle: number;
  readonly capacity: number;
  readonly overloaded: boolean;
}

/** A carga por semana do setor (todos os times somados) ou de um time. */
export function weeklyLoad(report: TeamReport, teamId: string | null): LoadWeek[] {
  const teams: readonly ReportTeam[] =
    teamId === null ? report.teams : report.teams.filter((team) => team.teamId === teamId);
  const count = teams[0]?.weeks.length ?? 0;

  return Array.from({ length: count }, (_, index) => {
    const weeks = teams.map((team) => team.weeks[index]);
    return {
      label: formatShortCalendarDate(weeks[0].weekStart),
      allocated: weeks.reduce((sum, week) => sum + week.allocatedHours, 0),
      idle: weeks.reduce((sum, week) => sum + week.idleHours, 0),
      capacity: weeks.reduce((sum, week) => sum + week.capacityHours, 0),
      overloaded: weeks.some((week) => week.isOverloaded),
    };
  });
}

export interface TeamOccupancy {
  readonly teamId: string;
  readonly teamName: string;
  /** 0 a 1 (pode passar de 1 só se a API disser); `null` sem capacidade. */
  readonly occupancy: number | null;
  readonly overloadedWeeks: number;
}

/** A ocupação de cada time no período, do mais ocupado ao menos ocupado. */
export function teamOccupancy(report: TeamReport): TeamOccupancy[] {
  return report.teams
    .map((team) => {
      const summary = summarizeTeam(team);
      return {
        teamId: team.teamId,
        teamName: team.teamName,
        occupancy: summary.occupancy,
        overloadedWeeks: summary.overloadedWeeks,
      };
    })
    .sort((a, b) => (b.occupancy ?? -1) - (a.occupancy ?? -1));
}

export interface TimelineRow {
  readonly id: string;
  readonly title: string;
  readonly startLabel: string;
  readonly endLabel: string;
  readonly leftPct: number;
  /** Largura da parte "sem margem" e da parte da margem, em % da escala. */
  readonly workPct: number;
  readonly marginPct: number;
  readonly late: boolean;
}

export interface Timeline {
  readonly rows: TimelineRow[];
  /** Posição de "hoje" na escala (%), ou `null` se hoje fica fora dela. */
  readonly todayPct: number | null;
  readonly fromLabel: string;
  readonly toLabel: string;
}

/** A linha do tempo das próximas demandas previsíveis, em ordem de execução. */
export function buildTimeline(
  items: readonly ScheduleItem[],
  today: string,
  limit: number,
): Timeline {
  const rows = items
    .filter(
      (item) =>
        isOpen(item) &&
        item.isPredictable &&
        item.forecastStartDate !== null &&
        item.forecastEndDate !== null,
    )
    .sort((a, b) => a.executionRank - b.executionRank)
    .slice(0, limit);

  if (rows.length === 0) {
    return { rows: [], todayPct: null, fromLabel: '', toLabel: '' };
  }

  const starts = rows.map((item) => item.forecastStartDate as string);
  const ends = rows.map((item) => item.forecastEndDate as string);
  const from = [...starts, today].reduce((min, d) => (d < min ? d : min));
  const to = ends.reduce((max, d) => (d > max ? d : max));
  const span = Math.max(daysBetween(from, to), 1);
  const pct = (date: string) => (daysBetween(from, date) / span) * 100;

  return {
    rows: rows.map((item) => {
      const start = item.forecastStartDate as string;
      const end = item.forecastEndDate as string;
      const noMargin = item.endDateWithoutMargin ?? end;
      const left = pct(start);
      const work = Math.max(pct(noMargin) - left, 1.2);
      return {
        id: item.demandId,
        title: item.title,
        startLabel: formatShortCalendarDate(start),
        endLabel: formatShortCalendarDate(end),
        leftPct: left,
        workPct: work,
        marginPct: Math.max(pct(end) - left - work, 0),
        late: item.exceedsInformedEndDate,
      };
    }),
    todayPct: today >= from && today <= to ? pct(today) : null,
    fromLabel: formatShortCalendarDate(from),
    toLabel: formatShortCalendarDate(to),
  };
}
