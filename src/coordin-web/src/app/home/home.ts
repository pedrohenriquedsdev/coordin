import { Component } from '@angular/core';

/** Página inicial provisória; o cronograma (change 5) vai ocupar o lugar dela. */
@Component({
  selector: 'app-home',
  template: `
    <section class="rounded-lg border border-slate-200 bg-white p-4 sm:p-6">
      <h2 class="text-lg font-semibold">Planejamento de entregas</h2>
      <p class="mt-2 text-slate-600">
        Apoio à decisão do setor de desenvolvimento. As telas de planejamento chegam nas próximas
        etapas.
      </p>
    </section>
  `,
})
export class Home {}
