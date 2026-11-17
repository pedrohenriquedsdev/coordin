import { parsePosition } from './demand-position';

describe('parsePosition', () => {
  it('treats an empty field as the end of the queue, with no position', () => {
    expect(parsePosition('', 5)).toEqual({ value: null, valid: true });
    expect(parsePosition('   ', 5)).toEqual({ value: null, valid: true });
  });

  it('accepts a whole number from 1 up to the end of the queue', () => {
    expect(parsePosition('1', 5)).toEqual({ value: 1, valid: true });
    expect(parsePosition(' 5 ', 5)).toEqual({ value: 5, valid: true });
  });

  it('rejects zero, a number above the limit, decimals, negatives and letters', () => {
    expect(parsePosition('0', 5).valid).toBe(false);
    expect(parsePosition('6', 5).valid).toBe(false);
    expect(parsePosition('1,5', 5).valid).toBe(false);
    expect(parsePosition('-1', 5).valid).toBe(false);
    expect(parsePosition('abc', 5).valid).toBe(false);
  });
});
