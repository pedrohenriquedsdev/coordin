import { Component, computed, inject } from '@angular/core';
import { ChartComponent } from '../charts/chart.component';
import { DARK_CHART_THEME } from '../charts/chart-theme';
import { Clock, addDays } from '../core/dates/calendar-date';
import { Icon } from '../core/ui/icon';
import { Loader } from '../core/ui/loader';
import { PageHeader } from '../core/ui/page-header';
import { Reveal } from '../core/ui/hub-motion';
import { ThemeService } from '../core/ui/theme.service';
import { AccuracyStore } from './accuracy.store';
import { deviationChart, estimateChart, excludedNotes } from './accuracy-view';

/**
 * Página de indicadores de precisão (RF-23): filtro por período de conclusão, gráficos de desvio e de estimado ×
 * real, e uma tabela com os mesmos valores. Não calcula nada; a regra fica no motor e na API.
 */
@Component({
  selector: 'app-accuracy-page',
  imports: [ChartComponent, PageHeader, Icon, Loader, Reveal],
  providers: [AccuracyStore],
  templateUrl: './accuracy-page.html',
})
export class AccuracyPage {
  protected readonly store = inject(AccuracyStore);

  private readonly theme = inject(ThemeService);
  private readonly clock = inject(Clock);

  private readonly chartTheme = computed(() =>
    this.theme.isDark() ? DARK_CHART_THEME : undefined,
  );
  protected readonly deviation = computed(() =>
    deviationChart(this.store.rows(), this.chartTheme()),
  );
  protected readonly estimate = computed(() => estimateChart(this.store.rows(), this.chartTheme()));

  /** O setor (primeira linha), para os indicadores do topo. */
  protected readonly sector = computed(() => this.store.rows()[0] ?? null);

  protected readonly presets = [
    { id: 'year', label: 'Este ano' },
    { id: '90', label: 'Últimos 90 dias' },
    { id: '30', label: 'Últimos 30 dias' },
  ] as const;

  /** Atalho de período: ajusta as duas datas e já busca. */
  protected usePreset(id: 'year' | '90' | '30'): void {
    const today = this.clock.today();
    this.store.to.set(today);
    this.store.from.set(id === 'year' ? `${today.slice(0, 4)}-01-01` : addDays(today, -Number(id)));
    this.store.search();
  }

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
