import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { App } from './app';
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
    expect(main?.textContent).toContain('Planejamento de entregas');
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
      expect(element(fixture).textContent).not.toContain('Planejamento de entregas');
    });
  });
});
