import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { ApiConfiguration } from '../api/api-configuration';
import { captureCurrentPlan } from '../api/fn/validation/capture-current-plan';
import { evaluateValidation } from '../api/fn/validation/evaluate-validation';
import { getValidationBaseline } from '../api/fn/validation/get-validation-baseline';
import { getValidationStatus } from '../api/fn/validation/get-validation-status';
import { loadValidationDataset } from '../api/fn/validation/load-validation-dataset';
import { putValidationBaseline } from '../api/fn/validation/put-validation-baseline';
import { BaselineResponse } from '../api/models/baseline-response';
import { ValidationResult } from '../api/models/validation-result';
import { ValidationStatusResponse } from '../api/models/validation-status-response';
import { messageFor } from '../core/problems/messages';
import { readProblem } from '../core/problems/problem';
import {
  DEFAULT_MAX_EFFORT_FACTOR,
  DEFAULT_MIN_EFFORT_FACTOR,
  DEFAULT_SEED,
  effortRangeError,
  seedError,
  validationErrorLabel,
} from './labels';
import { wizardStep, WizardStep } from './wizard-step';

/** O que a tela carrega do servidor: aguardando, pronta, área desligada neste ambiente, ou falha de rede/servidor. */
export type ValidationLoad = 'loading' | 'ready' | 'unavailable' | 'failed';

/** O que a tela está fazendo com a API. Enquanto não for `idle`, os botões de ação ficam desabilitados. */
export type ValidationBusy =
  'idle' | 'loading-dataset' | 'saving-baseline' | 'capturing' | 'comparing';

/** Os parâmetros do realizado simulado digitados pelo gestor (PRD 7.3, passo 5). */
export interface ComparisonParams {
  readonly minEffortFactor: number;
  readonly maxEffortFactor: number;
  readonly seed: number;
}

/**
 * Estado do assistente de validação (RF-24). A etapa vem do status que o servidor registrou, então recarregar a
 * página nunca leva o gestor a um passo que a API já desfez. Os dados digitados ficam só na memória da tela: as
 * datas da linha de base, a confirmação da carga e os parâmetros da comparação. Nada disso é gravado sem o gestor
 * acionar o botão da etapa.
 */
@Injectable()
export class ValidationStore {
  private readonly http = inject(HttpClient);
  private readonly root = inject(ApiConfiguration).rootUrl;

  readonly status = signal<ValidationStatusResponse | null>(null);
  readonly load = signal<ValidationLoad>('loading');

  /** A etapa atual, ou `null` enquanto o status não chegou. */
  readonly step = computed<WizardStep | null>(() => {
    const current = this.status();
    return current === null ? null : wizardStep(current);
  });

  /** As demandas do conjunto na ordem de prioridade, com a linha de base já gravada (se houver). */
  readonly baseline = signal<BaselineResponse[]>([]);
  /** As datas digitadas na linha de base, por `demandId`. Só vão à API quando o gestor salvar. */
  readonly dates = signal<Record<string, string>>({});
  /** O gestor marcou que entende que a carga substitui todo o planejamento. */
  readonly confirmed = signal(false);
  readonly params = signal<ComparisonParams>({
    minEffortFactor: DEFAULT_MIN_EFFORT_FACTOR,
    maxEffortFactor: DEFAULT_MAX_EFFORT_FACTOR,
    seed: DEFAULT_SEED,
  });

  readonly busy = signal<ValidationBusy>('idle');
  /** A última mensagem para o gestor (sucesso, aviso ou falha de uma ação). */
  readonly message = signal<string | null>(null);
  readonly result = signal<ValidationResult | null>(null);

  /** Quantas demandas ainda estão sem data na linha de base (a etapa só avança com zero). */
  readonly missingDates = computed(
    () => this.baseline().filter((item) => !this.dates()[item.demandId]).length,
  );

  /** Busca o status do servidor. Um 404 significa que a área está desligada; outras falhas pedem "Tentar de novo". */
  refresh(): void {
    if (this.status() === null) {
      this.load.set('loading');
    }

    getValidationStatus(this.http, this.root).subscribe({
      next: (response) => {
        this.status.set(response.body);
        this.load.set('ready');
      },
      error: (error: unknown) => {
        const notFound = error instanceof HttpErrorResponse && error.status === 404;
        this.load.set(notFound ? 'unavailable' : 'failed');
      },
    });
  }

