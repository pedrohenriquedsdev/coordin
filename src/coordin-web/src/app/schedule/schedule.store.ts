import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { ApiConfiguration } from '../api/api-configuration';
import { getSchedule } from '../api/fn/schedule/get-schedule';
import { mapSchedule, Schedule } from '../planning/planning';

export type LoadStatus = 'loading' | 'ready' | 'failed';

/**
 * Estado da tela "Cronograma" (T02). O cronograma vem pronto da API: esta tela só o busca e o mostra,
 * sem recalcular nada (DT-03). Uma falha marca a tela como "falhou" e oferece "Tentar de novo".
 */
@Injectable()
export class ScheduleStore {
  private readonly http = inject(HttpClient);
  private readonly root = inject(ApiConfiguration).rootUrl;

  readonly schedule = signal<Schedule | null>(null);
  readonly status = signal<LoadStatus>('loading');

  /** Busca o cronograma. Se já havia dados, eles continuam na tela até a resposta chegar. */
  load(): void {
    if (this.schedule() === null) {
      this.status.set('loading');
    }

    getSchedule(this.http, this.root).subscribe({
      next: (response) => {
        this.schedule.set(mapSchedule(response.body));
        this.status.set('ready');
      },
      error: () => this.status.set('failed'),
    });
  }
}
