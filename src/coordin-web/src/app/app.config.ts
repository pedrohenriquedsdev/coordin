import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideApiConfiguration } from './api/api-configuration';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { provideLocalePtBr } from './core/locale';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideLocalePtBr(),
    provideHttpClient(withInterceptors([authInterceptor])),
    // Raiz vazia: a SPA e a API são da mesma origem (DT-01). O padrão gerado, http://localhost:5080/,
    // é de outra origem: a CSP (connect-src 'self') o bloquearia e o cookie do refresh token não iria.
    provideApiConfiguration(''),
  ],
};
