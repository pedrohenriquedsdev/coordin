import { DemandResponse } from '../api/models/demand-response';
import { ScheduleResponse } from '../api/models/schedule-response';
import { ScheduleItemResponse } from '../api/models/schedule-item-response';

/** Uma parte do cronograma: o time, com a estimativa que ele informou (nula = ainda não estimou). */
export interface ScheduleTeam {
  readonly teamId: string;
  readonly teamName: string;
  readonly estimatedEffortHours: number | null;
}

/**
 * Uma demanda no cronograma. As datas são datas de calendário (`yyyy-MM-dd`, texto). Os desvios são em
 * dias corridos, positivos quando houve atraso (DT-27), e só existem para demandas concluídas.
 */
export interface ScheduleItem {
  readonly demandId: string;
  readonly title: string;
  readonly priorityRank: number;
  readonly executionRank: number;
  readonly isOutOfPriorityOrder: boolean;
  readonly teams: readonly ScheduleTeam[];
  readonly totalEstimatedEffortHours: number;
  readonly startDate: string | null;
  readonly isPredictable: boolean;
  readonly unpredictableReason: string | null;
  readonly forecastStartDate: string | null;
  readonly endDateWithoutMargin: string | null;
  readonly forecastEndDate: string | null;
  readonly informedEndDate: string | null;
  readonly exceedsInformedEndDate: boolean;
  readonly completedOn: string | null;
  readonly baselineEndDate: string | null;
  readonly baselineDeviationDays: number | null;
  readonly forecastDeviationDays: number | null;
}

/** O cronograma inteiro, na ordem de execução. */
export interface Schedule {
  readonly executionOrderMode: string;
  readonly items: readonly ScheduleItem[];
}

/** Uma parte da demanda, com o nome do time, para a lista e o formulário. */
export interface DemandTeam {
  readonly teamId: string;
  readonly teamName: string;
  readonly estimatedEffortHours: number | null;
}

/** Uma demanda como a tela de demandas e o formulário a usam. */
export interface Demand {
  readonly id: string;
  readonly title: string;
  readonly priorityRank: number;
  readonly executionRank: number;
  readonly startDate: string | null;
  readonly informedEndDate: string | null;
  readonly completedOn: string | null;
  readonly teams: readonly DemandTeam[];
  readonly assignedPersonIds: readonly string[];
}

/** Os números da API chegam como `number | string` no cliente gerado; aqui viram `number`, e `null` continua `null`. */
function toNumber(value: number | string): number {
  return Number(value);
}

function toNullableNumber(value: number | string | null): number | null {
  return value === null ? null : Number(value);
}

function mapScheduleItem(item: ScheduleItemResponse): ScheduleItem {
  return {
    demandId: item.demandId,
    title: item.title,
    priorityRank: toNumber(item.priorityRank),
    executionRank: toNumber(item.executionRank),
    isOutOfPriorityOrder: item.isOutOfPriorityOrder,
    teams: item.teams.map((team) => ({
      teamId: team.teamId,
      teamName: team.teamName,
      estimatedEffortHours: toNullableNumber(team.estimatedEffortHours),
    })),
    totalEstimatedEffortHours: toNumber(item.totalEstimatedEffortHours),
    startDate: item.startDate,
    isPredictable: item.isPredictable,
    unpredictableReason: item.unpredictableReason,
    forecastStartDate: item.forecastStartDate,
    endDateWithoutMargin: item.endDateWithoutMargin,
    forecastEndDate: item.forecastEndDate,
    informedEndDate: item.informedEndDate,
    exceedsInformedEndDate: item.exceedsInformedEndDate,
    completedOn: item.completedOn,
    baselineEndDate: item.baselineEndDate,
    baselineDeviationDays: toNullableNumber(item.baselineDeviationDays),
    forecastDeviationDays: toNullableNumber(item.forecastDeviationDays),
  };
}

/** Converte a resposta de `GET /api/schedule` para o modelo da tela. */
export function mapSchedule(response: ScheduleResponse): Schedule {
  return {
    executionOrderMode: response.executionOrderMode,
    items: response.items.map(mapScheduleItem),
  };
}

/** Converte uma demanda de `GET /api/demands` para o modelo da tela. */
export function mapDemand(response: DemandResponse): Demand {
  return {
    id: response.id,
    title: response.title,
    priorityRank: toNumber(response.priorityRank),
    executionRank: toNumber(response.executionRank),
    startDate: response.startDate,
    informedEndDate: response.informedEndDate,
    completedOn: response.completedOn,
    teams: response.teams.map((team) => ({
      teamId: team.teamId,
      teamName: team.teamName,
      estimatedEffortHours: toNullableNumber(team.estimatedEffortHours),
    })),
    assignedPersonIds: [...response.assignedPersonIds],
  };
}
