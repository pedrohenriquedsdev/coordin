import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideApiConfiguration } from '../api/api-configuration';
import { provideLocalePtBr } from '../core/locale';
import { daysWithMargin, parseMargin, SettingsPage } from './settings-page';

/** Estes testes rodam também com TZ=Asia/Tokyo e America/Los_Angeles (RNF-07). */
describe('SettingsPage', () => {
  let fixture: ComponentFixture<SettingsPage>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideLocalePtBr(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SettingsPage);
  });

  afterEach(() => http.verify());

  const element = (): HTMLElement => fixture.nativeElement as HTMLElement;

  const request = (method: string, url: string): TestRequest =>
    http.expectOne((req) => req.method === method && req.url === url);

  async function open(margin: number | string = 20): Promise<void> {
    fixture.detectChanges();
    request('GET', '/api/settings').flush({
      executionOrderMode: 'FollowPriority',
      safetyMarginPercent: margin,
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  const input = (): HTMLInputElement =>
    element().querySelector('#safety-margin') as HTMLInputElement;

  const type = (text: string): void => {
    input().value = text;
    input().dispatchEvent(new Event('input'));
  };

  const submit = (): void => {
    element().querySelector('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  };

  it('shows the margin in force', async () => {
    await open(20);

    expect(input().value).toBe('20');
  });

  it('saves the margin the manager typed and confirms it', async () => {
    await open(20);
    type('30');

    submit();
    request('PUT', '/api/settings').flush({
      executionOrderMode: 'FollowPriority',
      safetyMarginPercent: 30,
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(element().textContent).toContain('Margem salva.');
    expect(input().value).toBe('30');
  });

  it('accepts zero, and sends it as zero', async () => {
    await open(20);
    type('0');

    submit();

    expect(request('PUT', '/api/settings').request.body).toEqual({ safetyMarginPercent: 0 });
  });

  it.each(['-1', '1000,01', '10,555', ''])(
    'refuses "%s" with the message, without calling the API',
    async (text) => {
      await open(20);
      type(text);

      submit();
      fixture.detectChanges();

      expect(element().querySelector('[role="alert"]')?.textContent).toContain(
        'A margem deve estar entre 0% e 1000%, com no máximo 2 casas decimais.',
      );
    },
  );

  it('shows the message of an API refusal and keeps what was typed', async () => {
    await open(20);
    type('30');

    submit();
    request('PUT', '/api/settings').flush(
      { detail: 'A margem deve estar entre 0% e 1000%.', errors: {} },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(element().querySelector('[role="alert"]')?.textContent).toContain(
      'A margem deve estar entre 0% e 1000%.',
    );
    expect(input().value).toBe('30');
  });

  it('offers to try again when the settings cannot be loaded', async () => {
    fixture.detectChanges();
    request('GET', '/api/settings').flush(null, { status: 500, statusText: 'Erro' });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(element().textContent).toContain('Não foi possível carregar as configurações.');
    const retry = [...element().querySelectorAll('button')].find((b) =>
      b.textContent?.includes('Tentar de novo'),
    );
    retry?.click();
    request('GET', '/api/settings').flush({
      executionOrderMode: 'FollowPriority',
      safetyMarginPercent: 20,
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(input().value).toBe('20');
  });

  describe('example, shortcuts and appearance', () => {
    const exampleText = (): string =>
      (element().querySelector('[aria-live="polite"]')?.textContent ?? '').replace(/\s+/g, ' ');

    it('shows how many days a 20-day demand has with the margin in force', async () => {
      await open(20);

      expect(exampleText()).toContain('20 dias sem margem passa a ter 24 dias com margem');
    });

    it('follows the typing, rounds up, and never goes below the base', async () => {
      await open(20);

      type('5');
      fixture.detectChanges();
      expect(exampleText()).toContain('21 dias com margem'); // 20 × 1,05 = 21

      type('0');
      fixture.detectChanges();
      expect(exampleText()).toContain('20 dias com margem');

      type('12,5');
      fixture.detectChanges();
      expect(exampleText()).toContain('23 dias com margem'); // 22,5 sobe para 23
    });

    it('asks for a valid margin instead of showing an example for a bad value', async () => {
      await open(20);

      type('abc');
      fixture.detectChanges();

      expect(exampleText()).toContain('Digite uma margem válida');
    });

    it('fills the field from a shortcut and from the slider', async () => {
      await open(20);

      [...element().querySelectorAll('button')]
        .find((b) => b.textContent?.trim() === '30%')
        ?.click();
      fixture.detectChanges();
      expect(input().value).toBe('30');

      const slider = element().querySelector<HTMLInputElement>('#margin-slider')!;
      slider.value = '45';
      slider.dispatchEvent(new Event('input'));
      fixture.detectChanges();
      expect(input().value).toBe('45');
    });

    it('offers the light and dark themes, with the chosen one marked', async () => {
      localStorage.setItem('coordin-theme', 'light');
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [
          provideHttpClient(),
          provideHttpClientTesting(),
          provideApiConfiguration(''),
          provideLocalePtBr(),
        ],
      });
      http = TestBed.inject(HttpTestingController);
      fixture = TestBed.createComponent(SettingsPage);
      await open(20);

      const buttons = [...element().querySelectorAll('[aria-label="Tema"] button')];
      expect(buttons[0].getAttribute('aria-pressed')).toBe('true');

      (buttons[1] as HTMLButtonElement).click();
      fixture.detectChanges();
      expect(buttons[1].getAttribute('aria-pressed')).toBe('true');
      localStorage.removeItem('coordin-theme');
    });
  });
});

describe('parseMargin', () => {
  it.each([
    ['0', 0],
    ['20', 20],
    ['12,5', 12.5],
    ['100.25', 100.25],
    ['1000', 1000],
    ['-1', null],
    ['1000,01', null],
    ['10,555', null],
    ['', null],
    ['abc', null],
  ])('reads "%s"', (text, expected) => {
    expect(parseMargin(text)).toBe(expected);
  });
});

describe('daysWithMargin', () => {
  it('rounds up and never shortens the duration', () => {
    expect(daysWithMargin(20, 0)).toBe(20);
    expect(daysWithMargin(20, 20)).toBe(24);
    expect(daysWithMargin(20, 12.5)).toBe(23);
    expect(daysWithMargin(7, 10)).toBe(8); // 7,7 sobe para 8
    expect(daysWithMargin(20, 0.01)).toBe(21); // qualquer margem positiva amplia
  });
});
