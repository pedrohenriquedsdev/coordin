import { Component, inject } from '@angular/core';
import { stepLabel } from './wizard-step';
import { ValidationStore } from './validation.store';

/**
 * Tela "Validação" (RF-24). Nesta etapa mostra a situação da área e a etapa atual do assistente; os passos
 * (carga, linha de base, plano atual e comparação) entram nas próximas tarefas. Não tem regra de negócio.
 */
@Component({
  selector: 'app-validation-page',
  providers: [ValidationStore],
  templateUrl: './validation-page.html',
})
export class ValidationPage {
  protected readonly store = inject(ValidationStore);

  protected readonly stepLabel = stepLabel;

  constructor() {
    this.store.refresh();
  }
}
