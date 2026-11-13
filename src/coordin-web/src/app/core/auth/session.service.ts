import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, finalize, map, Observable, of, shareReplay, tap } from 'rxjs';
import { ApiConfiguration } from '../../api/api-configuration';
import { login } from '../../api/fn/auth/login';
import { logout } from '../../api/fn/auth/logout';
import { refresh } from '../../api/fn/auth/refresh';

/**
 * Sessão do gestor no navegador (DT-28).
 *
 * O token de acesso (JWT de 15 minutos) vive **só na memória** deste serviço: nada em
 * `localStorage`, `sessionStorage` ou cookie legível por JavaScript. Assim um script injetado na
 * página não encontra nenhum token para roubar. Recarregar a página apaga a memória, e a sessão é
 * restaurada por `refresh()`, que usa o cookie `HttpOnly` do refresh token (que o JavaScript não lê).
 */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ApiConfiguration);

  private readonly token = signal<string | null>(null);

  /** Token de acesso atual, somente leitura. Quem o usa é o interceptor HTTP. */
  readonly accessToken = this.token.asReadonly();

  readonly isAuthenticated = computed(() => this.token() !== null);

  // A renovação em andamento, compartilhada por todos que a pedirem enquanto ela não termina.
  private refreshInFlight: Observable<boolean> | null = null;

  /** Entra com e-mail e senha. Erra (HTTP 401, 400 ou 429) sem guardar nada. */
  login(email: string, password: string): Observable<void> {
    return login(this.http, this.config.rootUrl, { body: { email, password } }).pipe(
      tap((response) => this.token.set(response.body.accessToken)),
      map(() => undefined),
    );
  }

  /**
   * Renova a sessão pelo cookie do refresh token e devolve se deu certo. Falhar encerra a sessão.
   *
   * Várias chamadas ao mesmo tempo **compartilham uma única requisição**. Isso é essencial por
   * causa da rotação (DT-28): cada refresh token vale uma vez, e usar o mesmo cookie duas vezes
   * seria lido pela API como roubo e derrubaria todas as sessões.
   */
  refresh(): Observable<boolean> {
    if (this.refreshInFlight === null) {
      const shared = refresh(this.http, this.config.rootUrl).pipe(
        map((response) => {
          this.token.set(response.body.accessToken);
          return true;
        }),
        catchError(() => {
          this.clear();
          return of(false);
        }),
        finalize(() => {
          this.refreshInFlight = null;
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );

      this.refreshInFlight = shared;
      // Dispara já: a requisição sai quando refresh() é chamado, haja ou não quem escute o resultado.
      shared.subscribe();
    }

    return this.refreshInFlight;
  }

  /** Encerra a sessão no servidor e localmente; localmente mesmo que o servidor não responda. */
  logout(): Observable<void> {
    return logout(this.http, this.config.rootUrl).pipe(
      catchError(() => of(undefined)),
      tap(() => this.clear()),
      map(() => undefined),
    );
  }

  /** Descarta o token da memória. */
  clear(): void {
    this.token.set(null);
  }
}
