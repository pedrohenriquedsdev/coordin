import { Component, input, ViewEncapsulation } from '@angular/core';

/** Indicador de carregamento (anel que gira e ponto que pulsa). O texto fica sempre escrito ao lado. */
@Component({
  selector: 'app-loader',
  encapsulation: ViewEncapsulation.None,
  host: { class: 'loader', role: 'status' },
  template: `
    <span class="loader-ring" aria-hidden="true"><span class="loader-dot"></span></span>
    <span class="text-sm font-medium text-slate-600">{{ label() }}</span>
  `,
})
export class Loader {
  readonly label = input('Carregando…');
}
