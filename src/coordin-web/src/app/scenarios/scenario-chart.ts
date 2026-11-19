import type { ChartConfiguration } from '../charts/chart-setup';
import { ScenarioItem } from './scenarios';

/** Cores do texto e da grade do gráfico (o Chart.js desenha em canvas e não herda o CSS). */
export interface ScenarioChartTheme {
  readonly text: string;
  readonly grid: string;
}

const LIGHT: ScenarioChartTheme = { text: '#3f5a5e', grid: 'rgba(63, 90, 94, 0.14)' };
export const DARK_SCENARIO_CHART: ScenarioChartTheme = {
  text: '#b3cacd',
  grid: 'rgba(179, 202, 205, 0.16)',
};

/**
 * Cores das barras. A contribuição negativa diminui o critério (a demanda ajuda o cenário); a positiva o aumenta.
 * A cor reforça o sentido, mas ele também aparece em texto na tabela ao lado ("melhora" e "piora").
 */
export const SCENARIO_COLORS = {
  improves: '#1ec1cb',
  worsens: '#cc0000',
} as const;

/**
 * Configuração do gráfico de justificativa de um cenário (RF-13; DT-36): uma barra horizontal por demanda, com a
 * contribuição dela para a diferença do critério. Horizontal porque lê melhor títulos longos. Função pura: os
 * valores e a escolha das demandas vêm de fora; aqui só se organizam para desenhar.
 */
export function buildScenarioChart(
  items: readonly ScenarioItem[],
  theme: ScenarioChartTheme = LIGHT,
): ChartConfiguration<'bar'> {
  return {
    type: 'bar',
    data: {
      labels: items.map((item) => item.title),
      datasets: [
        {
          label: 'Contribuição para o critério',
          data: items.map((item) => item.scoreContributionDelta),
          borderRadius: 6,
          backgroundColor: items.map((item) =>
            item.scoreContributionDelta < 0 ? SCENARIO_COLORS.improves : SCENARIO_COLORS.worsens,
          ),
        },
      ],
    },
    options: {
      indexAxis: 'y',
      responsive: true,
      maintainAspectRatio: false,
      scales: {
        x: {
          grid: { color: theme.grid },
          ticks: { color: theme.text },
          title: { display: true, text: 'Contribuição para o critério', color: theme.text },
        },
        y: { grid: { display: false }, ticks: { color: theme.text } },
      },
      plugins: { legend: { display: false } },
    },
  };
}
