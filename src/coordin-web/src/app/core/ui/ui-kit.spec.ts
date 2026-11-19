import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { GlowButton } from './glow-button';
import { GlowField } from './glow-field';
import { HoldButton } from './hold-button';
import { Loader } from './loader';

@Component({
  imports: [HoldButton, GlowButton, GlowField, Loader],
  template: `
    <app-hold-button (confirmed)="count = count + 1" [holdMs]="1000" />
    <app-glow-button type="submit" [disabled]="off()">Entrar</app-glow-button>
    <app-glow-field><input id="x" /></app-glow-field>
    <app-loader label="Buscando…" />
  `,
})
class Host {
  count = 0;
  off = signal(false);
}

describe('UI kit', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  function setup() {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    return { fixture, root, hold: root.querySelector('.hold-btn') as HTMLButtonElement };
  }

  const down = (el: HTMLElement) =>
    el.dispatchEvent(new PointerEvent('pointerdown', { button: 0 }));

  describe('HoldButton', () => {
    it('confirms only after being held for the whole time', () => {
      const { fixture, hold } = setup();

      down(hold);
      vi.advanceTimersByTime(999);
      expect(fixture.componentInstance.count).toBe(0);

      vi.advanceTimersByTime(2);
      expect(fixture.componentInstance.count).toBe(1);
      fixture.detectChanges();
      expect(hold.dataset['state']).toBe('done');
    });

    it('cancels when released early', () => {
      const { fixture, hold } = setup();

      down(hold);
      vi.advanceTimersByTime(500);
      hold.dispatchEvent(new PointerEvent('pointerup'));
      vi.advanceTimersByTime(2000);

      expect(fixture.componentInstance.count).toBe(0);
      fixture.detectChanges();
      expect(hold.dataset['state']).toBe('idle');
    });

    it('can be held with the keyboard', () => {
      const { fixture, hold } = setup();

      hold.dispatchEvent(new KeyboardEvent('keydown', { key: ' ' }));
      vi.advanceTimersByTime(1001);

      expect(fixture.componentInstance.count).toBe(1);
    });

    it('goes back to idle after confirming', () => {
      const { fixture, hold } = setup();

      down(hold);
      vi.advanceTimersByTime(1001 + 1800);
      fixture.detectChanges();

      expect(hold.dataset['state']).toBe('idle');
    });
  });

  it('GlowButton is a real, disableable button', () => {
    const { fixture, root } = setup();
    const button = root.querySelector('.gb-button') as HTMLButtonElement;

    expect(button.type).toBe('submit');
    expect(button.textContent).toContain('Entrar');

    fixture.componentInstance.off.set(true);
    fixture.detectChanges();
    expect(button.disabled).toBe(true);
  });

  it('GlowField keeps the projected input and hides its decoration from readers', () => {
    const { root } = setup();

    expect(root.querySelector('.gf-body #x')).not.toBeNull();
    expect(root.querySelector('.gf-border')?.getAttribute('aria-hidden')).toBe('true');
  });

  it('Loader announces its text as a status', () => {
    const { root } = setup();
    const loader = root.querySelector('app-loader');

    expect(loader?.getAttribute('role')).toBe('status');
    expect(loader?.textContent).toContain('Buscando…');
  });
});
