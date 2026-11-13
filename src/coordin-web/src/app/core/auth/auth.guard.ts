import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionService } from './session.service';

/**
 * Guarda das rotas protegidas (RNF-04): sem sessão, leva a `/login` guardando o destino pedido em
 * `returnUrl` para voltar a ele depois de entrar.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const session = inject(SessionService);
  const router = inject(Router);

  if (session.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(
    ['/login'],
    state.url === '/' ? {} : { queryParams: { returnUrl: state.url } },
  );
};

/**
 * Guarda do caminho que não existe. O roteador resolve `redirectTo` ANTES das guardas, então um
 * redirecionamento cego perderia o destino pedido por um visitante. Aqui o visitante vai ao login
 * guardando o destino (como em qualquer rota protegida); quem já tem sessão vai à página inicial.
 */
export const unknownPageGuard: CanActivateFn = (route, state) => {
  const result = authGuard(route, state);

  return result === true ? inject(Router).createUrlTree(['/']) : result;
};

/** Guarda inversa de `/login`: quem já tem sessão não precisa ver a tela de login. */
export const guestGuard: CanActivateFn = () => {
  const session = inject(SessionService);
  const router = inject(Router);

  return session.isAuthenticated() ? router.createUrlTree(['/']) : true;
};
