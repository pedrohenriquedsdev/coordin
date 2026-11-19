import type { ChartConfiguration } from '../charts/chart-setup';
import { LoadWeek, WeeklyDeliveries } from './home-data';

/** Cores do tema atual para o Chart.js (que desenha em canvas e não herda o CSS). */
export interface ChartTheme {
  readonly text: string;
  readonly grid: string;
}

export const LIGHT_CHART: ChartTheme = { text: '#3f5a5e', grid: 'rgba(63, 90, 94, 0.14)' };
export const DARK_CHART: ChartTheme = { text: '#b3cacd', grid: 'rgba(179, 202, 205, 0.16)' };

const BLUE = '#1ec1cb';
const BLUE_SOFT = 'rgba(30, 193, 203, 0.28)';
const RED = '#cc0000';

function baseOptions(theme: ChartTheme, yTitle: string): ChartConfiguration<'bar'>['options'] {
  return {
    responsive: true,
    maintainAspectRatio: false,
    animation: { duration: 700 },
    interaction: { mode: 'index', intersect: false },
    scales: {
      x: { stacked: true, grid: { display: false }, ticks: { color: theme.text } },
      y: {
        stacked: true,
        beginAtZero: true,
        grid: { color: theme.grid },
        ticks: { color: theme.text, precision: 0 },
        title: { display: true, text: yTitle, color: theme.text },
      },
    },
    plugins: { legend: { position: 'bottom', labels: { color: theme.text, usePointStyle: true } } },
  };
}

/** Carga por semana: horas alocadas e ociosas empilhadas; a semana sobrecarregada fica em vermelho. */
export function buildLoadChart(
  weeks: readonly LoadWeek[],
  theme: ChartTheme,
): ChartConfiguration<'bar'> {
  return {
    type: 'bar',
    data: {
      labels: weeks.map((week) => week.label),
      datasets: [
        {
          label: 'Horas alocadas',
          data: weeks.map((week) => week.allocated),
          backgroundColor: weeks.map((week) => (week.overloaded ? RED : BLUE)),
          borderRadius: 6,
          stack: 'hours',
        },
        {
          label: 'Horas ociosas',
          data: weeks.map((week) => week.idle),
          backgroundColor: BLUE_SOFT,
          borderRadius: 6,
          stack: 'hours',
        },
      ],
    },
    options: baseOptions(theme, 'Horas'),
  };
}

/** Entregas previstas por semana: as que cabem no prazo informado e as que passam dele. */
export function buildDeliveriesChart(
  series: WeeklyDeliveries,
  theme: ChartTheme,
): ChartConfiguration<'bar'> {
  return {
    type: 'bar',
    data: {
      labels: series.labels,
      datasets: [
        {
          label: 'Dentro do prazo',
          data: series.onTime,
          backgroundColor: '#1ec1cb',
          borderRadius: 6,
          stack: 'deliveries',
        },
        {
          label: 'Passam do prazo informado',
          data: series.late,
          backgroundColor: RED,
          borderRadius: 6,
          stack: 'deliveries',
        },
      ],
    },
    options: baseOptions(theme, 'Demandas'),
  };
}
