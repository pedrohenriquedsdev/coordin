import { Route } from '@angular/router';
import { routes } from '../app.routes';

/** Todas as funções `loadComponent` da árvore de rotas, na ordem em que aparecem. */
function lazyLoaders(list: readonly Route[]): (() => Promise<unknown>)[] {
  return list.flatMap((route) => [
    ...(typeof route.loadComponent === 'function'
      ? [route.loadComponent as () => Promise<unknown>]
      : []),
    ...lazyLoaders(route.children ?? []),
  ]);
}

/**
 * Carrega, de uma vez, o código de todas as telas carregadas sob demanda.
 *
 * Em produção, o navegador baixa cada tela quando ela é aberta. Num teste que abre uma tela pelas rotas de
 * verdade, esse primeiro carregamento (a tela e tudo o que ela importa, como o Chart.js) entra na conta do
 * tempo do teste e, com a máquina ocupada, passa dos segundos que o teste tem. Chamar esta função num
 * `beforeAll` (com tempo folgado) deixa o código carregado, e o teste mede só o comportamento.
 *
 * Lê a lista das próprias rotas, então uma tela nova entra sozinha.
 */
export async function preloadPages(): Promise<void> {
  await Promise.all(lazyLoaders(routes).map((load) => load()));
}
