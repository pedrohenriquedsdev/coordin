import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DemandHistoryStore } from './demand-history.store';

/**
 * Tela de detalhe de uma demanda (RF-20, RF-21): os dois históricos em ordem cronológica. Só lê; não há edição de
 * histórico, porque ele é somente de inclusão (DT-14).
 */
@Component({
  selector: 'app-demand-history-page',
  imports: [RouterLink],
  providers: [DemandHistoryStore],
  templateUrl: './demand-history-page.html',
})
export class DemandHistoryPage {
  protected readonly store = inject(DemandHistoryStore);
  /** O identificador da demanda, da rota; a tela de detalhe volta a buscar com ele em "Tentar de novo". */
  protected readonly idValue = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';

  constructor() {
    this.store.refresh(this.idValue);
  }
}
