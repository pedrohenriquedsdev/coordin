/**
 * Rótulos em português da tela de planejamento (RF-10, RF-12). As regras (motivos, desvios) vêm prontas
 * da API; aqui só se escreve o texto para o gestor.
 */

/** O motivo de uma demanda não previsível, em português. Um motivo desconhecido vira um texto genérico. */
export function unpredictableReasonLabel(reason: string | null): string {
  switch (reason) {
    case 'MissingEstimate':
      return 'Falta a estimativa de algum time';
    case 'MissingStartDate':
      return 'Sem data de início';
    case 'NoCapacity':
      return 'Sem capacidade: falta pessoa alocada ou a capacidade é zero';
    case 'PrerequisiteUnpredictable':
      return 'Depende de uma demanda que não tem previsão';
    default:
      return 'Sem previsão';
  }
}

/**
 * O desvio em dias corridos, com a palavra certa: "6 dias de atraso", "1 dia de atraso", "2 dias antes",
 * "1 dia antes" ou "no prazo" (DT-27). Positivo é atraso; negativo, antes do prazo.
 */
export function deviationLabel(days: number): string {
  if (days === 0) {
    return 'no prazo';
  }

  const amount = Math.abs(days);
  const unit = amount === 1 ? 'dia' : 'dias';
  return days > 0 ? `${amount} ${unit} de atraso` : `${amount} ${unit} antes`;
}

/** Horas em pt-BR (`80 h`, `12,5 h`); `null` vira "sem estimativa", porque o time ainda não estimou. */
export function hoursLabel(value: number | null): string {
  return value === null
    ? 'sem estimativa'
    : `${value.toLocaleString('pt-BR', { maximumFractionDigits: 2 })} h`;
}

/**
 * A linha "Depende de: A, B" do cronograma (RF-18), ou `null` quando a demanda não tem pré-requisitos.
 * Os títulos vêm na ordem da API, que já é a de prioridade do pré-requisito.
 */
export function prerequisitesLabel(titles: readonly string[]): string | null {
  return titles.length === 0 ? null : `Depende de: ${titles.join(', ')}`;
}
