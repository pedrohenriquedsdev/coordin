import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { ApiConfiguration } from '../api/api-configuration';
import { getValidationStatus } from '../api/fn/validation/get-validation-status';
import { ValidationStatusResponse } from '../api/models/validation-status-response';
import { wizardStep, WizardStep } from './wizard-step';

/** O que a tela carrega do servidor: aguardando, pronta, área desligada neste ambiente, ou falha de rede/servidor. */
export type ValidationLoad = 'loading' | 'ready' | 'unavailable' | 'failed';

/**
 * Estado do assistente de validação (RF-24). A etapa vem do status que o servidor registrou, então recarregar a
 * página nunca leva o gestor a um passo que a API já desfez. Os dados digitados ficam só na memória da tela.
 */
@Injectable()
export class ValidationStore {
  private readonly http = inject(HttpClient);
  private readonly root = inject(ApiConfiguration).rootUrl;

  readonly status = signal<ValidationStatusResponse | null>(null);
  readonly load = signal<ValidationLoad>('loading');

  /** A etapa atual, ou `null` enquanto o status não chegou. */
  readonly step = computed<WizardStep | null>(() => {
    const current = this.status();
    return current === null ? null : wizardStep(current);
  });

  /** Busca o status do servidor. Um 404 significa que a área está desligada; outras falhas pedem "Tentar de novo". */
  refresh(): void {
    if (this.status() === null) {
      this.load.set('loading');
    }

    getValidationStatus(this.http, this.root).subscribe({
      next: (response) => {
        this.status.set(response.body);
        this.load.set('ready');
      },
      error: (error: unknown) => {
        const notFound = error instanceof HttpErrorResponse && error.status === 404;
        this.load.set(notFound ? 'unavailable' : 'failed');
      },
    });
  }
}
