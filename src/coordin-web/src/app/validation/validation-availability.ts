import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { ApiConfiguration } from '../api/api-configuration';
import { getValidationStatus } from '../api/fn/validation/get-validation-status';

/** Situação da área de validação neste ambiente (RF-24): ainda não consultada, consultando, habilitada ou não. */
export type AvailabilityState = 'unknown' | 'checking' | 'available' | 'unavailable';

/**
 * Diz à casca se a área de validação existe neste ambiente. A área só existe com `Validation__Enabled=true`
 * (DT-37); a API responde 404 quando ela está desligada. A consulta roda uma vez por sessão: o `reset` ao sair
 * a torna de novo consultável no próximo login.
 */
@Injectable({ providedIn: 'root' })
export class ValidationAvailability {
  private readonly http = inject(HttpClient);
  private readonly root = inject(ApiConfiguration).rootUrl;

  readonly state = signal<AvailabilityState>('unknown');
  readonly available = computed(() => this.state() === 'available');

  /** Consulta o status uma vez. Qualquer falha (inclusive 404) deixa a área indisponível, sem mostrar erro na casca. */
  check(): void {
    if (this.state() !== 'unknown') {
      return;
    }

    this.state.set('checking');
    getValidationStatus(this.http, this.root).subscribe({
      next: () => this.state.set('available'),
      error: () => this.state.set('unavailable'),
    });
  }

  /** Volta ao estado inicial, para a próxima sessão consultar de novo. */
  reset(): void {
    this.state.set('unknown');
  }
}
