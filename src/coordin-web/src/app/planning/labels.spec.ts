import { deviationLabel, prerequisitesLabel, unpredictableReasonLabel } from './labels';

describe('unpredictableReasonLabel', () => {
  it('names each of the four reasons in Portuguese', () => {
    expect(unpredictableReasonLabel('MissingEstimate')).toBe('Falta a estimativa de algum time');
    expect(unpredictableReasonLabel('MissingStartDate')).toBe('Sem data de início');
    expect(unpredictableReasonLabel('NoCapacity')).toBe(
      'Sem capacidade: falta pessoa alocada ou a capacidade é zero',
    );
    expect(unpredictableReasonLabel('PrerequisiteUnpredictable')).toBe(
      'Depende de uma demanda que não tem previsão',
    );
  });

  it('falls back to a generic text for an unknown or missing reason', () => {
    expect(unpredictableReasonLabel(null)).toBe('Sem previsão');
    expect(unpredictableReasonLabel('Other')).toBe('Sem previsão');
  });
});

describe('deviationLabel', () => {
  it.each([
    [6, '6 dias de atraso'],
    [1, '1 dia de atraso'],
    [0, 'no prazo'],
    [-1, '1 dia antes'],
    [-2, '2 dias antes'],
  ])('labels %i as "%s"', (days, expected) => {
    expect(deviationLabel(days)).toBe(expected);
  });
});

describe('prerequisitesLabel', () => {
  it('returns null when there are no prerequisites', () => {
    expect(prerequisitesLabel([])).toBeNull();
  });

  it('lists one prerequisite', () => {
    expect(prerequisitesLabel(['Pagamentos'])).toBe('Depende de: Pagamentos');
  });

  it('lists several prerequisites in the given order', () => {
    expect(prerequisitesLabel(['A', 'B', 'C'])).toBe('Depende de: A, B, C');
  });
});
