import { ScenarioItemResponse } from '../api/models/scenario-item-response';
import { ScenarioResponse } from '../api/models/scenario-response';

/** O critério de comparação (DT-25) no cronograma atual e no cenário. Menor é melhor; a diferença negativa é melhoria. */
export interface ScenarioScore {
  readonly current: number;
  readonly scenario: number;
  readonly delta: number;
}

/**
 * Uma demanda dentro de um cenário (RF-13). As datas são datas de calendário (`yyyy-MM-dd`, texto). A diferença
 * em dias é corrida e positiva quando a demanda atrasa (DT-27). `weight` e `scoreContributionDelta` justificam o
 * cenário (DT-36): a soma das contribuições é igual à diferença do critério.
 */
export interface ScenarioItem {
  readonly demandId: string;
  readonly title: string;
  readonly priorityRank: number;
  readonly newExecutionRank: number;
  readonly currentForecastEndDate: string;
  readonly forecastStartDate: string;
  readonly forecastEndDate: string;
  readonly deltaDays: number;
  readonly weight: number;
  readonly scoreContributionDelta: number;
}

/** Um cenário, recomendado ou simulado: o mesmo formato nos dois casos (RF-14). */
export interface Scenario {
  readonly executionOrder: readonly string[];
  readonly items: readonly ScenarioItem[];
  readonly sectorEndDate: string | null;
  readonly sectorDeltaDays: number;
  readonly score: ScenarioScore;
  readonly isComparable: boolean;
}

function mapScenarioItem(item: ScenarioItemResponse): ScenarioItem {
  return {
    demandId: item.demandId,
    title: item.title,
    priorityRank: Number(item.priorityRank),
    newExecutionRank: Number(item.newExecutionRank),
    currentForecastEndDate: item.currentForecastEndDate,
    forecastStartDate: item.forecastStartDate,
    forecastEndDate: item.forecastEndDate,
    deltaDays: Number(item.deltaDays),
    weight: Number(item.weight),
    scoreContributionDelta: Number(item.scoreContributionDelta),
  };
}

/** Converte a resposta de um cenário da API para o modelo da tela: os números do cliente gerado viram `number`. */
export function mapScenario(response: ScenarioResponse): Scenario {
  return {
    executionOrder: [...response.executionOrder],
    items: response.items.map(mapScenarioItem),
    sectorEndDate: response.sectorEndDate,
    sectorDeltaDays: Number(response.sectorDeltaDays),
    score: {
      current: Number(response.score.current),
      scenario: Number(response.score.scenario),
      delta: Number(response.score.delta),
    },
    isComparable: response.isComparable,
  };
}
