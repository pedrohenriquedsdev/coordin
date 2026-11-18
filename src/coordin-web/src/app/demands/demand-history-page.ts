import { Component, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DemandHistoryStore } from './demand-history.store';

/**
 * Tela de detalhe de uma demanda (RF-20, RF-21): os dois históricos em ordem cronológica. Só lê; não há edição de
 * histórico, porque ele é somente de inclusão (DT-14). Reage a troca de demanda na mesma tela (voltar e avançar do
 * navegador entre dois detalhes), porque o Angular reaproveita o componente quando só o parâmetro muda.
 */
@Component({
  selector: 'app-demand-history-page',
  imports: [RouterLink],
  providers: [DemandHistoryStore],
  templateUrl: './demand-history-page.html',
})
export class DemandHistoryPage {
  protected readonly store = inject(DemandHistoryStore);

  /** O identificador da demanda que a tela está mostrando; "Tentar de novo" busca de novo com ele. */
  protected idValue = '';

  constructor() {
    inject(ActivatedRoute)
      .paramMap.pipe(takeUntilDestroyed())
      .subscribe((params) => {
        this.idValue = params.get('id') ?? '';
        this.store.refresh(this.idValue);
      });
  }
}
