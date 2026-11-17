import { ScenarioResponse } from '../api/models/scenario-response';
import { mapScenario } from './scenarios';

const response: ScenarioResponse = {
  executionOrder: ['b', 'a', 'c'],
  isComparable: true,
  items: [
    {
      demandId: 'b',
      title: 'Bravo',
      priorityRank: '2',
      newExecutionRank: 1,
      currentForecastEndDate: '2027-03-10',
      forecastStartDate: '2027-02-01',
      forecastEndDate: '2027-03-05',
      deltaDays: '-5',
      weight: '9',
      scoreContributionDelta: '-120',
    },
    {
      demandId: 'a',
      title: 'Alfa',
      priorityRank: 1,
      newExecutionRank: '2',
      currentForecastEndDate: '2027-03-01',
      forecastStartDate: '2027-02-10',
      forecastEndDate: '2027-03-04',
      deltaDays: 3,
      weight: 10,
      scoreContributionDelta: 40,
    },
  ],
  score: { current: '15840', scenario: 15210, delta: '-630' },
  sectorDeltaDays: '-6',
  sectorEndDate: '2027-02-10',
};

describe('mapScenario', () => {
  it('turns every number from the generated client into a real number', () => {
    const scenario = mapScenario(response);

    expect(scenario.score).toEqual({ current: 15840, scenario: 15210, delta: -630 });
    expect(scenario.sectorDeltaDays).toBe(-6);
    expect(scenario.items[0]).toMatchObject({
      priorityRank: 2,
      newExecutionRank: 1,
      deltaDays: -5,
      weight: 9,
      scoreContributionDelta: -120,
    });
    expect(scenario.items[1].newExecutionRank).toBe(2);
    expect(typeof scenario.items[0].priorityRank).toBe('number');
  });

  it('keeps the dates as calendar text, the order and the comparable flag', () => {
    const scenario = mapScenario(response);

    expect(scenario.executionOrder).toEqual(['b', 'a', 'c']);
    expect(scenario.isComparable).toBe(true);
    expect(scenario.sectorEndDate).toBe('2027-02-10');
    expect(scenario.items[0].forecastEndDate).toBe('2027-03-05');
    expect(scenario.items.map((item) => item.demandId)).toEqual(['b', 'a']);
  });

  it('keeps a missing sector end date as null instead of inventing one', () => {
    const scenario = mapScenario({ ...response, sectorEndDate: null, isComparable: false });

    expect(scenario.sectorEndDate).toBeNull();
    expect(scenario.isComparable).toBe(false);
  });

  it('copies the order, so changing the response later does not change the scenario', () => {
    const original = { ...response, executionOrder: ['x', 'y'] };
    const scenario = mapScenario(original);
    original.executionOrder.push('z');

    expect(scenario.executionOrder).toEqual(['x', 'y']);
  });
});
