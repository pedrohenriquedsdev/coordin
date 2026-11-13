import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { catchError, EMPTY, forkJoin, map, Observable, switchMap, tap } from 'rxjs';
import { ApiConfiguration } from '../api/api-configuration';
import { createAbsence } from '../api/fn/absences/create-absence';
import { deleteAbsence } from '../api/fn/absences/delete-absence';
import { getAbsences } from '../api/fn/absences/get-absences';
import { updateAbsence } from '../api/fn/absences/update-absence';
import { createHoliday } from '../api/fn/holidays/create-holiday';
import { deleteHoliday } from '../api/fn/holidays/delete-holiday';
import { getHolidays } from '../api/fn/holidays/get-holidays';
import { getPeople } from '../api/fn/people/get-people';
import { getTeams } from '../api/fn/teams/get-teams';
import { AbsenceRequest } from '../api/models/absence-request';
import { AbsenceResponse } from '../api/models/absence-response';
import { HolidayRequest } from '../api/models/holiday-request';
import { HolidayResponse } from '../api/models/holiday-response';
import { PersonResponse } from '../api/models/person-response';
import { TeamResponse } from '../api/models/team-response';

export type LoadStatus = 'loading' | 'ready' | 'failed';

/**
 * Estado da tela "Calendário" (ausências e feriados; RF-03). Depois de cada escrita bem-sucedida as
 * listas são recarregadas do servidor (TDD 5.1). As datas são datas de calendário (`yyyy-MM-dd`) e
 * nunca viram `Date`: vão e voltam como texto.
 */
@Injectable()
export class CalendarStore {
  private readonly http = inject(HttpClient);
  private readonly root = inject(ApiConfiguration).rootUrl;

  readonly absences = signal<AbsenceResponse[]>([]);
  readonly holidays = signal<HolidayResponse[]>([]);
  readonly people = signal<PersonResponse[]>([]);
  readonly teams = signal<TeamResponse[]>([]);
  readonly status = signal<LoadStatus>('loading');

  load(): void {
    if (this.status() !== 'ready') {
      this.status.set('loading');
    }

    this.fetch().subscribe();
  }

  createAbsence(personId: string, body: AbsenceRequest): Observable<void> {
    return createAbsence(this.http, this.root, { personId, body }).pipe(
      switchMap(() => this.fetch()),
    );
  }

  updateAbsence(id: string, body: AbsenceRequest): Observable<void> {
    return updateAbsence(this.http, this.root, { id, body }).pipe(switchMap(() => this.fetch()));
  }

  deleteAbsence(id: string): Observable<void> {
    return deleteAbsence(this.http, this.root, { id }).pipe(switchMap(() => this.fetch()));
  }

  createHoliday(body: HolidayRequest): Observable<void> {
    return createHoliday(this.http, this.root, { body }).pipe(switchMap(() => this.fetch()));
  }

  deleteHoliday(id: string): Observable<void> {
    return deleteHoliday(this.http, this.root, { id }).pipe(switchMap(() => this.fetch()));
  }

  /** Busca as quatro listas e aplica no estado. Uma falha aqui marca a tela como "falhou", sem propagar. */
  private fetch(): Observable<void> {
    return forkJoin([
      getAbsences(this.http, this.root),
      getHolidays(this.http, this.root),
      getPeople(this.http, this.root),
      getTeams(this.http, this.root),
    ]).pipe(
      tap(([absences, holidays, people, teams]) => {
        this.absences.set(absences.body);
        this.holidays.set(holidays.body);
        this.people.set(people.body);
        this.teams.set(teams.body);
        this.status.set('ready');
      }),
      map(() => undefined),
      catchError(() => {
        this.status.set('failed');
        return EMPTY;
      }),
    );
  }
}
