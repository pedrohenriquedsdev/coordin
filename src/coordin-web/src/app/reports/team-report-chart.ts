import type { ChartConfiguration } from '../charts/chart-setup';
import { ReportTeam, weekLabel } from './team-report';

/** Cores do gráfico: o destaque da sobrecarga usa vermelho, além de o texto dizer "Sobrecarga" na tabela. */
export const CHART_COLORS = {
  allocated: '#334155',
  overloaded: '#b91c1c',
  idle: '#cbd5e1',
} as const;

/**
 * Configuração do gráfico de um time (RF-17; DT-36): uma barra por semana, com as horas alocadas e as horas
 * ociosas empilhadas (as duas somam a capacidade). A barra de uma semana sobrecarregada ganha a cor de
 * destaque. Função pura: os números vêm do motor; aqui só se organizam para desenhar.
 */
export function buildTeamChart(team: ReportTeam): ChartConfiguration<'bar'> {
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
          stack: 'hours',
        },
        {
          label: 'Horas ociosas',
          data: team.weeks.map((week) => week.idleHours),
          backgroundColor: CHART_COLORS.idle,
          stack: 'hours',
        },
      ],
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      scales: {
        x: { stacked: true },
        y: { stacked: true, beginAtZero: true, title: { display: true, text: 'Horas' } },
      },
      plugins: { legend: { position: 'bottom' } },
    },
  };
}
