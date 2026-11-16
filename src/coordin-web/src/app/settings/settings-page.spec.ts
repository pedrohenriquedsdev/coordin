import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideApiConfiguration } from '../api/api-configuration';
import { provideLocalePtBr } from '../core/locale';
import { parseMargin, SettingsPage } from './settings-page';

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
