import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DemandRequest } from '../api/models/demand-request';
import { messageFor } from '../core/problems/messages';
import { readProblem } from '../core/problems/problem';
import { Demand } from '../planning/planning';
import { DemandFormStore } from './demand-form.store';

/** Texto do esforço inválido (RF-06): zero, negativo ou com mais de duas casas. */
const EFFORT_INVALID = 'O esforço deve ser maior que zero, com no máximo 2 casas decimais.';
const TITLE_REQUIRED = 'Informe o título da demanda.';
const TITLE_TOO_LONG = 'O título deve ter no máximo 200 caracteres.';

/** Lê um esforço digitado em horas: `''` é "ainda sem estimativa" (`null`); senão, precisa ser > 0 com até 2 casas. */
export function parseEffort(text: string): { value: number | null; valid: boolean } {
  const trimmed = text.trim();
  if (trimmed === '') {
    return { value: null, valid: true };
  }

  const normalized = trimmed.replace(',', '.');
  if (!/^\d+(\.\d{1,2})?$/.test(normalized)) {
    return { value: null, valid: false };
  }

  const value = Number(normalized);
  return value > 0 ? { value, valid: true } : { value: null, valid: false };
}

/**
 * Formulário de demanda (T04; RF-04 a RF-07). Os times entram marcados com um campo de horas (vazio =
 * ainda sem estimativa). As pessoas oferecidas são só as dos times marcados; ao desmarcar um time, as
 * pessoas dele saem da alocação (RF-05).
 */
@Component({
  selector: 'app-demand-form-page',
  imports: [ReactiveFormsModule, RouterLink],
  providers: [DemandFormStore],
  templateUrl: './demand-form-page.html',
})
export class DemandFormPage {
  protected readonly store = inject(DemandFormStore);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly router = inject(Router);
  private readonly demandId = inject(ActivatedRoute).snapshot.paramMap.get('id');

  protected readonly editing = this.demandId !== null;
  protected readonly form = this.fb.group({
    title: ['', Validators.required],
    startDate: [''],
    informedEndDate: [''],
  });

  /** Times marcados, com o texto das horas de cada um (`''` = sem estimativa). */
  protected readonly selected = signal<Record<string, string>>({});
  protected readonly assignedPersonIds = signal<string[]>([]);
  protected readonly locked = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly effortError = signal<string | null>(null);
  protected readonly saving = signal(false);

  /** As pessoas dos times marcados, na ordem da lista de pessoas. */
  protected readonly offeredPeople = computed(() =>
    this.store.people().filter((person) => this.isChecked(person.teamId)),
  );

  constructor() {
    this.reload();
  }

  /** Carrega (ou recarrega, no "Tentar de novo") os times, as pessoas e, na edição, a demanda. */
  protected reload(): void {
    this.store.load(this.demandId).subscribe({
      next: (demand) => {
        if (demand !== null) {
          this.fill(demand);
        }
      },
    });
  }

  protected isChecked(teamId: string): boolean {
    return Object.hasOwn(this.selected(), teamId);
  }

  protected isAssigned(personId: string): boolean {
    return this.assignedPersonIds().includes(personId);
  }

  protected toggleTeam(teamId: string, checked: boolean): void {
    if (checked) {
      this.selected.update((current) => ({ ...current, [teamId]: '' }));
      return;
    }

    const next = { ...this.selected() };
    delete next[teamId];
    this.selected.set(next);
    const removedPeople = new Set(
      this.store
        .people()
        .filter((p) => p.teamId === teamId)
        .map((p) => p.id),
    );
    this.assignedPersonIds.update((ids) => ids.filter((id) => !removedPeople.has(id)));
  }

  protected setHours(teamId: string, text: string): void {
    this.selected.update((current) => ({ ...current, [teamId]: text }));
    this.effortError.set(null);
  }

  protected togglePerson(personId: string, checked: boolean): void {
    this.assignedPersonIds.update((ids) =>
      checked ? [...ids, personId] : ids.filter((id) => id !== personId),
    );
  }

  protected hoursOf(teamId: string): string {
    return this.selected()[teamId] ?? '';
  }

  protected save(): void {
    this.message.set(null);
    this.effortError.set(null);
    if (this.form.controls.title.invalid) {
      this.message.set(TITLE_REQUIRED);
      return;
    }

    const title = this.form.controls.title.value.trim();
    if (title === '') {
      this.message.set(TITLE_REQUIRED);
      return;
    }

    if (title.length > 200) {
      this.message.set(TITLE_TOO_LONG);
      return;
    }

    const teams: DemandRequest['teams'] = [];
    for (const [teamId, text] of Object.entries(this.selected())) {
      const effort = parseEffort(text);
      if (!effort.valid) {
        this.effortError.set(EFFORT_INVALID);
        return;
      }

      teams.push({ teamId, estimatedEffortHours: effort.value });
    }

    const { startDate, informedEndDate } = this.form.getRawValue();
    const body: DemandRequest = {
      title,
      startDate: startDate === '' ? null : startDate,
      informedEndDate: informedEndDate === '' ? null : informedEndDate,
      teams,
      assignedPersonIds: this.assignedPersonIds(),
    };

    this.saving.set(true);
    this.store.save(body, this.demandId).subscribe({
      complete: () => {
        this.saving.set(false);
        this.router.navigateByUrl('/demands');
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.message.set(messageFor(readProblem(error), 'Não foi possível salvar a demanda.'));
      },
    });
  }

  private fill(demand: Demand): void {
    if (demand.completedOn !== null) {
      this.locked.set(true);
      return;
    }

    this.form.setValue({
      title: demand.title,
      startDate: demand.startDate ?? '',
      informedEndDate: demand.informedEndDate ?? '',
    });
    this.selected.set(
      Object.fromEntries(
        demand.teams.map((team) => [
          team.teamId,
          team.estimatedEffortHours === null ? '' : String(team.estimatedEffortHours),
        ]),
      ),
    );
    this.assignedPersonIds.set([...demand.assignedPersonIds]);
  }
}
