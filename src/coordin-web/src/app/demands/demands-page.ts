import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Clock, formatCalendarDate, isEndBeforeStart } from '../core/dates/calendar-date';
import { messageFor } from '../core/problems/messages';
import { GlowField } from '../core/ui/glow-field';
import { HoldButton } from '../core/ui/hold-button';
import { Reveal } from '../core/ui/hub-motion';
import { Icon } from '../core/ui/icon';
import { Pager } from '../core/ui/pager';
import { ShowMore } from '../core/ui/show-more';
import { Loader } from '../core/ui/loader';
import { PageHeader } from '../core/ui/page-header';
import { normalizeText } from '../schedule/schedule-view';
import { readProblem } from '../core/problems/problem';
import { deviationLabel, hoursLabel, unpredictableReasonLabel } from '../planning/labels';
import { Demand, ScheduleItem } from '../planning/planning';
import { DemandsStore, MoveDirection } from './demands.store';

export type DemandTab = 'active' | 'completed';

/** A mensagem de quando a data de conclusão cai antes do início (a mesma da API, DT-18). */
const COMPLETION_BEFORE_START = 'A data de conclusão não pode ser anterior ao início da demanda.';

/**
 * Tela "Demandas" (T03, T06; RF-04, RF-12): as demandas ativas na ordem de prioridade, com Subir e Descer,
 * exclusão com confirmação e conclusão na própria linha; e a aba "Concluídas", só de leitura.
 */
/** Mensagem quando o esforço real informado não é um número maior que zero. */
const ACTUAL_EFFORT_INVALID = 'Informe um esforço real maior que zero, ou deixe em branco.';

@Component({
  selector: 'app-demands-page',
  imports: [RouterLink, PageHeader, Icon, Loader, GlowField, HoldButton, Reveal, ShowMore],
  providers: [DemandsStore],
  templateUrl: './demands-page.html',
})
export class DemandsPage {
  protected readonly store = inject(DemandsStore);
  private readonly clock = inject(Clock);

  protected readonly tab = signal<DemandTab>('active');
  protected readonly message = signal<string | null>(null);
  protected readonly busyId = signal<string | null>(null);
  protected readonly deletingId = signal<string | null>(null);
  protected readonly completingId = signal<string | null>(null);
  protected readonly completedOn = signal('');
  /** O esforço real digitado na conclusão (RF-22); vazio = não informado. */
  protected readonly actualEffort = signal('');
  protected readonly query = signal('');
  /** Com uma busca ativa a lista é parcial: reordenar ficaria confuso, então Subir e Descer esperam. */
  protected readonly searching = computed(() => this.query().trim() !== '');

  private matches(demand: Demand): boolean {
    const needle = normalizeText(this.query());
    return needle === '' || normalizeText(demand.title).includes(needle);
  }

  protected readonly activeList = computed(() =>
    this.store.active().filter((d) => this.matches(d)),
  );
  protected readonly completedList = computed(() =>
    this.store.completed().filter((d) => this.matches(d)),
  );
  protected readonly visible = computed(() =>
    this.tab() === 'active' ? this.activeList() : this.completedList(),
  );

  /** "Mostrar mais": só os primeiros itens aparecem; Subir, Descer e a busca continuam olhando a lista inteira. */
  protected readonly pager = new Pager();
  protected readonly activeShown = computed(() => this.pager.slice(this.activeList()));
  protected readonly completedShown = computed(() => this.pager.slice(this.completedList()));

  protected onSearch(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
    this.pager.reset();
  }

  protected clearSearch(): void {
    this.query.set('');
    this.pager.reset();
  }

  constructor() {
    this.store.load();
  }

  protected selectTab(tab: DemandTab): void {
    this.tab.set(tab);
    this.pager.reset();
    this.cancelAll();
  }

  protected hoursOf = hoursLabel;

  protected reasonOf(item: ScheduleItem | undefined): string {
    return item?.isPredictable
      ? 'Previsível'
      : unpredictableReasonLabel(item?.unpredictableReason ?? null);
  }

  protected dateOf(value: string | null): string {
    return value ? formatCalendarDate(value) : '—';
  }

  protected deviation(days: number | null): string {
    return days === null ? 'Sem previsão para comparar' : deviationLabel(days);
  }

  protected canMove(demand: Demand, direction: MoveDirection): boolean {
    return this.store.canMove(demand.id, direction);
  }

  protected move(demand: Demand, direction: MoveDirection): void {
    this.message.set(null);
    this.busyId.set(demand.id);
    this.store.move(demand.id, direction).subscribe({
      complete: () => this.busyId.set(null),
      error: (error: unknown) =>
        this.fail(error, 'Não foi possível alterar a ordem de prioridade.'),
    });
  }

  protected startDelete(demand: Demand): void {
    this.cancelAll();
    this.deletingId.set(demand.id);
  }

  protected confirmDelete(demand: Demand): void {
    this.message.set(null);
    this.busyId.set(demand.id);
    this.store.delete(demand.id).subscribe({
      complete: () => {
        this.busyId.set(null);
        this.deletingId.set(null);
      },
      error: (error: unknown) => this.fail(error, 'Não foi possível excluir a demanda.'),
    });
  }

  protected startComplete(demand: Demand): void {
    this.cancelAll();
    this.completingId.set(demand.id);
    this.completedOn.set(this.clock.today());
    this.actualEffort.set('');
  }

  protected setActualEffort(event: Event): void {
    this.actualEffort.set((event.target as HTMLInputElement).value);
  }

  protected setCompletedOn(event: Event): void {
    this.completedOn.set((event.target as HTMLInputElement).value);
  }

  protected confirmComplete(demand: Demand): void {
    this.message.set(null);
    const date = this.completedOn();
    if (date === '') {
      this.message.set('Informe a data de conclusão.');
      return;
    }

    if (demand.startDate !== null && isEndBeforeStart(demand.startDate, date)) {
      this.message.set(COMPLETION_BEFORE_START);
      return;
    }

    const effortText = this.actualEffort().trim().replace(',', '.');
    let actualEffortHours: number | null = null;
    if (effortText !== '') {
      const hours = Number(effortText);
      if (!Number.isFinite(hours) || hours <= 0) {
        this.message.set(ACTUAL_EFFORT_INVALID);
        return;
      }

      actualEffortHours = hours;
    }

    this.busyId.set(demand.id);
    this.store.complete(demand.id, date, actualEffortHours).subscribe({
      complete: () => {
        this.busyId.set(null);
        this.completingId.set(null);
      },
      error: (error: unknown) => this.fail(error, 'Não foi possível registrar a conclusão.'),
    });
  }

  protected cancelAll(): void {
    this.message.set(null);
    this.deletingId.set(null);
    this.completingId.set(null);
  }

  private fail(error: unknown, fallback: string): void {
    this.busyId.set(null);
    this.message.set(messageFor(readProblem(error), fallback));
  }
}
