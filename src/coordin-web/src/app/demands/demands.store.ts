import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, EMPTY, forkJoin, map, Observable, switchMap, tap } from 'rxjs';
import { ApiConfiguration } from '../api/api-configuration';
import { completeDemand } from '../api/fn/demands/complete-demand';
import { deleteDemand } from '../api/fn/demands/delete-demand';
import { getDemands } from '../api/fn/demands/get-demands';
import { reorderDemands } from '../api/fn/demands/reorder-demands';
import { getSchedule } from '../api/fn/schedule/get-schedule';
import { Demand, mapDemand, mapSchedule, Schedule, ScheduleItem } from '../planning/planning';

export type LoadStatus = 'loading' | 'ready' | 'failed';
export type MoveDirection = 'up' | 'down';

/**
 * Estado da tela "Demandas" (T03, T06). A lista completa vem em ordem de prioridade (as concluídas também,
 * porque a API exige a lista inteira ao reordenar). Cada escrita bem-sucedida recarrega tudo do servidor;
 * uma escrita recusada não muda nada aqui. O sistema nunca reordena sozinho (regras 2 e 3).
 */
@Injectable()
export class DemandsStore {
  private readonly http = inject(HttpClient);
  private readonly root = inject(ApiConfiguration).rootUrl;

  /** Todas as demandas, na ordem de prioridade. */
  readonly demands = signal<Demand[]>([]);
  readonly schedule = signal<Schedule | null>(null);
  readonly status = signal<LoadStatus>('loading');

  readonly active = computed(() => this.demands().filter((demand) => demand.completedOn === null));
  readonly completed = computed(() =>
    this.demands().filter((demand) => demand.completedOn !== null),
  );

  load(): void {
    if (this.demands().length === 0) {
      this.status.set('loading');
    }

    this.fetch().subscribe();
  }

  /** A linha do cronograma de uma demanda (situação e desvios), se o cronograma já chegou. */
  itemOf(demandId: string): ScheduleItem | undefined {
    return this.schedule()?.items.find((item) => item.demandId === demandId);
  }

  /** Pode subir ou descer? Só se houver uma demanda ativa na direção pedida (concluídas são puladas). */
  canMove(demandId: string, direction: MoveDirection): boolean {
    const list = this.demands();
    const index = list.findIndex((demand) => demand.id === demandId);
    return index !== -1 && this.neighborIndex(list, index, direction) !== -1;
  }

  /**
   * Troca a demanda com a ativa vizinha na direção pedida e salva a lista **completa** dos ids. As
   * concluídas mantêm a posição. Sem vizinho ativo, não faz nada.
   */
  move(demandId: string, direction: MoveDirection): Observable<void> {
    const list = [...this.demands()];
    const index = list.findIndex((demand) => demand.id === demandId);
    const neighbor = index === -1 ? -1 : this.neighborIndex(list, index, direction);
    if (neighbor === -1) {
      return EMPTY;
    }

    [list[index], list[neighbor]] = [list[neighbor], list[index]];
    return reorderDemands(this.http, this.root, {
      body: { demandIds: list.map((demand) => demand.id) },
    }).pipe(switchMap(() => this.fetch()));
  }

  delete(id: string): Observable<void> {
    return deleteDemand(this.http, this.root, { id }).pipe(switchMap(() => this.fetch()));
  }

  /** Registra a data real de conclusão. A data é texto `yyyy-MM-dd`, sem `Date`. */
  /** Conclui a demanda. O esforço real é opcional; quando informado, tem de ser maior que zero (RF-22). */
  complete(
    id: string,
    completedOn: string,
    actualEffortHours: number | null = null,
  ): Observable<void> {
    return completeDemand(this.http, this.root, {
      id,
      body: { completedOn, actualEffortHours },
    }).pipe(switchMap(() => this.fetch()));
  }

  private neighborIndex(list: readonly Demand[], index: number, direction: MoveDirection): number {
    const step = direction === 'up' ? -1 : 1;
    for (
      let candidate = index + step;
      candidate >= 0 && candidate < list.length;
      candidate += step
    ) {
      if (list[candidate].completedOn === null) {
        return candidate;
      }
    }

    return -1;
  }

  /** Busca as demandas e o cronograma. Uma falha marca a tela como "falhou", sem propagar. */
  private fetch(): Observable<void> {
    return forkJoin([getDemands(this.http, this.root), getSchedule(this.http, this.root)]).pipe(
      tap(([demands, schedule]) => {
        this.demands.set(demands.body.map(mapDemand));
        this.schedule.set(mapSchedule(schedule.body));
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
