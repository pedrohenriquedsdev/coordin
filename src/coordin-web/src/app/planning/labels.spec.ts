import { deviationLabel, unpredictableReasonLabel } from './labels';

describe('unpredictableReasonLabel', () => {
  it('names each of the three reasons in Portuguese', () => {
    expect(unpredictableReasonLabel('MissingEstimate')).toBe('Falta a estimativa de algum time');
    expect(unpredictableReasonLabel('MissingStartDate')).toBe('Sem data de início');
    expect(unpredictableReasonLabel('NoCapacity')).toBe(
      'Sem capacidade: falta pessoa alocada ou a capacidade é zero',
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
