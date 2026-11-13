import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideApiConfiguration } from './api/api-configuration';
import { routes } from './app.routes';
import { SessionService } from './core/auth/session.service';

/** As rotas de verdade, com as guardas de verdade e só a sessão simulada (RNF-04). */
describe('routes', () => {
  const authenticated = signal(false);
  const homeText = 'Planejamento de entregas';

  beforeEach(() => {
    authenticated.set(false);
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        // As telas pedem dados à API: nos testes de navegação, um HttpClient de teste, sem rede de verdade.
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        { provide: SessionService, useValue: { isAuthenticated: authenticated } },
      ],
    });
  });

  async function open(url: string) {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    return { harness, url: TestBed.inject(Router).url };
  }

  describe('without a session', () => {
    it('sends the home page to /login and shows none of its content', async () => {
      const { harness, url } = await open('/');

      expect(url).toBe('/login');
      expect(harness.routeNativeElement?.textContent).not.toContain(homeText);
    });

    it('remembers the page that was requested', async () => {
      const { url } = await open('/cronograma');

      expect(url).toBe('/login?returnUrl=%2Fcronograma');
    });

    it.each(['/teams', '/calendar'])('sends %s to /login, remembering it', async (path) => {
      const { url } = await open(path);

      expect(url).toBe(`/login?returnUrl=${encodeURIComponent(path)}`);
    });

    it('shows the login page', async () => {
      const { harness, url } = await open('/login');

      expect(url).toBe('/login');
      expect(harness.routeNativeElement?.textContent).toContain('Entrar');
    });
  });

  describe('with a session', () => {
    beforeEach(() => authenticated.set(true));

    it('shows the home page', async () => {
      const { harness, url } = await open('/');

      expect(url).toBe('/');
      expect(harness.routeNativeElement?.textContent).toContain(homeText);
    });

    it.each([
      ['/teams', 'Times ('],
      ['/calendar', 'Calendário'],
    ])('opens %s', async (path, title) => {
      const { harness, url } = await open(path);

      expect(url).toBe(path);
      expect(harness.routeNativeElement?.textContent).toContain(title);
    });

    it('sends /login to the home page', async () => {
      const { url } = await open('/login');

      expect(url).toBe('/');
    });

    it('sends a page that does not exist to the home page', async () => {
      const { url } = await open('/nao-existe');

      expect(url).toBe('/');
    });
  });
});
