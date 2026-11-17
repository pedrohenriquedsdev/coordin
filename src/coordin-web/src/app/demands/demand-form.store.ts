import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { catchError, EMPTY, forkJoin, map, Observable, of, switchMap, tap } from 'rxjs';
import { ApiConfiguration } from '../api/api-configuration';
import { createDemand } from '../api/fn/demands/create-demand';
import { simulateDemand } from '../api/fn/demands/simulate-demand';
import { getDemand } from '../api/fn/demands/get-demand';
import { getDemands } from '../api/fn/demands/get-demands';
import { updateDemand } from '../api/fn/demands/update-demand';
import { getPeople } from '../api/fn/people/get-people';
import { getTeams } from '../api/fn/teams/get-teams';
import { DemandRequest } from '../api/models/demand-request';
import { Demand, DemandSimulation, mapDemand, mapDemandSimulation } from '../planning/planning';
import { Person, Team } from '../teams/teams.store';

export type FormStatus = 'loading' | 'ready' | 'failed' | 'missing';

/**
 * Estado do formulário de demanda (T04; RF-04 a RF-07). Carrega os times, as pessoas e, na edição, a
 * demanda. Salvar envia o agregado inteiro (DT-17) e não guarda nada localmente: a lista é recarregada
 * pela tela de demandas depois.
 */
@Injectable()
export class DemandFormStore {
  private readonly http = inject(HttpClient);
  private readonly root = inject(ApiConfiguration).rootUrl;

  readonly teams = signal<Team[]>([]);
  readonly people = signal<Person[]>([]);
  /** Todas as demandas, para escolher os pré-requisitos (a própria é tirada pela tela). */
  readonly candidates = signal<Demand[]>([]);
  readonly status = signal<FormStatus>('loading');

  /** Carrega times, pessoas, as demandas e, se houver id, a demanda. Um 404 marca a demanda como não encontrada. */
  load(id: string | null): Observable<Demand | null> {
    this.status.set('loading');
    return forkJoin([
      getTeams(this.http, this.root),
      getPeople(this.http, this.root),
      getDemands(this.http, this.root),
    ]).pipe(
      tap(([teams, people, demands]) => {
        this.candidates.set(demands.body.map(mapDemand));
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
      }),
      switchMap(() => (id === null ? of(null) : this.fetchDemand(id))),
      tap(() => this.status.set('ready')),
      catchError((error: { status?: number }) => {
        this.status.set(error.status === 404 ? 'missing' : 'failed');
        return EMPTY;
      }),
    );
  }

  /** Salva o agregado: cria (sem id) ou altera (com id). */
  save(body: DemandRequest, id: string | null): Observable<void> {
    const call =
      id === null
        ? createDemand(this.http, this.root, { body })
        : updateDemand(this.http, this.root, { id, body });
    return call.pipe(map(() => undefined));
  }

  /** Simula o impacto de uma nova demanda (RF-16). A API calcula sem gravar nada. */
  simulate(body: DemandRequest): Observable<DemandSimulation> {
    return simulateDemand(this.http, this.root, { body }).pipe(
      map((response) => mapDemandSimulation(response.body)),
    );
  }

  private fetchDemand(id: string): Observable<Demand> {
    return getDemand(this.http, this.root, { id }).pipe(
      map((response) => mapDemand(response.body)),
    );
  }
}
