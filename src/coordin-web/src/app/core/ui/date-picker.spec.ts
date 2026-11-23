import { ApplicationRef, Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Clock } from '../dates/calendar-date';
import { DatePicker } from './date-picker';

@Component({
  imports: [ReactiveFormsModule, DatePicker],
  template: `
    <input id="date" type="date" appDatePicker [formControl]="control" />
    <input id="other" type="date" appDatePicker [formControl]="other" />
  `,
})
class Host {
  readonly control = new FormControl('', { nonNullable: true });
  readonly other = new FormControl('', { nonNullable: true });
}

/** O calendário abre pela faixa do ícone, na direita do campo; em jsdom o campo tem largura zero, então qualquer `clientX` maior que -44 cai nela. */
const ON_ICON = 0;
const FAR_LEFT = -200;

describe('DatePicker', () => {
  let fixture: ComponentFixture<Host>;
  let app: ApplicationRef;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        // "Hoje" fixo, para o destaque e o botão "Hoje" não dependerem do dia em que o teste roda.
        { provide: Clock, useValue: { now: () => 0, today: () => '2026-11-22' } },
      ],
    });
    fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    app = TestBed.inject(ApplicationRef);
  });

  afterEach(() => {
    fixture.destroy();
    document.querySelectorAll('app-date-popup').forEach((node) => node.remove());
  });

  const input = (id = 'date'): HTMLInputElement =>
    (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(`#${id}`)!;
  const popup = (): HTMLElement | null => document.querySelector('app-date-popup');
  const day = (iso: string): HTMLButtonElement | null =>
    popup()?.querySelector<HTMLButtonElement>(`button[data-date="${iso}"]`) ?? null;
  const text = (): string => popup()?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  const button = (label: string): HTMLButtonElement | undefined =>
    [...(popup()?.querySelectorAll('button') ?? [])].find(
      (b) => b.textContent?.trim() === label || b.getAttribute('aria-label') === label,
    );

  async function settle(): Promise<void> {
    app.tick();
    await fixture.whenStable();
    app.tick();
  }

  async function open(id = 'date'): Promise<void> {
    input(id).dispatchEvent(new MouseEvent('click', { bubbles: true, clientX: ON_ICON }));
    await settle();
  }

  const key = (target: Element, name: string, init: KeyboardEventInit = {}): void => {
    target.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, ...init }));
  };

  it('opens the calendar when the click lands on the icon, and not when it lands on the digits', async () => {
    input().dispatchEvent(new MouseEvent('click', { bubbles: true, clientX: FAR_LEFT }));
    await settle();
    expect(popup()).toBeNull();

    await open();
    expect(popup()).not.toBeNull();
    expect(popup()?.getAttribute('role')).toBe('dialog');
  });

  it('opens on the month of the date in the field, with that day marked', async () => {
    fixture.componentInstance.control.setValue('2026-12-10');
    await open();

    expect(text()).toContain('dezembro de 2026');
    expect(day('2026-12-10')?.getAttribute('aria-pressed')).toBe('true');
    expect(day('2026-12-11')?.getAttribute('aria-pressed')).toBe('false');
  });

  it('opens on today when the field is empty, with today marked', async () => {
    await open();

    expect(text()).toContain('novembro de 2026');
    expect(day('2026-11-22')?.getAttribute('aria-current')).toBe('date');
  });

  it('draws six weeks from Monday, with the days of the neighbour months', async () => {
    await open();

    // Novembro de 2026 começa num domingo: a grade começa na segunda 26/10 e termina no domingo 06/12.
    expect(day('2026-10-26')).not.toBeNull();
    expect(day('2026-12-06')).not.toBeNull();
    expect(popup()?.querySelectorAll('button[data-date]').length).toBe(42);
  });

  it('writes the chosen day in the field as yyyy-MM-dd and reaches the form, then closes', async () => {
    await open();

    day('2026-11-25')?.click();
    await settle();

    expect(input().value).toBe('2026-11-25');
    expect(fixture.componentInstance.control.value).toBe('2026-11-25');
    expect(popup()).toBeNull();
    expect(document.activeElement).toBe(input());
  });

  it('picks today with the "Hoje" button', async () => {
    fixture.componentInstance.control.setValue('2026-01-05');
    await open();

    button('Hoje')?.click();
    await settle();

    expect(fixture.componentInstance.control.value).toBe('2026-11-22');
  });

  it('clears the field with the "Limpar" button', async () => {
    fixture.componentInstance.control.setValue('2026-01-05');
    await open();

    button('Limpar')?.click();
    await settle();

    expect(fixture.componentInstance.control.value).toBe('');
    expect(input().value).toBe('');
  });

  it('changes the month with the arrows, keeping the day of the month', async () => {
    await open();

    button('Próximo mês')?.click();
    await settle();
    expect(text()).toContain('dezembro de 2026');
    expect(day('2026-12-22')?.getAttribute('tabindex')).toBe('0');

    button('Mês anterior')?.click();
    button('Mês anterior')?.click();
    await settle();
    expect(text()).toContain('outubro de 2026');
  });

  it('keeps the last day of a shorter month when the month changes (31 of January to 28 of February)', async () => {
    fixture.componentInstance.control.setValue('2027-01-31');
    await open();

    key(popup()!, 'PageDown');
    await settle();

    expect(text()).toContain('fevereiro de 2027');
    expect(day('2027-02-28')?.getAttribute('tabindex')).toBe('0');
  });

  it('moves the marked day with the arrow keys, by a day and by a week', async () => {
    await open();
    const root = popup()!;

    key(root, 'ArrowRight');
    await settle();
    expect(day('2026-11-23')?.getAttribute('tabindex')).toBe('0');

    key(root, 'ArrowDown');
    await settle();
    expect(day('2026-11-30')?.getAttribute('tabindex')).toBe('0');

    key(root, 'ArrowDown');
    await settle();
    expect(text()).toContain('dezembro de 2026'); // 07/12: o mês muda junto com o dia
    expect(day('2026-12-07')?.getAttribute('tabindex')).toBe('0');

    key(root, 'ArrowLeft');
    key(root, 'ArrowUp');
    await settle();
    expect(day('2026-11-29')?.getAttribute('tabindex')).toBe('0');
  });

  it('goes to the start and to the end of the week with Home and End, and by year with Shift + Page', async () => {
    await open(); // 22/11/2026 é um domingo
    const root = popup()!;

    key(root, 'Home');
    await settle();
    expect(day('2026-11-16')?.getAttribute('tabindex')).toBe('0');

    key(root, 'End');
    await settle();
    expect(day('2026-11-22')?.getAttribute('tabindex')).toBe('0');

    key(root, 'PageDown', { shiftKey: true });
    await settle();
    expect(text()).toContain('novembro de 2027');
  });

  it('closes with Escape and gives the focus back to the field', async () => {
    await open();

    key(popup()!, 'Escape');
    await settle();

    expect(popup()).toBeNull();
    expect(document.activeElement).toBe(input());
  });

  it('closes when the click lands outside, and stays open when it lands inside', async () => {
    await open();

    popup()!.dispatchEvent(new Event('pointerdown', { bubbles: true }));
    await settle();
    expect(popup()).not.toBeNull();

    document.body.dispatchEvent(new Event('pointerdown', { bubbles: true }));
    await settle();
    expect(popup()).toBeNull();
  });

  it('opens with Alt + ArrowDown and with F4, from the keyboard', async () => {
    key(input(), 'ArrowDown', { altKey: true });
    await settle();
    expect(popup()).not.toBeNull();

    key(popup()!, 'Escape');
    await settle();
    key(input(), 'F4');
    await settle();
    expect(popup()).not.toBeNull();
  });

  it('does not open for a disabled field', async () => {
    fixture.componentInstance.control.disable();
    fixture.detectChanges();

    await open();

    expect(popup()).toBeNull();
  });

  it('keeps one calendar at a time', async () => {
    await open('date');
    await open('other');

    expect(document.querySelectorAll('app-date-popup').length).toBe(1);
  });

  it('removes the calendar when the screen is destroyed with it open', async () => {
    await open();

    fixture.destroy();

    expect(popup()).toBeNull();
  });
});
