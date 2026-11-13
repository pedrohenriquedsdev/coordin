import { HttpErrorResponse } from '@angular/common/http';
import { isServiceFailure, readProblem } from './problem';

describe('readProblem', () => {
  it('reads the stable code and the message of a conflict', () => {
    const problem = readProblem(
      new HttpErrorResponse({
        status: 409,
        error: { status: 409, code: 'team-name-taken', detail: 'Já existe um time com esse nome.' },
      }),
    );

    expect(problem).toEqual({
      status: 409,
      code: 'team-name-taken',
      detail: 'Já existe um time com esse nome.',
      fieldErrors: {},
    });
  });

  it('reads the errors of each field of a validation failure', () => {
    const problem = readProblem(
      new HttpErrorResponse({
        status: 400,
        error: {
          errors: {
            name: ['O nome do time é obrigatório.'],
            endDate: ['Fim antes do início.', 'x'],
          },
        },
      }),
    );

    expect(problem.status).toBe(400);
    expect(problem.code).toBeNull();
    expect(problem.fieldErrors).toEqual({
      name: ['O nome do time é obrigatório.'],
      endDate: ['Fim antes do início.', 'x'],
    });
  });

  it('ignores parts of the body that are not in the expected shape', () => {
    const problem = readProblem(
      new HttpErrorResponse({
        status: 400,
        error: { code: 42, detail: {}, errors: { name: 'texto', other: [1, 'ok'] } },
      }),
    );

    expect(problem.code).toBeNull();
    expect(problem.detail).toBeNull();
    expect(problem.fieldErrors).toEqual({ other: ['ok'] });
  });

  it('reads a Problem Details that arrives as JSON text (requests made with responseType text)', () => {
    const problem = readProblem(
      new HttpErrorResponse({
        status: 409,
        error:
          '{"code":"team-has-people","detail":"Não é possível excluir um time que tem pessoas."}',
      }),
    );

    expect(problem.code).toBe('team-has-people');
    expect(problem.detail).toBe('Não é possível excluir um time que tem pessoas.');
  });

  it('copes with a body that is plain text or empty', () => {
    expect(readProblem(new HttpErrorResponse({ status: 500, error: 'Erro' })).code).toBeNull();
    expect(readProblem(new HttpErrorResponse({ status: 404, error: null })).fieldErrors).toEqual(
      {},
    );
  });

  it('treats a network failure (status 0) and a non-HTTP error as no response', () => {
    expect(
      readProblem(new HttpErrorResponse({ status: 0, error: new ProgressEvent('error') })).status,
    ).toBe(0);
    expect(readProblem(new Error('qualquer')).status).toBe(0);
    expect(readProblem(undefined).status).toBe(0);
  });
});

describe('isServiceFailure', () => {
  it.each([
    [0, true],
    [500, true],
    [503, true],
    [400, false],
    [404, false],
    [409, false],
  ])('answers for status %i', (status, expected) => {
    expect(isServiceFailure({ status, code: null, detail: null, fieldErrors: {} })).toBe(expected);
  });
});
