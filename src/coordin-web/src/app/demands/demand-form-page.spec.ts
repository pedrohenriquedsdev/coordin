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
  async function open(params: Record<string, string> = {}, existing?: unknown): Promise<void> {
    routeParams = params;
    fixture = TestBed.createComponent(DemandFormPage);
    // A tela pede os times e as pessoas primeiro; a demanda vem depois, na edição.
    request('GET', '/api/teams').flush(TEAMS);
    request('GET', '/api/people').flush(PEOPLE);
    if (params['id']) {
      request('GET', `/api/demands/${params['id']}`).flush(existing ?? demand());
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
    request('GET', '/api/demands/nada').flush(null, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(element().textContent).toContain('Demanda não encontrada.');
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
