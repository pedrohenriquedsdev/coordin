import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { provideApiConfiguration } from '../../api/api-configuration';
import { authInterceptor } from './auth.interceptor';
import { SessionService } from './session.service';

describe('authInterceptor', () => {
  let session: SessionService;
  let client: HttpClient;
  let http: HttpTestingController;
  let router: { url: string; navigate: ReturnType<typeof vi.fn> };

  const unauthorized = { status: 401, statusText: 'Unauthorized' };

  beforeEach(() => {
    router = { url: '/cronograma', navigate: vi.fn().mockResolvedValue(true) };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        { provide: Router, useValue: router },
      ],
    });
    session = TestBed.inject(SessionService);
    client = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function signIn(token = 'token-1'): Promise<void> {
    const login = firstValueFrom(session.login('gestor@coordin.test', 'uma-senha-12345'));
    http.expectOne('/api/auth/login').flush({ accessToken: token });
    await login;
  }

  describe('sending the token', () => {
    it('adds the bearer token to calls to the API', async () => {
      await signIn('token-1');

      const result = firstValueFrom(client.get('/api/demandas'));
      const request = http.expectOne('/api/demandas');
      expect(request.request.headers.get('Authorization')).toBe('Bearer token-1');
      request.flush([]);
      await result;
    });

    it('sends no Authorization header when there is no session', async () => {
      const result = firstValueFrom(client.get('/api/health'));
      const request = http.expectOne('/api/health');
      expect(request.request.headers.has('Authorization')).toBe(false);
      request.flush({});
      await result;
    });

    it('never sends the token outside the API (other sites, static files)', async () => {
      await signIn();

      for (const url of [
        'https://outro-site.example/api/dados',
        '/assets/logo.svg',
        'https://outro-site.example/x',
      ]) {
        const result = firstValueFrom(client.get(url, { responseType: 'text' }));
        const request = http.expectOne(url);
        expect(request.request.headers.has('Authorization')).toBe(false);
        request.flush('ok');
        await result;
      }
    });

    it('leaves the authentication endpoints alone, even with an active session', async () => {
      await signIn();

      for (const path of ['/api/auth/login', '/api/auth/refresh', '/api/auth/logout']) {
        const result = firstValueFrom(client.post(path, {}));
        const request = http.expectOne(path);
        expect(request.request.headers.has('Authorization')).toBe(false);
        request.flush({ accessToken: 'qualquer' });
        await result;
      }
    });
  });

  describe('renewing an expired token', () => {
    it('renews the session and repeats the call, without the caller noticing', async () => {
      await signIn('token-1');

      const result = firstValueFrom(client.get<string>('/api/demandas'));
      http.expectOne('/api/demandas').flush({ status: 401 }, unauthorized);

      http.expectOne('/api/auth/refresh').flush({ accessToken: 'token-2' });

      const retry = http.expectOne('/api/demandas');
      expect(retry.request.headers.get('Authorization')).toBe('Bearer token-2');
      retry.flush('dados');

      expect(await result).toBe('dados');
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('makes a single renewal when several calls fail at the same time', async () => {
      await signIn('token-1');

      const results = [
        firstValueFrom(client.get<string>('/api/demandas')),
        firstValueFrom(client.get<string>('/api/times')),
        firstValueFrom(client.get<string>('/api/pessoas')),
      ];
      for (const path of ['/api/demandas', '/api/times', '/api/pessoas']) {
        http.expectOne(path).flush({ status: 401 }, unauthorized);
      }

      // Três 401, uma renovação: senão a rotação do refresh token derrubaria a sessão (DT-28).
      const renewals = http.match('/api/auth/refresh');
      expect(renewals).toHaveLength(1);
      renewals[0].flush({ accessToken: 'token-2' });

      for (const path of ['/api/demandas', '/api/times', '/api/pessoas']) {
        const retry = http.expectOne(path);
        expect(retry.request.headers.get('Authorization')).toBe('Bearer token-2');
        retry.flush(path);
      }

      expect(await Promise.all(results)).toEqual(['/api/demandas', '/api/times', '/api/pessoas']);
    });

    it('repeats a call only once: a second 401 reaches the caller without another renewal', async () => {
      await signIn('token-1');

      const result = firstValueFrom(client.get('/api/demandas'));
      http.expectOne('/api/demandas').flush({ status: 401 }, unauthorized);
      http.expectOne('/api/auth/refresh').flush({ accessToken: 'token-2' });
      http.expectOne('/api/demandas').flush({ status: 401 }, unauthorized);

      await expect(result).rejects.toMatchObject({ status: 401 });
      http.expectNone('/api/auth/refresh');
    });

    it('ends the session and goes to /login when the renewal is rejected', async () => {
      await signIn('token-1');

      const result = firstValueFrom(client.get('/api/demandas'));
      http.expectOne('/api/demandas').flush({ status: 401 }, unauthorized);
      http.expectOne('/api/auth/refresh').flush({ status: 401 }, unauthorized);

      await expect(result).rejects.toMatchObject({ status: 401 });
      expect(session.isAuthenticated()).toBe(false);
      expect(router.navigate).toHaveBeenCalledWith(['/login'], {
        queryParams: { returnUrl: '/cronograma' },
      });
    });
  });

  describe('errors that are not an expired session', () => {
    it('lets a 500 through without trying to renew', async () => {
      await signIn();

      const result = firstValueFrom(client.get('/api/demandas'));
      http.expectOne('/api/demandas').flush({}, { status: 500, statusText: 'Server Error' });

      await expect(result).rejects.toMatchObject({ status: 500 });
      http.expectNone('/api/auth/refresh');
    });

    it('does not try to renew when the login itself is rejected', async () => {
      const result = firstValueFrom(client.post('/api/auth/login', {}));
      http.expectOne('/api/auth/login').flush({ status: 401 }, unauthorized);

      await expect(result).rejects.toMatchObject({ status: 401 });
      http.expectNone('/api/auth/refresh');
      expect(router.navigate).not.toHaveBeenCalled();
    });
  });
});
