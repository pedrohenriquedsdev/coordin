import { Component, computed, input } from '@angular/core';
import { ChartComponent } from '../charts/chart.component';
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
  imports: [ChartComponent],
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

  protected readonly efficiencyChart = computed(() => buildEfficiencyChart(this.result()));
  protected readonly efficiencyRows = computed(() => efficiencyTableRows(this.result()));
  protected readonly deviationChart = computed(() => buildDeviationChart(this.result()));
  protected readonly averages = computed(() => averageRows(this.result()));
  protected readonly deviations = computed(() => deviationRows(this.result(), this.titles()));
  protected readonly priorities = computed(() => priorityTableRows(this.result(), this.titles()));
  protected readonly prioritySummary = computed(() => priorityCheckSummary(this.result()));
}
