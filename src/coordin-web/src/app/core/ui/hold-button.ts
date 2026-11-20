import {
  Component,
  DestroyRef,
  inject,
  input,
  output,
  signal,
  ViewEncapsulation,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { Icon } from './icon';

type HoldState = 'idle' | 'holding' | 'done';

const RESET_MS = 1800;

/**
 * Botão de "segurar para confirmar": uma ação destrutiva só acontece depois de manter o botão
 * pressionado (mouse, toque ou a tecla Espaço/Enter). Soltar antes cancela. Serve como a
 * confirmação explícita que o sistema exige antes de apagar algo.
 */
@Component({
  selector: 'app-hold-button',
  imports: [Icon, NgTemplateOutlet],
  encapsulation: ViewEncapsulation.None,
  host: { class: 'inline-block' },
  template: `
    <ng-template #content let-done="done">
      <span class="hold-content">
        <app-icon [name]="done ? 'check' : 'trash'" />
        <span>{{ done ? doneLabel() : label() }}</span>
      </span>
    </ng-template>

    <button
      type="button"
      class="hold-btn"
      [attr.data-state]="state()"
      [style]="'--hold-ms:' + holdMs() + 'ms'"
      [disabled]="disabled()"
      (pointerdown)="onPointerDown($event)"
      (pointerup)="cancel()"
      (pointerleave)="cancel()"
      (pointercancel)="cancel()"
      (keydown)="onKeyDown($event)"
      (keyup)="onKeyUp($event)"
      (blur)="cancel()"
      (contextmenu)="$event.preventDefault()"
    >
      <ng-container *ngTemplateOutlet="content; context: { done: state() === 'done' }" />
      <span class="hold-fill" aria-hidden="true">
        <ng-container *ngTemplateOutlet="content; context: { done: state() === 'done' }" />
      </span>
    </button>
    <span class="sr-only" role="status">{{ state() === 'done' ? doneLabel() : '' }}</span>
  `,
})
export class HoldButton {
  readonly label = input('Segure para excluir');
  readonly doneLabel = input('Excluído');
  /** Quanto tempo é preciso segurar, em milissegundos. */
  readonly holdMs = input(1600);
  readonly disabled = input(false);
  /** Emitido uma vez, quando o tempo completa. */
  readonly confirmed = output<void>();

  protected readonly state = signal<HoldState>('idle');
  private timer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.clearTimer());
  }

  protected onPointerDown(event: PointerEvent): void {
    if (event.button === 0) {
      this.start();
    }
  }

  protected onKeyDown(event: KeyboardEvent): void {
    if (event.key !== ' ' && event.key !== 'Enter') {
      return;
    }
    event.preventDefault();
    if (!event.repeat) {
      this.start();
    }
  }

  protected onKeyUp(event: KeyboardEvent): void {
    if (event.key === ' ' || event.key === 'Enter') {
      this.cancel();
    }
  }

  protected cancel(): void {
    if (this.state() === 'holding') {
      this.clearTimer();
      this.state.set('idle');
    }
  }

  private start(): void {
    if (this.state() !== 'idle') {
      return;
    }
    this.state.set('holding');
    this.timer = setTimeout(() => {
      this.state.set('done');
      this.confirmed.emit();
      this.timer = setTimeout(() => this.state.set('idle'), RESET_MS);
    }, this.holdMs());
  }

  private clearTimer(): void {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }
}
