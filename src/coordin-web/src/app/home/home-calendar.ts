import { Component, computed, effect, ElementRef, input, signal, viewChild } from '@angular/core';
import { animate } from 'motion';
import { AbsenceResponse } from '../api/models/absence-response';
import { HolidayResponse } from '../api/models/holiday-response';
import {
  addMonths,
  formatLongCalendarDate,
  formatMonth,
  isSameMonth,
  monthGrid,
  startOfMonth,
  WEEKDAY_SHORT,
} from '../core/dates/calendar-date';
import { canAnimate } from '../core/ui/hub-motion';
import { Icon } from '../core/ui/icon';
import { ScheduleItem } from '../planning/planning';
import { buildDayEvents, DayEvent } from './home-data';

const KIND_LABEL = {
  holiday: 'Feriado',
  absence: 'Ausência',
  delivery: 'Entrega prevista',
} as const;

/**
 * Calendário do mês, interativo: navega entre meses, marca feriados, ausências e entregas previstas, e
 * mostra o detalhe do dia escolhido. Cada dia é um botão com a descrição completa para leitores de tela;
 * as cores dos pontos sempre têm o texto equivalente na legenda e no painel do dia.
 */
@Component({
  selector: 'app-home-calendar',
  imports: [Icon],
  templateUrl: './home-calendar.html',
})
export class HomeCalendar {
  readonly today = input.required<string>();
  readonly holidays = input.required<readonly HolidayResponse[]>();
  readonly absences = input.required<readonly AbsenceResponse[]>();
  readonly items = input.required<readonly ScheduleItem[]>();
  /** Mostra a entrega prevista na legenda (a tela de Calendário não carrega o cronograma e a esconde). */
  readonly showDeliveries = input(true);

  protected readonly weekdays = WEEKDAY_SHORT;
  protected readonly kindLabel = KIND_LABEL;

  private readonly shownMonth = signal<string | null>(null);
  private readonly picked = signal<string | null>(null);
  private direction = 1;
  private readonly gridElement = viewChild<ElementRef<HTMLElement>>('grid');

  protected readonly month = computed(() => this.shownMonth() ?? startOfMonth(this.today()));
  protected readonly selected = computed(() => this.picked() ?? this.today());
  protected readonly title = computed(() => formatMonth(this.month()));
  protected readonly days = computed(() => monthGrid(this.month()));

  private readonly events = computed(() => {
    const grid = this.days();
    return buildDayEvents({
      holidays: this.holidays(),
      absences: this.absences(),
      items: this.items(),
      from: grid[0],
      to: grid[grid.length - 1],
    });
  });

  protected readonly selectedEvents = computed<DayEvent[]>(
    () => this.events().get(this.selected()) ?? [],
  );
  protected readonly selectedTitle = computed(() => formatLongCalendarDate(this.selected()));

  constructor() {
    // Ao trocar de mês, a grade desliza de leve na direção da navegação.
    effect(() => {
      this.month();
      const element = this.gridElement()?.nativeElement;
      if (element && canAnimate(element)) {
        animate(
          element,
          {
            opacity: [0.2, 1],
            transform: [`translateX(${this.direction * 24}px)`, 'translateX(0)'],
          },
          { duration: 0.35, ease: [0.16, 1, 0.3, 1] },
        );
      }
    });
  }

  protected eventsOf(date: string): DayEvent[] {
    return this.events().get(date) ?? [];
  }

  protected kinds(date: string): ('holiday' | 'absence' | 'delivery')[] {
    return [...new Set(this.eventsOf(date).map((event) => event.kind))];
  }

  protected dayNumber(date: string): number {
    return Number(date.slice(8));
  }

  protected inMonth(date: string): boolean {
    return isSameMonth(date, this.month());
  }

  protected label(date: string): string {
    const count = this.eventsOf(date).length;
    const text = count === 0 ? 'sem eventos' : count === 1 ? '1 evento' : `${count} eventos`;
    return `${formatLongCalendarDate(date)}: ${text}`;
  }

  protected go(offset: number): void {
    this.direction = offset;
    this.shownMonth.set(addMonths(this.month(), offset));
  }

  protected goToday(): void {
    this.direction = this.month() < startOfMonth(this.today()) ? 1 : -1;
    this.shownMonth.set(startOfMonth(this.today()));
    this.picked.set(this.today());
  }

  protected pick(date: string): void {
    this.picked.set(date);
    if (!this.inMonth(date)) {
      this.direction = date < this.month() ? -1 : 1;
      this.shownMonth.set(startOfMonth(date));
    }
  }
}
