import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { ApiConfiguration } from '../api/api-configuration';
import { getSchedule } from '../api/fn/schedule/get-schedule';
import { applyScenario } from '../api/fn/scenarios/apply-scenario';
import { getScenarioRecommendations } from '../api/fn/scenarios/get-scenario-recommendations';
import { resetToPriority } from '../api/fn/scenarios/reset-to-priority';
import { simulateScenario } from '../api/fn/scenarios/simulate-scenario';
import { messageFor } from '../core/problems/messages';
import { readProblem } from '../core/problems/problem';
import { mapSchedule, Schedule } from '../planning/planning';
import { buildFullOrder } from './scenario-view';
import { mapScenario, Scenario } from './scenarios';

export type LoadStatus = 'loading' | 'ready' | 'failed';

/** O que a tela está fazendo com a API. Enquanto não for `idle`, todos os botões de ação ficam desabilitados. */
export type BusyState = 'idle' | 'searching' | 'simulating' | 'applying';

/**
 * Estado da tela "Cenários" (RF-13 a RF-15). Os cenários vêm prontos da API: esta tela só os pede, mostra e
 * envia a ordem que o gestor aprova. Nada é aplicado sem o gestor pedir, e a busca nunca roda sozinha.
 */
@Injectable()
export class ScenariosStore {
  private readonly http = inject(HttpClient);
  private readonly root = inject(ApiConfiguration).rootUrl;

  readonly schedule = signal<Schedule | null>(null);
  readonly status = signal<LoadStatus>('loading');
  readonly busy = signal<BusyState>('idle');
  readonly recommendations = signal<Scenario[]>([]);
  /** Se já houve uma busca de recomendações nesta tela (para a mensagem "nenhuma alternativa"). */
  readonly searched = signal(false);
  readonly simulation = signal<Scenario | null>(null);
  readonly lastApplied = signal<Scenario | null>(null);
  readonly message = signal<string | null>(null);

  /** Busca o cronograma vigente, que é a base das ordens que o gestor monta. Não busca recomendações. */
  load(): void {
    if (this.schedule() === null) {
      this.status.set('loading');
    }

    getSchedule(this.http, this.root).subscribe({
      next: (response) => {
        this.schedule.set(mapSchedule(response.body));
        this.status.set('ready');
      },
      error: () => this.status.set('failed'),
    });
  }

  /** Pede as recomendações (RF-13). Se falhar, os cenários que já estavam na tela continuam. */
  recommend(): void {
    this.busy.set('searching');
    this.message.set(null);
    getScenarioRecommendations(this.http, this.root).subscribe({
      next: (response) => {
        this.recommendations.set(response.body.map(mapScenario));
        this.searched.set(true);
        this.busy.set('idle');
      },
      error: (error: unknown) => {
        this.busy.set('idle');
        this.message.set(
          messageFor(readProblem(error), 'Não foi possível buscar as recomendações.'),
        );
      },
    });
  }

  /**
   * Simula a ordem que o gestor montou (RF-14). `editedOpen` são só as demandas abertas, na ordem nova;
   * `buildFullOrder` completa a ordem com as concluídas, que a API exige. Não grava nada.
   */
  simulate(editedOpen: readonly string[]): void {
    const schedule = this.schedule();
    if (schedule === null) {
      return;
    }

    const currentOrder = schedule.items.map((item) => item.demandId);
    const openIds = new Set(
      schedule.items.filter((item) => item.completedOn === null).map((item) => item.demandId),
    );
    const executionOrder = buildFullOrder(currentOrder, openIds, editedOpen);

    this.busy.set('simulating');
    this.message.set(null);
    simulateScenario(this.http, this.root, { body: { executionOrder } }).subscribe({
      next: (response) => {
        this.simulation.set(mapScenario(response.body));
        this.busy.set('idle');
      },
      error: (error: unknown) => {
        this.busy.set('idle');
        this.message.set(messageFor(readProblem(error), 'Não foi possível simular a ordem.'));
      },
    });
  }

  /** Tira um cenário da tela. Não chama a API: rejeitar não muda nada no servidor (RF-15). */
  reject(scenario: Scenario): void {
    this.recommendations.update((list) => list.filter((entry) => entry !== scenario));
    if (this.simulation() === scenario) {
      this.simulation.set(null);
    }
  }

  /** Tira o resultado da simulação da tela ("Restaurar a ordem atual"). Não chama a API. */
  clearSimulation(): void {
    this.simulation.set(null);
    this.message.set(null);
  }

  /** Aprova um cenário (RF-15): grava a ordem de execução do cenário. A prioridade não muda. */
  apply(scenario: Scenario): void {
    this.busy.set('applying');
    this.message.set(null);
    applyScenario(this.http, this.root, {
      body: { executionOrder: [...scenario.executionOrder] },
    }).subscribe({
      next: (response) => {
        this.lastApplied.set(mapScenario(response.body));
        this.afterChange();
      },
      error: (error: unknown) => {
        this.busy.set('idle');
        this.message.set(messageFor(readProblem(error), 'Não foi possível aprovar o cenário.'));
      },
    });
  }

  /** Volta a ordem de execução para a ordem de prioridade (RF-15). Só é chamado depois da confirmação da tela. */
  resetToPriority(): void {
    this.busy.set('applying');
    this.message.set(null);
    resetToPriority(this.http, this.root).subscribe({
      next: () => {
        this.lastApplied.set(null);
        this.afterChange();
      },
      error: (error: unknown) => {
        this.busy.set('idle');
        this.message.set(messageFor(readProblem(error), 'Não foi possível voltar à prioridade.'));
      },
    });
  }

  /** Depois de mudar a ordem, os cenários da tela deixam de valer: somem e o cronograma é recarregado. */
  private afterChange(): void {
    this.recommendations.set([]);
    this.simulation.set(null);
    this.searched.set(false);
    this.busy.set('idle');
    this.load();
  }
}
