import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideApiConfiguration } from '../api/api-configuration';
import { provideLocalePtBr } from '../core/locale';
import { SchedulePage } from './schedule-page';

/** Um item do cronograma com os campos da API; os números chegam como texto, como o cliente gerado os entrega. */
function item(overrides: Record<string, unknown> = {}) {
  return {
    demandId: 'd1',
    title: 'Checkout',
    priorityRank: 1,
    executionRank: 1,
    isOutOfPriorityOrder: false,
    teams: [{ teamId: 't1', teamName: 'Web', estimatedEffortHours: '80' }],
    totalEstimatedEffortHours: '80',
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

/**
 * Estes testes rodam também com TZ=Asia/Tokyo e America/Los_Angeles: nenhuma data pode mudar de dia
 * (RNF-07). A tabela e os cartões existem os dois no DOM; o CSS é que escolhe qual aparece.
 */
describe('SchedulePage', () => {
  let fixture: ComponentFixture<SchedulePage>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideLocalePtBr(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SchedulePage);
  });

  afterEach(() => http.verify());

  const request = (): TestRequest =>
    http.expectOne((req) => req.method === 'GET' && req.url === '/api/schedule');

  const answer = async (items: unknown[]) => {
    request().flush({ executionOrderMode: 'FollowPriority', items });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const element = (): HTMLElement => fixture.nativeElement as HTMLElement;

  it('shows the title "Cronograma" and the loading text first', () => {
    fixture.detectChanges();

    expect(element().querySelector('h2')?.textContent).toContain('Cronograma');
    expect(element().textContent).toContain('Carregando…');
    request().flush({ executionOrderMode: 'FollowPriority', items: [] });
  });

  it('explains that there are no demands and offers to register one', async () => {
    fixture.detectChanges();
    await answer([]);

    expect(element().textContent).toContain('Ainda não há demandas.');
    expect(element().querySelector('a[href="/demands/new"]')?.textContent).toContain(
      'Cadastrar demanda',
    );
  });

  it('shows the failure with "Tentar de novo", which asks again', async () => {
    fixture.detectChanges();
    request().flush('erro', { status: 500, statusText: 'Erro' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(element().querySelector('[role="alert"]')?.textContent).toContain(
      'Não foi possível carregar o cronograma.',
    );
    const retry = [...element().querySelectorAll('button')].find((b) =>
      b.textContent?.includes('Tentar de novo'),
    );
    retry?.click();
    request().flush({ executionOrderMode: 'FollowPriority', items: [] });
  });

  it('shows the two forecast dates with their labels, the teams with hours and the total', async () => {
    fixture.detectChanges();
    await answer([
      item({
        teams: [
          { teamId: 't1', teamName: 'Web', estimatedEffortHours: '80' },
          { teamId: 't2', teamName: 'Dados', estimatedEffortHours: null },
        ],
      }),
    ]);

    const table = element().querySelector('[aria-label="Cronograma em tabela"]')?.textContent ?? '';
    expect(table).toContain('Sem margem: 08/12/2026');
    expect(table).toContain('Com margem: 10/12/2026');
    expect(table).toContain('Web: 80 h');
    expect(table).toContain('Dados: sem estimativa');
    expect(table).toContain('Total: 80 h');
  });

  it('shows the reason and no invented dates for an unpredictable demand', async () => {
    fixture.detectChanges();
    await answer([
      item({
        isPredictable: false,
        unpredictableReason: 'NoCapacity',
        forecastStartDate: null,
        endDateWithoutMargin: null,
        forecastEndDate: null,
      }),
    ]);

    const table = element().querySelector('[aria-label="Cronograma em tabela"]')?.textContent ?? '';
    expect(table).toContain('Sem capacidade: falta pessoa alocada ou a capacidade é zero');
    expect(table).not.toContain('Sem margem');
    expect(table).toContain('—');
  });

  it('flags a forecast past the informed end and an out-of-order demand, with text', async () => {
    fixture.detectChanges();
    await answer([
      item({
        informedEndDate: '2026-12-05',
        exceedsInformedEndDate: true,
        isOutOfPriorityOrder: true,
      }),
    ]);

    const table = element().querySelector('[aria-label="Cronograma em tabela"]')?.textContent ?? '';
    expect(table).toContain('Passa do prazo informado');
    expect(table).toContain('Fora da ordem de prioridade');
  });

  it('shows the deviations of a completed demand in words', async () => {
    fixture.detectChanges();
    await answer([
      item({
        completedOn: '2026-12-16',
        baselineDeviationDays: 6,
        forecastDeviationDays: 2,
      }),
    ]);

    const table = element().querySelector('[aria-label="Cronograma em tabela"]')?.textContent ?? '';
    expect(table).toContain('Concluída em 16/12/2026');
    expect(table).toContain('6 dias de atraso');
    expect(table).toContain('2 dias de atraso');
  });

  it('keeps the same data in the card list, for the small screen', async () => {
    fixture.detectChanges();
    await answer([item({ title: 'Checkout', completedOn: null })]);

    const cards = element().querySelector('[aria-label="Cronograma em cartões"]');
    expect(cards?.querySelector('h3')?.textContent).toContain('1. Checkout');
    expect(cards?.textContent).toContain('Com margem: 10/12/2026');
  });

  it('shows the prerequisites in the table and in the cards', async () => {
    fixture.detectChanges();
    await answer([
      item({
        prerequisites: [
          { demandId: 'd2', title: 'Pagamentos' },
          { demandId: 'd3', title: 'Login' },
        ],
      }),
    ]);

    const table = element().querySelector('[aria-label="Cronograma em tabela"]')?.textContent ?? '';
    const cards =
      element().querySelector('[aria-label="Cronograma em cartões"]')?.textContent ?? '';
    expect(table).toContain('Depende de: Pagamentos, Login');
    expect(cards).toContain('Depende de: Pagamentos, Login');
  });

  it('shows no "Depende de" line for a demand without prerequisites', async () => {
    fixture.detectChanges();
    await answer([item()]);

    expect(element().textContent).not.toContain('Depende de:');
  });

  it('shows the prerequisite reason and no invented dates', async () => {
    fixture.detectChanges();
    await answer([
      item({
        isPredictable: false,
        unpredictableReason: 'PrerequisiteUnpredictable',
        forecastStartDate: null,
        endDateWithoutMargin: null,
        forecastEndDate: null,
        prerequisites: [{ demandId: 'd2', title: 'Pagamentos' }],
      }),
    ]);

    const table = element().querySelector('[aria-label="Cronograma em tabela"]')?.textContent ?? '';
    expect(table).toContain('Depende de uma demanda que não tem previsão');
    expect(table).toContain('Depende de: Pagamentos');
    expect(table).not.toContain('Com margem');
  });

  it('keeps the prerequisite line when the forecast start is pushed later than the registered start', async () => {
    fixture.detectChanges();
    await answer([
      item({
        startDate: '2026-12-01',
        forecastStartDate: '2026-12-11',
        prerequisites: [{ demandId: 'd2', title: 'Pagamentos' }],
      }),
    ]);

    const table = element().querySelector('[aria-label="Cronograma em tabela"]')?.textContent ?? '';
    expect(table).toContain('11/12/2026');
    expect(table).toContain('Depende de: Pagamentos');
  });
});
