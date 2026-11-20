import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { addDays, Clock, formatCalendarDate } from '../core/dates/calendar-date';
import { CountUp, GrowBar, Reveal } from '../core/ui/hub-motion';
import { GlowField } from '../core/ui/glow-field';
import { Icon } from '../core/ui/icon';
import { Loader } from '../core/ui/loader';
import { PageHeader } from '../core/ui/page-header';
import { buildTimeline } from '../home/home-data';
import { HomeTimeline } from '../home/home-timeline';
import { deviationLabel, prerequisitesLabel, unpredictableReasonLabel } from '../planning/labels';
import { ScheduleItem } from '../planning/planning';
import { ResetToPriority } from '../scenarios/reset-to-priority';
import { ScheduleStore } from './schedule.store';
import { countByFilter, filterItems, ScheduleFilter, summarizeSchedule } from './schedule-view';

/**
 * Tela "Cronograma" (T02, M01; RF-11): tela inicial, com as demandas na ordem de execução. Em telas
 * largas aparece como tabela; em telas de 360 px, como cartões (RNF-03). Estado e alerta sempre em texto.
 */
@Component({
  selector: 'app-schedule-page',
  imports: [
    RouterLink,
    ResetToPriority,
    PageHeader,
    Icon,
    Loader,
    GlowField,
    HomeTimeline,
    Reveal,
    CountUp,
    GrowBar,
  ],
  providers: [ScheduleStore],
  templateUrl: './schedule-page.html',
})
export class SchedulePage {
  protected readonly store = inject(ScheduleStore);
  protected readonly today = inject(Clock).today();

  protected readonly filters: readonly { id: ScheduleFilter; label: string }[] = [
    { id: 'all', label: 'Todas' },
    { id: 'predictable', label: 'Previsíveis' },
    { id: 'unpredictable', label: 'Sem previsão' },
    { id: 'late', label: 'Passam do prazo' },
    { id: 'completed', label: 'Concluídas' },
  ];
  protected readonly ranges: readonly { days: number | null; label: string }[] = [
    { days: 30, label: '30 dias' },
    { days: 60, label: '60 dias' },
    { days: 90, label: '90 dias' },
    { days: null, label: 'Tudo' },
  ];

  protected readonly filter = signal<ScheduleFilter>('all');
  protected readonly query = signal('');
  protected readonly range = signal<number | null>(60);

  private readonly items = computed(() => this.store.schedule()?.items ?? []);
  protected readonly summary = computed(() => summarizeSchedule(this.items()));
  protected readonly counts = computed(() => countByFilter(this.items()));
  protected readonly visible = computed(() =>
    filterItems(this.items(), this.filter(), this.query()),
  );

  /** A linha do tempo mostra as demandas que terminam dentro do período escolhido (ou todas). */
  protected readonly timeline = computed(() => {
    const days = this.range();
    const limit = days === null ? null : addDays(this.today, days);
    const inRange = this.items().filter(
      (item) => limit === null || (item.forecastEndDate !== null && item.forecastEndDate <= limit),
    );
    return buildTimeline(inRange, this.today, 12);
  });

  constructor() {
    this.store.load();
  }

  protected onSearch(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }

  protected clearFilters(): void {
    this.filter.set('all');
    this.query.set('');
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

  /** A linha "Depende de: A, B" (RF-18), ou `null` quando a demanda não depende de nenhuma. */
  protected prerequisitesOf(item: ScheduleItem): string | null {
    return prerequisitesLabel(item.prerequisites.map((prerequisite) => prerequisite.title));
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
