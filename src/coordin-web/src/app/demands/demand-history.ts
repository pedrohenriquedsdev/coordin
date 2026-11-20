import { EstimateHistoryResponse } from '../api/models/estimate-history-response';
import { ForecastHistoryResponse } from '../api/models/forecast-history-response';
import { formatCalendarDate } from '../core/dates/calendar-date';

/**
 * Apresentação dos históricos de uma demanda (RF-20, RF-21). A API já traz tudo calculado; aqui só se escreve o
 * texto para o gestor: a causa em português, as horas com vírgula e as datas no formato do calendário.
 */

const CAUSE_LABELS: Record<string, string> = {
  NewDemand: 'Nova demanda',
  Estimate: 'Estimativa',
  Allocation: 'Alocação',
  Capacity: 'Capacidade',
  Absence: 'Ausência',
  Dependency: 'Dependência',
  ApprovedScenario: 'Cenário aprovado',
  SafetyMargin: 'Margem de segurança',
};

/** A causa em português. Uma causa desconhecida aparece como veio, para não esconder um valor novo. */
export function causeLabel(cause: string): string {
  return CAUSE_LABELS[cause] ?? cause;
}

/** Horas com uma casa decimal e vírgula; `—` quando não havia estimativa. */
export function hoursText(value: number | string | null): string {
  if (value === null) {
    return '—';
  }

  return Number(value).toLocaleString('pt-BR', { maximumFractionDigits: 2 });
}

/** Um instante (da API, em UTC) no fuso de Brasília, como o gestor lê no relógio dele. */
export function instantText(iso: string): string {
  return new Intl.DateTimeFormat('pt-BR', {
    timeZone: 'America/Sao_Paulo',
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(iso));
}

/** Uma data de calendário como `dd/MM/aaaa`; `—` quando não havia data. */
export function calendarText(value: string | null): string {
  return value === null ? '—' : formatCalendarDate(value);
}

export interface EstimateHistoryRow {
  readonly team: string;
  readonly previous: string;
  readonly next: string;
  readonly reason: string;
  readonly changedAt: string;
}

export function estimateHistoryRows(
  items: readonly EstimateHistoryResponse[],
): EstimateHistoryRow[] {
  return items.map((item) => ({
    team: item.teamName ?? 'Time removido',
    previous: hoursText(item.previousHours),
    next: hoursText(item.newHours),
    reason: item.reason ?? '',
    changedAt: instantText(item.changedAt),
  }));
}

export interface ForecastHistoryRow {
  readonly previous: string;
  readonly next: string;
  readonly cause: string;
  readonly changedAt: string;
}

export function forecastHistoryRows(
  items: readonly ForecastHistoryResponse[],
): ForecastHistoryRow[] {
  return items.map((item) => ({
    previous: calendarText(item.previousEndDate),
    next: calendarText(item.newEndDate),
    cause: causeLabel(item.cause),
    changedAt: instantText(item.changedAt),
  }));
}

export type TimelineKind = 'estimate' | 'forecast';

/** Um acontecimento da linha do tempo da demanda: uma mudança de estimativa ou da data prevista. */
export interface TimelineEvent {
  readonly kind: TimelineKind;
  /** O instante (milissegundos), só para ordenar; quem lê vê `changedAt`. */
  readonly at: number;
  readonly title: string;
  readonly text: string;
  readonly detail: string;
  readonly changedAt: string;
}

/**
 * Junta os dois históricos em uma linha do tempo, do mais recente para o mais antigo. O histórico é somente de
 * inclusão (DT-14): aqui nada se altera nem se esconde, só se ordena e se escreve em palavras.
 */
export function buildTimeline(
  estimates: readonly EstimateHistoryResponse[],
  forecasts: readonly ForecastHistoryResponse[],
): TimelineEvent[] {
  const events: TimelineEvent[] = [
    ...estimates.map((item): TimelineEvent => ({
      kind: 'estimate',
      at: Date.parse(item.changedAt),
      title: 'Estimativa alterada',
      text: `${item.teamName ?? 'Time removido'}: ${hoursText(item.previousHours)} → ${hoursText(item.newHours)} h`,
      detail: item.reason ?? '',
      changedAt: instantText(item.changedAt),
    })),
    ...forecasts.map((item): TimelineEvent => ({
      kind: 'forecast',
      at: Date.parse(item.changedAt),
      title: 'Previsão atualizada',
      text: `${calendarText(item.previousEndDate)} → ${calendarText(item.newEndDate)}`,
      detail: `Causa: ${causeLabel(item.cause)}`,
      changedAt: instantText(item.changedAt),
    })),
  ];

  return events.sort((a, b) => b.at - a.at);
}
