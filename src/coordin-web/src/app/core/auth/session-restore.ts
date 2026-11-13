import { EnvironmentProviders, inject, provideAppInitializer } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { SessionService } from './session.service';

/**
 * Restaura a sessão antes de o roteador decidir a primeira rota (DT-28).
 *
 * Recarregar a página apaga o token de acesso, que só existe em memória. Mas o cookie do refresh
 * token (`HttpOnly`) continua no navegador, então uma renovação devolve o token sem novo login. Se não
 * houver cookie válido, a renovação falha em silêncio e a guarda leva o gestor a `/login`.
 */
export function provideSessionRestore(): EnvironmentProviders {
  return provideAppInitializer(async () => {
    // refresh() nunca dá erro: devolve verdadeiro ou falso. O resultado importa menos que a espera.
    await firstValueFrom(inject(SessionService).refresh());
  });
}
