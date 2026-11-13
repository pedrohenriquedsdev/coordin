import { Component, inject } from '@angular/core';
import { AbsencesSection } from './absences-section';
import { CalendarStore } from './calendar.store';
import { HolidaysSection } from './holidays-section';

/** Tela "Calendário" (protótipo T08; RF-03): as ausências das pessoas e os feriados do setor. */
@Component({
  selector: 'app-calendar-page',
  imports: [AbsencesSection, HolidaysSection],
  providers: [CalendarStore],
  template: `
    <h2 class="text-lg font-semibold">Calendário</h2>

    @switch (store.status()) {
      @case ('loading') {
        <p class="mt-3 text-slate-600">Carregando…</p>
      }
      @case ('failed') {
        <div role="alert" class="mt-3 rounded-md bg-red-50 p-3 text-sm text-red-800">
          <p>Não foi possível carregar o calendário.</p>
          <button
            type="button"
            (click)="store.load()"
            class="mt-2 rounded-md border border-red-300 px-3 py-1 font-medium hover:bg-red-100"
          >
            Tentar de novo
          </button>
        </div>
      }
      @default {
        <div class="mt-4 space-y-10">
          <app-absences-section />
          <app-holidays-section />
        </div>
      }
    }
  `,
})
export class CalendarPage {
  protected readonly store = inject(CalendarStore);

  constructor() {
    this.store.load();
  }
}
