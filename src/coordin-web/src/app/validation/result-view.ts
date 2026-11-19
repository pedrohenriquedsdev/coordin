import type { ChartConfiguration } from '../charts/chart-setup';
import { ChartTheme, LIGHT_CHART_THEME } from '../charts/chart-theme';
import { formatCalendarDate } from '../core/dates/calendar-date';
import { deviationLabel } from '../planning/labels';
import { ValidationResult } from '../api/models/validation-result';
import { deviationChartSeries, efficiencyChartSeries, priorityRows, toNumber } from './labels';

/** Cores dos gráficos da validação. O planejamento com o Coordin usa o verde de destaque. */
export const RESULT_COLORS = {
  currentProcess: '#2f8cff',
  withCoordin: '#1ec1cb',
  signed: '#2f8cff',
  absolute: '#1ec1cb',
} as const;

function chartOptions(
  theme: ChartTheme,
  beginAtZero: boolean,
): ChartConfiguration<'bar'>['options'] {
  return {
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      x: { grid: { display: false }, ticks: { color: theme.text } },
      y: { beginAtZero, grid: { color: theme.grid }, ticks: { color: theme.text } },
    },
    plugins: { legend: { position: 'bottom', labels: { color: theme.text, usePointStyle: true } } },
  };
}

/** Um número com uma casa decimal, com a vírgula do pt-BR. O gráfico e a tabela usam o mesmo número. */
export function formatMetric(value: number): string {
  return value.toLocaleString('pt-BR', { maximumFractionDigits: 1 });
}

/**
 * Configuração do gráfico de eficiência (RF-24; DT-36): uma barra por métrica, uma cor por planejamento. Função
 * pura: os números vêm do resultado da API.
 */
export function buildEfficiencyChart(
  result: ValidationResult,
  theme: ChartTheme = LIGHT_CHART_THEME,
): ChartConfiguration<'bar'> {
  const series = efficiencyChartSeries(result);
  return {
    type: 'bar',
    data: {
      labels: series.labels,
      datasets: [
        {
          label: 'Planejamento atual',
          data: series.currentProcess,
          backgroundColor: RESULT_COLORS.currentProcess,
          borderRadius: 6,
        },
        {
          label: 'Com o Coordin',
          data: series.withCoordin,
          backgroundColor: RESULT_COLORS.withCoordin,
          borderRadius: 6,
        },
      ],
    },
    options: chartOptions(theme, true),
  };
}

/**
 * Configuração do gráfico de desvios (RF-24; DT-36): para a linha de base e para o Coordin, a média com sinal e a
 * média absoluta, em dias corridos. Positivo é atraso.
 */
export function buildDeviationChart(
  result: ValidationResult,
  theme: ChartTheme = LIGHT_CHART_THEME,
): ChartConfiguration<'bar'> {
  const series = deviationChartSeries(result);
  return {
    type: 'bar',
    data: {
      labels: series.labels,
      datasets: [
        {
          label: 'Média com sinal (dias)',
          data: series.signed,
          backgroundColor: RESULT_COLORS.signed,
          borderRadius: 6,
        },
        {
          label: 'Média absoluta (dias)',
          data: series.absolute,
          backgroundColor: RESULT_COLORS.absolute,
          borderRadius: 6,
        },
      ],
    },
    options: chartOptions(theme, false),
  };
}

/** Uma linha da tabela de médias: a mesma informação do gráfico de desvios, com o texto formatado. */
export interface AverageRow {
  readonly label: string;
  readonly signed: string;
  readonly absolute: string;
}

export function averageRows(result: ValidationResult): AverageRow[] {
  const series = deviationChartSeries(result);
  return [
    {
      label: series.labels[0],
      signed: formatMetric(series.signed[0]),
      absolute: formatMetric(series.absolute[0]),
    },
    {
      label: series.labels[1],
      signed: formatMetric(series.signed[1]),
      absolute: formatMetric(series.absolute[1]),
    },
  ];
}

/** Uma linha da tabela por demanda, com as datas em dd/MM/aaaa e os dois desvios em palavras. */
export interface DeviationRow {
  readonly demandId: string;
  readonly title: string;
  readonly baselineDate: string;
  readonly coordinForecastEndDate: string;
  readonly simulatedActualEndDate: string;
  readonly baselineText: string;
  readonly coordinText: string;
}

/** As demandas do desvio, na ordem em que o resultado as traz. Sem título conhecido, mostra o identificador. */
export function deviationRows(
  result: ValidationResult,
  titles: ReadonlyMap<string, string>,
): DeviationRow[] {
  return result.deviation.items.map((item) => ({
    demandId: item.demandId,
    title: titles.get(item.demandId) ?? item.demandId,
    baselineDate: formatCalendarDate(item.baselineDate),
    coordinForecastEndDate: formatCalendarDate(item.coordinForecastEndDate),
    simulatedActualEndDate: formatCalendarDate(item.simulatedActualEndDate),
    baselineText: deviationLabel(toNumber(item.baselineDeviationDays)),
    coordinText: deviationLabel(toNumber(item.coordinDeviationDays)),
  }));
}

/** Uma linha da checagem das prioritárias, com o texto do atraso e o destaque de quem piora. */
export interface PriorityTableRow {
  readonly rank: number;
  readonly title: string;
  readonly currentEnd: string;
  readonly coordinEnd: string;
  readonly deltaText: string;
  readonly worsened: boolean;
}

export function priorityTableRows(
  result: ValidationResult,
  titles: ReadonlyMap<string, string>,
): PriorityTableRow[] {
  return priorityRows(result).map((row) => ({
    rank: row.rank,
    title: titles.get(row.demandId) ?? row.demandId,
    currentEnd: formatCalendarDate(row.currentEnd),
    coordinEnd: formatCalendarDate(row.coordinEnd),
    deltaText: deviationLabel(row.deltaDays),
    worsened: row.worsened,
  }));
}

/** Uma linha da tabela de eficiência, com os números formatados como no gráfico (uma linha por métrica). */
export interface EfficiencyTableRow {
  readonly label: string;
  readonly currentProcess: string;
  readonly withCoordin: string;
}

/**
 * As quatro métricas de eficiência na tabela: as mesmas do gráfico (ociosidade e sobrecargas) e as duas que não
 * cabem na mesma escala (dispersão e prazo do setor). Números com vírgula; datas em dd/MM/aaaa.
 */
export function efficiencyTableRows(result: ValidationResult): EfficiencyTableRow[] {
  const current = result.efficiency.currentProcess;
  const withCoordin = result.efficiency.withCoordin;
  return [
    {
      label: 'Ociosidade (horas)',
      currentProcess: formatMetric(toNumber(current.idleHours)),
      withCoordin: formatMetric(toNumber(withCoordin.idleHours)),
    },
    {
      label: 'Dispersão da ocupação (pontos)',
      currentProcess: formatMetric(toNumber(current.occupancySpreadPoints)),
      withCoordin: formatMetric(toNumber(withCoordin.occupancySpreadPoints)),
    },
    {
      label: 'Sobrecargas (períodos)',
      currentProcess: formatMetric(toNumber(current.overloadedPeriods)),
      withCoordin: formatMetric(toNumber(withCoordin.overloadedPeriods)),
    },
    {
      label: 'Prazo do setor',
      currentProcess:
        current.sectorEndDate === null ? '—' : formatCalendarDate(current.sectorEndDate),
      withCoordin:
        withCoordin.sectorEndDate === null ? '—' : formatCalendarDate(withCoordin.sectorEndDate),
    },
  ];
}
