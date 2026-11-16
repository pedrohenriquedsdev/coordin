import { addDays, mondayOf, sundayOf, weeksBetween } from './calendar-date';

/** Estes testes rodam também com TZ=Asia/Tokyo e America/Los_Angeles: as semanas não podem mudar de dia. */
describe('calendar week helpers', () => {
  it('finds the Monday of a Wednesday, a Monday and a Sunday', () => {
    expect(mondayOf('2026-11-04')).toBe('2026-11-02'); // quarta
    expect(mondayOf('2026-11-02')).toBe('2026-11-02'); // segunda
    expect(mondayOf('2026-11-08')).toBe('2026-11-02'); // domingo fecha a semana anterior à próxima segunda
  });

  it('finds the Sunday that closes the week', () => {
    expect(sundayOf('2026-11-04')).toBe('2026-11-08');
    expect(sundayOf('2026-11-08')).toBe('2026-11-08');
  });

  it('crosses the month and the year boundaries without shifting a day', () => {
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
    expect(addDays('2027-01-01', -1)).toBe('2026-12-31');
    expect(mondayOf('2027-01-01')).toBe('2026-12-28'); // uma sexta-feira
  });

  it('counts the whole weeks the period covers', () => {
    expect(weeksBetween('2026-11-04', '2026-11-10')).toBe(2); // quarta a terça seguinte
    expect(weeksBetween('2026-11-02', '2026-11-06')).toBe(1); // a mesma semana
  });
});
