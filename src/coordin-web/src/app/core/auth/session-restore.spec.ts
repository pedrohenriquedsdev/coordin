import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideApiConfiguration } from '../../api/api-configuration';
import { provideSessionRestore } from './session-restore';
import { SessionService } from './session.service';

describe('provideSessionRestore', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideSessionRestore(),
      ],
    });
  });

  it('renews the session on startup, so reloading the page needs no new login', async () => {
    const init = TestBed.inject(ApplicationInitStatus);
    const http = TestBed.inject(HttpTestingController);

    const request = http.expectOne('/api/auth/refresh');
    expect(request.request.method).toBe('POST');
    request.flush({ accessToken: 'restaurado' });
    await init.donePromise;

    expect(TestBed.inject(SessionService).isAuthenticated()).toBe(true);
    expect(TestBed.inject(SessionService).accessToken()).toBe('restaurado');
  });

  it('starts the app without a session when there is no valid cookie', async () => {
    const init = TestBed.inject(ApplicationInitStatus);
    const http = TestBed.inject(HttpTestingController);

    http
      .expectOne('/api/auth/refresh')
      .flush({ status: 401 }, { status: 401, statusText: 'Unauthorized' });
    await init.donePromise; // a falha não trava a inicialização

    expect(TestBed.inject(SessionService).isAuthenticated()).toBe(false);
  });
});
