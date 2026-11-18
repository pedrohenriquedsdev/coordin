import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { ApiConfiguration } from '../api/api-configuration';
import { exportSchedule } from '../api/fn/schedule/export-schedule';
import { getSchedule } from '../api/fn/schedule/get-schedule';
import { messageFor } from '../core/problems/messages';
import { readProblem } from '../core/problems/problem';
import { mapSchedule, Schedule } from '../planning/planning';
import { csvBlob, downloadBlob, fileNameFrom } from './schedule-export';

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
  /** Verdadeiro enquanto o arquivo da planilha está sendo baixado (RF-19). */
  readonly exporting = signal(false);
  /** A mensagem da última falha da exportação; `null` quando não há falha. */
  readonly exportError = signal<string | null>(null);

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

  /** Baixa a planilha do cronograma vigente (RF-19). O nome vem do cabeçalho da API; a falha fica na tela. */
  exportCsv(): void {
    this.exporting.set(true);
    this.exportError.set(null);
    exportSchedule(this.http, this.root).subscribe({
      next: (response) => {
        downloadBlob(
          csvBlob(response.body),
          fileNameFrom(response.headers.get('Content-Disposition')),
        );
      },
      error: (error: unknown) => {
        this.exporting.set(false);
        this.exportError.set(
          messageFor(readProblem(error), 'Não foi possível exportar o cronograma.'),
        );
      },
      complete: () => this.exporting.set(false),
    });
  }
}
