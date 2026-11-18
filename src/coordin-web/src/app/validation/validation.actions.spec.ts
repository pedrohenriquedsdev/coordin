import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideApiConfiguration } from '../api/api-configuration';
import { SERVICE_FAILURE } from '../core/problems/messages';
import { ValidationStore } from './validation.store';

const STATUS_URL = '/api/validation/status';
const BASELINE_URL = '/api/validation/baseline';
const DATASET_URL = '/api/validation/dataset';
const PLAN_URL = '/api/validation/current-plan';
const EVALUATE_URL = '/api/validation/evaluate';

const ready = {
  demandCount: 2,
  baselineCount: 0,
  currentPlanCaptured: false,
  currentPlanCapturedAt: null,
};

/** Um resultado mínimo da comparação, só com o que a tela de cálculo do passo 3 usa. */
const result = {
  periodStart: '2027-01-04',
  periodEnd: '2027-03-14',
  efficiency: {
    currentProcess: {
      idleHours: 1,
      occupancySpreadPoints: 2,
      overloadedPeriods: 0,
      sectorEndDate: null,
    },
    withCoordin: {
      idleHours: 1,
      occupancySpreadPoints: 2,
      overloadedPeriods: 0,
      sectorEndDate: null,
    },
  },
  deviation: {
    averageBaselineDeviationDays: 0,
    averageCoordinDeviationDays: 0,
    averageBaselineAbsoluteDeviationDays: 0,
    averageCoordinAbsoluteDeviationDays: 0,
    items: [],
  },
  priorityCheck: { items: [], worsenedCount: 0 },
};

describe('ValidationStore actions', () => {
  let store: ValidationStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ValidationStore,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
      ],
    });
    store = TestBed.inject(ValidationStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.match(() => true));

  describe('loading the dataset', () => {
    it('refuses to load without the confirmation, and calls nothing', () => {
      store.loadDataset();

      expect(store.message()).toBe('Marque a confirmação antes de carregar o conjunto.');
      http.expectNone(DATASET_URL);
    });

    it('sends the confirmation, reports the counts and refreshes the status', () => {
      store.confirmed.set(true);
      store.loadDataset();

      const request = http.expectOne(DATASET_URL);
      expect(request.request.body).toEqual({ confirmReplaceAll: true });
      request.flush({ teams: 7, people: 35, absences: 5, holidays: 4, demands: 30 });

      expect(store.message()).toBe('Conjunto carregado: 7 times, 35 pessoas, 30 demandas.');
      expect(store.confirmed()).toBe(false);
      http.expectOne(STATUS_URL).flush(ready);
    });

    it('shows the generic service message when the load fails with a server error', () => {
      store.confirmed.set(true);
      store.loadDataset();

      http.expectOne(DATASET_URL).flush(null, { status: 500, statusText: 'Server Error' });

      expect(store.busy()).toBe('idle');
      expect(store.message()).toBe(SERVICE_FAILURE);
    });

    it('shows the API detail when the load is refused with a rule message', () => {
      store.confirmed.set(true);
      store.loadDataset();

      http
        .expectOne(DATASET_URL)
        .flush(
          { status: 400, detail: 'Confirmação ausente.' },
          { status: 400, statusText: 'Bad Request' },
        );

      expect(store.message()).toBe('Confirmação ausente.');
    });
  });

  describe('the baseline', () => {
    function loadedBaseline(): void {
      store.loadBaseline();
      http.expectOne(BASELINE_URL).flush([
        { demandId: 'a', title: 'Alfa', baselineDate: null },
        { demandId: 'b', title: 'Beta', baselineDate: null },
      ]);
    }

    it('keeps the dates already saved in the server', () => {
      store.loadBaseline();
      http.expectOne(BASELINE_URL).flush([
        { demandId: 'a', title: 'Alfa', baselineDate: '2027-06-30' },
        { demandId: 'b', title: 'Beta', baselineDate: null },
      ]);

      expect(store.dates()).toEqual({ a: '2027-06-30' });
      expect(store.missingDates()).toBe(1);
    });

    it('refuses to save while some demand has no date, and calls nothing', () => {
      loadedBaseline();
      store.setDate('a', '2027-06-30');

      store.saveBaseline();

      expect(store.message()).toBe('Faltam 1 datas na linha de base.');
      http.expectNone((req) => req.method === 'PUT');
    });

    it('saves every demand with its date, in the same order, and refreshes', () => {
      loadedBaseline();
      store.setDate('a', '2027-06-30');
      store.setDate('b', '2027-07-01');

      store.saveBaseline();

      const request = http.expectOne((req) => req.method === 'PUT' && req.url === BASELINE_URL);
      expect(request.request.body).toEqual({
        baselines: [
          { demandId: 'a', baselineDate: '2027-06-30' },
          { demandId: 'b', baselineDate: '2027-07-01' },
        ],
      });
      request.flush(null, { status: 204, statusText: 'No Content' });

      expect(store.message()).toBe('Linha de base gravada.');
      http.expectOne(STATUS_URL).flush(ready);
      http.expectOne(BASELINE_URL).flush([]);
    });
  });

  describe('the current plan', () => {
    it('reports an existing plan as information, not as a failure', () => {
      store.capturePlan();

      http.expectOne(PLAN_URL).flush(
        {
          type: 'about:blank',
          title: 'Conflito',
          status: 409,
          code: 'validation-current-plan-exists',
        },
        { status: 409, statusText: 'Conflict' },
      );

      expect(store.message()).toBe('O planejamento atual já está guardado.');
      expect(store.busy()).toBe('idle');
      http.expectOne(STATUS_URL).flush(ready);
    });
  });

  describe('the comparison', () => {
    it('refuses a range that is not valid, and calls nothing', () => {
      store.setParam('minEffortFactor', 1.2);

      store.evaluate();

      expect(store.message()).toBe('O fator mínimo não pode ser maior que o máximo.');
      http.expectNone(EVALUATE_URL);
    });

    it('sends the parameters, keeps the result and does not refresh the status', () => {
      store.evaluate();

      const request = http.expectOne(EVALUATE_URL);
      expect(request.request.body).toEqual({
        minEffortFactor: 0.9,
        maxEffortFactor: 1.1,
        seed: 20261126,
      });
      request.flush(result);

      expect(store.result()).toEqual(result);
      expect(store.busy()).toBe('idle');
      http.expectNone(STATUS_URL);
    });

    it('shows the rule message when the comparison has nothing to compare', () => {
      store.evaluate();

      http
        .expectOne(EVALUATE_URL)
        .flush(
          { status: 409, code: 'validation-nothing-to-compare' },
          { status: 409, statusText: 'Conflict' },
        );

      expect(store.message()).toBe('Não há demanda previsível para comparar.');
      expect(store.result()).toBeNull();
    });
  });
});
