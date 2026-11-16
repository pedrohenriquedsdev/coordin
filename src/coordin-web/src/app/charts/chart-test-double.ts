import { ChartFactory } from './chart.component';

/**
 * Dublê do Chart.js para os testes. O jsdom não desenha em `<canvas>`: a fábrica de gráficos
 * (`CHART_FACTORY`) é trocada por esta, que só conta as chamadas. Fica em `src/app` e é compilado pelo build
 * de produção, por isso não usa nada exclusivo dos testes (como `vi`).
 */
export class FakeChart {
  updates = 0;
  destroys = 0;
  data: unknown;
  options: unknown;

  constructor(
    readonly canvas: HTMLCanvasElement,
    configuration: { data: unknown; options?: unknown },
  ) {
    this.data = configuration.data;
    this.options = configuration.options;
  }

  update(): void {
    this.updates += 1;
  }

  destroy(): void {
    this.destroys += 1;
  }
}

/** Cria a fábrica falsa e a lista de gráficos criados por ela. */
export function createFakeChartFactory(): { factory: ChartFactory; created: FakeChart[] } {
  const created: FakeChart[] = [];
  const factory = ((canvas, configuration) => {
    const chart = new FakeChart(canvas, configuration);
    created.push(chart);
    return chart as unknown as ReturnType<ChartFactory>;
  }) as ChartFactory;
  return { factory, created };
}
