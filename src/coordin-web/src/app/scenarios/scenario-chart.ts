import type { ChartConfiguration } from '../charts/chart-setup';
import { ScenarioItem } from './scenarios';

/**
 * Cores das barras. A contribuição negativa diminui o critério (a demanda ajuda o cenário); a positiva o aumenta.
 * A cor reforça o sentido, mas ele também aparece em texto na tabela ao lado ("melhora" e "piora").
 */
export const SCENARIO_COLORS = {
  improves: '#166534',
  worsens: '#b91c1c',
} as const;

/**
 * Configuração do gráfico de justificativa de um cenário (RF-13; DT-36): uma barra horizontal por demanda, com a
 * contribuição dela para a diferença do critério. Horizontal porque lê melhor títulos longos. Função pura: os
 * valores e a escolha das demandas vêm de fora; aqui só se organizam para desenhar.
 */
export function buildScenarioChart(items: readonly ScenarioItem[]): ChartConfiguration<'bar'> {
  return {
    type: 'bar',
    data: {
      labels: items.map((item) => item.title),
      datasets: [
        {
          label: 'Contribuição para o critério',
          data: items.map((item) => item.scoreContributionDelta),
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
        x: { title: { display: true, text: 'Contribuição para o critério' } },
      },
      plugins: { legend: { display: false } },
    },
  };
}
