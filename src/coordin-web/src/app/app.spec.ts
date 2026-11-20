import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { App } from './app';
import { provideApiConfiguration } from './api/api-configuration';
import { routes } from './app.routes';
import { SessionService } from './core/auth/session.service';

describe('App', () => {
  const authenticated = signal(true);
  let logout: ReturnType<typeof vi.fn<() => ReturnType<SessionService['logout']>>>;

  beforeEach(async () => {
    authenticated.set(true);
    logout = vi.fn(() => {
      authenticated.set(false);
      return of(undefined);
    });

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter(routes),
        // As telas pedem dados à API: nos testes de navegação, um HttpClient de teste, sem rede de verdade.
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        // A página inicial é protegida: os testes usam uma sessão simulada.
        { provide: SessionService, useValue: { isAuthenticated: authenticated, logout } },
      ],
    }).compileComponents();
  });

  const element = (fixture: { nativeElement: unknown }) => fixture.nativeElement as HTMLElement;
  const signOutButton = (root: HTMLElement) =>
    [...root.querySelectorAll('header button')].find((button) =>
      button.textContent?.includes('Sair'),
    );

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);

    expect(fixture.componentInstance).toBeTruthy();
  });

  it('shows the product name in the header', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const header = element(fixture).querySelector('header h1');
    expect(header?.textContent).toContain('Coordin');
  });

  it('has a main content area for the routed page', async () => {
    const fixture = TestBed.createComponent(App);
    await TestBed.inject(Router).navigateByUrl('/');
    await fixture.whenStable();

    const main = element(fixture).querySelector('main');
    expect(main).not.toBeNull();
    expect(main?.textContent).toContain('Cronograma');
  });

  describe('sign out', () => {
    it('shows the "Sair" button while there is a session', async () => {
      const fixture = TestBed.createComponent(App);
      await TestBed.inject(Router).navigateByUrl('/');
      await fixture.whenStable();

      expect(signOutButton(element(fixture))).toBeDefined();
    });

    it('hides the "Sair" button on the login screen', async () => {
      authenticated.set(false);
      const fixture = TestBed.createComponent(App);
      await TestBed.inject(Router).navigateByUrl('/login');
      await fixture.whenStable();

      expect(TestBed.inject(Router).url).toBe('/login');
      expect(signOutButton(element(fixture))).toBeUndefined();
    });

    it('ends the session and opens the login screen', async () => {
      const fixture = TestBed.createComponent(App);
      const router = TestBed.inject(Router);
      await router.navigateByUrl('/');
      await fixture.whenStable();

      (signOutButton(element(fixture)) as HTMLButtonElement).click();
      await fixture.whenStable();

      expect(logout).toHaveBeenCalledTimes(1);
      expect(router.url).toBe('/login');
      expect(signOutButton(element(fixture))).toBeUndefined();
    });

    it('does not show protected content when going back after signing out', async () => {
      const fixture = TestBed.createComponent(App);
      const router = TestBed.inject(Router);
      await router.navigateByUrl('/');
      await fixture.whenStable();
      (signOutButton(element(fixture)) as HTMLButtonElement).click();
      await fixture.whenStable();

      // É o que o botão "voltar" do navegador faz: pede de novo a página protegida.
      await router.navigateByUrl('/');
      await fixture.whenStable();

      expect(router.url).toBe('/login');
      expect(element(fixture).textContent).not.toContain('Cronograma');
    });
  });
  describe('navigation', () => {
    const links = (root: HTMLElement) => [
      ...root.querySelectorAll<HTMLAnchorElement>('header nav a'),
    ];

    it('shows the links to the screens while there is a session', async () => {
      const fixture = TestBed.createComponent(App);
      await TestBed.inject(Router).navigateByUrl('/');
      await fixture.whenStable();

      const anchors = links(element(fixture));
      expect(anchors.map((a) => a.textContent?.trim())).toEqual([
        'Início',
        'Cronograma',
        'Demandas',
        'Cenários',
        'Relatório por time',
        'Precisão',
        'Times e pessoas',
        'Calendário',
        'Configurações',
      ]);
      expect(anchors.map((a) => a.getAttribute('href'))).toEqual([
        '/inicio',
        '/',
        '/demands',
        '/scenarios',
        '/reports/teams',
        '/reports/accuracy',
        '/teams',
        '/calendar',
        '/settings',
      ]);
    });

    it('marks the link of the open screen as the current page', async () => {
      const fixture = TestBed.createComponent(App);
      await TestBed.inject(Router).navigateByUrl('/calendar');
      await fixture.whenStable();

      const [, , , , , , teams, calendar] = links(element(fixture));
      expect(calendar.getAttribute('aria-current')).toBe('page');
      expect(teams.hasAttribute('aria-current')).toBe(false);
    });

    it('has no links on the login screen', async () => {
      authenticated.set(false);
      const fixture = TestBed.createComponent(App);
      await TestBed.inject(Router).navigateByUrl('/login');
      await fixture.whenStable();

      expect(element(fixture).querySelector('header nav')).toBeNull();
    });

    it('opens each screen from its link', async () => {
      const fixture = TestBed.createComponent(App);
      await TestBed.inject(Router).navigateByUrl('/');
      await fixture.whenStable();

      links(element(fixture))[6].click();
      await fixture.whenStable();
      expect(TestBed.inject(Router).url).toBe('/teams');
      expect(element(fixture).querySelector('main')?.textContent).toContain('Times (');

      links(element(fixture))[7].click();
      await fixture.whenStable();
      expect(TestBed.inject(Router).url).toBe('/calendar');
      expect(element(fixture).querySelector('main')?.textContent).toContain('Calendário');
    });
  });

  describe('validation area', () => {
    const validationLink = (root: HTMLElement) =>
      [...root.querySelectorAll('header nav a')].find(
        (link) => link.textContent?.trim() === 'Validação',
      );

    it('shows the Validação link when the status responds', async () => {
      const fixture = TestBed.createComponent(App);
      const http = TestBed.inject(HttpTestingController);
      fixture.detectChanges();
      TestBed.flushEffects();

      http
        .expectOne((req) => req.url === '/api/validation/status')
        .flush({
          demandCount: 0,
          baselineCount: 0,
          currentPlanCaptured: false,
          currentPlanCapturedAt: null,
        });
      fixture.detectChanges();

      expect(validationLink(element(fixture))).toBeDefined();
      http.verify();
    });

    it('hides the Validação link when the API answers 404', async () => {
      const fixture = TestBed.createComponent(App);
      const http = TestBed.inject(HttpTestingController);
      fixture.detectChanges();
      TestBed.flushEffects();

      http
        .expectOne((req) => req.url === '/api/validation/status')
        .flush(null, { status: 404, statusText: 'Not Found' });
      fixture.detectChanges();

      expect(validationLink(element(fixture))).toBeUndefined();
      http.verify();
    });

    it('does not check the status before there is a session', async () => {
      authenticated.set(false);
      const fixture = TestBed.createComponent(App);
      const http = TestBed.inject(HttpTestingController);
      fixture.detectChanges();
      TestBed.flushEffects();

      http.expectNone((req) => req.url === '/api/validation/status');
      http.verify();
    });
  });
});
