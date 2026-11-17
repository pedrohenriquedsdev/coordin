import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideApiConfiguration } from '../api/api-configuration';
import { ResetToPriority } from './reset-to-priority';

const WARNING = 'A ordem de execução difere da ordem de prioridade.';
const ASK = 'Voltar à ordem de prioridade';

describe('ResetToPriority', () => {
  let fixture: ComponentFixture<ResetToPriority>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideApiConfiguration('')],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ResetToPriority);
  });

  afterEach(() => http.verify());

  const element = (): HTMLElement => fixture.nativeElement as HTMLElement;

  const button = (label: string): HTMLButtonElement | undefined =>
    [...element().querySelectorAll('button')].find((b) => b.textContent?.trim() === label);

  const render = (mode: string): void => {
    fixture.componentRef.setInput('mode', mode);
    fixture.detectChanges();
  };

  const reset = () => http.expectOne({ method: 'POST', url: '/api/scenarios/reset-to-priority' });

  it('shows nothing when the order follows the priority', () => {
    render('FollowPriority');

    expect(element().textContent?.trim()).toBe('');
    expect(button(ASK)).toBeUndefined();
  });

  it('shows the warning and the button in the custom mode, without any request', () => {
    render('Custom');

    expect(element().textContent).toContain(WARNING);
    expect(button(ASK)).toBeDefined();
  });

  it('sends nothing and goes back to the button when the confirmation is cancelled', () => {
    render('Custom');
    button(ASK)?.click();
    fixture.detectChanges();
    expect(button('Confirmar')).toBeDefined();

    button('Cancelar')?.click();
    fixture.detectChanges();

    expect(button(ASK)).toBeDefined();
    http.expectNone('/api/scenarios/reset-to-priority');
  });

  it('resets only after the confirmation and then tells the screen it is done', () => {
    render('Custom');
    let done = 0;
    fixture.componentInstance.done.subscribe(() => done++);

    button(ASK)?.click();
    fixture.detectChanges();
    http.expectNone('/api/scenarios/reset-to-priority');

    button('Confirmar')?.click();
    reset().flush('', { status: 204, statusText: 'No Content' });

    expect(done).toBe(1);
  });

  it('keeps the warning and the button, and shows a message, when the reset fails', () => {
    render('Custom');
    button(ASK)?.click();
    fixture.detectChanges();
    button('Confirmar')?.click();

    reset().flush('erro', { status: 500, statusText: 'Erro' });
    fixture.detectChanges();

    expect(element().querySelector('[role="alert"]')).not.toBeNull();
    expect(element().textContent).toContain(WARNING);
    expect(button(ASK)).toBeDefined();
  });
});
