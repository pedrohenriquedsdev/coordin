import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CalendarDatePipe } from './calendar-date.pipe';
import {
  Clock,
  formatCalendarDate,
  formatPeriod,
  isEndBeforeStart,
  saoPauloDate,
} from './calendar-date';

/** Estes testes rodam com TZ=UTC, America/Sao_Paulo e Asia/Tokyo: o resultado não pode depender do fuso. */
describe('calendar dates', () => {
  describe('formatCalendarDate', () => {
    it.each([
      ['2026-12-31', '31/12/2026'],
      ['2027-01-01', '01/01/2027'],
      ['2026-11-28', '28/11/2026'],
      ['2028-02-29', '29/02/2028'],
    ])('turns %s into %s', (value, expected) => {
      expect(formatCalendarDate(value)).toBe(expected);
    });

    it.each(['', 'texto', '31/12/2026', '2026-12-31T00:00:00Z', '2026-1-1'])(
      'returns %j untouched because it is not a calendar date',
      (value) => {
        expect(formatCalendarDate(value)).toBe(value);
      },
    );
  });

  describe('formatPeriod', () => {
    it.each([
      ['2027-01-04', '2027-01-15', '04–15/01'],
      ['2026-12-21', '2027-01-04', '21/12–04/01'],
      ['2027-02-08', '2027-02-08', '08/02'],
      ['2027-01-30', '2027-02-02', '30/01–02/02'],
    ])('shows %s to %s as %s', (start, end, expected) => {
      expect(formatPeriod(start, end)).toBe(expected);
    });
  });

  describe('isEndBeforeStart', () => {
    it('is true only when the end is earlier than the start', () => {
      expect(isEndBeforeStart('2027-01-15', '2027-01-04')).toBe(true);
      expect(isEndBeforeStart('2027-01-04', '2027-01-04')).toBe(false);
      expect(isEndBeforeStart('2026-12-31', '2027-01-01')).toBe(false);
    });
  });

  describe('today in São Paulo', () => {
    it.each([
      // 02:00 UTC ainda é 23:00 do dia anterior em São Paulo (UTC-3).
      ['2026-11-28T02:59:59Z', '2026-11-27'],
      ['2026-11-28T03:00:00Z', '2026-11-28'],
      ['2026-12-31T12:00:00Z', '2026-12-31'],
      ['2027-01-01T02:00:00Z', '2026-12-31'],
    ])('turns the instant %s into the date %s', (instant, expected) => {
      expect(saoPauloDate(Date.parse(instant))).toBe(expected);
    });

    it('is what the Clock service answers, and it can be controlled in tests', () => {
      class FixedClock extends Clock {
        override now(): number {
          return Date.parse('2027-01-01T02:00:00Z');
        }
      }
      TestBed.configureTestingModule({ providers: [{ provide: Clock, useClass: FixedClock }] });

      expect(TestBed.inject(Clock).today()).toBe('2026-12-31');
    });
  });

  describe('calendarDate pipe', () => {
    @Component({
      imports: [CalendarDatePipe],
      template: `<span id="a">{{ '2026-12-31' | calendarDate }}</span
        ><span id="b">{{ empty | calendarDate }}</span>`,
    })
    class Host {
      protected readonly empty = null;
    }

    it('formats a date and renders nothing for an empty value', async () => {
      const fixture = TestBed.createComponent(Host);
      await fixture.whenStable();

      const root = fixture.nativeElement as HTMLElement;
      expect(root.querySelector('#a')?.textContent).toBe('31/12/2026');
      expect(root.querySelector('#b')?.textContent).toBe('');
    });
  });
});

describe('month helpers', () => {
  it('builds a 42-day grid that starts on a Monday and covers the month', async () => {
    const { monthGrid } = await import('./calendar-date');
    const grid = monthGrid('2026-12-15');

    expect(grid).toHaveLength(42);
    expect(grid[0]).toBe('2026-11-30'); // segunda-feira
    expect(grid).toContain('2026-12-01');
    expect(grid).toContain('2026-12-31');
  });

  it('moves between months, including the year change', async () => {
    const { addMonths, startOfMonth, daysBetween } = await import('./calendar-date');

    expect(startOfMonth('2026-12-15')).toBe('2026-12-01');
    expect(addMonths('2026-12-15', 1)).toBe('2027-01-01');
    expect(addMonths('2026-01-31', -1)).toBe('2025-12-01');
    expect(daysBetween('2026-12-01', '2026-12-11')).toBe(10);
  });

  it('writes the month and the long date in Portuguese', async () => {
    const { formatMonth, formatLongCalendarDate } = await import('./calendar-date');

    expect(formatMonth('2026-12-01')).toBe('dezembro de 2026');
    expect(formatLongCalendarDate('2026-03-05')).toBe('5 de março de 2026');
  });
});
