import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { formatCalendarDate } from '../core/dates/calendar-date';
import { deviationLabel, unpredictableReasonLabel } from '../planning/labels';
import { ScheduleItem } from '../planning/planning';
import { ScheduleStore } from './schedule.store';

/**
 * Tela "Cronograma" (T02, M01; RF-11): tela inicial, com as demandas na ordem de execução. Em telas
 * largas aparece como tabela; em telas de 360 px, como cartões (RNF-03). Estado e alerta sempre em texto.
 */
@Component({
  selector: 'app-schedule-page',
  imports: [RouterLink],
  providers: [ScheduleStore],
  templateUrl: './schedule-page.html',
})
export class SchedulePage {
  protected readonly store = inject(ScheduleStore);

  constructor() {
    this.store.load();
  }

  /** Data de calendário em `dd/MM/yyyy`; sem data, um travessão (nunca uma data inventada). */
  protected dateOf(value: string | null): string {
    return value ? formatCalendarDate(value) : '—';
  }

  /** Horas em pt-BR, com "sem estimativa" quando o time ainda não estimou. */
  protected hoursOf(value: number | null): string {
    return value === null
      ? 'sem estimativa'
      : `${value.toLocaleString('pt-BR', { maximumFractionDigits: 2 })} h`;
  }

  protected reasonOf(item: ScheduleItem): string {
    return unpredictableReasonLabel(item.unpredictableReason);
  }

  /** Desvio de uma demanda concluída: contra a linha de base, e contra a previsão vigente. */
  protected baselineDeviation(item: ScheduleItem): string {
    return item.baselineDeviationDays === null
      ? 'Sem previsão para comparar'
      : deviationLabel(item.baselineDeviationDays);
  }

  protected forecastDeviation(item: ScheduleItem): string {
    return item.forecastDeviationDays === null
      ? 'Sem previsão para comparar'
      : deviationLabel(item.forecastDeviationDays);
  }
}
