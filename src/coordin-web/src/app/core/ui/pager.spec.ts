import { Component, computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Pager } from './pager';
import { ShowMore } from './show-more';

const numbers = (count: number): number[] => Array.from({ length: count }, (_, i) => i + 1);

describe('Pager', () => {
  it('shows only the first page of a long list', () => {
    const pager = new Pager(10);

    expect(pager.slice(numbers(25))).toEqual(numbers(10));
  });

  it('shows the whole list when it is shorter than a page', () => {
    const pager = new Pager(10);

    expect(pager.slice(numbers(3))).toEqual([1, 2, 3]);
    expect(pager.slice([])).toEqual([]);
  });

  it('adds one page each time more is requested, up to the end of the list', () => {
    const pager = new Pager(10);

    pager.more();
    expect(pager.slice(numbers(25))).toHaveLength(20);
    pager.more();
    expect(pager.slice(numbers(25))).toHaveLength(25);
    pager.more();
    expect(pager.slice(numbers(25))).toHaveLength(25);
  });

  it('goes back to the first page on reset', () => {
    const pager = new Pager(10);
    pager.more();
    pager.more();

    pager.reset();

    expect(pager.slice(numbers(25))).toHaveLength(10);
  });

  it('does not change the list it is given', () => {
    const pager = new Pager(2);
    const list = numbers(5);

    pager.slice(list);

    expect(list).toEqual(numbers(5));
  });

  it('updates a computed list when more is requested', () => {
    TestBed.runInInjectionContext(() => {
      const pager = new Pager(2);
      const list = signal(numbers(5));
      const shown = computed(() => pager.slice(list()));

      expect(shown()).toEqual([1, 2]);
      pager.more();
      expect(shown()).toEqual([1, 2, 3, 4]);
    });
  });
});

@Component({
  imports: [ShowMore],
  template: `<app-show-more
    [shown]="shown()"
    [total]="total()"
    (more)="clicks.update((n) => n + 1)"
  />`,
})
class Host {
  readonly shown = signal(10);
  readonly total = signal(31);
  readonly clicks = signal(0);
}

describe('ShowMore', () => {
  const render = () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    return fixture;
  };
  const text = (fixture: ReturnType<typeof render>) =>
    (fixture.nativeElement as HTMLElement).textContent?.replace(/\s+/g, ' ').trim() ?? '';

  it('says how many are shown and asks for more when the list is not over', () => {
    const fixture = render();

    expect(text(fixture)).toContain('Mostrando 10 de 31');
    const button = (fixture.nativeElement as HTMLElement).querySelector('button');
    button?.click();
    expect(fixture.componentInstance.clicks()).toBe(1);
  });

  it('disappears when the whole list is already shown', () => {
    const fixture = render();
    fixture.componentInstance.shown.set(31);
    fixture.detectChanges();

    expect(text(fixture)).toBe('');
    expect((fixture.nativeElement as HTMLElement).querySelector('button')).toBeNull();
  });
});
