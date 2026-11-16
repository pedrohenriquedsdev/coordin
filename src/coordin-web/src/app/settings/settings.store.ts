import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { map, Observable, tap } from 'rxjs';
import { ApiConfiguration } from '../api/api-configuration';
import { getSettings } from '../api/fn/settings/get-settings';
import { updateSettings } from '../api/fn/settings/update-settings';

export type LoadStatus = 'loading' | 'ready' | 'failed';

/**
 * Estado da tela "Configurações" (RF-09; DT-22). Mostra e altera só a margem de segurança: o modo de
 * ordem não muda por aqui (regras 2 e 3). A margem vigente volta do servidor depois de cada salvamento.
 */
@Injectable()
export class SettingsStore {
  private readonly http = inject(HttpClient);
  private readonly root = inject(ApiConfiguration).rootUrl;

  readonly safetyMarginPercent = signal<number | null>(null);
  readonly status = signal<LoadStatus>('loading');

  load(): Observable<void> {
    this.status.set('loading');
    return getSettings(this.http, this.root).pipe(
      tap((response) => {
        this.safetyMarginPercent.set(Number(response.body.safetyMarginPercent));
        this.status.set('ready');
      }),
      map(() => undefined),
    );
  }

  save(percent: number): Observable<void> {
    return updateSettings(this.http, this.root, { body: { safetyMarginPercent: percent } }).pipe(
      tap((response) => this.safetyMarginPercent.set(Number(response.body.safetyMarginPercent))),
      map(() => undefined),
    );
  }
}
