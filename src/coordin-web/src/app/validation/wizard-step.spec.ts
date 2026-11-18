import { ValidationStatusResponse } from '../api/models/validation-status-response';
import { stepLabel, wizardStep } from './wizard-step';

/** Um status com os campos da API (contagens em texto, como o cliente gerado as entrega). */
function status(overrides: Partial<ValidationStatusResponse> = {}): ValidationStatusResponse {
  return {
    demandCount: '30',
    baselineCount: '0',
    currentPlanCaptured: false,
    currentPlanCapturedAt: null,
    ...overrides,
  };
}

describe('wizardStep', () => {
  it('starts at the load step when the dataset was never loaded', () => {
    expect(wizardStep(status({ demandCount: '0' }))).toBe('load');
  });

  it('asks for the baseline while some demand has no baseline date', () => {
    expect(wizardStep(status({ baselineCount: '29' }))).toBe('baseline');
    expect(wizardStep(status({ baselineCount: '0' }))).toBe('baseline');
  });

  it('asks for the current plan once every demand has a baseline', () => {
    expect(wizardStep(status({ baselineCount: '30' }))).toBe('plan');
  });

  it('allows comparing once the current plan is saved', () => {
    expect(wizardStep(status({ baselineCount: '30', currentPlanCaptured: true }))).toBe('compare');
  });

  it('reads counts that arrive as numbers too', () => {
    expect(wizardStep({ ...status(), demandCount: 30, baselineCount: 30 })).toBe('plan');
  });
});

describe('stepLabel', () => {
  it.each([
    ['load', 'Carregar o conjunto fictício'],
    ['baseline', 'Registrar a linha de base'],
    ['plan', 'Guardar o planejamento atual'],
    ['compare', 'Comparar'],
  ] as const)('names the %s step in Portuguese', (step, text) => {
    expect(stepLabel(step)).toBe(text);
  });
});
