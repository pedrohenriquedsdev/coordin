import { DecimalPipe } from '@angular/common';
import { Component, effect, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { readProblem } from '../core/problems/problem';
import { messageFor } from './messages';
import { Person, TeamsStore } from './teams.store';

/**
 * As pessoas do time escolhido (RF-02): adicionar, editar (inclusive mudar de time) e excluir.
 * A capacidade é em horas por dia útil, de 0 a 24; zero é aceito e significa "não contribui".
 * Estas validações só evitam idas inúteis ao servidor: quem decide é a API.
 */
@Component({
  selector: 'app-people-section',
  imports: [ReactiveFormsModule, DecimalPipe],
  templateUrl: './people-section.html',
})
export class PeopleSection {
  protected readonly store = inject(TeamsStore);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly form = this.fb.group({
    name: ['', Validators.required],
    dailyCapacityHours: this.fb.control<number | null>(null, [
      Validators.required,
      Validators.min(0),
      Validators.max(24),
    ]),
    teamId: [''],
  });

  protected readonly editingId = signal<string | null>(null);
  protected readonly deletingId = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly message = signal<string | null>(null);

  constructor() {
    // Ao escolher outro time, o formulário volta ao estado de "adicionar".
    effect(() => {
      this.store.selectedId();
      this.reset();
    });
  }

  protected edit(person: Person): void {
    this.message.set(null);
    this.deletingId.set(null);
    this.editingId.set(person.id);
    this.form.setValue({
      name: person.name,
      dailyCapacityHours: person.dailyCapacityHours,
      teamId: person.teamId,
    });
  }

  protected cancelEdit(): void {
    this.reset();
  }

  protected submit(): void {
    const team = this.store.selectedTeam();
    if (team === null) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const editing = this.editingId();
    const input = {
      name: value.name.trim(),
      teamId: editing === null ? team.id : value.teamId,
      dailyCapacityHours: value.dailyCapacityHours ?? 0,
    };

    this.saving.set(true);
    this.message.set(null);
    const action =
      editing === null ? this.store.createPerson(input) : this.store.updatePerson(editing, input);

    action.subscribe({
      error: (error: unknown) => {
        this.saving.set(false);
        this.message.set(messageFor(readProblem(error), 'Não foi possível salvar a pessoa.'));
      },
      complete: () => {
        this.saving.set(false);
        this.reset();
      },
    });
  }

  protected confirmDelete(person: Person): void {
    this.saving.set(true);
    this.message.set(null);

    this.store.deletePerson(person.id).subscribe({
      error: (error: unknown) => {
        this.saving.set(false);
        this.message.set(messageFor(readProblem(error), 'Não foi possível excluir a pessoa.'));
      },
      complete: () => {
        this.saving.set(false);
        this.deletingId.set(null);
      },
    });
  }

  private reset(): void {
    this.editingId.set(null);
    this.deletingId.set(null);
    this.message.set(null);
    this.form.reset({ name: '', dailyCapacityHours: null, teamId: '' });
  }
}
