import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideApiConfiguration } from '../api/api-configuration';
import { provideLocalePtBr } from '../core/locale';
import { DemandFormPage, parseEffort } from './demand-form-page';

const TEAMS = [
  { id: 't1', name: 'Web', peopleCount: 1, dailyCapacityHours: 8 },
  { id: 't2', name: 'Dados', peopleCount: 1, dailyCapacityHours: 6 },
];

const PEOPLE = [
  { id: 'p1', name: 'Ana', teamId: 't1', teamName: 'Web', dailyCapacityHours: 8 },
  { id: 'p2', name: 'Bruno', teamId: 't2', teamName: 'Dados', dailyCapacityHours: 6 },
];

/** Os parâmetros da rota de cada teste: vazio para "nova", `{ id }` para "editar". */
let routeParams: Record<string, string> = {};

/** Uma demanda como a API a devolve. */
function demand(overrides: Record<string, unknown> = {}) {
  return {
    id: 'd1',
    title: 'Checkout',
    priorityRank: 1,
    executionRank: 1,
    startDate: '2026-12-01',
    informedEndDate: '2026-12-20',
    completedOn: null,
    teams: [{ teamId: 't1', teamName: 'Web', estimatedEffortHours: '40' }],
    assignedPersonIds: ['p1'],
    prerequisiteIds: [],
    ...overrides,
  };
}

/**
 * Estes testes rodam também com TZ=Asia/Tokyo e America/Los_Angeles: as datas do formulário são texto
 * `yyyy-MM-dd` e chegam à API sem mudar de dia (RNF-07).
 */
