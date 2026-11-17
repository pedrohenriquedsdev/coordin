import { buildScenarioChart, SCENARIO_COLORS } from './scenario-chart';
import { ScenarioItem } from './scenarios';

function item(title: string, scoreContributionDelta: number): ScenarioItem {
  return {
    demandId: title.toLowerCase(),
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

describe('buildScenarioChart', () => {
  const configuration = buildScenarioChart([item('Alfa', -120), item('Bravo', 40)]);

  it('draws horizontal bars, so long titles stay readable', () => {
    expect(configuration.type).toBe('bar');
    expect(configuration.options?.indexAxis).toBe('y');
  });

  it('has one label and one value per demand, in the order received', () => {
    expect(configuration.data.labels).toEqual(['Alfa', 'Bravo']);
    expect(configuration.data.datasets).toHaveLength(1);
    expect(configuration.data.datasets[0].data).toEqual([-120, 40]);
  });

  it('colors an improvement and a worsening differently', () => {
    expect(configuration.data.datasets[0].backgroundColor).toEqual([
      SCENARIO_COLORS.improves,
      SCENARIO_COLORS.worsens,
    ]);
  });

  it('turns the legend off and titles the axis', () => {
    expect(configuration.options?.plugins?.legend?.display).toBe(false);
    expect(configuration.options?.scales?.['x']).toMatchObject({
      title: { display: true, text: 'Contribuição para o critério' },
    });
  });

  it('builds an empty chart for no demands', () => {
    const empty = buildScenarioChart([]);

    expect(empty.data.labels).toEqual([]);
    expect(empty.data.datasets[0].data).toEqual([]);
  });
});
