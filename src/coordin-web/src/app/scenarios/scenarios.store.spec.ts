import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideApiConfiguration } from '../api/api-configuration';
import { ScenariosStore } from './scenarios.store';

/** Um item do cronograma com os campos da API (só o que o mapeamento usa). */
function item(demandId: string, title: string, completedOn: string | null = null) {
  return {
    demandId,
    title,
    priorityRank: 1,
    executionRank: 1,
    isOutOfPriorityOrder: false,
    teams: [],
    totalEstimatedEffortHours: '0',
    startDate: null,
    isPredictable: true,
    unpredictableReason: null,
    forecastStartDate: null,
    endDateWithoutMargin: null,
    forecastEndDate: null,
    informedEndDate: null,
    exceedsInformedEndDate: false,
    completedOn,
    baselineEndDate: null,
    baselineDeviationDays: null,
    forecastDeviationDays: null,
    prerequisites: [],
  };
}

/** Um cenário com os campos da API (números em texto, como o cliente gerado os entrega). */
function scenario(executionOrder: string[], delta = '-10') {
  return {
    executionOrder,
    isComparable: true,
    items: [
      {
        demandId: executionOrder[0],
        title: 'Alfa',
        priorityRank: '1',
        newExecutionRank: '1',
        currentForecastEndDate: '2027-03-01',
        forecastStartDate: '2027-02-01',
        forecastEndDate: '2027-03-04',
        deltaDays: '3',
        weight: '10',
        scoreContributionDelta: delta,
      },
    ],
    score: { current: '100', scenario: '90', delta },
    sectorDeltaDays: '0',
    sectorEndDate: '2027-03-04',
  };
}

describe('ScenariosStore', () => {
  let store: ScenariosStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        ScenariosStore,
      ],
    });
    store = TestBed.inject(ScenariosStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const scheduleRequest = (): TestRequest =>
    http.expectOne((req) => req.method === 'GET' && req.url === '/api/schedule');

  const loadSchedule = (items: unknown[]) => {
    store.load();
    scheduleRequest().flush({ executionOrderMode: 'FollowPriority', items });
  };

  it('loads only the schedule, never the recommendations', () => {
    loadSchedule([item('a', 'Alfa')]);

    expect(store.status()).toBe('ready');
    expect(store.searched()).toBe(false);
    http.expectNone((req) => req.url.includes('recommendations'));
  });

  it('shows the recommendations and marks the search as done', () => {
    loadSchedule([item('a', 'Alfa')]);
    store.recommend();
    http.expectOne((req) => req.url === '/api/scenarios/recommendations').flush([scenario(['a'])]);

    expect(store.recommendations()).toHaveLength(1);
    expect(store.recommendations()[0].score.delta).toBe(-10);
    expect(store.searched()).toBe(true);
    expect(store.busy()).toBe('idle');
  });

  it('keeps the scenarios on screen when a new search fails', () => {
    loadSchedule([item('a', 'Alfa')]);
    store.recommend();
    http.expectOne('/api/scenarios/recommendations').flush([scenario(['a'])]);

    store.recommend();
    http
      .expectOne('/api/scenarios/recommendations')
      .flush('erro', { status: 500, statusText: 'Erro' });

    expect(store.recommendations()).toHaveLength(1);
    expect(store.message()).not.toBeNull();
    expect(store.busy()).toBe('idle');
  });

  it('sends the full order, keeping a completed demand in its own place', () => {
    loadSchedule([item('a', 'Alfa'), item('done', 'Feita', '2027-01-05'), item('b', 'Bravo')]);
    store.simulate(['b', 'a']);

    const request = http.expectOne('/api/scenarios/simulate');
    expect(request.request.body).toEqual({ executionOrder: ['b', 'done', 'a'] });
    request.flush(scenario(['b', 'done', 'a']));
    expect(store.simulation()?.executionOrder).toEqual(['b', 'done', 'a']);
  });

  it('removes a rejected scenario from the screen without calling the API', () => {
    loadSchedule([item('a', 'Alfa')]);
    store.recommend();
    http.expectOne('/api/scenarios/recommendations').flush([scenario(['a'])]);

    store.reject(store.recommendations()[0]);

    expect(store.recommendations()).toHaveLength(0);
    // o afterEach confere que nenhuma chamada extra foi feita
  });

  it('applies the scenario order, clears the old scenarios and reloads the schedule', () => {
    loadSchedule([item('a', 'Alfa')]);
    store.recommend();
    http.expectOne('/api/scenarios/recommendations').flush([scenario(['a'])]);

    store.apply(store.recommendations()[0]);
    const apply = http.expectOne('/api/scenarios/apply');
    expect(apply.request.body).toEqual({ executionOrder: ['a'] });
    apply.flush(scenario(['a']));

    expect(store.recommendations()).toHaveLength(0);
    expect(store.lastApplied()).not.toBeNull();
    scheduleRequest().flush({ executionOrderMode: 'Custom', items: [item('a', 'Alfa')] });
    expect(store.schedule()?.executionOrderMode).toBe('Custom');
  });

  it('keeps the scenario and shows the message when the approval fails', () => {
    loadSchedule([item('a', 'Alfa')]);
    store.recommend();
    http.expectOne('/api/scenarios/recommendations').flush([scenario(['a'])]);

    store.apply(store.recommendations()[0]);
    http.expectOne('/api/scenarios/apply').flush('erro', { status: 500, statusText: 'Erro' });

    expect(store.recommendations()).toHaveLength(1);
    expect(store.message()).not.toBeNull();
    expect(store.busy()).toBe('idle');
  });
});
