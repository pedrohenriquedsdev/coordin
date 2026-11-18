import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { ApiConfiguration } from '../api/api-configuration';
import { getDemand } from '../api/fn/demands/get-demand';
import { getEstimateHistory } from '../api/fn/demands/get-estimate-history';
import { getForecastHistory } from '../api/fn/demands/get-forecast-history';
import { EstimateHistoryResponse } from '../api/models/estimate-history-response';
import { ForecastHistoryResponse } from '../api/models/forecast-history-response';
import { estimateHistoryRows, forecastHistoryRows } from './demand-history';

/** Situação da tela de detalhe: aguardando, pronta, demanda inexistente, ou falha. */
export type DetailLoad = 'loading' | 'ready' | 'notFound' | 'failed';

/**
 * Estado da tela de detalhe de uma demanda (RF-20, RF-21). Busca a demanda e os dois históricos juntos: se a
 * demanda não existe, a tela diz isso, em vez de mostrar um erro genérico.
 */
@Injectable()
export class DemandHistoryStore {
  private readonly http = inject(HttpClient);
  private readonly root = inject(ApiConfiguration).rootUrl;

  readonly load = signal<DetailLoad>('loading');
  readonly title = signal('');
  readonly estimates = signal<EstimateHistoryResponse[]>([]);
  readonly forecasts = signal<ForecastHistoryResponse[]>([]);

  readonly estimateRows = computed(() => estimateHistoryRows(this.estimates()));
  readonly forecastRows = computed(() => forecastHistoryRows(this.forecasts()));

  refresh(id: string): void {
    this.load.set('loading');
    forkJoin({
      demand: getDemand(this.http, this.root, { id }).pipe(map((response) => response.body)),
      estimates: getEstimateHistory(this.http, this.root, { id }).pipe(
        map((response) => response.body),
      ),
      forecasts: getForecastHistory(this.http, this.root, { id }).pipe(
        map((response) => response.body),
      ),
    }).subscribe({
      next: ({ demand, estimates, forecasts }) => {
        this.title.set(demand.title);
        this.estimates.set(estimates);
        this.forecasts.set(forecasts);
        this.load.set('ready');
      },
      error: (error: unknown) => {
        const notFound = error instanceof HttpErrorResponse && error.status === 404;
        this.load.set(notFound ? 'notFound' : 'failed');
      },
    });
  }
}
