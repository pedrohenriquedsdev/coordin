import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideApiConfiguration } from '../api/api-configuration';
import { provideLocalePtBr } from '../core/locale';
import { CalendarPage } from './calendar-page';

const TEAMS = [
  { id: 't1', name: 'Pagamentos', peopleCount: 2, dailyCapacityHours: 14 },
  { id: 't2', name: 'Web', peopleCount: 1, dailyCapacityHours: 8 },
];

const PEOPLE = [
  { id: 'pa', name: 'Ana', teamId: 't1', teamName: 'Pagamentos', dailyCapacityHours: 8 },
  { id: 'pc', name: 'Carla', teamId: 't1', teamName: 'Pagamentos', dailyCapacityHours: 6 },
  { id: 'pf', name: 'Fábio', teamId: 't2', teamName: 'Web', dailyCapacityHours: 8 },
];

const ABSENCES = [
  {
    id: 'a2',
    personId: 'pf',
    personName: 'Fábio',
    teamId: 't2',
    teamName: 'Web',
    type: 'Other',
    startDate: '2026-12-21',
    endDate: '2026-12-22',
  },
  {
    id: 'a1',
    personId: 'pc',
    personName: 'Carla',
    teamId: 't1',
    teamName: 'Pagamentos',
    type: 'Vacation',
    startDate: '2027-01-04',
    endDate: '2027-01-15',
  },
];

const HOLIDAYS = [
  { id: 'h1', date: '2026-12-25', description: 'Natal' },
  { id: 'h2', date: '2027-01-01', description: null },
];

