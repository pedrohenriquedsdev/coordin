import { Component, input } from '@angular/core';

/**
 * Cabeçalho padrão das telas internas: linha de apoio, título (o `h2` da página), descrição e, à
 * direita, as ações principais (conteúdo projetado). Mantém todas as telas com a mesma cara.
 */
@Component({
  selector: 'app-page-header',
  template: `
    <header class="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div class="max-w-3xl min-w-0">
        <p class="text-xs font-semibold tracking-wide text-brand-700 uppercase">{{ eyebrow() }}</p>
        <h2 class="mt-1 text-3xl font-extrabold tracking-tight sm:text-4xl">{{ title() }}</h2>
        @if (description(); as text) {
          <p class="mt-2 text-sm text-slate-600 sm:text-base">{{ text }}</p>
        }
      </div>
      <div class="flex flex-wrap items-center gap-3"><ng-content /></div>
    </header>
  `,
})
export class PageHeader {
  readonly eyebrow = input.required<string>();
  readonly title = input.required<string>();
  readonly description = input<string | null>(null);
}
