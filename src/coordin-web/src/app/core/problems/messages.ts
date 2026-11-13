import { ApiProblem, isServiceFailure } from './problem';

/** Mensagem para quando a API não respondeu ou falhou por dentro: a culpa não é do gestor. */
export const SERVICE_FAILURE = 'Não foi possível concluir agora. Tente novamente em instantes.';

/**
 * A mensagem de um erro de escrita para o gestor. A API já devolve mensagens em português
 * (`detail` dos conflitos e `errors` dos campos), então a tela as usa; o `fallback` cobre o resto.
 */
export function messageFor(problem: ApiProblem, fallback: string): string {
  if (isServiceFailure(problem)) {
    return SERVICE_FAILURE;
  }

  const fieldMessages = Object.values(problem.fieldErrors).flat();
  if (fieldMessages.length > 0) {
    return fieldMessages.join(' ');
  }

  return problem.detail ?? fallback;
}
