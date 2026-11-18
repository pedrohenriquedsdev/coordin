import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, untracked } from '@angular/core';
import { stepLabel } from './wizard-step';
import { ValidationResultView } from './validation-result';
import { ValidationStore } from './validation.store';

/**
 * Tela "Validação" (RF-24): o assistente em quatro etapas do PRD 7.3 (carregar, linha de base, plano atual e
 * comparação). A etapa vem do servidor; esta classe só liga os botões ao store. Não tem regra de negócio.
 */
@Component({
  selector: 'app-validation-page',
  imports: [DatePipe, ValidationResultView],
  providers: [ValidationStore],
  templateUrl: './validation-page.html',
})
export class ValidationPage {
  protected readonly store = inject(ValidationStore);

  protected readonly stepLabel = stepLabel;
  /** Os títulos das demandas do conjunto, para o resultado mostrar nomes em vez de identificadores. */
  protected readonly titles = computed(
    () => new Map(this.store.baseline().map((item) => [item.demandId, item.title])),
  );

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
