import { HttpErrorResponse } from '@angular/common/http';

/** O que uma tela precisa saber de uma resposta de erro da API (Problem Details, DT-18). */
export interface ApiProblem {
  /** Status HTTP; `0` quando a API não respondeu (rede fora do ar, por exemplo). */
  readonly status: number;
  /** Código estável de um conflito de regra (`team-name-taken`, `team-has-people`...), se houver. */
  readonly code: string | null;
  /** Mensagem em português já pronta para exibir, se a API mandou. */
  readonly detail: string | null;
  /** Erros por campo de uma validação (`400`), com a chave em `camelCase`. */
  readonly fieldErrors: Readonly<Record<string, readonly string[]>>;
}

const NO_RESPONSE: ApiProblem = { status: 0, code: null, detail: null, fieldErrors: {} };

/** Lê o Problem Details de um erro do `HttpClient`. Qualquer outro erro vira "sem resposta". */
export function readProblem(error: unknown): ApiProblem {
  if (!(error instanceof HttpErrorResponse)) {
    return NO_RESPONSE;
  }

  const body = bodyOf(error);

  return {
    status: error.status,
    code: typeof body['code'] === 'string' ? body['code'] : null,
    detail: typeof body['detail'] === 'string' ? body['detail'] : null,
    fieldErrors: readFieldErrors(body['errors']),
  };
}

/** A API não respondeu ou falhou por dentro: a tela deve oferecer "tentar de novo", sem culpar o gestor. */
export function isServiceFailure(problem: ApiProblem): boolean {
  return problem.status === 0 || problem.status >= 500;
}

/**
 * O corpo do erro como objeto. As funções geradas de `DELETE` e `logout` pedem a resposta como texto
 * (o sucesso é `204`, sem corpo), então um Problem Details de erro chega como uma string JSON.
 */
function bodyOf(error: HttpErrorResponse): Record<string, unknown> {
  const raw: unknown = error.error;

  if (typeof raw === 'string') {
    try {
      return asRecord(JSON.parse(raw));
    } catch {
      return {};
    }
  }

  return asRecord(raw);
}

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function readFieldErrors(errors: unknown): Record<string, string[]> {
  if (typeof errors !== 'object' || errors === null) {
    return {};
  }

  const result: Record<string, string[]> = {};
  for (const [field, messages] of Object.entries(errors)) {
    if (Array.isArray(messages)) {
      result[field] = messages.filter((message): message is string => typeof message === 'string');
    }
  }

  return result;
}