  /** Busca a linha de base e preenche as datas já gravadas. */
  loadBaseline(): void {
    getValidationBaseline(this.http, this.root).subscribe({
      next: (response) => {
        this.baseline.set(response.body);
        const dates: Record<string, string> = {};
        for (const item of response.body) {
          if (item.baselineDate) {
            dates[item.demandId] = item.baselineDate;
          }
        }
        this.dates.set(dates);
      },
      error: (error: unknown) =>
        this.message.set(
          messageFor(readProblem(error), 'Não foi possível carregar a linha de base.'),
        ),
    });
  }

  /** Guarda a data digitada para uma demanda. */
  setDate(demandId: string, date: string): void {
    this.dates.update((current) => ({ ...current, [demandId]: date }));
  }

  /** Carrega o conjunto fictício (RF-24, passo 1). Sem a confirmação marcada, não chama a API. */
  loadDataset(): void {
    if (!this.confirmed()) {
      this.message.set('Marque a confirmação antes de carregar o conjunto.');
      return;
    }

    this.busy.set('loading-dataset');
    loadValidationDataset(this.http, this.root, { body: { confirmReplaceAll: true } }).subscribe({
      next: (response) => {
        const loaded = response.body;
        this.message.set(
          `Conjunto carregado: ${loaded.teams} times, ${loaded.people} pessoas, ${loaded.demands} demandas.`,
        );
        this.confirmed.set(false);
        this.result.set(null);
        this.dates.set({});
        this.busy.set('idle');
        this.refresh();
      },
      error: (error: unknown) => {
        this.message.set(messageFor(readProblem(error), 'Não foi possível carregar o conjunto.'));
        this.busy.set('idle');
      },
    });
  }

  /** Grava a linha de base de todas as demandas (RF-24, passo 2). Com alguma sem data, não chama a API. */
  saveBaseline(): void {
    const missing = this.missingDates();
    if (missing > 0) {
      this.message.set(`Faltam ${missing} datas na linha de base.`);
      return;
    }

    const dates = this.dates();
    this.busy.set('saving-baseline');
    const body = {
      baselines: this.baseline().map((item) => ({
        demandId: item.demandId,
        baselineDate: dates[item.demandId],
      })),
    };
    putValidationBaseline(this.http, this.root, { body }).subscribe({
      next: () => {
        this.message.set('Linha de base gravada.');
        this.busy.set('idle');
        this.refresh();
        this.loadBaseline();
      },
      error: (error: unknown) => {
        this.message.set(
          messageFor(readProblem(error), 'Não foi possível gravar a linha de base.'),
        );
        this.busy.set('idle');
      },
    });
  }

  /**
   * Guarda o planejamento do processo atual (RF-24, passo 3). Se ele já foi guardado, a API responde 409 com o
   * código `validation-current-plan-exists`: é uma informação, não uma falha.
   */
  capturePlan(): void {
    this.busy.set('capturing');
    captureCurrentPlan(this.http, this.root).subscribe({
      next: () => {
        this.message.set('Planejamento atual guardado.');
        this.busy.set('idle');
        this.refresh();
      },
      error: (error: unknown) => {
        const problem = readProblem(error);
        this.message.set(
          validationErrorLabel(problem.code) ??
            messageFor(problem, 'Não foi possível guardar o planejamento atual.'),
        );
        this.busy.set('idle');
        if (problem.code === 'validation-current-plan-exists') {
          this.refresh();
        }
      },
    });
  }

  /** Atualiza um parâmetro digitado (faixa de esforço ou semente). */
  setParam(name: keyof ComparisonParams, value: number): void {
    this.params.update((current) => ({ ...current, [name]: value }));
  }

  /**
   * Compara os dois planejamentos (RF-24, passos 5 a 7). Os parâmetros são conferidos antes do envio; a comparação
   * não grava nada, e o resultado fica só na tela.
   */
  evaluate(): void {
    const { minEffortFactor, maxEffortFactor, seed } = this.params();
    const problem = effortRangeError(minEffortFactor, maxEffortFactor) ?? seedError(seed);
    if (problem !== null) {
      this.message.set(problem);
      return;
    }

    this.busy.set('comparing');
    evaluateValidation(this.http, this.root, {
      body: { minEffortFactor, maxEffortFactor, seed },
    }).subscribe({
      next: (response) => {
        this.result.set(response.body);
        this.message.set(null);
        this.busy.set('idle');
      },
      error: (error: unknown) => {
        const failure = readProblem(error);
        this.message.set(
          validationErrorLabel(failure.code) ??
            messageFor(failure, 'Não foi possível fazer a comparação.'),
        );
        this.busy.set('idle');
      },
    });
  }
}
