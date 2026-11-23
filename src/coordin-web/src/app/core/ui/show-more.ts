import { Component, input, output } from '@angular/core';

/**
 * O fim de uma lista longa: "Mostrando 10 de 31" e o botão "Mostrar mais". Some sozinho quando a lista
 * inteira já está à mostra, então as telas com poucos itens não ganham nada de novo.
 */
@Component({
  selector: 'app-show-more',
  template: `
    @if (total() > shown()) {
      <div class="mt-4 flex flex-col items-center gap-2">
        <p class="text-sm text-slate-600" role="status">Mostrando {{ shown() }} de {{ total() }}</p>
        <button
          type="button"
          class="btn-secondary touch-chip rounded-full px-6"
          (click)="more.emit()"
        >
          Mostrar mais
        </button>
      </div>
    }
  `,
})
export class ShowMore {
  /** Quantos itens estão à mostra agora. */
  readonly shown = input.required<number>();
  /** Quantos itens a lista tem no total. */
  readonly total = input.required<number>();
  /** O gestor pediu mais itens. */
  readonly more = output<void>();
}
