import { HttpErrorResponse } from '@angular/common/http';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, Subject, throwError } from 'rxjs';
import { SessionService } from '../core/auth/session.service';
import { Login } from './login';

@Component({ template: 'Destino' })
class Destination {}

describe('Login', () => {
  let login: ReturnType<typeof vi.fn<(email: string, password: string) => Observable<void>>>;
  let root: HTMLElement;
  let harness: RouterTestingHarness;

  const invalidCredentials =
    'E-mail ou senha inválidos. Após 5 tentativas erradas, o acesso é bloqueado por 15 minutos.';

  beforeEach(() => {
    login = vi.fn<(email: string, password: string) => Observable<void>>(() => of(undefined));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'login', component: Login },
          { path: 'cronograma', component: Destination },
          { path: '', component: Destination },
        ]),
        { provide: SessionService, useValue: { login, isAuthenticated: signal(false) } },
      ],
    });
  });

  async function open(url = '/login'): Promise<void> {
    harness = await RouterTestingHarness.create(url);
    root = harness.routeNativeElement as HTMLElement;
  }

  async function settle(): Promise<void> {
    harness.fixture.detectChanges();
    await harness.fixture.whenStable();
  }

  function type(selector: string, value: string): void {
    const input = root.querySelector<HTMLInputElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  async function submit(
    email = 'gestor@coordin.test',
    password = 'uma-senha-12345',
  ): Promise<void> {
    type('#email', email);
    type('#password', password);
    root.querySelector<HTMLButtonElement>('button[type=submit]')!.click();
    await settle();
  }

  const url = () => TestBed.inject(Router).url;
  const alert = () => root.querySelector('[role=alert]')?.textContent?.trim();

  it('shows the fields and the button in pt-BR', async () => {
    await open();

    expect(root.querySelector('h2')?.textContent).toContain('Entrar');
    expect(root.querySelector('label[for=email]')?.textContent).toContain('E-mail');
    expect(root.querySelector('label[for=password]')?.textContent).toContain('Senha');
    expect(root.querySelector('button[type=submit]')?.textContent).toContain('Entrar');
  });

  it('hides what is typed in the password field', async () => {
    await open();

    expect(root.querySelector('#password')?.getAttribute('type')).toBe('password');
  });

  describe('signing in', () => {
    it('sends the credentials and opens the home page', async () => {
      await open();

      await submit('  gestor@coordin.test  ', 'uma-senha-12345');

      expect(login).toHaveBeenCalledExactlyOnceWith('gestor@coordin.test', 'uma-senha-12345');
      expect(url()).toBe('/');
    });

    it('opens the page the manager was trying to reach', async () => {
      await open('/login?returnUrl=%2Fcronograma');

      await submit();

      expect(url()).toBe('/cronograma');
    });

    it('ignores a return address that points to another site', async () => {
      await open('/login?returnUrl=%2F%2Fsite-falso.com');

      await submit();

      expect(url()).toBe('/');
    });

    it('disables the button while the request is in flight', async () => {
      const pending = new Subject<void>();
      login.mockReturnValue(pending);
      await open();

      await submit();
      const button = root.querySelector<HTMLButtonElement>('button[type=submit]')!;
      expect(button.disabled).toBe(true);

      // Um segundo envio durante a espera não gasta outra tentativa.
      button.click();
      await settle();
      expect(login).toHaveBeenCalledTimes(1);

      pending.next();
      pending.complete();
      await settle();
      expect(url()).toBe('/');
    });
  });

  describe('required fields', () => {
    it('points out the empty fields and does not call the API', async () => {
      await open();

      root.querySelector<HTMLButtonElement>('button[type=submit]')!.click();
      await settle();

      expect(root.textContent).toContain('Informe o e-mail.');
      expect(root.textContent).toContain('Informe a senha.');
      expect(login).not.toHaveBeenCalled();
      expect(url()).toBe('/login');
    });

    it('points out only the field that is missing', async () => {
      await open();

      await submit('gestor@coordin.test', '');

      expect(root.textContent).toContain('Informe a senha.');
      expect(root.textContent).not.toContain('Informe o e-mail.');
      expect(login).not.toHaveBeenCalled();
    });
  });

  describe('failures', () => {
    it('shows the message of the prototype when the credentials are rejected', async () => {
      login.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 401 })));
      await open();

      await submit();

      expect(url()).toBe('/login');
      expect(alert()).toBe(invalidCredentials);
    });

    it('clears the wrong password but keeps the e-mail, and lets the manager try again', async () => {
      login.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 401 })));
      await open();

      await submit('gestor@coordin.test', 'senha-errada-123456');

      expect(root.querySelector<HTMLInputElement>('#password')?.value).toBe('');
      expect(root.querySelector<HTMLInputElement>('#email')?.value).toBe('gestor@coordin.test');
      expect(root.querySelector<HTMLButtonElement>('button[type=submit]')?.disabled).toBe(false);
    });

    it('asks the manager to wait when there are too many attempts', async () => {
      login.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 429 })));
      await open();

      await submit();

      expect(alert()).toContain('tentativas demais');
      expect(alert()).toContain('Aguarde');
    });

    it('shows a generic message when the server cannot be reached', async () => {
      login.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 0 })));
      await open();

      await submit();

      expect(alert()).toContain('Não foi possível entrar agora');
    });

    it('removes the previous message on a new attempt', async () => {
      login.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 401 })));
      await open();
      await submit();
      expect(alert()).toBe(invalidCredentials);

      await submit();

      expect(alert()).toBeUndefined();
      expect(url()).toBe('/');
    });
  });
});
