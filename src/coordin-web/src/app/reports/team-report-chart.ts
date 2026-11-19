import type { ChartConfiguration } from '../charts/chart-setup';
import { ReportTeam, weekLabel } from './team-report';

/** Cores do gráfico: o destaque da sobrecarga usa vermelho, além de o texto dizer "Sobrecarga" na tabela. */
export const CHART_COLORS = {
  allocated: '#1ec1cb',
  overloaded: '#cc0000',
  idle: 'rgba(47, 140, 255, 0.3)',
} as const;

/** Cores do texto e da grade (o Chart.js desenha em canvas e não herda o CSS). */
export interface TeamChartTheme {
  readonly text: string;
  readonly grid: string;
}

const LIGHT_THEME: TeamChartTheme = { text: '#3f5a5e', grid: 'rgba(63, 90, 94, 0.14)' };
export const DARK_TEAM_CHART: TeamChartTheme = {
  text: '#b3cacd',
  grid: 'rgba(179, 202, 205, 0.16)',
};

/**
 * Configuração do gráfico de um time (RF-17; DT-36): uma barra por semana, com as horas alocadas e as horas
 * ociosas empilhadas (as duas somam a capacidade). A barra de uma semana sobrecarregada ganha a cor de
 * destaque. Função pura: os números vêm do motor; aqui só se organizam para desenhar.
 */
export function buildTeamChart(
  team: ReportTeam,
  theme: TeamChartTheme = LIGHT_THEME,
): ChartConfiguration<'bar'> {
  return {
    type: 'bar',
    data: {
      labels: team.weeks.map((week) => weekLabel(week)),
      datasets: [
        {
          label: 'Horas alocadas',
          data: team.weeks.map((week) => week.allocatedHours),
          backgroundColor: team.weeks.map((week) =>
            week.isOverloaded ? CHART_COLORS.overloaded : CHART_COLORS.allocated,
          ),
          borderRadius: 6,
          stack: 'hours',
        },
        {
          label: 'Horas ociosas',
          data: team.weeks.map((week) => week.idleHours),
          backgroundColor: CHART_COLORS.idle,
          borderRadius: 6,
          stack: 'hours',
        },
      ],
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      scales: {
        x: { stacked: true, grid: { display: false }, ticks: { color: theme.text } },
        y: {
          stacked: true,
          beginAtZero: true,
          grid: { color: theme.grid },
          ticks: { color: theme.text },
          title: { display: true, text: 'Horas', color: theme.text },
        },
      },
      plugins: {
        legend: { position: 'bottom', labels: { color: theme.text, usePointStyle: true } },
      },
    },
  };
}
