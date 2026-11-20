import { Component, computed, inject, input } from '@angular/core';
import { ChartComponent } from '../charts/chart.component';
import { DARK_CHART_THEME } from '../charts/chart-theme';
import { Icon } from '../core/ui/icon';
import { ThemeService } from '../core/ui/theme.service';
import { formatCalendarDate } from '../core/dates/calendar-date';
import { ValidationResult } from '../api/models/validation-result';
import { priorityCheckSummary } from './labels';
import {
  averageRows,
  buildDeviationChart,
  buildEfficiencyChart,
  deviationRows,
  efficiencyTableRows,
  priorityTableRows,
} from './result-view';

/**
 * O resultado da comparação (RF-24): eficiência, desvios e a checagem das prioritárias, cada um com gráfico e
 * tabela com os mesmos valores. Não calcula nada: tudo vem pronto do resultado da API.
 */
@Component({
  selector: 'app-validation-result',
  imports: [ChartComponent, Icon],
  templateUrl: './validation-result.html',
})
export class ValidationResultView {
  readonly result = input.required<ValidationResult>();
  /** Os títulos das demandas, por identificador (a API manda só o identificador nos resultados). */
  readonly titles = input.required<ReadonlyMap<string, string>>();

  protected readonly period = computed(() => {
    const current = this.result();
    return `${formatCalendarDate(current.periodStart)} a ${formatCalendarDate(current.periodEnd)}`;
  });

  private readonly theme = inject(ThemeService);
  private readonly chartTheme = computed(() =>
    this.theme.isDark() ? DARK_CHART_THEME : undefined,
  );

  protected readonly efficiencyChart = computed(() =>
    buildEfficiencyChart(this.result(), this.chartTheme()),
  );
  protected readonly efficiencyRows = computed(() => efficiencyTableRows(this.result()));
  protected readonly deviationChart = computed(() =>
    buildDeviationChart(this.result(), this.chartTheme()),
  );
  protected readonly averages = computed(() => averageRows(this.result()));
  protected readonly deviations = computed(() => deviationRows(this.result(), this.titles()));
  protected readonly priorities = computed(() => priorityTableRows(this.result(), this.titles()));
  protected readonly prioritySummary = computed(() => priorityCheckSummary(this.result()));
}
