import { ValidationResult } from '../api/models/validation-result';

/**
 * Regras de apresentação da validação por simulação (RF-24; DT-36). A API já calcula tudo; aqui só se checam
 * os parâmetros antes do envio, se escrevem os motivos de erro e se montam as séries dos gráficos.
 */

/** Os valores sugeridos no assistente (PRD 7.3; M2): faixa de 0,9 a 1,1 e a semente do conjunto. */
export const DEFAULT_MIN_EFFORT_FACTOR = 0.9;
export const DEFAULT_MAX_EFFORT_FACTOR = 1.1;
export const DEFAULT_SEED = 20261126;

/**
 * O motivo de a faixa de variação do esforço não servir, ou `null` quando serve. A API exige mínimo maior que zero
 * e mínimo não maior que o máximo; a tela mostra o motivo antes de enviar.
 */
export function effortRangeError(min: number, max: number): string | null {
  if (!Number.isFinite(min) || !Number.isFinite(max)) {
    return 'Informe os dois fatores como números.';
  }

  if (min <= 0) {
    return 'O fator mínimo precisa ser maior que zero.';
  }

  if (min > max) {
    return 'O fator mínimo não pode ser maior que o máximo.';
  }

  return null;
}

/** A semente precisa ser um número inteiro, como a API espera. */
export function seedError(seed: number): string | null {
  return Number.isInteger(seed) ? null : 'A semente precisa ser um número inteiro.';
}

/**
 * O texto de um código de erro da validação (Problem Details, DT-18). Um código desconhecido cai no texto
 * genérico da casca, por isso o retorno é `null` nesse caso.
 */
export function validationErrorLabel(code: string | null): string | null {
  switch (code) {
    case 'validation-current-plan-missing':
      return 'Guarde o planejamento atual antes de comparar.';
    case 'validation-baseline-incomplete':
      return 'Falta a linha de base de alguma demanda.';
    case 'validation-nothing-to-compare':
      return 'Não há demanda previsível para comparar.';
    case 'validation-current-plan-exists':
      return 'O planejamento atual já está guardado.';
    default:
      return null;
  }
}

/** Converte um número que a API pode mandar como texto (decimais viram string no JSON gerado). */
export function toNumber(value: number | string): number {
  return typeof value === 'number' ? value : Number(value);
}

/**
 * Séries do gráfico de eficiência: só ociosidade (horas) e sobrecargas (períodos), que têm a mesma unidade de
 * contagem. Dispersão e prazo do setor não cabem na mesma escala e ficam só na tabela.
 */
export function efficiencyChartSeries(result: ValidationResult): {
  labels: string[];
  currentProcess: number[];
  withCoordin: number[];
} {
  const current = result.efficiency.currentProcess;
  const withCoordin = result.efficiency.withCoordin;
  return {
    labels: ['Ociosidade (horas)', 'Sobrecargas (períodos)'],
    currentProcess: [toNumber(current.idleHours), toNumber(current.overloadedPeriods)],
    withCoordin: [toNumber(withCoordin.idleHours), toNumber(withCoordin.overloadedPeriods)],
  };
}

/** Séries do gráfico de desvios: a média com sinal e a absoluta, para a linha de base e para o Coordin. */
export function deviationChartSeries(result: ValidationResult): {
  labels: string[];
  signed: number[];
  absolute: number[];
} {
  const deviation = result.deviation;
  return {
    labels: ['Linha de base', 'Coordin'],
    signed: [
      toNumber(deviation.averageBaselineDeviationDays),
      toNumber(deviation.averageCoordinDeviationDays),
    ],
    absolute: [
      toNumber(deviation.averageBaselineAbsoluteDeviationDays),
      toNumber(deviation.averageCoordinAbsoluteDeviationDays),
    ],
  };
}

/** Uma linha da checagem das prioritárias, com o destaque de quem piora com o Coordin. */
export interface PriorityRow {
  rank: number;
  demandId: string;
  currentEnd: string;
  coordinEnd: string;
  deltaDays: number;
  worsened: boolean;
}

/** As demandas prioritárias na ordem de prioridade, marcando as que pioram (positivo é atraso). */
export function priorityRows(result: ValidationResult): PriorityRow[] {
  return result.priorityCheck.items.map((item) => {
    const deltaDays = toNumber(item.deltaDays);
    return {
      rank: toNumber(item.priorityRank),
      demandId: item.demandId,
      currentEnd: item.currentForecastEndDate,
      coordinEnd: item.coordinForecastEndDate,
      deltaDays,
      worsened: deltaDays > 0,
    };
  });
}

/** A mensagem da checagem: quantas das prioritárias pioram, com a palavra certa para o plural. */
export function priorityCheckSummary(result: ValidationResult): string {
  const worsened = toNumber(result.priorityCheck.worsenedCount);
  if (worsened === 0) {
    return 'Nenhuma das demandas prioritárias piora com o Coordin.';
  }
  return worsened === 1
    ? '1 demanda prioritária piora com o Coordin.'
    : `${worsened} demandas prioritárias pioram com o Coordin.`;
}
