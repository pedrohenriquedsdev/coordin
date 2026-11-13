import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { provideApiConfiguration } from '../../api/api-configuration';
import { SessionService } from './session.service';

describe('SessionService', () => {
  let session: SessionService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideApiConfiguration('')],
    });
    session = TestBed.inject(SessionService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    vi.restoreAllMocks();
  });

  describe('login', () => {
    it('posts the credentials and keeps the access token in memory', async () => {
      expect(session.isAuthenticated()).toBe(false);

      const result = firstValueFrom(session.login('gestor@coordin.test', 'uma-senha-12345'));
      const request = http.expectOne('/api/auth/login');
      expect(request.request.method).toBe('POST');
      expect(request.request.body).toEqual({
        email: 'gestor@coordin.test',
        password: 'uma-senha-12345',
      });
      request.flush({ accessToken: 'token-de-acesso' });
      await result;

      expect(session.isAuthenticated()).toBe(true);
      expect(session.accessToken()).toBe('token-de-acesso');
    });

    it('never writes the token to the browser storage or to a cookie', async () => {
      const setItem = vi.spyOn(Storage.prototype, 'setItem');

      const result = firstValueFrom(session.login('gestor@coordin.test', 'uma-senha-12345'));
      http.expectOne('/api/auth/login').flush({ accessToken: 'token-secreto-unico' });
      await result;

      expect(setItem).not.toHaveBeenCalled();
      expect(localStorage.length).toBe(0);
      expect(sessionStorage.length).toBe(0);
      expect(document.cookie).not.toContain('token-secreto-unico');
    });

    it('fails without keeping anything when the credentials are rejected', async () => {
      const result = firstValueFrom(session.login('gestor@coordin.test', 'errada'));
      http
        .expectOne('/api/auth/login')
        .flush({ status: 401 }, { status: 401, statusText: 'Unauthorized' });

      await expect(result).rejects.toMatchObject({ status: 401 });
      expect(session.isAuthenticated()).toBe(false);
      expect(session.accessToken()).toBeNull();
    });
  });

  describe('refresh', () => {
    it('stores the new access token and reports success', async () => {
      const result = firstValueFrom(session.refresh());
      const request = http.expectOne('/api/auth/refresh');
      expect(request.request.method).toBe('POST');
      request.flush({ accessToken: 'renovado' });

      expect(await result).toBe(true);
      expect(session.accessToken()).toBe('renovado');
    });

    it('shares a single request between simultaneous callers', async () => {
      const callers = [
        firstValueFrom(session.refresh()),
        firstValueFrom(session.refresh()),
        firstValueFrom(session.refresh()),
      ];

      // Três chamadas, uma única requisição: o refresh token só vale uma vez (rotação).
      const requests = http.match('/api/auth/refresh');
      expect(requests).toHaveLength(1);
      requests[0].flush({ accessToken: 'unico' });

      expect(await Promise.all(callers)).toEqual([true, true, true]);
    });

    it('answers every simultaneous caller with the same result', async () => {
      const first = firstValueFrom(session.refresh());
      const second = firstValueFrom(session.refresh());

      http.expectOne('/api/auth/refresh').flush({ accessToken: 'compartilhado' });

      expect(await first).toBe(true);
      expect(await second).toBe(true);
      expect(session.accessToken()).toBe('compartilhado');
    });

    it('allows a new renewal once the previous one has finished', async () => {
      const first = firstValueFrom(session.refresh());
      http.expectOne('/api/auth/refresh').flush({ accessToken: 'um' });
      await first;

      const second = firstValueFrom(session.refresh());
      http.expectOne('/api/auth/refresh').flush({ accessToken: 'dois' });
      await second;

      expect(session.accessToken()).toBe('dois');
    });

    it('ends the session and reports failure when the renewal is rejected', async () => {
      const login = firstValueFrom(session.login('gestor@coordin.test', 'uma-senha-12345'));
      http.expectOne('/api/auth/login').flush({ accessToken: 'ainda-valia' });
      await login;
      expect(session.isAuthenticated()).toBe(true);

      const result = firstValueFrom(session.refresh());
      http
        .expectOne('/api/auth/refresh')
        .flush({ status: 401 }, { status: 401, statusText: 'Unauthorized' });

      expect(await result).toBe(false);
      expect(session.isAuthenticated()).toBe(false);
    });
  });

  describe('logout', () => {
    it('calls the API and discards the token', async () => {
      const login = firstValueFrom(session.login('gestor@coordin.test', 'uma-senha-12345'));
      http.expectOne('/api/auth/login').flush({ accessToken: 'abc' });
      await login;

      const result = firstValueFrom(session.logout());
      const request = http.expectOne('/api/auth/logout');
      expect(request.request.method).toBe('POST');
      request.flush(null, { status: 204, statusText: 'No Content' });
      await result;

      expect(session.isAuthenticated()).toBe(false);
    });

    it('discards the token locally even when the server cannot be reached', async () => {
      const login = firstValueFrom(session.login('gestor@coordin.test', 'uma-senha-12345'));
      http.expectOne('/api/auth/login').flush({ accessToken: 'abc' });
      await login;

      const result = firstValueFrom(session.logout());
      http.expectOne('/api/auth/logout').error(new ProgressEvent('error'));
      await result;

      expect(session.isAuthenticated()).toBe(false);
    });
  });
});
