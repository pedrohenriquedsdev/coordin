import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { SessionService } from './session.service';

// Endpoints em que o token não faz sentido: login ainda não tem sessão, e refresh e logout usam o cookie.
const AUTH_ENDPOINTS = ['/api/auth/login', '/api/auth/refresh', '/api/auth/logout'];

/**
 * Interceptor da sessão do gestor (DT-28):
 *
 * 1. anexa `Authorization: Bearer` às chamadas à API da própria aplicação, e só a elas;
 * 2. se um endpoint protegido responde 401 (token expirado), renova a sessão e repete a chamada
 *    **uma única vez**; chamadas simultâneas compartilham uma só renovação (ver `SessionService.refresh`);
 * 3. se a renovação falha, encerra a sessão e leva o gestor a `/login`.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const path = apiPath(request.url);

  // Fora da nossa API (outro site, arquivos estáticos) o token nunca é enviado: não vaza para terceiros.
  if (path === null || AUTH_ENDPOINTS.includes(path)) {
    return next(request);
  }

  const session = inject(SessionService);
  const router = inject(Router);

  return next(withBearer(request, session.accessToken())).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }

      return session.refresh().pipe(
        switchMap((renewed) => {
          if (!renewed) {
            // O refresh token também não vale mais: a sessão acabou.
            void router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
            return throwError(() => error);
          }

          // Repete uma vez. Se falhar de novo, o erro segue para quem chamou (sem novo ciclo).
          return next(withBearer(request, session.accessToken()));
        }),
      );
    }),
  );
};

function withBearer(request: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token === null
    ? request
    : request.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}

/** O caminho da URL se ela é da mesma origem da aplicação e começa com `/api/`; senão, `null`. */
function apiPath(url: string): string | null {
  const parsed = new URL(url, globalThis.location.origin);

  return parsed.origin === globalThis.location.origin && parsed.pathname.startsWith('/api/')
    ? parsed.pathname
    : null;
}
