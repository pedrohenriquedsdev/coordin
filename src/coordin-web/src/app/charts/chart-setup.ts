import {
  BarController,
  BarElement,
  CategoryScale,
  Chart,
  Legend,
  LinearScale,
  Tooltip,
} from 'chart.js';

/**
 * Registro dos componentes do Chart.js que os gráficos do Coordin usam (DT-36). Registrar só o necessário
 * mantém o pacote pequeno, em vez de importar `chart.js/auto` (que traz todos os tipos de gráfico).
 */
Chart.register(BarController, BarElement, CategoryScale, LinearScale, Tooltip, Legend);

export { Chart };
export type { ChartConfiguration } from 'chart.js';
