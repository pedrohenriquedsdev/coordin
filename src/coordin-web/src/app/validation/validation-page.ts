import { DatePipe } from '@angular/common';
import { Component, effect, inject, untracked } from '@angular/core';
import { priorityCheckSummary } from './labels';
import { stepLabel } from './wizard-step';
import { ValidationStore } from './validation.store';

/**
 * Tela "Validação" (RF-24): o assistente em quatro etapas do PRD 7.3 (carregar, linha de base, plano atual e
 * comparação). A etapa vem do servidor; esta classe só liga os botões ao store. Não tem regra de negócio.
 */
@Component({
  selector: 'app-validation-page',
  imports: [DatePipe],
  providers: [ValidationStore],
  templateUrl: './validation-page.html',
})
export class ValidationPage {
  protected readonly store = inject(ValidationStore);

  protected readonly stepLabel = stepLabel;
  protected readonly priorityCheckSummary = priorityCheckSummary;

  constructor() {
    this.store.refresh();

    // A linha de base só é buscada quando o gestor chega à etapa dela.
    effect(() => {
      if (this.store.step() === 'baseline') {
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
