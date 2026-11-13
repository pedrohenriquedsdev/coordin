import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, EMPTY, forkJoin, map, Observable, switchMap, tap } from 'rxjs';
import { ApiConfiguration } from '../api/api-configuration';
import { getAbsences } from '../api/fn/absences/get-absences';
import { AbsenceResponse } from '../api/models/absence-response';
import { createPerson } from '../api/fn/people/create-person';
import { deletePerson } from '../api/fn/people/delete-person';
import { getPeople } from '../api/fn/people/get-people';
import { updatePerson } from '../api/fn/people/update-person';
import { createTeam } from '../api/fn/teams/create-team';
import { deleteTeam } from '../api/fn/teams/delete-team';
import { getTeams } from '../api/fn/teams/get-teams';
import { updateTeam } from '../api/fn/teams/update-team';

export interface Team {
  readonly id: string;
  readonly name: string;
  readonly peopleCount: number;
  readonly dailyCapacityHours: number;
}

export interface Person {
  readonly id: string;
  readonly name: string;
  readonly teamId: string;
  readonly teamName: string;
  readonly dailyCapacityHours: number;
}

export interface PersonInput {
  readonly name: string;
  readonly teamId: string;
  readonly dailyCapacityHours: number;
}

export type LoadStatus = 'loading' | 'ready' | 'failed';

/**
 * Estado da tela "Times e pessoas". Depois de cada escrita bem-sucedida os dados são recarregados do
 * servidor (TDD 5.1), em vez de remendados no navegador: a tela nunca diverge da API. As escritas que
 * falham devolvem o erro a quem chamou, para a tela mostrar a mensagem sem perder o que foi digitado.
 */
@Injectable()
export class TeamsStore {
  private readonly http = inject(HttpClient);
  private readonly root = inject(ApiConfiguration).rootUrl;

  readonly teams = signal<Team[]>([]);
  readonly people = signal<Person[]>([]);
  readonly absences = signal<AbsenceResponse[]>([]);
  readonly status = signal<LoadStatus>('loading');
  readonly selectedId = signal<string | null>(null);

  readonly selectedTeam = computed(
    () => this.teams().find((team) => team.id === this.selectedId()) ?? null,
  );
  readonly selectedPeople = computed(() =>
    this.people().filter((person) => person.teamId === this.selectedId()),
  );

  /** Carrega times e pessoas. Se já havia dados, eles continuam na tela até a resposta chegar. */
  load(): void {
    if (this.teams().length === 0) {
      this.status.set('loading');
    }

    this.fetch().subscribe();
  }

  select(id: string): void {
    this.selectedId.set(id);
  }

  createTeam(name: string): Observable<void> {
    return createTeam(this.http, this.root, { body: { name } }).pipe(
      tap((response) => this.selectedId.set(response.body.id)),
      switchMap(() => this.fetch()),
    );
  }

  renameTeam(id: string, name: string): Observable<void> {
    return updateTeam(this.http, this.root, { id, body: { name } }).pipe(
      switchMap(() => this.fetch()),
    );
  }

  deleteTeam(id: string): Observable<void> {
    return deleteTeam(this.http, this.root, { id }).pipe(switchMap(() => this.fetch()));
  }

  createPerson(input: PersonInput): Observable<void> {
    return createPerson(this.http, this.root, { body: input }).pipe(switchMap(() => this.fetch()));
  }

  updatePerson(id: string, input: PersonInput): Observable<void> {
    return updatePerson(this.http, this.root, { id, body: input }).pipe(
      switchMap(() => this.fetch()),
    );
  }

  deletePerson(id: string): Observable<void> {
    return deletePerson(this.http, this.root, { id }).pipe(switchMap(() => this.fetch()));
  }

  /** Busca as duas listas e aplica no estado. Uma falha aqui marca a tela como "falhou", sem propagar. */
  private fetch(): Observable<void> {
    return forkJoin([
      getTeams(this.http, this.root),
      getPeople(this.http, this.root),
      getAbsences(this.http, this.root),
    ]).pipe(
      tap(([teams, people, absences]) => {
        this.absences.set(absences.body);
        this.teams.set(
          teams.body.map((team) => ({
            id: team.id,
            name: team.name,
            peopleCount: Number(team.peopleCount),
            dailyCapacityHours: Number(team.dailyCapacityHours),
          })),
        );
        this.people.set(
          people.body.map((person) => ({
            id: person.id,
            name: person.name,
            teamId: person.teamId,
            teamName: person.teamName,
            dailyCapacityHours: Number(person.dailyCapacityHours),
          })),
        );

        // Mantém o time escolhido; se ele sumiu (ou nunca houve um), escolhe o primeiro.
        const first = this.teams()[0];
        if (this.selectedTeam() === null) {
          this.selectedId.set(first?.id ?? null);
        }

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
