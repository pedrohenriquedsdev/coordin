import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideApiConfiguration } from '../api/api-configuration';
import { ValidationAvailability } from './validation-availability';

describe('ValidationAvailability', () => {
  let availability: ValidationAvailability;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideApiConfiguration('')],
    });
    availability = TestBed.inject(ValidationAvailability);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const statusRequest = () => http.expectOne((req) => req.url === '/api/validation/status');

  it('is unknown until it is checked', () => {
    expect(availability.state()).toBe('unknown');
    expect(availability.available()).toBe(false);
  });

  it('becomes available when the status responds', () => {
    availability.check();
    expect(availability.state()).toBe('checking');

    statusRequest().flush({
      demandCount: 0,
      baselineCount: 0,
      currentPlanCaptured: false,
      currentPlanCapturedAt: null,
    });

    expect(availability.state()).toBe('available');
    expect(availability.available()).toBe(true);
  });

  it('becomes unavailable when the API answers 404 (the switch is off)', () => {
    availability.check();
    statusRequest().flush(null, { status: 404, statusText: 'Not Found' });

    expect(availability.state()).toBe('unavailable');
    expect(availability.available()).toBe(false);
  });

  it('becomes unavailable on any other failure, without an error message here', () => {
    availability.check();
    statusRequest().flush(null, { status: 500, statusText: 'Server Error' });

    expect(availability.state()).toBe('unavailable');
  });

  it('checks only once while the state is known', () => {
    availability.check();
    statusRequest().flush({
      demandCount: 0,
      baselineCount: 0,
      currentPlanCaptured: false,
      currentPlanCapturedAt: null,
    });

    availability.check();

    http.expectNone((req) => req.url === '/api/validation/status');
  });

  it('checks again after reset, for the next session', () => {
    availability.check();
    statusRequest().flush(null, { status: 404, statusText: 'Not Found' });

    availability.reset();
    availability.check();

    expect(availability.state()).toBe('checking');
    statusRequest().flush({
      demandCount: 0,
      baselineCount: 0,
      currentPlanCaptured: false,
      currentPlanCapturedAt: null,
    });
    expect(availability.available()).toBe(true);
  });
});
