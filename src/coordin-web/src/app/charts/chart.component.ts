import {
  AfterViewInit,
  Component,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  InjectionToken,
  input,
  untracked,
  viewChild,
} from '@angular/core';
import { Chart, ChartConfiguration } from './chart-setup';

/** Cria o gráfico sobre um `<canvas>`. Em produção é o Chart.js; os testes trocam por um dublê (DT-36). */
export type ChartFactory = (
  canvas: HTMLCanvasElement,
  configuration: ChartConfiguration<'bar'>,
) => Chart<'bar'>;

export const CHART_FACTORY = new InjectionToken<ChartFactory>('CHART_FACTORY', {
  providedIn: 'root',
  factory: () => (canvas, configuration) => new Chart(canvas, configuration),
});

/**
 * Gráfico do Chart.js em um componente próprio (DT-36). Recebe a configuração pronta por entrada e não tem
 * regra de negócio: quem monta a configuração é a tela, a partir dos dados do motor.
 *
 * O gráfico é criado quando o `<canvas>` existe (`ngAfterViewInit`), atualizado quando a configuração muda
 * (`effect`) e destruído ao sair da tela.
 */
@Component({
  selector: 'app-chart',
  template: `
    <div class="relative h-72 w-full">
      <canvas #canvas role="img" [attr.aria-label]="label()"></canvas>
    </div>
  `,
})
export class ChartComponent implements AfterViewInit {
  readonly configuration = input.required<ChartConfiguration<'bar'>>();
  readonly label = input('Gráfico');

  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private readonly createChart = inject(CHART_FACTORY);
  private chart: Chart<'bar'> | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.chart?.destroy());

    effect(() => {
      const configuration = this.configuration();
      untracked(() => this.update(configuration));
    });
  }

  /** O canvas já existe quando a tela termina de desenhar: aqui o gráfico é criado. */
  ngAfterViewInit(): void {
    this.chart = this.createChart(this.canvas().nativeElement, this.configuration());
  }

  /** Atualiza o gráfico que já existe; antes da criação, não há o que atualizar. */
  private update(configuration: ChartConfiguration<'bar'>): void {
    if (this.chart === null) {
      return;
    }

    this.chart.data = configuration.data;
    this.chart.options = configuration.options ?? {};
    this.chart.update();
  }
}
