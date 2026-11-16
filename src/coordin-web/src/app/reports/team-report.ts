import { addDays, formatShortCalendarDate, mondayOf } from '../core/dates/calendar-date';
import { ReportDemandResponse } from '../api/models/report-demand-response';
import { TeamReportResponse } from '../api/models/team-report-response';
import { TeamReportTeamResponse } from '../api/models/team-report-team-response';
import { TeamReportWeekResponse } from '../api/models/team-report-week-response';

/** Uma demanda que deixou a fila do time acima da capacidade na semana. */
export interface WaitingDemand {
  readonly id: string;
  readonly title: string;
}

/** Uma semana completa (segunda a domingo) de um time. `occupancy` é nula quando a capacidade é zero. */
export interface ReportWeek {
  readonly weekStart: string;
  readonly weekEnd: string;
  readonly capacityHours: number;
  readonly allocatedHours: number;
  readonly occupancy: number | null;
  readonly idleHours: number;
  readonly isOverloaded: boolean;
  readonly overloadedDemands: readonly WaitingDemand[];
}

/** Um time no período: as semanas em ordem e as demandas não previsíveis que o envolvem. */
export interface ReportTeam {
  readonly teamId: string;
  readonly teamName: string;
  readonly unpredictableDemandCount: number;
  readonly weeks: readonly ReportWeek[];
}

/** O relatório do período: as semanas completas que o cobrem e cada time. */
export interface TeamReport {
  readonly periodStart: string;
  readonly periodEnd: string;
  readonly teams: readonly ReportTeam[];
}

/** Os totais de um time no período, para o resumo da tela. */
export interface TeamSummary {
  readonly teamName: string;
  readonly capacityHours: number;
  readonly allocatedHours: number;
  readonly occupancy: number | null;
  readonly idleHours: number;
  readonly overloadedWeeks: number;
  readonly unpredictableDemandCount: number;
}

/** O período padrão da tela: da segunda-feira desta semana até o domingo da oitava semana. */
export const DEFAULT_PERIOD_WEEKS = 8;

/** Os números da API chegam como `number | string` no cliente gerado; aqui viram `number`. */
function toNumber(value: number | string): number {
  return Number(value);
}

function toNullableNumber(value: number | string | null): number | null {
  return value === null ? null : Number(value);
}

function mapWeek(week: TeamReportWeekResponse): ReportWeek {
  return {
    weekStart: week.weekStart,
    weekEnd: week.weekEnd,
    capacityHours: toNumber(week.capacityHours),
    allocatedHours: toNumber(week.allocatedHours),
    occupancy: toNullableNumber(week.occupancy),
    idleHours: toNumber(week.idleHours),
    isOverloaded: week.isOverloaded,
    overloadedDemands: week.overloadedDemands.map((demand: ReportDemandResponse) => ({
      id: demand.id,
      title: demand.title,
    })),
  };
}

function mapTeam(team: TeamReportTeamResponse): ReportTeam {
  return {
    teamId: team.teamId,
    teamName: team.teamName,
    unpredictableDemandCount: toNumber(team.unpredictableDemandCount),
    weeks: team.weeks.map(mapWeek),
  };
}

/** Converte a resposta de `GET /api/reports/teams` para o modelo da tela. */
export function mapTeamReport(response: TeamReportResponse): TeamReport {
  return {
    periodStart: response.periodStart,
    periodEnd: response.periodEnd,
    teams: response.teams.map(mapTeam),
  };
}

/**
 * Os totais de um time no período. A ocupação é o total alocado sobre o total de capacidade; sem capacidade
 * no período, não há ocupação para mostrar (nula).
 */
export function summarizeTeam(team: ReportTeam): TeamSummary {
  const capacityHours = team.weeks.reduce((sum, week) => sum + week.capacityHours, 0);
  const allocatedHours = team.weeks.reduce((sum, week) => sum + week.allocatedHours, 0);
  return {
    teamName: team.teamName,
    capacityHours,
    allocatedHours,
    occupancy: capacityHours > 0 ? allocatedHours / capacityHours : null,
    idleHours: team.weeks.reduce((sum, week) => sum + week.idleHours, 0),
    overloadedWeeks: team.weeks.filter((week) => week.isOverloaded).length,
    unpredictableDemandCount: team.unpredictableDemandCount,
  };
}

/** Rótulo curto de uma semana, de segunda a domingo: `04/01 a 10/01`. */
export function weekLabel(week: ReportWeek): string {
  return `${formatShortCalendarDate(week.weekStart)} a ${formatShortCalendarDate(week.weekEnd)}`;
}

/** O período padrão, a partir de uma data de hoje (de preferência a de São Paulo): segunda desta semana até o domingo da oitava. */
export function defaultPeriod(today: string): { from: string; to: string } {
  const from = mondayOf(today);
  return { from, to: addDays(from, DEFAULT_PERIOD_WEEKS * 7 - 1) };
}
