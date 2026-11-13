import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { authGuard, guestGuard, unknownPageGuard } from './auth.guard';
import { SessionService } from './session.service';

describe('guards', () => {
  const authenticated = signal(false);
  const route = {} as ActivatedRouteSnapshot;
  const stateFor = (url: string) => ({ url }) as RouterStateSnapshot;

  beforeEach(() => {
    authenticated.set(false);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: SessionService, useValue: { isAuthenticated: authenticated } },
      ],
    });
  });

  const run = (guard: typeof authGuard, url = '/') =>
    TestBed.runInInjectionContext(() => guard(route, stateFor(url)));

  const serialize = (result: unknown) => TestBed.inject(Router).serializeUrl(result as UrlTree);

  describe('authGuard', () => {
    it('lets an authenticated manager in', () => {
      authenticated.set(true);

      expect(run(authGuard, '/cronograma')).toBe(true);
    });

    it('sends a visitor to /login, remembering the requested page', () => {
      expect(serialize(run(authGuard, '/cronograma?mes=11'))).toBe(
        '/login?returnUrl=%2Fcronograma%3Fmes%3D11',
      );
    });

    it('does not clutter the login URL when the home page was requested', () => {
      expect(serialize(run(authGuard, '/'))).toBe('/login');
    });
  });

  describe('unknownPageGuard', () => {
    it('sends a visitor to /login, remembering the page they typed', () => {
      expect(serialize(run(unknownPageGuard, '/cronograma'))).toBe(
        '/login?returnUrl=%2Fcronograma',
      );
    });

    it('sends an authenticated manager to the home page', () => {
      authenticated.set(true);

      expect(serialize(run(unknownPageGuard, '/nao-existe'))).toBe('/');
    });
  });

  describe('guestGuard', () => {
    it('lets a visitor see the login page', () => {
      expect(run(guestGuard, '/login')).toBe(true);
    });

    it('sends an authenticated manager away from the login page', () => {
      authenticated.set(true);

      expect(serialize(run(guestGuard, '/login'))).toBe('/');
    });
  });
});