/** Estes testes rodam também com TZ=Asia/Tokyo e America/Sao_Paulo: nenhuma data pode mudar de dia. */
describe('CalendarPage', () => {
  let fixture: ComponentFixture<CalendarPage>;
  let http: HttpTestingController;
  let root: HTMLElement;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideLocalePtBr(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const request = (method: string, url: string): TestRequest =>
    http.expectOne((req) => req.method === method && req.url === url);

  const settle = async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  /** A tela pede as quatro listas ao abrir e depois de cada escrita bem-sucedida. */
  async function answerLists(
    absences: unknown[] = ABSENCES,
    holidays: unknown[] = HOLIDAYS,
  ): Promise<void> {
    request('GET', '/api/absences').flush(absences);
    request('GET', '/api/holidays').flush(holidays);
    request('GET', '/api/people').flush(PEOPLE);
    request('GET', '/api/teams').flush(TEAMS);
    await settle();
  }

  async function open(
    absences: unknown[] = ABSENCES,
    holidays: unknown[] = HOLIDAYS,
  ): Promise<void> {
    fixture = TestBed.createComponent(CalendarPage);
    root = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();
    await answerLists(absences, holidays);
  }

  const text = () => root.textContent?.replace(/\s+/g, ' ') ?? '';
  const section = (label: string) =>
    root.querySelector(`section[aria-label="${label}"]`) as HTMLElement;
  const rows = (label: string) =>
    [...section(label).querySelectorAll('tbody tr')].map((row) =>
      [...row.querySelectorAll('td')].slice(0, -1).map((cell) => cell.textContent?.trim()),
    );

  const button = (scope: HTMLElement, label: string): HTMLButtonElement => {
    const found = [...scope.querySelectorAll('button')].find(
      (item) => item.textContent?.trim() === label,
    );
    if (!found) {
      throw new Error(`botão "${label}" não encontrado`);
    }
    return found;
  };

  const type = (selector: string, value: string) => {
    const input = root.querySelector<HTMLInputElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  };

  const choose = (selector: string, value: string) => {
    const select = root.querySelector<HTMLSelectElement>(selector)!;
    select.value = value;
    select.dispatchEvent(new Event('change'));
  };

  const click = async (scope: HTMLElement, label: string) => {
    button(scope, label).click();
    await settle();
  };

  const alertText = (scope: HTMLElement) =>
    scope.querySelector('[role=alert]')?.textContent?.trim();

  describe('the page', () => {
    it('shows "Carregando…" until the lists arrive', () => {
      fixture = TestBed.createComponent(CalendarPage);
      root = fixture.nativeElement as HTMLElement;
      fixture.detectChanges();

      expect(text()).toContain('Carregando…');

      request('GET', '/api/absences').flush([]);
      request('GET', '/api/holidays').flush([]);
      request('GET', '/api/people').flush([]);
      request('GET', '/api/teams').flush([]);
    });

    it('shows a failure message and lets the manager try again', async () => {
      fixture = TestBed.createComponent(CalendarPage);
      root = fixture.nativeElement as HTMLElement;
      fixture.detectChanges();
      request('GET', '/api/absences').flush({}, { status: 500, statusText: 'Server Error' });
      await settle();
      http.match((req) => req.method === 'GET'); // descarta os pedidos cancelados pelo forkJoin

      expect(text()).toContain('Não foi possível carregar o calendário.');
      await click(root, 'Tentar de novo');
      await answerLists();

      expect(text()).toContain('Ausências das pessoas');
    });
  });

  describe('absences', () => {
    it('lists every absence with the person, the team, the type and the dates as dd/MM/yyyy', async () => {
      await open();

      expect(rows('Ausências das pessoas')).toEqual([
        ['Fábio', 'Web', 'Outra', '21/12/2026', '22/12/2026'],
        ['Carla', 'Pagamentos', 'Férias', '04/01/2027', '15/01/2027'],
      ]);
    });

    it('filters by team in the browser', async () => {
      await open();

      choose('#absence-filter', 't1');
      await settle();

      expect(rows('Ausências das pessoas')).toEqual([
        ['Carla', 'Pagamentos', 'Férias', '04/01/2027', '15/01/2027'],
      ]);

      choose('#absence-filter', '');
      await settle();
      expect(rows('Ausências das pessoas')).toHaveLength(2);
    });

    it('says so when the chosen team has no absences', async () => {
      await open([ABSENCES[0]]);

      choose('#absence-filter', 't1');
      await settle();

      expect(text()).toContain('Nenhuma ausência neste time.');
    });

    it('explains that there are no absences yet', async () => {
      await open([]);

      expect(text()).toContain('Ainda não há ausências registradas.');
    });

    it('registers an absence, sending the chosen dates exactly as yyyy-MM-dd', async () => {
      await open();
      const scope = section('Ausências das pessoas');

      choose('#absence-person', 'pc');
      choose('#absence-type', 'Other');
      type('#absence-start', '2026-12-31');
      type('#absence-end', '2027-01-02');
      await click(scope, 'Registrar ausência');

      const post = request('POST', '/api/people/pc/absences');
      expect(post.request.body).toEqual({
        type: 'Other',
        startDate: '2026-12-31',
        endDate: '2027-01-02',
      });
      post.flush({}, { status: 201, statusText: 'Created' });
      await answerLists([
        ...ABSENCES,
        {
          id: 'a3',
          personId: 'pc',
          personName: 'Carla',
          teamId: 't1',
          teamName: 'Pagamentos',
          type: 'Other',
          startDate: '2026-12-31',
          endDate: '2027-01-02',
        },
      ]);

      expect(rows('Ausências das pessoas').at(-1)).toEqual([
        'Carla',
        'Pagamentos',
        'Outra',
        '31/12/2026',
        '02/01/2027',
      ]);
      expect(root.querySelector<HTMLInputElement>('#absence-start')?.value).toBe('');
    });

    it('refuses an end before the start, without calling the API', async () => {
      await open();
      const scope = section('Ausências das pessoas');

      choose('#absence-person', 'pc');
      type('#absence-start', '2027-01-15');
      type('#absence-end', '2027-01-04');
      await click(scope, 'Registrar ausência');

      expect(text()).toContain('O fim não pode ser anterior ao início.');
      http.expectNone((req) => req.method === 'POST');
    });

    it('accepts an absence of a single day', async () => {
      await open();

      choose('#absence-person', 'pf');
      type('#absence-start', '2027-02-08');
      type('#absence-end', '2027-02-08');
      await click(section('Ausências das pessoas'), 'Registrar ausência');

      expect(request('POST', '/api/people/pf/absences').request.body).toMatchObject({
        startDate: '2027-02-08',
        endDate: '2027-02-08',
      });
    });

    it('asks for the person and the dates, without calling the API', async () => {
      await open();

      await click(section('Ausências das pessoas'), 'Registrar ausência');

      expect(text()).toContain('Escolha a pessoa.');
      expect(text()).toContain('Informe a data de início.');
      expect(text()).toContain('Informe a data de fim.');
      http.expectNone((req) => req.method === 'POST');
    });

    it('asks only for the type and the period: there is no field for the reason', async () => {
      await open();
      const form = section('Ausências das pessoas').querySelector('form')!;

      const labels = [...form.querySelectorAll('label')].map((label) => label.textContent?.trim());

      expect(labels).toEqual(['Pessoa', 'Tipo', 'Início', 'Fim']);
      expect(form.querySelector('textarea, input[type=text]')).toBeNull();
      expect(form.textContent).toContain('Sem motivo detalhado');
    });

    it('shows the message of the API without losing what was typed', async () => {
      await open();
      choose('#absence-person', 'pc');
      type('#absence-start', '2027-01-04');
      type('#absence-end', '2027-01-15');

      await click(section('Ausências das pessoas'), 'Registrar ausência');
      request('POST', '/api/people/pc/absences').flush(
        { errors: { endDate: ['O fim não pode ser anterior ao início.'] } },
        { status: 400, statusText: 'Bad Request' },
      );
      await settle();

      expect(alertText(section('Ausências das pessoas'))).toBe(
        'O fim não pode ser anterior ao início.',
      );
      expect(root.querySelector<HTMLInputElement>('#absence-end')?.value).toBe('2027-01-15');
    });

    it('edits the period of an absence, keeping the person fixed', async () => {
      await open();
      const scope = section('Ausências das pessoas');

      scope
        .querySelector<HTMLButtonElement>('button[aria-label="Editar ausência de Carla"]')!
        .click();
      await settle();
      expect(text()).toContain('Editar ausência');
      expect(root.querySelector<HTMLSelectElement>('#absence-person')?.disabled).toBe(true);
      type('#absence-end', '2027-01-20');
      await click(scope, 'Salvar alterações');

      const put = request('PUT', '/api/absences/a1');
      expect(put.request.body).toEqual({
        type: 'Vacation',
        startDate: '2027-01-04',
        endDate: '2027-01-20',
      });
      put.flush({});
      await answerLists();

      expect(text()).toContain('Registrar ausência');
    });

    it('deletes an absence only after the manager confirms', async () => {
      await open();
      const scope = section('Ausências das pessoas');

      scope
        .querySelector<HTMLButtonElement>('button[aria-label="Excluir ausência de Fábio"]')!
        .click();
      await settle();
      http.expectNone((req) => req.method === 'DELETE');
      await click(scope, 'Confirmar');

      request('DELETE', '/api/absences/a2').flush(null, { status: 204, statusText: 'No Content' });
      await answerLists([ABSENCES[1]]);

      expect(rows('Ausências das pessoas')).toHaveLength(1);
    });
  });

  describe('holidays', () => {
    it('lists the holidays by date, with "—" when there is no description', async () => {
      await open();

      expect(rows('Feriados do setor')).toEqual([
        ['25/12/2026', 'Natal'],
        ['01/01/2027', '—'],
      ]);
    });

    it('adds a holiday, sending the date exactly as chosen', async () => {
      await open();
      const scope = section('Feriados do setor');

      type('#holiday-date', '2027-04-21');
      type('#holiday-description', '  Tiradentes ');
      await click(scope, 'Adicionar feriado');

      const post = request('POST', '/api/holidays');
      expect(post.request.body).toEqual({ date: '2027-04-21', description: 'Tiradentes' });
      post.flush({}, { status: 201, statusText: 'Created' });
      await answerLists(ABSENCES, [
        ...HOLIDAYS,
        { id: 'h3', date: '2027-04-21', description: 'Tiradentes' },
      ]);

      expect(rows('Feriados do setor').at(-1)).toEqual(['21/04/2027', 'Tiradentes']);
    });

    it('sends no description when it is left blank', async () => {
      await open();

      type('#holiday-date', '2027-05-01');
      await click(section('Feriados do setor'), 'Adicionar feriado');

      expect(request('POST', '/api/holidays').request.body).toEqual({
        date: '2027-05-01',
        description: null,
      });
    });

    it('asks for the date without calling the API', async () => {
      await open();

      await click(section('Feriados do setor'), 'Adicionar feriado');

      expect(text()).toContain('Informe a data do feriado.');
      http.expectNone((req) => req.method === 'POST');
    });

    it('explains a date that is already a holiday', async () => {
      await open();
      type('#holiday-date', '2026-12-25');

      await click(section('Feriados do setor'), 'Adicionar feriado');
      request('POST', '/api/holidays').flush(
        { code: 'holiday-date-taken', detail: 'Essa data já é feriado.' },
        { status: 409, statusText: 'Conflict' },
      );
      await settle();

      expect(alertText(section('Feriados do setor'))).toBe('Essa data já é feriado.');
      expect(root.querySelector<HTMLInputElement>('#holiday-date')?.value).toBe('2026-12-25');
    });

    it('removes a holiday only after the manager confirms', async () => {
      await open();
      const scope = section('Feriados do setor');

      scope
        .querySelector<HTMLButtonElement>('button[aria-label="Remover feriado de 25/12/2026"]')!
        .click();
      await settle();
      http.expectNone((req) => req.method === 'DELETE');
      await click(scope, 'Confirmar');

      request('DELETE', '/api/holidays/h1').flush(null, { status: 204, statusText: 'No Content' });
      await answerLists(ABSENCES, [HOLIDAYS[1]]);

      expect(rows('Feriados do setor')).toEqual([['01/01/2027', '—']]);
    });

    it('explains that there are no holidays yet', async () => {
      await open(ABSENCES, []);

      expect(text()).toContain('Ainda não há feriados cadastrados.');
    });
  });
});
