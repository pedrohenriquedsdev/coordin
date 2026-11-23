import {
  afterNextRender,
  Component,
  computed,
  ElementRef,
  inject,
  Injector,
  input,
  OnInit,
  output,
  signal,
} from '@angular/core';
import {
  addDays,
  addMonths,
  formatLongCalendarDate,
  formatMonth,
  isSameMonth,
  mondayOf,
  monthGrid,
  sundayOf,
  WEEKDAY_SHORT,
} from '../dates/calendar-date';

/**
 * O calendário que abre ao lado de um campo de data (ver `DatePicker`). Mostra um mês, de segunda a domingo,
 * com a data escolhida, o dia de hoje e os botões "Limpar" e "Hoje". Só trabalha com datas de calendário
 * (`aaaa-mm-dd`, sem fuso), pelas mesmas funções do restante do sistema.
 *
 * Teclado: setas movem o dia (esquerda e direita, um dia; cima e baixo, uma semana), Home e End vão ao início
 * e ao fim da semana, PageUp e PageDown trocam de mês (com Shift, de ano), Enter ou Espaço escolhem e Esc fecha.
 */
@Component({
  selector: 'app-date-popup',
  host: {
    role: 'dialog',
    'aria-label': 'Escolher data',
    class:
      'block w-[min(20rem,calc(100vw-1rem))] rounded-2xl border border-slate-300 bg-white p-3 shadow-pop',
    '(keydown)': 'onKeydown($event)',
  },
  template: `
    <div class="flex items-center justify-between gap-2">
      <button
        type="button"
        class="btn-secondary h-10 w-10 !p-0 md:h-9 md:w-9"
        aria-label="Mês anterior"
        (click)="shiftMonth(-1)"
      >
        <span aria-hidden="true">‹</span>
      </button>
      <p class="text-sm font-bold text-slate-900 first-letter:uppercase" aria-live="polite">
        {{ title() }}
      </p>
      <button
        type="button"
        class="btn-secondary h-10 w-10 !p-0 md:h-9 md:w-9"
        aria-label="Próximo mês"
        (click)="shiftMonth(1)"
      >
        <span aria-hidden="true">›</span>
      </button>
    </div>

    <div
      class="mt-2 grid grid-cols-7 text-center text-xs font-semibold text-slate-600"
      aria-hidden="true"
    >
      @for (weekday of weekdays; track weekday) {
        <span class="py-1">{{ weekday }}</span>
      }
    </div>

    <div class="grid grid-cols-7 gap-0.5" role="group" [attr.aria-label]="title()">
      @for (date of days(); track date) {
        <button
          type="button"
          class="flex h-10 items-center justify-center rounded-xl border text-sm transition-colors md:h-9"
          [class]="classOf(date)"
          [attr.data-date]="date"
          [attr.tabindex]="date === focused() ? 0 : -1"
          [attr.aria-label]="label(date)"
          [attr.aria-pressed]="date === value()"
          [attr.aria-current]="date === today() ? 'date' : null"
          (click)="picked.emit(date)"
        >
          {{ dayNumber(date) }}
        </button>
      }
    </div>

    <div class="mt-3 flex items-center justify-between gap-2">
      <button type="button" class="btn-secondary btn-sm rounded-full" (click)="cleared.emit()">
        Limpar
      </button>
      <button
        type="button"
        class="btn-secondary btn-sm rounded-full"
        (click)="picked.emit(today())"
      >
        Hoje
      </button>
    </div>
  `,
})
export class DatePopup implements OnInit {
  /** A data hoje no campo (`aaaa-mm-dd`), ou vazio. O calendário abre nela; sem ela, em "hoje". */
  readonly value = input('');
  /** O dia de hoje (do `Clock`), para o destaque e para o botão "Hoje". */
  readonly today = input.required<string>();

  /** O gestor escolheu um dia (ou "Hoje"). */
  readonly picked = output<string>();
  /** O gestor pediu "Limpar". */
  readonly cleared = output<void>();
  /** O gestor pediu para fechar (Esc). */
  readonly closed = output<void>();

  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  protected readonly weekdays = WEEKDAY_SHORT;
  /** O dia que o teclado está marcando; o mês mostrado é sempre o dele. */
  protected readonly focused = signal('');
  protected readonly title = computed(() => formatMonth(this.focused()));
  protected readonly days = computed(() => monthGrid(this.focused()));

  ngOnInit(): void {
    this.focused.set(this.value() || this.today());
  }

  /** Põe o foco do navegador no dia marcado (depois que a tela desenhou o mês novo). */
  focusDay(): void {
    afterNextRender(
      () => {
        this.element.nativeElement
          .querySelector<HTMLButtonElement>(`button[data-date="${this.focused()}"]`)
          ?.focus();
      },
      { injector: this.injector },
    );
  }

  protected shiftMonth(months: number): void {
    // Mantém o dia do mês, ou o último dia dele quando o mês novo é mais curto (31 de março → 28 de fevereiro).
    const day = Number(this.focused().slice(8));
    const first = addMonths(this.focused(), months);
    const lastDay = Number(addDays(addMonths(first, 1), -1).slice(8));
    this.focused.set(addDays(first, Math.min(day, lastDay) - 1));
  }

  protected onKeydown(event: KeyboardEvent): void {
    const current = this.focused();
    const moves: Record<string, () => string> = {
      ArrowLeft: () => addDays(current, -1),
      ArrowRight: () => addDays(current, 1),
      ArrowUp: () => addDays(current, -7),
      ArrowDown: () => addDays(current, 7),
      Home: () => mondayOf(current),
      End: () => sundayOf(current),
    };

    if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      this.closed.emit();
      return;
    }

    if (event.key === 'PageUp' || event.key === 'PageDown') {
      event.preventDefault();
      this.shiftMonth((event.key === 'PageUp' ? -1 : 1) * (event.shiftKey ? 12 : 1));
      this.focusDay();
      return;
    }

    const move = moves[event.key];
    if (move) {
      event.preventDefault();
      this.focused.set(move());
      this.focusDay();
    }
  }

  protected dayNumber(date: string): number {
    return Number(date.slice(8));
  }

  protected label(date: string): string {
    return formatLongCalendarDate(date);
  }

  protected classOf(date: string): string {
    if (date === this.value()) {
      return 'border-transparent bg-linear-to-br from-brand-500 to-azure-500 font-bold text-[#021a1d]';
    }
    if (date === this.today()) {
      return 'border-brand-400 bg-brand-50 font-semibold text-brand-700';
    }
    return isSameMonth(date, this.focused())
      ? 'border-transparent text-slate-800 hover:bg-brand-100/60'
      : 'border-transparent text-slate-500 hover:bg-brand-100/40';
  }
}
