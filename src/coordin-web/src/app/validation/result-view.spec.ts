import { ValidationResult } from '../api/models/validation-result';
import {
  averageRows,
  buildDeviationChart,
  buildEfficiencyChart,
  deviationRows,
  efficiencyTableRows,
  formatMetric,
  priorityTableRows,
} from './result-view';

/** Um resultado com números que permitem conferir cada gráfico contra a sua tabela. */
function sampleResult(): ValidationResult {
  return {
    periodStart: '2027-01-04',
    periodEnd: '2027-03-14',
    efficiency: {
      currentProcess: {
        idleHours: 412.5,
        occupancySpreadPoints: 38,
        overloadedPeriods: 9,
        sectorEndDate: '2027-03-12',
      },
      withCoordin: {
        idleHours: '296',
        occupancySpreadPoints: '21.5',
        overloadedPeriods: 4,
        sectorEndDate: null,
      },
    },
    deviation: {
      averageBaselineDeviationDays: 9.4,
      averageCoordinDeviationDays: '2.8',
      averageBaselineAbsoluteDeviationDays: 11.2,
      averageCoordinAbsoluteDeviationDays: '4.1',
      items: [
        {
          demandId: 'a',
          baselineDate: '2027-06-30',
          coordinForecastEndDate: '2027-02-03',
          simulatedActualEndDate: '2027-02-05',
          baselineDeviationDays: -146,
          coordinDeviationDays: 2,
        },
      ],
    },
    priorityCheck: {
      worsenedCount: 1,
      items: [
        {
          demandId: 'a',
          priorityRank: 1,
          currentForecastEndDate: '2027-02-01',
          coordinForecastEndDate: '2027-02-03',
          deltaDays: 2,
        },
        {
          demandId: 'z',
          priorityRank: 2,
          currentForecastEndDate: '2027-02-10',
          coordinForecastEndDate: '2027-02-08',
          deltaDays: '-2',
        },
      ],
    },
  };
}

describe('formatMetric', () => {
  it('uses a decimal comma and at most one decimal place', () => {
    expect(formatMetric(412.5)).toBe('412,5');
    expect(formatMetric(296)).toBe('296');
    expect(formatMetric(9.44)).toBe('9,4');
  });
});

describe('the charts and their tables carry the same values', () => {
  it('the efficiency chart plots exactly the numbers that the table shows', () => {
    const chart = buildEfficiencyChart(sampleResult());
    const datasets = chart.data.datasets as { data: number[] }[];

    expect(datasets[0].data).toEqual([412.5, 9]);
    expect(datasets[1].data).toEqual([296, 4]);
    expect(formatMetric(datasets[0].data[0])).toBe('412,5');
  });

  it('the deviation chart plots exactly the averages that the table shows', () => {
    const chart = buildDeviationChart(sampleResult());
    const datasets = chart.data.datasets as { data: number[] }[];
    const rows = averageRows(sampleResult());

    expect(datasets[0].data).toEqual([9.4, 2.8]);
    expect(datasets[1].data).toEqual([11.2, 4.1]);
    expect(rows).toEqual([
      { label: 'Linha de base', signed: '9,4', absolute: '11,2' },
      { label: 'Coordin', signed: '2,8', absolute: '4,1' },
    ]);
  });
});

describe('deviationRows', () => {
  it('shows the title, the dates in dd/MM/aaaa and the deviations in words', () => {
    const rows = deviationRows(sampleResult(), new Map([['a', 'Alfa']]));

    expect(rows).toEqual([
      {
        demandId: 'a',
        title: 'Alfa',
        baselineDate: '30/06/2027',
        coordinForecastEndDate: '03/02/2027',
        simulatedActualEndDate: '05/02/2027',
        baselineText: '146 dias antes',
        coordinText: '2 dias de atraso',
      },
    ]);
  });

  it('falls back to the identifier when the title is not known', () => {
    const rows = deviationRows(sampleResult(), new Map());

    expect(rows[0].title).toBe('a');
  });
});

describe('priorityTableRows', () => {
  it('marks only the demand that worsens, with its title and the difference in words', () => {
    const rows = priorityTableRows(sampleResult(), new Map([['a', 'Alfa']]));

    expect(rows.map((row) => [row.rank, row.title, row.worsened])).toEqual([
      [1, 'Alfa', true],
      [2, 'z', false],
    ]);
    expect(rows[0].deltaText).toBe('2 dias de atraso');
    expect(rows[1].deltaText).toBe('2 dias antes');
  });
});

describe('efficiencyTableRows', () => {
  it('shows the same numbers as the chart, with a decimal comma and dates in dd/MM/aaaa', () => {
    const rows = efficiencyTableRows(sampleResult());

    expect(rows).toEqual([
      { label: 'Ociosidade (horas)', currentProcess: '412,5', withCoordin: '296' },
      { label: 'Dispersão da ocupação (pontos)', currentProcess: '38', withCoordin: '21,5' },
      { label: 'Sobrecargas (períodos)', currentProcess: '9', withCoordin: '4' },
      { label: 'Prazo do setor', currentProcess: '12/03/2027', withCoordin: '—' },
    ]);
  });
});
