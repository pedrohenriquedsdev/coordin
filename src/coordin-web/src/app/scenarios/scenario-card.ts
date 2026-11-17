import { Component, computed, input, output, signal } from '@angular/core';
import { ChartComponent } from '../charts/chart.component';
import { formatCalendarDate } from '../core/dates/calendar-date';
import { deviationLabel } from '../planning/labels';
import { buildScenarioChart } from './scenario-chart';
import { contributionLabel, scoreLabel, topContributions } from './scenario-view';
import { Scenario } from './scenarios';

/**
 * Um cenário na tela (RF-13, RF-14, RF-15): o resumo, a justificativa em gráfico e tabela e a tabela completa.
 * As recomendações e a simulação usam este mesmo cartão. A aprovação pede confirmação em dois passos (D5);
 * rejeitar só avisa a tela, que tira o cenário da lista.
 */
@Component({
  selector: 'app-scenario-card',
  imports: [ChartComponent],
  templateUrl: './scenario-card.html',
})
export class ScenarioCard {
  readonly scenario = input.required<Scenario>();
  readonly title = input.required<string>();
  readonly canApprove = input(true);

  readonly approve = output<Scenario>();
  readonly reject = output<Scenario>();

  /** Depois do primeiro clique em "Aprovar", o cartão mostra a confirmação e só então emite. */
  protected readonly confirming = signal(false);

  /** As demandas que mais contribuem (no máximo 10): o gráfico e a tabela de justificativa usam estas. */
  protected readonly justification = computed(() => topContributions(this.scenario().items));

  protected readonly chartConfiguration = computed(() => buildScenarioChart(this.justification()));

  /** Quantas demandas têm contribuição diferente de zero, para o texto "de N". */
  protected readonly contributingCount = computed(
    () => this.scenario().items.filter((item) => item.scoreContributionDelta !== 0).length,
  );

  protected dateOf(value: string | null): string {
    return value ? formatCalendarDate(value) : '—';
  }

  protected scoreOf(value: number): string {
    return scoreLabel(value);
  }

  protected deltaOf(days: number): string {
    return deviationLabel(days);
  }

  protected contributionOf(value: number): string {
    return contributionLabel(value);
  }

  protected askApproval(): void {
    this.confirming.set(true);
  }

  protected cancelApproval(): void {
    this.confirming.set(false);
  }

  protected confirmApproval(): void {
    this.confirming.set(false);
    this.approve.emit(this.scenario());
  }

  protected rejectScenario(): void {
    this.confirming.set(false);
    this.reject.emit(this.scenario());
  }
}
