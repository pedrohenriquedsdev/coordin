import { HttpClient } from '@angular/common/http';
import { Component, inject, input, output, signal } from '@angular/core';
import { ApiConfiguration } from '../api/api-configuration';
import { resetToPriority } from '../api/fn/scenarios/reset-to-priority';
import { messageFor } from '../core/problems/messages';
import { readProblem } from '../core/problems/problem';

/**
 * Aviso e botão para voltar a ordem de execução à ordem de prioridade (RF-15). Aparece só quando o modo é
 * `Custom`, e é usado no cronograma e na tela de cenários. A volta exige a confirmação da pessoa, em dois passos,
 * e avisa a tela de quem o usa (`done`) para que ela recarregue o que mudou.
 */
@Component({
  selector: 'app-reset-to-priority',
  templateUrl: './reset-to-priority.html',
})
export class ResetToPriority {
  private readonly http = inject(HttpClient);
  private readonly root = inject(ApiConfiguration).rootUrl;

  /** O modo de execução atual do cronograma: `Custom` mostra o aviso; `FollowPriority` não mostra nada. */
  readonly mode = input.required<string>();

  /** Avisa quem usa o componente de que a ordem voltou à prioridade. */
  readonly done = output<void>();

  protected readonly confirming = signal(false);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);

  protected ask(): void {
    this.message.set(null);
    this.confirming.set(true);
  }

  protected cancel(): void {
    this.confirming.set(false);
  }

  protected confirm(): void {
    this.busy.set(true);
    this.message.set(null);
    resetToPriority(this.http, this.root).subscribe({
      next: () => {
        this.busy.set(false);
        this.confirming.set(false);
        this.done.emit();
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.confirming.set(false);
        this.message.set(
          messageFor(readProblem(error), 'Não foi possível voltar à ordem de prioridade.'),
        );
      },
    });
  }
}
