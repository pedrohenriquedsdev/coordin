import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { ApiConfiguration } from '../api/api-configuration';
import { getAbsences } from '../api/fn/absences/get-absences';
import { getHolidays } from '../api/fn/holidays/get-holidays';
import { getTeamReport } from '../api/fn/reports/get-team-report';
import { getSchedule } from '../api/fn/schedule/get-schedule';
import { getSettings } from '../api/fn/settings/get-settings';
import { getTeams } from '../api/fn/teams/get-teams';
import { AbsenceResponse } from '../api/models/absence-response';
import { HolidayResponse } from '../api/models/holiday-response';
import { Clock } from '../core/dates/calendar-date';
import { mapSchedule, Schedule } from '../planning/planning';
import { defaultPeriod, mapTeamReport, TeamReport } from '../reports/team-report';

/** Um dado da tela inicial: ainda carregando, indisponível (a chamada falhou) ou pronto. */
export type Loadable<T> = { state: 'loading' } | { state: 'failed' } | { state: 'ready'; value: T };

const loading = { state: 'loading' } as const;

/**
 * Números da tela inicial (HUD). Cada dado vem de uma chamada própria e falha sozinho: se uma não
 * responde, só o cartão dela mostra "indisponível". Nada é calculado aqui: o cronograma e os cadastros
 * chegam prontos da API (DT-03); só se contam e se escolhem valores.
 */
@Injectable()
export class HomeStore {
  private readonly http = inject(HttpClient);
  private readonly root = inject(ApiConfiguration).rootUrl;
  private readonly clock = inject(Clock);

  /** Hoje, em São Paulo (`aaaa-mm-dd`). */
  readonly today = this.clock.today();

  readonly schedule = signal<Loadable<Schedule>>(loading);
  readonly teams = signal<Loadable<{ teams: number; people: number }>>(loading);
  readonly holidays = signal<Loadable<readonly HolidayResponse[]>>(loading);
  readonly absences = signal<Loadable<readonly AbsenceResponse[]>>(loading);
  readonly report = signal<Loadable<TeamReport>>(loading);
  readonly margin = signal<Loadable<number>>(loading);

  /** Verdadeiro enquanto qualquer um dos números ainda não respondeu. */
  readonly loading = computed(() =>
    [
      this.schedule(),
      this.teams(),
      this.holidays(),
      this.margin(),
      this.absences(),
      this.report(),
    ].some((data) => data.state === 'loading'),
  );

  private readonly open = computed(() => {
    const schedule = this.schedule();
    return schedule.state === 'ready'
      ? schedule.value.items.filter((item) => item.completedOn === null)
      : null;
  });

  /** Demandas ainda não concluídas. */
  readonly activeCount = computed(() => this.open()?.length ?? null);

  /** Demandas concluídas. */
  readonly completedCount = computed(() => {
    const schedule = this.schedule();
    return schedule.state === 'ready'
      ? schedule.value.items.filter((item) => item.completedOn !== null).length
      : null;
  });

  /** Demandas abertas sem previsão (RF-10). */
  readonly unpredictableCount = computed(
    () => this.open()?.filter((item) => !item.isPredictable).length ?? null,
  );

  /** Demandas abertas cuja previsão passa do prazo informado. */
  readonly lateCount = computed(
    () => this.open()?.filter((item) => item.exceedsInformedEndDate).length ?? null,
  );

  /** O prazo do setor: a maior data de fim, com margem, entre as demandas abertas previsíveis. */
  readonly sectorEnd = computed<string | null>(() => {
    const dates = (this.open() ?? [])
      .map((item) => item.forecastEndDate)
      .filter((date): date is string => date !== null);
    // Datas ISO (aaaa-mm-dd) ordenam como texto; nada passa por `Date` (RNF-07).
    return dates.length === 0 ? null : dates.reduce((latest, d) => (d > latest ? d : latest));
  });

  load(): void {
    getSchedule(this.http, this.root).subscribe({
      next: (r) => this.schedule.set({ state: 'ready', value: mapSchedule(r.body) }),
      error: () => this.schedule.set({ state: 'failed' }),
    });
    getTeams(this.http, this.root).subscribe({
      next: (r) =>
        this.teams.set({
          state: 'ready',
          value: {
            teams: r.body.length,
            people: r.body.reduce((sum, team) => sum + Number(team.peopleCount), 0),
          },
        }),
      error: () => this.teams.set({ state: 'failed' }),
    });
    getHolidays(this.http, this.root).subscribe({
      next: (r) => this.holidays.set({ state: 'ready', value: r.body }),
      error: () => this.holidays.set({ state: 'failed' }),
    });
    getAbsences(this.http, this.root).subscribe({
      next: (r) => this.absences.set({ state: 'ready', value: r.body }),
      error: () => this.absences.set({ state: 'failed' }),
    });
    const period = defaultPeriod(this.today);
    getTeamReport(this.http, this.root, period).subscribe({
      next: (r) => this.report.set({ state: 'ready', value: mapTeamReport(r.body) }),
      error: () => this.report.set({ state: 'failed' }),
    });
    getSettings(this.http, this.root).subscribe({
      next: (r) => this.margin.set({ state: 'ready', value: Number(r.body.safetyMarginPercent) }),
      error: () => this.margin.set({ state: 'failed' }),
    });
  }
}
