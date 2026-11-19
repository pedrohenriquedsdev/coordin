/** Cores do texto e da grade de um gráfico (o Chart.js desenha em canvas e não herda o CSS do tema). */
export interface ChartTheme {
  readonly text: string;
  readonly grid: string;
}

export const LIGHT_CHART_THEME: ChartTheme = { text: '#3f5a5e', grid: 'rgba(63, 90, 94, 0.14)' };
export const DARK_CHART_THEME: ChartTheme = { text: '#b3cacd', grid: 'rgba(179, 202, 205, 0.16)' };
