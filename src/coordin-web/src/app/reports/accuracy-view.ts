import type { ChartConfiguration } from '../charts/chart-setup';
import { AccuracyIndicatorsResponse } from '../api/models/accuracy-indicators-response';
import { AccuracyResponse } from '../api/models/accuracy-response';

/**
 * Apresentação dos indicadores de precisão (RF-23; DT-36). A API já calcula tudo; aqui se monta o texto, as séries
 * dos gráficos e as linhas da tabela, a partir dos mesmos números, para o gráfico e a tabela nunca divergirem.
 */

export const ACCURACY_COLORS = {
  baseline: '#334155',
  completion: '#94a3b8',
  estimate: '#0f766e',
} as const;

/** O motivo de o período não servir, ou `null` quando serve. A API também recusa, mas a tela avisa antes. */
export function periodError(from: string, to: string): string | null {
  if (from === '' || to === '') {
    return 'Informe a data inicial e a final.';
  }

  if (from > to) {
    return 'A data inicial não pode ser posterior à final.';
  }

  return null;
}

/** Um número com uma casa decimal e vírgula; `—` quando não há média (nenhuma demanda entrou nela). */
export function metricText(value: number | string | null, suffix = ''): string {
  if (value === null) {
    return '—';
  }

  return `${Number(value).toLocaleString('pt-BR', { maximumFractionDigits: 1 })}${suffix}`;
}

/** Uma linha da tabela: o setor ou um time, com os mesmos números que os gráficos mostram. */
export interface AccuracyRow {
  readonly label: string;
  readonly isSector: boolean;
  readonly isAboveSector: boolean;
  readonly used: number;
  readonly baselineDays: number | null;
  readonly completionDays: number | null;
  readonly onTime: number | null;
  readonly estimateDifference: number | null;
  readonly baselineText: string;
  readonly completionText: string;
  readonly onTimeText: string;
  readonly estimateText: string;
}

function num(value: number | string | null): number | null {
  return value === null ? null : Number(value);
}

function rowFrom(
  label: string,
  indicators: AccuracyIndicatorsResponse,
  isSector: boolean,
  isAboveSector: boolean,
): AccuracyRow {
  const baselineDays = num(indicators.averageDeviationFromBaselineDays);
  const completionDays = num(indicators.averageDeviationFromCompletionForecastDays);
  const onTime = num(indicators.onTimePercentage);
  const estimateDifference = num(indicators.averageEstimateDifferencePercent);
  return {
    label,
    isSector,
    isAboveSector,
    used: Number(indicators.usedCount),
    baselineDays,
    completionDays,
    onTime,
    estimateDifference,
    baselineText: metricText(baselineDays, ' dias'),
    completionText: metricText(completionDays, ' dias'),
    onTimeText: metricText(onTime, '%'),
    estimateText: metricText(estimateDifference, '%'),
  };
}

/** O setor primeiro, depois os times na ordem que a API manda (alfabética). */
export function accuracyRows(response: AccuracyResponse): AccuracyRow[] {
  return [
    rowFrom('Setor', response.sector, true, false),
    ...response.teams.map((team) =>
      rowFrom(team.teamName ?? 'Time sem nome', team.indicators, false, team.isAboveSector),
    ),
  ];
}

/** O gráfico de desvio de prazo: a média contra a linha de base e contra a previsão da conclusão, em dias. */
export function deviationChart(rows: readonly AccuracyRow[]): ChartConfiguration<'bar'> {
  return {
    type: 'bar',
    data: {
      labels: rows.map((row) => row.label),
      datasets: [
        {
          label: 'Contra a primeira previsão (dias)',
          data: rows.map((row) => row.baselineDays),
          backgroundColor: ACCURACY_COLORS.baseline,
        },
        {
          label: 'Contra a previsão da conclusão (dias)',
          data: rows.map((row) => row.completionDays),
          backgroundColor: ACCURACY_COLORS.completion,
        },
      ],
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { position: 'bottom' } },
    },
  };
}

/** O gráfico de estimado × real: a diferença média em percentual, por time e para o setor. */
export function estimateChart(rows: readonly AccuracyRow[]): ChartConfiguration<'bar'> {
  return {
    type: 'bar',
    data: {
      labels: rows.map((row) => row.label),
      datasets: [
        {
          label: 'Real em relação ao estimado (%)',
          data: rows.map((row) => row.estimateDifference),
          backgroundColor: ACCURACY_COLORS.estimate,
        },
      ],
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { position: 'bottom' } },
    },
  };
}

/**
 * Os motivos de demanda de fora de um indicador, em frases. Quem não entrou por falta de dado aparece com a
 * contagem, para o gestor saber que o número é de um conjunto menor.
 */
export function excludedNotes(indicators: AccuracyIndicatorsResponse): string[] {
  const reasons: [number | string, string][] = [
    [
      indicators.excludedWithoutSnapshot,
      'sem previsão de conclusão (concluídas antes desta versão)',
    ],
    [indicators.excludedWithoutBaseline, 'sem primeira previsão'],
    [indicators.excludedWithoutActualEffort, 'sem esforço real'],
    [indicators.excludedWithoutEstimate, 'sem estimativa'],
  ];

  return reasons
    .map(([count, reason]) => [Number(count), reason] as const)
    .filter(([count]) => count > 0)
    .map(([count, reason]) =>
      count === 1
        ? `1 demanda ficou de fora (${reason})`
        : `${count} demandas ficaram de fora (${reason})`,
    );
}
