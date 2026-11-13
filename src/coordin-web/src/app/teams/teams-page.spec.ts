import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideApiConfiguration } from '../api/api-configuration';
import { Clock } from '../core/dates/calendar-date';
import { provideLocalePtBr } from '../core/locale';
import { TeamsPage } from './teams-page';

const TEAMS = [
  { id: 't1', name: 'Pagamentos', peopleCount: 4, dailyCapacityHours: 22 },
  { id: 't2', name: 'Web', peopleCount: 0, dailyCapacityHours: 0 },
];

const PEOPLE = [
  { id: 'p1', name: 'Ana', teamId: 't1', teamName: 'Pagamentos', dailyCapacityHours: 8 },
  { id: 'p2', name: 'Carla', teamId: 't1', teamName: 'Pagamentos', dailyCapacityHours: 6.5 },
  { id: 'p3', name: 'Diego', teamId: 't1', teamName: 'Pagamentos', dailyCapacityHours: 0 },
];

/** "Hoje" fixo (13/11/2026 em São Paulo), para a coluna de próxima ausência não depender do dia em que o teste roda. */
class FixedClock extends Clock {
  override now(): number {
    return Date.parse('2026-11-13T15:00:00Z');
  }
}

describe('TeamsPage', () => {
  let fixture: ComponentFixture<TeamsPage>;
  let http: HttpTestingController;
  let root: HTMLElement;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideLocalePtBr(),
        { provide: Clock, useClass: FixedClock },
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

  /** Abre a tela e responde às duas listas que ela pede ao carregar. */
  async function open(
    teams: unknown[] = TEAMS,
    people: unknown[] = PEOPLE,
    absences: unknown[] = [],
  ): Promise<void> {
    fixture = TestBed.createComponent(TeamsPage);
    root = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();
    request('GET', '/api/teams').flush(teams);
    request('GET', '/api/people').flush(people);
    request('GET', '/api/absences').flush(absences);
    await settle();
  }

  /** Depois de uma escrita bem-sucedida, a tela recarrega as duas listas. */
  async function answerReload(teams: unknown[] = TEAMS, people: unknown[] = PEOPLE): Promise<void> {
    request('GET', '/api/teams').flush(teams);
    request('GET', '/api/people').flush(people);
    request('GET', '/api/absences').flush([]);
    await settle();
  }

  const text = () => root.textContent?.replace(/\s+/g, ' ') ?? '';

  const button = (label: string): HTMLButtonElement => {
    const found = [...root.querySelectorAll('button')].find(
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

  const click = async (label: string) => {
    button(label).click();
    await settle();
  };

  const alertText = () => root.querySelector('[role=alert]')?.textContent?.trim();

  // ---- Lista e detalhe (task 7.1) ----

  describe('the list of teams', () => {
    it('shows "Carregando…" until the lists arrive', () => {
      fixture = TestBed.createComponent(TeamsPage);
      root = fixture.nativeElement as HTMLElement;
      fixture.detectChanges();

      expect(text()).toContain('Carregando…');

      request('GET', '/api/teams').flush([]);
      request('GET', '/api/people').flush([]);
      request('GET', '/api/absences').flush([]);
    });

    it('shows each team with its people and capacity', async () => {
      await open();

      expect(text()).toContain('Times (2)');
      expect(text()).toContain('4 pessoas · 22 h/dia');
      expect(text()).toContain('0 pessoas · 0 h/dia');
    });

    it('shows the capacity of the chosen team, summing its people', async () => {
      await open();

      expect(text()).toContain('Capacidade do time: 22 h/dia útil');
      expect(text()).toContain('Ana');
      expect(text()).toContain('Carla');
    });

    it('lets the manager choose another team to see its details', async () => {
      await open();

      [...root.querySelectorAll('section[aria-label=Times] li button')][1].dispatchEvent(
        new Event('click'),
      );
      await settle();

      expect(text()).toContain('Capacidade do time: 0 h/dia útil');
      expect(text()).toContain('Este time ainda não tem pessoas.');
    });

    it('explains that there are no teams yet and offers "+ Novo time"', async () => {
      await open([], []);

      expect(text()).toContain('Ainda não há times.');
      expect(button('+ Novo time')).toBeTruthy();
    });

    it('shows a failure message and lets the manager try again', async () => {
      fixture = TestBed.createComponent(TeamsPage);
      root = fixture.nativeElement as HTMLElement;
      fixture.detectChanges();
      // O forkJoin cancela o pedido de pessoas assim que o de times falha: só há o que responder em um.
      request('GET', '/api/teams').flush({}, { status: 500, statusText: 'Server Error' });
      await settle();

      expect(alertText()).toContain('Não foi possível carregar os times.');
      http.match((req) => req.url === '/api/people' || req.url === '/api/absences'); // cancelados pelo forkJoin

      await click('Tentar de novo');
      await answerReload();

      expect(text()).toContain('Times (2)');
    });
  });

  describe('creating a team', () => {
    it('saves a new team, reloads the lists and chooses the new team', async () => {
      await open();

      await click('+ Novo time');
      type('#new-team-name', '  Mobile  ');
      await click('Salvar time');

      const post = request('POST', '/api/teams');
      expect(post.request.body).toEqual({ name: 'Mobile' });
      post.flush(
        { id: 't3', name: 'Mobile', peopleCount: 0, dailyCapacityHours: 0 },
        { status: 201, statusText: 'Created' },
      );
      await answerReload([
        ...TEAMS,
        { id: 't3', name: 'Mobile', peopleCount: 0, dailyCapacityHours: 0 },
      ]);

      expect(text()).toContain('Times (3)');
      expect(root.querySelector('#team-name')).toHaveProperty('value', 'Mobile');
      expect(root.querySelector('#new-team-name')).toBeNull();
    });

    it('explains a repeated name without losing what was typed', async () => {
      await open();
      await click('+ Novo time');
      type('#new-team-name', 'pagamentos');

      await click('Salvar time');
      request('POST', '/api/teams').flush(
        { status: 409, code: 'team-name-taken', detail: 'Já existe um time com esse nome.' },
        { status: 409, statusText: 'Conflict' },
      );
      await settle();

      expect(alertText()).toBe('Já existe um time com esse nome.');
      expect(root.querySelector<HTMLInputElement>('#new-team-name')?.value).toBe('pagamentos');
    });

    it('asks for the name and does not call the API when it is empty', async () => {
      await open();
      await click('+ Novo time');

      await click('Salvar time');

      expect(text()).toContain('Informe o nome do time.');
      http.expectNone((req) => req.method === 'POST');
    });

    it('can be cancelled', async () => {
      await open();
      await click('+ Novo time');

      await click('Cancelar');

      expect(root.querySelector('#new-team-name')).toBeNull();
      expect(button('+ Novo time')).toBeTruthy();
    });
  });

  describe('renaming and deleting a team', () => {
    it('renames the chosen team', async () => {
      await open();

      type('#team-name', 'Pagamentos 2');
      await click('Salvar nome');

      const put = request('PUT', '/api/teams/t1');
      expect(put.request.body).toEqual({ name: 'Pagamentos 2' });
      put.flush({ ...TEAMS[0], name: 'Pagamentos 2' });
      await answerReload([{ ...TEAMS[0], name: 'Pagamentos 2' }, TEAMS[1]]);

      expect(text()).toContain('Pagamentos 2');
    });

    it('blocks deleting a team that has people, saying why', async () => {
      await open();

      expect(button('Excluir time').disabled).toBe(true);
      expect(text()).toContain(
        'Indisponível: o time tem pessoas. Remova ou mova as pessoas antes.',
      );
    });

    it('deletes a team without people after the manager confirms', async () => {
      await open();
      [...root.querySelectorAll('section[aria-label=Times] li button')][1].dispatchEvent(
        new Event('click'),
      );
      await settle();

      await click('Excluir time');
      expect(text()).toContain('Excluir o time “Web”?');
      http.expectNone((req) => req.method === 'DELETE'); // ainda não: falta confirmar
      await click('Confirmar exclusão');

      request('DELETE', '/api/teams/t2').flush(null, { status: 204, statusText: 'No Content' });
      await answerReload([TEAMS[0]]);

      expect(text()).toContain('Times (1)');
    });

    it('shows the message of the API when the deletion is refused', async () => {
      await open();
      [...root.querySelectorAll('section[aria-label=Times] li button')][1].dispatchEvent(
        new Event('click'),
      );
      await settle();
      await click('Excluir time');

      await click('Confirmar exclusão');
      request('DELETE', '/api/teams/t2').flush(
        { code: 'team-has-people', detail: 'Não é possível excluir um time que tem pessoas.' },
        { status: 409, statusText: 'Conflict' },
      );
      await settle();

      expect(alertText()).toBe('Não é possível excluir um time que tem pessoas.');
    });

    it('shows a generic message when the server cannot be reached', async () => {
      await open();
      type('#team-name', 'Outro');

      await click('Salvar nome');
      request('PUT', '/api/teams/t1').error(new ProgressEvent('error'));
      await settle();

      expect(alertText()).toBe('Não foi possível concluir agora. Tente novamente em instantes.');
      expect(root.querySelector<HTMLInputElement>('#team-name')?.value).toBe('Outro');
    });
  });

  // ---- Pessoas do time (task 7.2) ----

  describe('the people of the team', () => {
    it('shows "não contribui" instead of a zero capacity, and decimals as pt-BR', async () => {
      await open();

      const capacities = [...root.querySelectorAll('tbody tr')].map((row) =>
        row.querySelectorAll('td')[1].textContent?.trim(),
      );
      expect(capacities).toEqual(['8', '6,5', 'não contribui']);
    });

    it('adds a person to the chosen team', async () => {
      await open();

      type('#person-name', '  Elisa ');
      type('#person-hours', '6');
      await click('Adicionar pessoa');

      const post = request('POST', '/api/people');
      expect(post.request.body).toEqual({ name: 'Elisa', teamId: 't1', dailyCapacityHours: 6 });
      post.flush(
        { id: 'p9', name: 'Elisa', teamId: 't1', teamName: 'Pagamentos', dailyCapacityHours: 6 },
        { status: 201, statusText: 'Created' },
      );
      await answerReload(
        [{ ...TEAMS[0], peopleCount: 5, dailyCapacityHours: 28 }, TEAMS[1]],
        [
          ...PEOPLE,
          { id: 'p9', name: 'Elisa', teamId: 't1', teamName: 'Pagamentos', dailyCapacityHours: 6 },
        ],
      );

      expect(text()).toContain('Elisa');
      expect(text()).toContain('5 pessoas · 28 h/dia');
      expect(root.querySelector<HTMLInputElement>('#person-name')?.value).toBe('');
    });

    it('accepts a capacity of zero', async () => {
      await open();

      type('#person-name', 'Heitor');
      type('#person-hours', '0');
      await click('Adicionar pessoa');

      expect(request('POST', '/api/people').request.body).toEqual({
        name: 'Heitor',
        teamId: 't1',
        dailyCapacityHours: 0,
      });
    });

    it('asks for the capacity and does not call the API when it is missing', async () => {
      await open();

      type('#person-name', 'Elisa');
      await click('Adicionar pessoa');

      expect(text()).toContain('Informe a capacidade em horas por dia útil.');
      http.expectNone((req) => req.method === 'POST');
    });

    it.each(['25', '-1', '24.5'])(
      'refuses the capacity %s without calling the API',
      async (hours) => {
        await open();

        type('#person-name', 'Elisa');
        type('#person-hours', hours);
        await click('Adicionar pessoa');

        expect(text()).toContain('A capacidade deve estar entre 0 e 24 horas por dia útil.');
        http.expectNone((req) => req.method === 'POST');
      },
    );

    it('asks for the name', async () => {
      await open();

      type('#person-hours', '8');
      await click('Adicionar pessoa');

      expect(text()).toContain('Informe o nome da pessoa.');
      http.expectNone((req) => req.method === 'POST');
    });

    it('shows the field errors that the API returns, keeping what was typed', async () => {
      await open();
      type('#person-name', 'Elisa');
      type('#person-hours', '8');

      await click('Adicionar pessoa');
      request('POST', '/api/people').flush(
        { errors: { dailyCapacityHours: ['A capacidade deve ter no máximo 2 casas decimais.'] } },
        { status: 400, statusText: 'Bad Request' },
      );
      await settle();

      expect(alertText()).toBe('A capacidade deve ter no máximo 2 casas decimais.');
      expect(root.querySelector<HTMLInputElement>('#person-name')?.value).toBe('Elisa');
    });

    it('moves a person to another team through "Editar"', async () => {
      await open();

      root.querySelector<HTMLButtonElement>('button[aria-label="Editar Ana"]')!.click();
      await settle();
      expect(text()).toContain('Editar pessoa');
      expect(root.querySelector<HTMLInputElement>('#person-name')?.value).toBe('Ana');
      const select = root.querySelector<HTMLSelectElement>('#person-team')!;
      select.value = 't2';
      select.dispatchEvent(new Event('change'));
      await click('Salvar alterações');

      const put = request('PUT', '/api/people/p1');
      expect(put.request.body).toEqual({ name: 'Ana', teamId: 't2', dailyCapacityHours: 8 });
      put.flush({ ...PEOPLE[0], teamId: 't2', teamName: 'Web' });
      await answerReload(
        [
          { ...TEAMS[0], peopleCount: 3, dailyCapacityHours: 14 },
          { ...TEAMS[1], peopleCount: 1, dailyCapacityHours: 8 },
        ],
        [{ ...PEOPLE[0], teamId: 't2', teamName: 'Web' }, PEOPLE[1], PEOPLE[2]],
      );

      expect(text()).toContain('3 pessoas · 14 h/dia');
      expect(text()).toContain('1 pessoa · 8 h/dia');
      expect(root.querySelector('button[aria-label="Editar Ana"]')).toBeNull(); // saiu do time escolhido
    });

    it('can cancel an edition', async () => {
      await open();
      root.querySelector<HTMLButtonElement>('button[aria-label="Editar Ana"]')!.click();
      await settle();

      await click('Cancelar');

      expect(text()).toContain('Adicionar pessoa');
      expect(root.querySelector<HTMLInputElement>('#person-name')?.value).toBe('');
    });

    it('deletes a person only after the manager confirms', async () => {
      await open();

      root.querySelector<HTMLButtonElement>('button[aria-label="Excluir Ana"]')!.click();
      await settle();
      expect(text()).toContain('Excluir Ana?');
      http.expectNone((req) => req.method === 'DELETE');
      await click('Confirmar');

      request('DELETE', '/api/people/p1').flush(null, { status: 204, statusText: 'No Content' });
      await answerReload(
        [{ ...TEAMS[0], peopleCount: 3, dailyCapacityHours: 6.5 }, TEAMS[1]],
        PEOPLE.slice(1),
      );

      expect(text()).not.toContain('Ana');
      expect(text()).toContain('3 pessoas · 6,5 h/dia');
    });
  });
  describe('the next absence of each person', () => {
    const absence = (personId: string, type: string, startDate: string, endDate: string) => ({
      id: `a-${personId}-${startDate}`,
      personId,
      personName: 'x',
      teamId: 't1',
      teamName: 'Pagamentos',
      type,
      startDate,
      endDate,
    });
    const cells = () =>
      [...root.querySelectorAll('tbody tr')].map((row) =>
        row.querySelectorAll('td')[2].textContent?.trim(),
      );

    it('shows the first absence that has not ended, in a short period format', async () => {
      await open(TEAMS, PEOPLE, [
        absence('p1', 'Vacation', '2027-01-04', '2027-01-15'),
        absence('p2', 'Other', '2026-12-21', '2027-01-04'),
      ]);

      expect(cells()).toEqual(['Férias 04–15/01', 'Outra 21/12–04/01', '—']);
    });

    it('shows "—" when every absence of the person has already ended', async () => {
      await open(TEAMS, PEOPLE, [absence('p1', 'Vacation', '2026-10-01', '2026-10-12')]);

      expect(cells()).toEqual(['—', '—', '—']);
    });

    it('counts an absence that ends today, and picks the earliest start among the ones still to come', async () => {
      await open(TEAMS, PEOPLE, [
        absence('p1', 'Other', '2026-12-01', '2026-12-02'),
        absence('p1', 'Vacation', '2026-11-12', '2026-11-13'), // termina hoje: ainda conta
        absence('p2', 'Vacation', '2026-11-12', '2026-11-12'), // terminou ontem: não conta
      ]);

      expect(cells()).toEqual(['Férias 12–13/11', '—', '—']);
    });
  });
});
