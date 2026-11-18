import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideApiConfiguration } from '../api/api-configuration';
import { Clock } from '../core/dates/calendar-date';
import { provideLocalePtBr } from '../core/locale';
import { DemandsPage } from './demands-page';

/** Uma demanda como a API a devolve (números em texto onde o cliente gerado os tipa assim). */
function demand(id: string, title: string, rank: number, completedOn: string | null = null) {
  return {
    id,
    title,
    priorityRank: String(rank),
    executionRank: String(rank),
    startDate: '2026-12-01',
    informedEndDate: null,
    completedOn,
    teams: [{ teamId: 't1', teamName: 'Web', estimatedEffortHours: '40' }],
    assignedPersonIds: [],
    prerequisiteIds: [],
  };
}

/** A linha do cronograma de uma demanda, só com os campos que a tela usa. */
function item(demandId: string, overrides: Record<string, unknown> = {}) {
  return {
    demandId,
    title: demandId,
    priorityRank: 1,
    executionRank: 1,
    isOutOfPriorityOrder: false,
    teams: [],
    totalEstimatedEffortHours: '40',
    startDate: '2026-12-01',
    isPredictable: true,
    unpredictableReason: null,
    forecastStartDate: '2026-12-01',
    endDateWithoutMargin: '2026-12-08',
    forecastEndDate: '2026-12-10',
    informedEndDate: null,
    exceedsInformedEndDate: false,
    completedOn: null,
    baselineEndDate: null,
    baselineDeviationDays: null,
    forecastDeviationDays: null,
    prerequisites: [],
    ...overrides,
  };
}

