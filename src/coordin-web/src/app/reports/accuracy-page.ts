import { Component, computed, inject } from '@angular/core';
import { ChartComponent } from '../charts/chart.component';
import { AccuracyStore } from './accuracy.store';
import { deviationChart, estimateChart, excludedNotes } from './accuracy-view';

/**
 * Página de indicadores de precisão (RF-23): filtro por período de conclusão, gráficos de desvio e de estimado ×
 * real, e uma tabela com os mesmos valores. Não calcula nada; a regra fica no motor e na API.
 */
@Component({
  selector: 'app-accuracy-page',
  imports: [ChartComponent],
  providers: [AccuracyStore],
  templateUrl: './accuracy-page.html',
})
export class AccuracyPage {
  protected readonly store = inject(AccuracyStore);

  protected readonly deviation = computed(() => deviationChart(this.store.rows()));
  protected readonly estimate = computed(() => estimateChart(this.store.rows()));

  /** As notas de demandas de fora, por linha (só do setor e dos times que aparecem). */
  protected notesOf(index: number): string[] {
    const current = this.store.response();
    if (current === null) {
      return [];
    }

    const indicators = index === 0 ? current.sector : current.teams[index - 1]?.indicators;
    return indicators === undefined ? [] : excludedNotes(indicators);
  }

  protected setFrom(event: Event): void {
    this.store.from.set((event.target as HTMLInputElement).value);
  }

  protected setTo(event: Event): void {
    this.store.to.set((event.target as HTMLInputElement).value);
  }

  protected submit(event: Event): void {
    event.preventDefault();
    this.store.search();
  }
}
