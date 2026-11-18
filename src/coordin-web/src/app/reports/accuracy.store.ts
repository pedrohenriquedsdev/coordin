import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { ApiConfiguration } from '../api/api-configuration';
import { getAccuracyReport } from '../api/fn/reports/get-accuracy-report';
import { AccuracyResponse } from '../api/models/accuracy-response';
import { messageFor } from '../core/problems/messages';
import { readProblem } from '../core/problems/problem';
import { Clock } from '../core/dates/calendar-date';
import { accuracyRows, periodError } from './accuracy-view';

export type AccuracyLoad = 'idle' | 'loading' | 'ready' | 'failed';

/**
 * Estado da página de indicadores de precisão (RF-23). A busca só roda quando o gestor pede, e o período padrão é
 * do início do ano até hoje, pelo relógio do sistema (DT-36).
 */
@Injectable()
export class AccuracyStore {
  private readonly http = inject(HttpClient);
  private readonly root = inject(ApiConfiguration).rootUrl;
  private readonly clock = inject(Clock);

  readonly from = signal(`${this.clock.today().slice(0, 4)}-01-01`);
  readonly to = signal(this.clock.today());
  readonly load = signal<AccuracyLoad>('idle');
  readonly response = signal<AccuracyResponse | null>(null);
  readonly message = signal<string | null>(null);

  /** As linhas da tabela e dos gráficos, na ordem em que aparecem (setor e depois os times). */
  readonly rows = computed(() => {
    const current = this.response();
    return current === null ? [] : accuracyRows(current);
  });

  search(): void {
    const problem = periodError(this.from(), this.to());
    if (problem !== null) {
      this.message.set(problem);
      return;
    }

    this.message.set(null);
    this.load.set('loading');
    getAccuracyReport(this.http, this.root, { from: this.from(), to: this.to() }).subscribe({
      next: (response) => {
        this.response.set(response.body);
        this.load.set('ready');
      },
      error: (error: unknown) => {
        this.message.set(
          messageFor(readProblem(error), 'Não foi possível calcular os indicadores.'),
        );
        this.load.set('failed');
      },
    });
  }
}
