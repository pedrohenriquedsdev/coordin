import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { map, Observable, tap } from 'rxjs';
import { ApiConfiguration } from '../api/api-configuration';
import { getTeamReport } from '../api/fn/reports/get-team-report';
import { mapTeamReport, TeamReport } from './team-report';

export type LoadStatus = 'loading' | 'ready' | 'failed';

/**
 * Estado da tela "Relatório por time" (T09; RF-17). O relatório é calculado pela API sobre o planejamento
 * vigente; esta tela só o busca e o mostra, sem recalcular (DT-02).
 */
@Injectable()
export class TeamReportStore {
  private readonly http = inject(HttpClient);
  private readonly root = inject(ApiConfiguration).rootUrl;

  readonly report = signal<TeamReport | null>(null);
  readonly status = signal<LoadStatus>('loading');

  /** Busca o relatório do período. Se já havia um, ele continua na tela até a resposta chegar. */
  load(from: string, to: string): Observable<void> {
    if (this.report() === null) {
      this.status.set('loading');
    }

    return getTeamReport(this.http, this.root, { from, to }).pipe(
      tap((response) => {
        this.report.set(mapTeamReport(response.body));
        this.status.set('ready');
      }),
      map(() => undefined),
    );
  }

  fail(): void {
    this.status.set('failed');
  }
}
