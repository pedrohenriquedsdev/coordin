import {
  buildFullOrder,
  CHART_LIMIT,
  contributionLabel,
  moveInList,
  scoreLabel,
  topContributions,
} from './scenario-view';
import { ScenarioItem } from './scenarios';

function item(demandId: string, title: string, scoreContributionDelta: number): ScenarioItem {
  return {
    demandId,
    title,
    priorityRank: 1,
    newExecutionRank: 1,
    currentForecastEndDate: '2027-03-01',
    forecastStartDate: '2027-02-01',
    forecastEndDate: '2027-03-01',
    deltaDays: 0,
    weight: 1,
    scoreContributionDelta,
  };
}

describe('buildFullOrder', () => {
  it('puts the edited open demands in the open slots when nothing is completed', () => {
    const current = ['a', 'b', 'c'];

    expect(buildFullOrder(current, new Set(current), ['c', 'a', 'b'])).toEqual(['c', 'a', 'b']);
  });

  it('keeps a completed demand at the start in its own position', () => {
    const current = ['done', 'a', 'b', 'c'];
    const open = new Set(['a', 'b', 'c']);

    expect(buildFullOrder(current, open, ['b', 'c', 'a'])).toEqual(['done', 'b', 'c', 'a']);
  });

  it('keeps completed demands in the middle and at the end in their own positions', () => {
    const current = ['a', 'done1', 'b', 'c', 'done2'];
    const open = new Set(['a', 'b', 'c']);

    expect(buildFullOrder(current, open, ['c', 'b', 'a'])).toEqual([
      'c',
      'done1',
      'b',
      'a',
      'done2',
    ]);
  });

  it('returns the current order when the edited list is the current one', () => {
    const current = ['done', 'a', 'b', 'done2', 'c'];
    const open = new Set(['a', 'b', 'c']);

    expect(buildFullOrder(current, open, ['a', 'b', 'c'])).toEqual(current);
  });

  it('is a permutation of the current order, with each demand exactly once', () => {
    const current = ['a', 'done', 'b', 'c', 'd'];
    const open = new Set(['a', 'b', 'c', 'd']);
    const result = buildFullOrder(current, open, ['d', 'a', 'c', 'b']);

    expect([...result].sort()).toEqual([...current].sort());
    expect(result.indexOf('done')).toBe(1);
  });

  it('does not change the lists it receives', () => {
    const current = ['a', 'done', 'b'];
    const edited = ['b', 'a'];
    buildFullOrder(current, new Set(['a', 'b']), edited);

    expect(current).toEqual(['a', 'done', 'b']);
    expect(edited).toEqual(['b', 'a']);
  });
});

describe('topContributions', () => {
  it('orders by the biggest absolute contribution, improvements and worsenings together', () => {
    const result = topContributions([item('a', 'A', -10), item('b', 'B', 50), item('c', 'C', -30)]);

    expect(result.map((entry) => entry.demandId)).toEqual(['b', 'c', 'a']);
  });

  it('drops demands whose contribution is zero', () => {
    const result = topContributions([item('a', 'A', 0), item('b', 'B', -5)]);

    expect(result.map((entry) => entry.demandId)).toEqual(['b']);
  });

  it('keeps only the limit, 10 by default, from the biggest down', () => {
    const items = Array.from({ length: 15 }, (_, index) =>
      item(`d${index}`, `D${index}`, -(index + 1)),
    );
    const result = topContributions(items);

    expect(CHART_LIMIT).toBe(10);
    expect(result).toHaveLength(10);
    expect(result[0].scoreContributionDelta).toBe(-15);
    expect(result[9].scoreContributionDelta).toBe(-6);
    expect(topContributions(items, 3)).toHaveLength(3);
  });

  it('breaks a tie by title and then by id, so the list never varies', () => {
    const items = [item('z', 'Beta', 20), item('y', 'Alfa', -20), item('x', 'Alfa', 20)];
    const result = topContributions(items);

    expect(result.map((entry) => entry.demandId)).toEqual(['x', 'y', 'z']);
    expect(topContributions([...items].reverse()).map((entry) => entry.demandId)).toEqual([
      'x',
      'y',
      'z',
    ]);
  });

  it('returns an empty list for no demands and does not change the input', () => {
    const items = [item('a', 'A', -1), item('b', 'B', -9)];
    topContributions(items);

    expect(topContributions([])).toEqual([]);
    expect(items.map((entry) => entry.demandId)).toEqual(['a', 'b']);
  });
});

describe('contributionLabel', () => {
  it('says "melhora" for a negative contribution, "piora" for a positive one and "neutra" for zero', () => {
    expect(contributionLabel(-36)).toBe('melhora');
    expect(contributionLabel(12)).toBe('piora');
    expect(contributionLabel(0)).toBe('neutra');
  });
});

describe('scoreLabel', () => {
  it('formats the criterion in pt-BR without decimals', () => {
    expect(scoreLabel(15840)).toBe('15.840');
    expect(scoreLabel(-630)).toBe('-630');
    expect(scoreLabel(0)).toBe('0');
  });
});

describe('moveInList', () => {
  it('moves a demand up by swapping with the one before it', () => {
    expect(moveInList(['a', 'b', 'c'], 'b', 'up')).toEqual(['b', 'a', 'c']);
  });

  it('moves a demand down by swapping with the one after it', () => {
    expect(moveInList(['a', 'b', 'c'], 'b', 'down')).toEqual(['a', 'c', 'b']);
  });

  it('does nothing at the edges or for an unknown id', () => {
    expect(moveInList(['a', 'b', 'c'], 'a', 'up')).toEqual(['a', 'b', 'c']);
    expect(moveInList(['a', 'b', 'c'], 'c', 'down')).toEqual(['a', 'b', 'c']);
    expect(moveInList(['a', 'b', 'c'], 'x', 'up')).toEqual(['a', 'b', 'c']);
  });

  it('returns a new list and leaves the original alone', () => {
    const original = ['a', 'b'];
    const result = moveInList(original, 'b', 'up');

    expect(result).not.toBe(original);
    expect(original).toEqual(['a', 'b']);
  });
});
