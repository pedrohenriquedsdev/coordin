import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, untracked } from '@angular/core';
import { Icon } from '../core/ui/icon';
import { Loader } from '../core/ui/loader';
import { PageHeader } from '../core/ui/page-header';
import { stepLabel, WizardStep } from './wizard-step';
import { ValidationResultView } from './validation-result';
import { ValidationStore } from './validation.store';

/**
 * Tela "Validação" (RF-24): o assistente em quatro etapas do PRD 7.3 (carregar, linha de base, plano atual e
 * comparação). A etapa vem do servidor; esta classe só liga os botões ao store. Não tem regra de negócio.
 */
@Component({
  selector: 'app-validation-page',
  imports: [DatePipe, ValidationResultView, PageHeader, Icon, Loader],
  providers: [ValidationStore],
  templateUrl: './validation-page.html',
})
export class ValidationPage {
  protected readonly store = inject(ValidationStore);

  protected readonly stepLabel = stepLabel;

  /** As quatro etapas no indicador de progresso, com um nome curto. */
  protected readonly steps: readonly { id: WizardStep; short: string }[] = [
    { id: 'load', short: 'Carregar' },
    { id: 'baseline', short: 'Linha de base' },
    { id: 'plan', short: 'Plano atual' },
    { id: 'compare', short: 'Comparar' },
  ];

  /** A posição da etapa atual (0 a 3); as anteriores estão concluídas. */
  protected readonly stepIndex = computed(() => {
    const current = this.store.step();
    return current === null ? -1 : this.steps.findIndex((step) => step.id === current);
  });

  /** Quantas demandas já têm data de linha de base, e a fatia disso no total (para a barra de progresso). */
  protected readonly baselineDone = computed(
    () => this.store.baseline().length - this.store.missingDates(),
  );
  protected readonly baselinePercent = computed(() => {
    const total = this.store.baseline().length;
    return total === 0 ? 0 : (this.baselineDone() / total) * 100;
  });
  /** Os títulos das demandas do conjunto, para o resultado mostrar nomes em vez de identificadores. */
  protected readonly titles = computed(
    () => new Map(this.store.baseline().map((item) => [item.demandId, item.title])),
  );

  constructor() {
    this.store.refresh();

    // A lista de demandas (com a linha de base) é buscada na etapa dela e na de comparar: nesta última,
    // serve para o resultado mostrar os nomes das demandas, mesmo com a página aberta direto nessa etapa.
    effect(() => {
      const step = this.store.step();
      if (step === 'baseline' || step === 'compare') {
        untracked(() => this.store.loadBaseline());
      }
    });
  }

  protected onNumber(name: 'minEffortFactor' | 'maxEffortFactor' | 'seed', event: Event): void {
    this.store.setParam(name, Number((event.target as HTMLInputElement).value));
  }

  protected onDate(demandId: string, event: Event): void {
    this.store.setDate(demandId, (event.target as HTMLInputElement).value);
  }

  protected onConfirm(event: Event): void {
    this.store.confirmed.set((event.target as HTMLInputElement).checked);
  }

  protected submit(event: Event, action: () => void): void {
    event.preventDefault();
    action();
  }
}
