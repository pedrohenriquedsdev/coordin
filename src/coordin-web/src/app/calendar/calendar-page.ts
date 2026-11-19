import { Component, inject } from '@angular/core';
import { Clock } from '../core/dates/calendar-date';
import { Icon } from '../core/ui/icon';
import { Loader } from '../core/ui/loader';
import { PageHeader } from '../core/ui/page-header';
import { HomeCalendar } from '../home/home-calendar';
import { AbsencesSection } from './absences-section';
import { CalendarStore } from './calendar.store';
import { HolidaysSection } from './holidays-section';

/** Tela "Calendário" (protótipo T08; RF-03): as ausências das pessoas e os feriados do setor. */
@Component({
  selector: 'app-calendar-page',
  imports: [AbsencesSection, HolidaysSection, PageHeader, Loader, Icon, HomeCalendar],
  providers: [CalendarStore],
  templateUrl: './calendar-page.html',
})
export class CalendarPage {
  protected readonly store = inject(CalendarStore);
  protected readonly today = inject(Clock).today();
  /** A tela de calendário não carrega o cronograma: o calendário do mês mostra só feriados e ausências. */
  protected readonly noItems = [] as const;

  constructor() {
    this.store.load();
  }
}
