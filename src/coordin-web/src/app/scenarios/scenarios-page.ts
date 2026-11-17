import { Component, computed, inject, signal } from '@angular/core';
import { moveInList, MoveDirection } from './scenario-view';
import { ResetToPriority } from './reset-to-priority';
import { ScenarioCard } from './scenario-card';
import { Scenario } from './scenarios';
import { ScenariosStore } from './scenarios.store';

/** Texto de quando a aprovação deu certo: a ordem muda e a prioridade não (RF-15). */
const APPLIED_TEXT = 'Ordem de execução alterada. A prioridade registrada não mudou.';

/**
 * Tela "Cenários" (RF-13, RF-14, RF-15). Mostra as recomendações sob pedido, deixa o gestor montar uma ordem
 * própria só com as demandas abertas (Subir e Descer, sem arrastar) e aprovar ou rejeitar cada cenário.
 * Não tem regra de negócio: a ordem completa e os números vêm do `ScenariosStore` e da API.
 */
@Component({
  selector: 'app-scenarios-page',
  imports: [ScenarioCard, ResetToPriority],
  providers: [ScenariosStore],
  templateUrl: './scenarios-page.html',
})
export class ScenariosPage {
  protected readonly store = inject(ScenariosStore);

  /** A ordem que o gestor montou com Subir e Descer; `null` enquanto ele não mexeu (vale a ordem vigente). */
  private readonly openOrder = signal<string[] | null>(null);

  /** As demandas abertas, na ordem de execução vigente. As concluídas não entram na lista de reordenar. */
  protected readonly openItems = computed(() =>
    (this.store.schedule()?.items ?? []).filter((item) => item.completedOn === null),
  );

  protected readonly titles = computed(
    () => new Map(this.openItems().map((item) => [item.demandId, item.title])),
  );

  /**
   * A ordem editável das abertas. Se o cronograma mudou depois do que o gestor montou (por exemplo, após uma
   * aprovação), a ordem montada deixa de valer e a vigente volta a valer.
   */
  protected readonly editedOrder = computed(() => {
    const current = this.openItems().map((item) => item.demandId);
    const edited = this.openOrder();
    const sameDemands =
      edited !== null &&
      edited.length === current.length &&
      current.every((id) => edited.includes(id));
    return sameDemands ? edited : current;
  });

  protected readonly busy = computed(() => this.store.busy() !== 'idle');

  protected readonly appliedText = APPLIED_TEXT;

  constructor() {
    this.store.load();
  }

  protected titleOf(id: string): string {
    return this.titles().get(id) ?? id;
  }

  protected move(id: string, direction: MoveDirection): void {
    this.openOrder.set(moveInList(this.editedOrder(), id, direction));
  }

  /** "Restaurar a ordem atual": volta à ordem vigente e tira o resultado da simulação da tela. */
  protected restore(): void {
    this.openOrder.set(null);
    this.store.clearSimulation();
  }

  protected simulate(): void {
    this.store.simulate(this.editedOrder());
  }

  protected approve(scenario: Scenario): void {
    this.store.apply(scenario);
  }

  protected reject(scenario: Scenario): void {
    this.store.reject(scenario);
  }
}
