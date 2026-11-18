import { AccuracyResponse } from '../api/models/accuracy-response';
import {
  accuracyRows,
  deviationChart,
  estimateChart,
  excludedNotes,
  metricText,
  periodError,
} from './accuracy-view';

const indicators = (overrides: Partial<Record<string, number | string | null>> = {}) => ({
  usedCount: 3,
  averageDeviationFromBaselineDays: 1,
  averageDeviationFromCompletionForecastDays: -2,
  onTimePercentage: 66.7,
  averageEstimateDifferencePercent: 12.5,
  excludedWithoutSnapshot: 0,
  excludedWithoutBaseline: 0,
  excludedWithoutActualEffort: 0,
  excludedWithoutEstimate: 0,
  ...overrides,
});

const response: AccuracyResponse = {
  from: '2027-01-01',
  to: '2027-01-31',
  sector: indicators() as AccuracyResponse['sector'],
  teams: [
    {
      teamId: 'a',
      teamName: 'Alfa',
      indicators: indicators({
        averageEstimateDifferencePercent: 30,
      }) as AccuracyResponse['sector'],
      isAboveSector: true,
    },
    {
      teamId: 'b',
      teamName: null,
      indicators: indicators({
        usedCount: 1,
        averageEstimateDifferencePercent: null,
      }) as AccuracyResponse['sector'],
      isAboveSector: false,
    },
  ],
};

describe('periodError', () => {
  it('accepts an ordered period, including the same day', () => {
    expect(periodError('2027-01-01', '2027-01-31')).toBeNull();
    expect(periodError('2027-01-15', '2027-01-15')).toBeNull();
  });

  it('refuses an empty date and an inverted period', () => {
    expect(periodError('', '2027-01-31')).toBe('Informe a data inicial e a final.');
    expect(periodError('2027-02-01', '2027-01-01')).toBe(
      'A data inicial não pode ser posterior à final.',
    );
  });
});

describe('metricText', () => {
  it('uses a decimal comma and a dash when there is no average', () => {
    expect(metricText(12.5, '%')).toBe('12,5%');
    expect(metricText(-3.8, ' dias')).toBe('-3,8 dias');
    expect(metricText(null)).toBe('—');
  });
});

describe('accuracyRows', () => {
  it('lists the sector first, then the teams, with the same numbers the charts plot', () => {
    const rows = accuracyRows(response);

    expect(rows.map((row) => row.label)).toEqual(['Setor', 'Alfa', 'Time sem nome']);
    expect(rows[0]).toMatchObject({
      isSector: true,
      baselineDays: 1,
      completionDays: -2,
      onTime: 66.7,
      estimateDifference: 12.5,
    });
    expect(rows[1]).toMatchObject({
      isAboveSector: true,
      estimateDifference: 30,
      estimateText: '30%',
    });
    expect(rows[2].estimateText).toBe('—');
  });
});

describe('the charts', () => {
  it('plot exactly the numbers of the rows, in the same order', () => {
    const rows = accuracyRows(response);
    const deviation = deviationChart(rows).data.datasets as { data: (number | null)[] }[];
    const estimate = estimateChart(rows).data.datasets as { data: (number | null)[] }[];

    expect(deviation[0].data).toEqual([1, 1, 1]);
    expect(deviation[1].data).toEqual([-2, -2, -2]);
    expect(estimate[0].data).toEqual([12.5, 30, null]);
    expect(deviationChart(rows).data.labels).toEqual(['Setor', 'Alfa', 'Time sem nome']);
  });
});

describe('excludedNotes', () => {
  it('says how many demands were left out of each indicator, with the reason', () => {
    const notes = excludedNotes(
      indicators({
        excludedWithoutActualEffort: 2,
        excludedWithoutBaseline: 1,
      }) as AccuracyResponse['sector'],
    );

    expect(notes).toEqual([
      '1 demanda ficou de fora (sem primeira previsão)',
      '2 demandas ficaram de fora (sem esforço real)',
    ]);
  });

  it('is empty when nothing was left out', () => {
    expect(excludedNotes(indicators() as AccuracyResponse['sector'])).toEqual([]);
  });
});