/** Estes testes rodam também com TZ=Asia/Tokyo e America/Los_Angeles: nenhuma data pode mudar de dia. */
describe('DemandsPage', () => {
  let fixture: ComponentFixture<DemandsPage>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideLocalePtBr(),
        provideRouter([]),
        // "Hoje" fixo, para a data sugerida na conclusão não depender do dia em que o teste roda.
        { provide: Clock, useValue: { now: () => 0, today: () => '2026-12-31' } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(DemandsPage);
  });

  afterEach(() => http.verify());

  const element = (): HTMLElement => fixture.nativeElement as HTMLElement;

  const request = (method: string, url: string): TestRequest =>
    http.expectOne((req) => req.method === method && req.url === url);

  /** A tela pede as demandas e o cronograma; depois de cada escrita, pede os dois de novo. */
  async function answerLists(demands: unknown[], items: unknown[] = []): Promise<void> {
    request('GET', '/api/demands').flush(demands);
    request('GET', '/api/schedule').flush({ executionOrderMode: 'FollowPriority', items });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  async function open(demands: unknown[], items: unknown[] = []): Promise<void> {
    fixture.detectChanges();
    await answerLists(demands, items);
  }

  const buttonIn = (scope: Element, text: string): HTMLButtonElement | undefined =>
    [...scope.querySelectorAll('button')].find((b) => b.textContent?.trim() === text);

  const listOf = (label: string): HTMLElement | null =>
    element().querySelector(`[aria-label="${label}"]`);

  it('lists the active demands in priority order, and the first one cannot go up', async () => {
    await open([demand('a', 'Alfa', 1), demand('b', 'Beta', 2)]);

    const active = listOf('Demandas ativas');
    expect(active?.querySelectorAll('li').length).toBe(2);
    const first = active?.querySelector('li') as HTMLElement;
    expect(first.textContent).toContain('1. Alfa');
    expect(buttonIn(first, 'Subir')?.disabled).toBe(true);
    expect(buttonIn(first, 'Descer')?.disabled).toBe(false);
  });

  it('moves a demand up by sending the whole list, with the two swapped', async () => {
    await open([demand('a', 'Alfa', 1), demand('b', 'Beta', 2)]);
    const second = listOf('Demandas ativas')?.querySelectorAll('li')[1] as HTMLElement;

    buttonIn(second, 'Subir')?.click();
    const reorder = request('PUT', '/api/demands/priority-order');
    expect(reorder.request.body).toEqual({ demandIds: ['b', 'a'] });
    reorder.flush(null, { status: 204, statusText: 'No Content' });
    await answerLists([demand('b', 'Beta', 1), demand('a', 'Alfa', 2)]);

    expect(listOf('Demandas ativas')?.querySelector('li')?.textContent).toContain('Beta');
  });

  it('skips the completed demand between two active ones and keeps it in place', async () => {
    await open([
      demand('a', 'Alfa', 1),
      demand('c', 'Gama', 2, '2026-12-05'),
      demand('b', 'Beta', 3),
    ]);
    const second = listOf('Demandas ativas')?.querySelectorAll('li')[1] as HTMLElement;

    buttonIn(second, 'Subir')?.click();

    expect(request('PUT', '/api/demands/priority-order').request.body).toEqual({
      demandIds: ['b', 'c', 'a'],
    });
  });

  it('shows a clear message and keeps the order when the reorder is refused', async () => {
    await open([demand('a', 'Alfa', 1), demand('b', 'Beta', 2)]);
    const second = listOf('Demandas ativas')?.querySelectorAll('li')[1] as HTMLElement;

    buttonIn(second, 'Subir')?.click();
    request('PUT', '/api/demands/priority-order').flush(
      { detail: 'A lista precisa conter todas as demandas, uma única vez cada.', errors: {} },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(element().querySelector('[role="alert"]')?.textContent).toContain(
      'A lista precisa conter todas as demandas',
    );
    expect(listOf('Demandas ativas')?.querySelector('li')?.textContent).toContain('Alfa');
  });

  it('asks before deleting, and cancelling sends nothing', async () => {
    await open([demand('a', 'Alfa', 1)]);
    const row = listOf('Demandas ativas')?.querySelector('li') as HTMLElement;

    buttonIn(row, 'Excluir')?.click();
    fixture.detectChanges();
    expect(element().textContent).toContain('Excluir a demanda “Alfa”?');
    buttonIn(element(), 'Cancelar')?.click();
    fixture.detectChanges();

    expect(element().textContent).not.toContain('Excluir a demanda “Alfa”?');
  });

  it('deletes the demand after the confirmation and reloads the list', async () => {
    await open([demand('a', 'Alfa', 1)]);
    buttonIn(listOf('Demandas ativas')?.querySelector('li') as HTMLElement, 'Excluir')?.click();
    fixture.detectChanges();

    buttonIn(element(), 'Confirmar exclusão')?.click();
    request('DELETE', '/api/demands/a').flush(null, { status: 204, statusText: 'No Content' });
    await answerLists([]);

    expect(element().textContent).toContain('Não há demandas ativas.');
  });

  it('suggests today as the completion date, and completes the demand with that date', async () => {
    await open([demand('a', 'Alfa', 1)]);
    buttonIn(listOf('Demandas ativas')?.querySelector('li') as HTMLElement, 'Concluir')?.click();
    fixture.detectChanges();

    const input = element().querySelector('input[type="date"]') as HTMLInputElement;
    expect(input.value).toBe('2026-12-31');
    buttonIn(element(), 'Confirmar conclusão')?.click();
    const complete = request('PUT', '/api/demands/a/completion');
    expect(complete.request.body).toEqual({ completedOn: '2026-12-31', actualEffortHours: null });
    complete.flush({ ...demand('a', 'Alfa', 1), completedOn: '2026-12-31' });
    await answerLists([demand('a', 'Alfa', 1, '2026-12-31')]);

    expect(element().textContent).toContain('Concluídas (1)');
  });

  it('sends the actual effort when the manager informs it, with a decimal comma', async () => {
    await open([demand('a', 'Alfa', 1)]);
    buttonIn(listOf('Demandas ativas')?.querySelector('li') as HTMLElement, 'Concluir')?.click();
    fixture.detectChanges();
    const effort = element().querySelector('input[id^="actual-effort-"]') as HTMLInputElement;
    effort.value = '52,5';
    effort.dispatchEvent(new Event('input'));

    buttonIn(element(), 'Confirmar conclusão')?.click();

    expect(request('PUT', '/api/demands/a/completion').request.body).toEqual({
      completedOn: '2026-12-31',
      actualEffortHours: 52.5,
    });
  });

  it('refuses an actual effort of zero or less, without calling the API', async () => {
    await open([demand('a', 'Alfa', 1)]);
    buttonIn(listOf('Demandas ativas')?.querySelector('li') as HTMLElement, 'Concluir')?.click();
    fixture.detectChanges();
    const effort = element().querySelector('input[id^="actual-effort-"]') as HTMLInputElement;
    effort.value = '0';
    effort.dispatchEvent(new Event('input'));

    buttonIn(element(), 'Confirmar conclusão')?.click();
    fixture.detectChanges();

    expect(element().textContent).toContain(
      'Informe um esforço real maior que zero, ou deixe em branco.',
    );
    http.expectNone((req) => req.url === '/api/demands/a/completion');
  });

  it('refuses a completion before the start, without calling the API', async () => {
    await open([demand('a', 'Alfa', 1)]);
    buttonIn(listOf('Demandas ativas')?.querySelector('li') as HTMLElement, 'Concluir')?.click();
    fixture.detectChanges();
    const input = element().querySelector('input[type="date"]') as HTMLInputElement;
    input.value = '2026-11-30';
    input.dispatchEvent(new Event('input'));

    buttonIn(element(), 'Confirmar conclusão')?.click();
    fixture.detectChanges();

    expect(element().querySelector('[role="alert"]')?.textContent).toContain(
      'A data de conclusão não pode ser anterior ao início da demanda.',
    );
    // Nenhuma requisição de conclusão: o verify do afterEach garante que não houve nenhuma.
  });

  it('shows the message of a completion the API refuses', async () => {
    await open([demand('a', 'Alfa', 1)]);
    buttonIn(listOf('Demandas ativas')?.querySelector('li') as HTMLElement, 'Concluir')?.click();
    fixture.detectChanges();
    buttonIn(element(), 'Confirmar conclusão')?.click();
    request('PUT', '/api/demands/a/completion').flush(
      {
        detail: 'A demanda já foi concluída e não pode mais ser alterada.',
        code: 'completed-demand-locked',
      },
      { status: 409, statusText: 'Conflict' },
    );
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(element().querySelector('[role="alert"]')?.textContent).toContain(
      'não pode mais ser alterada',
    );
  });

  it('shows the deviations of a completed demand in the Concluídas tab, read-only', async () => {
    await open(
      [demand('a', 'Alfa', 1, '2026-12-16')],
      [
        item('a', {
          completedOn: '2026-12-16',
          baselineDeviationDays: 6,
          forecastDeviationDays: 2,
          prerequisites: [],
        }),
      ],
    );
    buttonIn(element(), 'Concluídas (1)')?.click();
    fixture.detectChanges();

    const done = listOf('Demandas concluídas') as HTMLElement;
    expect(done.textContent).toContain('Concluída em 16/12/2026');
    expect(done.textContent).toContain('6 dias de atraso');
    expect(done.textContent).toContain('2 dias de atraso');
    expect(buttonIn(done, 'Excluir')).toBeUndefined();
    expect(buttonIn(done, 'Subir')).toBeUndefined();
  });
});
