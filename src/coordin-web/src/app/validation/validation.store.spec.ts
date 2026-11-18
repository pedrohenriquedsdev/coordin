import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideApiConfiguration } from '../api/api-configuration';
import { ValidationStore } from './validation.store';

const STATUS_URL = '/api/validation/status';

describe('ValidationStore', () => {
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

  afterEach(() => http.verify());

  it('starts loading, with no step yet', () => {
    expect(store.load()).toBe('loading');
    expect(store.step()).toBeNull();
  });

  it('derives the step from the status the server returned', () => {
    store.refresh();
    http
      .expectOne((req) => req.url === STATUS_URL)
      .flush({
        demandCount: 30,
        baselineCount: 30,
        currentPlanCaptured: false,
        currentPlanCapturedAt: null,
      });

    expect(store.load()).toBe('ready');
    expect(store.step()).toBe('plan');
  });

  it('shows the area as unavailable when the API answers 404', () => {
    store.refresh();
    http
      .expectOne((req) => req.url === STATUS_URL)
      .flush(null, { status: 404, statusText: 'Not Found' });

    expect(store.load()).toBe('unavailable');
  });

  it('shows a failure, not unavailable, for any other error', () => {
    store.refresh();
    http
      .expectOne((req) => req.url === STATUS_URL)
      .flush(null, { status: 500, statusText: 'Server Error' });

    expect(store.load()).toBe('failed');
  });

  it('keeps the last known status when a later refresh fails', () => {
    store.refresh();
    http
      .expectOne((req) => req.url === STATUS_URL)
      .flush({
        demandCount: 0,
        baselineCount: 0,
        currentPlanCaptured: false,
        currentPlanCapturedAt: null,
      });

    store.refresh();
    http
      .expectOne((req) => req.url === STATUS_URL)
      .flush(null, { status: 500, statusText: 'Server Error' });

    expect(store.load()).toBe('failed');
    expect(store.step()).toBe('load');
  });
});