describe('DemandFormPage', () => {
  let fixture: ComponentFixture<DemandFormPage>;
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    routeParams = {};
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideLocalePtBr(),
        provideRouter([{ path: 'demands', children: [] }]),
        {
          provide: ActivatedRoute,
          useFactory: () => ({ snapshot: { paramMap: convertToParamMap(routeParams) } }),
        },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => http.verify());

  const element = (): HTMLElement => fixture.nativeElement as HTMLElement;

  const request = (method: string, url: string): TestRequest =>
    http.expectOne((req) => req.method === method && req.url === url);

  /** Abre a tela: responde os times e as pessoas e, na edição, a demanda. */
  async function open(
    params: Record<string, string> = {},
    existing?: unknown,
    others: unknown[] = [],
  ): Promise<void> {
    routeParams = params;
    fixture = TestBed.createComponent(DemandFormPage);
    // A tela pede os times, as pessoas e as demandas (para "Depende de") primeiro; a demanda vem depois, na edição.
    request('GET', '/api/teams').flush(TEAMS);
    request('GET', '/api/people').flush(PEOPLE);
    if (params['id']) {
      const self = existing ?? demand();
      request('GET', '/api/demands').flush([self, ...others]);
      request('GET', `/api/demands/${params['id']}`).flush(self);
    } else {
      request('GET', '/api/demands').flush(others);
    }
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  const button = (text: string): HTMLButtonElement | undefined =>
    [...element().querySelectorAll('button')].find((b) => b.textContent?.trim() === text);

  const field = (id: string): HTMLInputElement =>
    element().querySelector(`#${id}`) as HTMLInputElement;

  /** Marca ou desmarca o time pelo texto do rótulo ("Envolver o time Web"). */
  const checkTeam = (teamId: string, checked: boolean): void => {
    const name = TEAMS.find((team) => team.id === teamId)!.name;
    const box = [...element().querySelectorAll<HTMLInputElement>('li input[type="checkbox"]')].find(
      (input) => input.closest('label')?.textContent?.includes(`Envolver o time ${name}`),
    )!;
    box.checked = checked;
    box.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  };

  const personBox = (name: string): HTMLInputElement =>
    [...element().querySelectorAll<HTMLInputElement>('input[type="checkbox"]')].find((input) =>
      input.closest('label')?.textContent?.includes(name),
    )!;

  it('creates a demand with the whole aggregate and goes back to the list', async () => {
    const navigate = vi.spyOn(router, 'navigateByUrl');
    await open();
    field('demand-title').value = 'Novo checkout';
    field('demand-title').dispatchEvent(new Event('input'));
    field('demand-start').value = '2026-12-01';
    field('demand-start').dispatchEvent(new Event('input'));
    checkTeam('t1', true);
    field('hours-t1').value = '80';
    field('hours-t1').dispatchEvent(new Event('input'));
    fixture.detectChanges();
    const ana = [...element().querySelectorAll('input[type="checkbox"]')].find((i) =>
      i.closest('li')?.textContent?.includes('Ana'),
    ) as HTMLInputElement;
    ana.checked = true;
    ana.dispatchEvent(new Event('change'));

    button('Salvar demanda')?.click();
    const create = request('POST', '/api/demands');
    expect(create.request.body).toEqual({
      title: 'Novo checkout',
      startDate: '2026-12-01',
      informedEndDate: null,
      teams: [{ teamId: 't1', estimatedEffortHours: 80 }],
      assignedPersonIds: ['p1'],
      prerequisiteIds: [],
    });
    create.flush({ id: 'd9' }, { status: 201, statusText: 'Created' });
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith('/demands');
  });

  it('saves a team without estimate as null, with the demand still valid', async () => {
    await open();
    field('demand-title').value = 'Sem estimativa';
    field('demand-title').dispatchEvent(new Event('input'));
    checkTeam('t2', true);
    fixture.detectChanges();

    button('Salvar demanda')?.click();

    expect(request('POST', '/api/demands').request.body.teams).toEqual([
      { teamId: 't2', estimatedEffortHours: null },
    ]);
  });

  it('asks for the title and does not call the API', async () => {
    await open();
    field('demand-title').value = '   ';
    field('demand-title').dispatchEvent(new Event('input'));

    button('Salvar demanda')?.click();
    fixture.detectChanges();

    expect(element().querySelector('[role="alert"]')?.textContent).toContain(
      'Informe o título da demanda.',
    );
  });

  it.each(['0', '-5', '1,234'])(
    'refuses the effort "%s" with the clear message, without calling the API',
    async (hours) => {
      await open();
      field('demand-title').value = 'Inválida';
      field('demand-title').dispatchEvent(new Event('input'));
      checkTeam('t1', true);
      field('hours-t1').value = hours;
      field('hours-t1').dispatchEvent(new Event('input'));

      button('Salvar demanda')?.click();
      fixture.detectChanges();

      expect(element().textContent).toContain(
        'O esforço deve ser maior que zero, com no máximo 2 casas decimais.',
      );
    },
  );

  it('shows the message of an API refusal and keeps what was typed', async () => {
    await open();
    field('demand-title').value = 'Repetida';
    field('demand-title').dispatchEvent(new Event('input'));

    button('Salvar demanda')?.click();
    request('POST', '/api/demands').flush(
      { detail: 'Um dos times informados não existe.', errors: {} },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(element().querySelector('[role="alert"]')?.textContent).toContain(
      'Um dos times informados não existe.',
    );
    expect(field('demand-title').value).toBe('Repetida');
  });

  it('shows the people only of the teams checked, and unassigns the people of a team unchecked', async () => {
    await open();
    checkTeam('t1', true);
    expect(element().textContent).toContain('Ana (Web)');
    expect(element().textContent).not.toContain('Bruno (Dados)');

    personBox('Ana').checked = true;
    personBox('Ana').dispatchEvent(new Event('change'));
    checkTeam('t1', false);
    checkTeam('t1', true);

    expect(personBox('Ana').checked).toBe(false);
  });

  it('fills the edit form with the current values', async () => {
    await open({ id: 'd1' });

    expect(element().querySelector('h2')?.textContent).toContain('Editar demanda');
    expect(field('demand-title').value).toBe('Checkout');
    expect(field('demand-start').value).toBe('2026-12-01');
    expect(field('hours-t1').value).toBe('40');
  });

  it('updates the demand with the new estimate that was missing', async () => {
    await open(
      { id: 'd1' },
      demand({ teams: [{ teamId: 't1', teamName: 'Web', estimatedEffortHours: null }] }),
    );
    field('hours-t1').value = '40';
    field('hours-t1').dispatchEvent(new Event('input'));

    button('Salvar demanda')?.click();

    expect(request('PUT', '/api/demands/d1').request.body.teams).toEqual([
      { teamId: 't1', estimatedEffortHours: 40 },
    ]);
  });

  it('sends the optional reason of the estimate change only when editing, and only if it was written', async () => {
    await open(
      { id: 'd1' },
      demand({ teams: [{ teamId: 't1', teamName: 'Web', estimatedEffortHours: 16 }] }),
    );
    field('hours-t1').value = '32';
    field('hours-t1').dispatchEvent(new Event('input'));
    const reason = document.getElementById('demand-reason') as HTMLInputElement;
    reason.value = '  escopo maior  ';
    reason.dispatchEvent(new Event('input'));

    button('Salvar demanda')?.click();

    expect(request('PUT', '/api/demands/d1').request.body.estimateChangeReason).toBe(
      'escopo maior',
    );
  });

  it('does not send a reason when none was written', async () => {
    await open(
      { id: 'd1' },
      demand({ teams: [{ teamId: 't1', teamName: 'Web', estimatedEffortHours: 16 }] }),
    );
    field('hours-t1').value = '32';
    field('hours-t1').dispatchEvent(new Event('input'));

    button('Salvar demanda')?.click();

    expect(request('PUT', '/api/demands/d1').request.body).not.toHaveProperty(
      'estimateChangeReason',
    );
  });

  it('does not offer the form for a completed demand', async () => {
    await open({ id: 'd1' }, demand({ completedOn: '2026-12-05' }));

    expect(element().textContent).toContain(
      'Esta demanda já foi concluída e não pode mais ser alterada.',
    );
    expect(element().querySelector('form')).toBeNull();
  });

  it('says that the demand was not found when the API answers 404', async () => {
    routeParams = { id: 'nada' };
    fixture = TestBed.createComponent(DemandFormPage);
    request('GET', '/api/teams').flush(TEAMS);
    request('GET', '/api/people').flush(PEOPLE);
    request('GET', '/api/demands').flush([]);
    request('GET', '/api/demands/nada').flush(null, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(element().textContent).toContain('Demanda não encontrada.');
  });

  describe('Depende de (pré-requisitos)', () => {
    const other = (id: string, title: string, completedOn: string | null = null) =>
      demand({ id, title, completedOn, teams: [], assignedPersonIds: [] });

    const prerequisiteBox = (title: string): HTMLInputElement =>
      [...element().querySelectorAll<HTMLInputElement>('input[type="checkbox"]')].find((input) =>
        input.closest('li')?.textContent?.trim().startsWith(title),
      )!;

    it('sends the chosen prerequisites with the demand', async () => {
      await open({}, undefined, [other('d2', 'Pagamentos'), other('d3', 'Login')]);
      field('demand-title').value = 'Novo checkout';
      field('demand-title').dispatchEvent(new Event('input'));
      checkTeam('t1', true);
      field('hours-t1').value = '8';
      field('hours-t1').dispatchEvent(new Event('input'));
      prerequisiteBox('Pagamentos').checked = true;
      prerequisiteBox('Pagamentos').dispatchEvent(new Event('change'));
      prerequisiteBox('Login').checked = true;
      prerequisiteBox('Login').dispatchEvent(new Event('change'));

      button('Salvar demanda')?.click();

      expect(request('POST', '/api/demands').request.body.prerequisiteIds).toEqual(['d2', 'd3']);
    });

    it('shows the current prerequisites checked and never offers the demand itself', async () => {
      await open({ id: 'd1' }, demand({ prerequisiteIds: ['d2'] }), [other('d2', 'Pagamentos')]);

      expect(prerequisiteBox('Pagamentos').checked).toBe(true);
      const labels = [...element().querySelectorAll('li label')].map((label) =>
        label.textContent?.trim(),
      );
      expect(labels.some((text) => text?.startsWith('Checkout'))).toBe(false);
    });

    it('says that there is no other demand, without blocking the save', async () => {
      await open();

      expect(element().textContent).toContain('Não há outras demandas para escolher.');
      expect(button('Salvar demanda')?.disabled).toBe(false);
    });

    it('marks a completed candidate as completed', async () => {
      await open({}, undefined, [other('d2', 'Pagamentos', '2026-12-05')]);

      expect(prerequisiteBox('Pagamentos').closest('li')?.textContent).toContain('(concluída)');
    });

    it('shows the cycle message and keeps the fields and the checks', async () => {
      const navigate = vi.spyOn(router, 'navigateByUrl');
      await open({}, undefined, [other('d2', 'Pagamentos')]);
      field('demand-title').value = 'Novo checkout';
      field('demand-title').dispatchEvent(new Event('input'));
      checkTeam('t1', true);
      field('hours-t1').value = '8';
      field('hours-t1').dispatchEvent(new Event('input'));
      prerequisiteBox('Pagamentos').checked = true;
      prerequisiteBox('Pagamentos').dispatchEvent(new Event('change'));

      button('Salvar demanda')?.click();
      request('POST', '/api/demands').flush(
        {
          detail: 'As dependências formariam um ciclo: Novo checkout → Pagamentos → Novo checkout.',
          code: 'dependency-cycle',
        },
        { status: 409, statusText: 'Conflict' },
      );
      await fixture.whenStable();
      fixture.detectChanges();

      expect(element().textContent).toContain('As dependências formariam um ciclo');
      expect(navigate).not.toHaveBeenCalled();
      expect(field('demand-title').value).toBe('Novo checkout');
      expect(prerequisiteBox('Pagamentos').checked).toBe(true);
    });

    it('saves again without the prerequisite that closed the cycle', async () => {
      const navigate = vi.spyOn(router, 'navigateByUrl');
      await open({}, undefined, [other('d2', 'Pagamentos')]);
      field('demand-title').value = 'Novo checkout';
      field('demand-title').dispatchEvent(new Event('input'));
      checkTeam('t1', true);
      field('hours-t1').value = '8';
      field('hours-t1').dispatchEvent(new Event('input'));
      prerequisiteBox('Pagamentos').checked = true;
      prerequisiteBox('Pagamentos').dispatchEvent(new Event('change'));
      button('Salvar demanda')?.click();
      request('POST', '/api/demands').flush(
        { detail: 'ciclo', code: 'dependency-cycle' },
        { status: 409, statusText: 'Conflict' },
      );
      await fixture.whenStable();

      prerequisiteBox('Pagamentos').checked = false;
      prerequisiteBox('Pagamentos').dispatchEvent(new Event('change'));
      button('Salvar demanda')?.click();
      const retry = request('POST', '/api/demands');
      expect(retry.request.body.prerequisiteIds).toEqual([]);
      retry.flush({ id: 'd9' }, { status: 201, statusText: 'Created' });
      await fixture.whenStable();

      expect(navigate).toHaveBeenCalledWith('/demands');
    });
  });

  describe('Posição e simulação de impacto (RF-16)', () => {
    const SIMULATION = {
      isPredictable: true,
      unpredictableReason: null,
      forecastStartDate: '2026-12-01',
      endDateWithoutMargin: '2026-12-08',
      forecastEndDate: '2026-12-10',
      position: '1',
      sectorEndDate: '2026-12-20',
      sectorDeltaDays: '2',
      changes: [
        {
          demandId: 'd2',
          title: 'Relatórios',
          currentForecastEndDate: '2026-12-10',
          newForecastEndDate: '2026-12-12',
          deltaDays: '2',
        },
      ],
    };

    /** Preenche o básico (título e um time com 80 horas) e deixa o formulário pronto para simular. */
    const fillBasics = (): void => {
      field('demand-title').value = 'Novo checkout';
      field('demand-title').dispatchEvent(new Event('input'));
      checkTeam('t1', true);
      field('hours-t1').value = '80';
      field('hours-t1').dispatchEvent(new Event('input'));
      fixture.detectChanges();
    };

    const BODY = {
      title: 'Novo checkout',
      startDate: null,
      informedEndDate: null,
      teams: [{ teamId: 't1', estimatedEffortHours: 80 }],
      assignedPersonIds: [],
      prerequisiteIds: [],
    };

    const simulate = async (response: object = SIMULATION): Promise<TestRequest> => {
      button('Simular impacto')?.click();
      const pending = request('POST', '/api/demands/simulate');
      pending.flush(response);
      await fixture.whenStable();
      fixture.detectChanges();
      return pending;
    };

    it('sends the position with the demand when it is filled', async () => {
      await open();
      fillBasics();
      field('demand-position').value = '1';
      field('demand-position').dispatchEvent(new Event('input'));

      button('Salvar demanda')?.click();
      const create = request('POST', '/api/demands');
      expect(create.request.body).toEqual({ ...BODY, position: 1 });
      create.flush({ id: 'd9' }, { status: 201, statusText: 'Created' });
      await fixture.whenStable();
    });

    it('refuses a position above the end of the queue, without calling the API', async () => {
      await open();
      fillBasics();
      field('demand-position').value = '2';
      field('demand-position').dispatchEvent(new Event('input'));

      button('Salvar demanda')?.click();
      fixture.detectChanges();

      expect(element().textContent).toContain('A posição deve ser um número de 1 a 1.');
      http.expectNone('/api/demands');
    });

    it('simulates the impact without saving, and shows the moved demands', async () => {
      await open();
      fillBasics();
      field('demand-position').value = '1';
      field('demand-position').dispatchEvent(new Event('input'));

      const pending = await simulate();

      expect(pending.request.body).toEqual({ ...BODY, position: 1 });
      expect(element().textContent).toContain('Impacto da nova demanda');
      expect(element().textContent).toContain('Posição usada: 1');
      expect(element().textContent).toContain('Relatórios');
      expect(element().textContent).toContain('Descartar simulação');
      http.expectNone('/api/demands');
    });

    it('says that no existing demand moves when the list of changes is empty', async () => {
      await open();
      fillBasics();

      await simulate({ ...SIMULATION, changes: [] });

      expect(element().textContent).toContain('Nenhuma demanda existente muda de data.');
    });

    it('shows the reason in words when the new demand is not predictable', async () => {
      await open();
      fillBasics();

      await simulate({
        ...SIMULATION,
        isPredictable: false,
        unpredictableReason: 'MissingEstimate',
        forecastEndDate: null,
      });

      expect(element().textContent).toContain('Falta a estimativa de algum time');
    });

    it('marks the result as out of date when a field changes after simulating', async () => {
      await open();
      fillBasics();
      await simulate();

      field('demand-title').value = 'Outro título';
      field('demand-title').dispatchEvent(new Event('input'));
      fixture.detectChanges();

      expect(element().textContent).toContain('Os dados mudaram desde a simulação');
      expect(element().textContent).not.toContain('Posição usada');
    });

    it('discards the simulation and keeps the form as it was', async () => {
      await open();
      fillBasics();
      await simulate();

      button('Descartar simulação')?.click();
      fixture.detectChanges();

      expect(element().textContent).not.toContain('Impacto da nova demanda');
      expect(field('demand-title').value).toBe('Novo checkout');
      http.expectNone('/api/demands');
    });

    it('keeps the form and shows the message when the simulation fails', async () => {
      await open();
      fillBasics();
      button('Simular impacto')?.click();

      request('POST', '/api/demands/simulate').flush('erro', { status: 500, statusText: 'Erro' });
      fixture.detectChanges();

      expect(element().textContent).toContain(
        'Não foi possível concluir agora. Tente novamente em instantes.',
      );
      expect(field('demand-title').value).toBe('Novo checkout');
    });

    it('saves the same body that was simulated', async () => {
      await open();
      fillBasics();
      field('demand-position').value = '1';
      field('demand-position').dispatchEvent(new Event('input'));
      const simulated = await simulate();

      button('Salvar demanda')?.click();
      const create = request('POST', '/api/demands');
      expect(create.request.body).toEqual(simulated.request.body);
      create.flush({ id: 'd9' }, { status: 201, statusText: 'Created' });
      await fixture.whenStable();
    });

    it('does not offer the position or the simulation when editing a demand', async () => {
      await open({ id: 'd1' });

      expect(field('demand-position')).toBeNull();
      expect(button('Simular impacto')).toBeUndefined();
    });
  });

  describe('summary and dependency search', () => {
    const summary = (): string =>
      element().querySelector('[aria-label="Resumo da demanda"]')?.textContent ?? '';

    it('follows the typing in the summary, and shows "Não salvo" for a new demand', async () => {
      await open();
      expect(summary()).toContain('Não salvo');
      expect(summary()).toContain('No fim da fila');

      field('demand-start').value = '2026-12-01';
      field('demand-start').dispatchEvent(new Event('input'));
      checkTeam('t1', true);
      field('hours-t1').value = '40';
      field('hours-t1').dispatchEvent(new Event('input'));
      checkTeam('t2', true);
      fixture.detectChanges();

      expect(summary()).toContain('01/12/2026');
      expect(summary()).toContain('Web, Dados');
      expect(summary()).toContain('40 h');
      expect(summary()).toContain('1 sem estimativa');
    });

    it('shows "Editando" in the summary of an existing demand, with the deadline', async () => {
      await open({ id: 'd1' });

      expect(summary()).toContain('Editando');
      expect(summary()).toContain('20/12/2026');
      expect(summary()).toContain('Mantida');
    });

    it('filters the dependency candidates by title, ignoring accents', async () => {
      await open({}, undefined, [
        { ...demand({ id: 'd2', title: 'Pagamentos', teams: [], assignedPersonIds: [] }) },
        { ...demand({ id: 'd3', title: 'Autenticação', teams: [], assignedPersonIds: [] }) },
      ]);

      const search = element().querySelector<HTMLInputElement>('input[type="search"]');
      search!.value = 'AUTENTICACAO';
      search!.dispatchEvent(new Event('input'));
      fixture.detectChanges();

      const titles = [...element().querySelectorAll('li label')].map((l) => l.textContent?.trim());
      expect(titles.some((t) => t?.startsWith('Autenticação'))).toBe(true);
      expect(titles.some((t) => t?.startsWith('Pagamentos'))).toBe(false);
    });

    it('shows the summary dates of a fresh simulation and drops them when it goes stale', async () => {
      await open();
      field('demand-title').value = 'Nova';
      field('demand-title').dispatchEvent(new Event('input'));
      checkTeam('t1', true);
      field('hours-t1').value = '8';
      field('hours-t1').dispatchEvent(new Event('input'));
      button('Simular impacto')?.click();
      request('POST', '/api/demands/simulate').flush({
        position: 1,
        isPredictable: true,
        unpredictableReason: null,
        forecastStartDate: '2026-12-01',
        endDateWithoutMargin: '2026-12-08',
        forecastEndDate: '2026-12-10',
        sectorEndDate: '2026-12-10',
        sectorDeltaDays: 0,
        changes: [],
      });
      fixture.detectChanges();
      expect(summary()).toContain('08/12/2026');
      expect(summary()).toContain('10/12/2026');

      field('demand-title').value = 'Outra';
      field('demand-title').dispatchEvent(new Event('input'));
      fixture.detectChanges();
      expect(summary()).not.toContain('08/12/2026');
    });
  });
});

describe('parseEffort', () => {
  it.each([
    ['', { value: null, valid: true }],
    ['80', { value: 80, valid: true }],
    ['12,5', { value: 12.5, valid: true }],
    ['0.01', { value: 0.01, valid: true }],
    ['0', { value: null, valid: false }],
    ['-5', { value: null, valid: false }],
    ['1,234', { value: null, valid: false }],
    ['abc', { value: null, valid: false }],
  ])('reads "%s"', (text, expected) => {
    expect(parseEffort(text)).toEqual(expected);
  });
});
