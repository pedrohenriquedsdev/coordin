import { ValidationStatusResponse } from '../api/models/validation-status-response';
import { toNumber } from './labels';

/** As quatro etapas do assistente, na ordem do PRD 7.3 (RF-24). */
export type WizardStep = 'load' | 'baseline' | 'plan' | 'compare';

/**
 * A etapa em que o gestor está, derivada do que o servidor registrou (não da memória da tela): sem demandas, o
 * conjunto não foi carregado; com demandas sem linha de base completa, falta a linha de base; sem o plano guardado,
 * falta guardar o plano atual; depois disso, dá para comparar.
 */
export function wizardStep(status: ValidationStatusResponse): WizardStep {
  const demands = toNumber(status.demandCount);
  if (demands === 0) {
    return 'load';
  }

  if (toNumber(status.baselineCount) < demands) {
    return 'baseline';
  }

  if (!status.currentPlanCaptured) {
    return 'plan';
  }

  return 'compare';
}

/** O nome da etapa em português, para o cabeçalho do assistente. */
export function stepLabel(step: WizardStep): string {
  switch (step) {
    case 'load':
      return 'Carregar o conjunto fictício';
    case 'baseline':
      return 'Registrar a linha de base';
    case 'plan':
      return 'Guardar o planejamento atual';
    case 'compare':
      return 'Comparar';
  }
}
