import { ValidationResult } from '../api/models/validation-result';
import {
  deviationChartSeries,
  effortRangeError,
  efficiencyChartSeries,
  efficiencyTableRows,
  priorityCheckSummary,
  priorityRows,
  seedError,
  toNumber,
  validationErrorLabel,
} from './labels';

/** Um resultado de exemplo, com números que permitem conferir cada série e cada linha da tabela. */
function sampleResult(overrides: Partial<ValidationResult> = {}): ValidationResult {
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
      items: [],
    },
    priorityCheck: {
      worsenedCount: 2,
      items: [
        {
          demandId: 'a',
          priorityRank: 1,
          currentForecastEndDate: '2027-02-01',
          coordinForecastEndDate: '2027-02-03',
          deltaDays: 2,
        },
        {
          demandId: 'b',
          priorityRank: 2,
          currentForecastEndDate: '2027-02-10',
          coordinForecastEndDate: '2027-02-08',
          deltaDays: '-2',
        },
      ],
    },
    ...overrides,
  };
}

describe('effortRangeError', () => {
  it('accepts the suggested range of the wizard', () => {
    expect(effortRangeError(0.9, 1.1)).toBeNull();
  });

  it('accepts a single fixed factor, with minimum equal to maximum', () => {
    expect(effortRangeError(1, 1)).toBeNull();
  });

  it('refuses a minimum that is zero or negative', () => {
    expect(effortRangeError(0, 1.1)).toBe('O fator mínimo precisa ser maior que zero.');
    expect(effortRangeError(-0.5, 1.1)).toBe('O fator mínimo precisa ser maior que zero.');
  });

  it('refuses a minimum above the maximum', () => {
    expect(effortRangeError(1.2, 1.1)).toBe('O fator mínimo não pode ser maior que o máximo.');
  });

  it('refuses values that are not numbers', () => {
    expect(effortRangeError(Number.NaN, 1.1)).toBe('Informe os dois fatores como números.');
    expect(effortRangeError(0.9, Number.POSITIVE_INFINITY)).toBe(
      'Informe os dois fatores como números.',
    );
  });
});

describe('seedError', () => {
  it('accepts an integer seed', () => {
    expect(seedError(20261126)).toBeNull();
  });

  it('refuses a fractional seed', () => {
    expect(seedError(1.5)).toBe('A semente precisa ser um número inteiro.');
  });
});

describe('validationErrorLabel', () => {
  it.each([
    ['validation-current-plan-missing', 'Guarde o planejamento atual antes de comparar.'],
    ['validation-baseline-incomplete', 'Falta a linha de base de alguma demanda.'],
    ['validation-nothing-to-compare', 'Não há demanda previsível para comparar.'],
    ['validation-current-plan-exists', 'O planejamento atual já está guardado.'],
  ])('names the %s code in Portuguese', (code, text) => {
    expect(validationErrorLabel(code)).toBe(text);
  });

  it('returns null for an unknown or missing code, so the shell shows its generic message', () => {
    expect(validationErrorLabel('something-else')).toBeNull();
    expect(validationErrorLabel(null)).toBeNull();
  });
});

describe('toNumber', () => {
  it('reads both numbers and the text the API sends for decimals', () => {
    expect(toNumber(2.5)).toBe(2.5);
    expect(toNumber('21.5')).toBe(21.5);
  });
});

describe('efficiencyChartSeries and efficiencyTableRows', () => {
  it('plots idle hours and overloaded periods for both plannings, reading text decimals as numbers', () => {
    const series = efficiencyChartSeries(sampleResult());
    expect(series.labels).toEqual(['Ociosidade (horas)', 'Sobrecargas (períodos)']);
    expect(series.currentProcess).toEqual([412.5, 9]);
    expect(series.withCoordin).toEqual([296, 4]);
  });

  it('shows every efficiency metric in the table, with the same values as the chart', () => {
    const rows = efficiencyTableRows(sampleResult());
    const idle = rows.find((row) => row.label === 'Ociosidade (horas)');
    const overloaded = rows.find((row) => row.label === 'Sobrecargas (períodos)');
    expect(idle).toEqual({
      label: 'Ociosidade (horas)',
      currentProcess: '412.5',
      withCoordin: '296',
    });
    expect(overloaded).toEqual({
      label: 'Sobrecargas (períodos)',
      currentProcess: '9',
      withCoordin: '4',
    });
    expect(rows.map((row) => row.label)).toEqual([
      'Ociosidade (horas)',
      'Dispersão da ocupação (pontos)',
      'Sobrecargas (períodos)',
      'Prazo do setor',
    ]);
  });

  it('shows a dash for a sector end date that is missing', () => {
    const prazo = efficiencyTableRows(sampleResult()).find((row) => row.label === 'Prazo do setor');
    expect(prazo).toEqual({
      label: 'Prazo do setor',
      currentProcess: '2027-03-12',
      withCoordin: '—',
    });
  });
});

describe('deviationChartSeries', () => {
  it('plots the signed and the absolute averages for the baseline and for the Coordin', () => {
    const series = deviationChartSeries(sampleResult());
    expect(series.labels).toEqual(['Linha de base', 'Coordin']);
    expect(series.signed).toEqual([9.4, 2.8]);
    expect(series.absolute).toEqual([11.2, 4.1]);
  });
});

describe('priorityRows and priorityCheckSummary', () => {
  it('keeps the priority order and marks only the demands that worsen', () => {
    const rows = priorityRows(sampleResult());
    expect(rows.map((row) => [row.rank, row.worsened])).toEqual([
      [1, true],
      [2, false],
    ]);
    expect(rows[1].deltaDays).toBe(-2);
  });

  it('says how many priorities worsen, with the right plural', () => {
    expect(priorityCheckSummary(sampleResult())).toBe(
      '2 demandas prioritárias pioram com o Coordin.',
    );
    const one = sampleResult({ priorityCheck: { items: [], worsenedCount: 1 } });
    expect(priorityCheckSummary(one)).toBe('1 demanda prioritária piora com o Coordin.');
    const none = sampleResult({ priorityCheck: { items: [], worsenedCount: 0 } });
    expect(priorityCheckSummary(none)).toBe(
      'Nenhuma das demandas prioritárias piora com o Coordin.',
    );
  });
});
