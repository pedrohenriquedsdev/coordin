import { DecimalPipe } from '@angular/common';
import { Component, effect, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { messageFor } from '../core/problems/messages';
import { readProblem } from '../core/problems/problem';
import { PeopleSection } from './people-section';
import { TeamsStore } from './teams.store';

/** "1 pessoa", "4 pessoas". */
function peopleLabel(count: number): string {
  return count === 1 ? '1 pessoa' : `${count} pessoas`;
}

/** Tela "Times e pessoas" (protótipo T07; RF-01, RF-02): lista de times à esquerda, o time escolhido à direita. */
@Component({
  selector: 'app-teams-page',
  imports: [ReactiveFormsModule, DecimalPipe, PeopleSection],
  providers: [TeamsStore],
  templateUrl: './teams-page.html',
})
export class TeamsPage {
  protected readonly store = inject(TeamsStore);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly newTeamForm = this.fb.group({ name: ['', Validators.required] });
  protected readonly renameForm = this.fb.group({ name: ['', Validators.required] });

  protected readonly creating = signal(false);
  protected readonly confirmingDelete = signal(false);
  protected readonly saving = signal(false);
  protected readonly message = signal<string | null>(null);

  protected readonly peopleLabel = peopleLabel;

  constructor() {
    this.store.load();

    // Ao escolher outro time (ou recarregar), o campo de nome mostra o nome dele e os avisos somem.
    effect(() => {
      const team = this.store.selectedTeam();
      this.renameForm.reset({ name: team?.name ?? '' });
      this.confirmingDelete.set(false);
      this.message.set(null);
    });
  }

  protected startNew(): void {
    this.newTeamForm.reset({ name: '' });
    this.message.set(null);
    this.creating.set(true);
  }

  protected cancelNew(): void {
    this.message.set(null);
    this.creating.set(false);
  }

  protected saveNew(): void {
    if (this.newTeamForm.invalid) {
      this.newTeamForm.markAllAsTouched();
      return;
    }

    this.run(
      this.store.createTeam(this.newTeamForm.getRawValue().name.trim()),
      'Não foi possível salvar o time.',
      () => this.creating.set(false),
    );
  }

  protected saveName(): void {
    const team = this.store.selectedTeam();
    if (team === null) {
      return;
    }

    if (this.renameForm.invalid) {
      this.renameForm.markAllAsTouched();
      return;
    }

    this.run(
      this.store.renameTeam(team.id, this.renameForm.getRawValue().name.trim()),
      'Não foi possível renomear o time.',
    );
  }

  protected confirmDelete(): void {
    const team = this.store.selectedTeam();
    if (team !== null) {
      this.run(this.store.deleteTeam(team.id), 'Não foi possível excluir o time.');
    }
  }

  /** Executa uma escrita, mostrando a mensagem do erro (e nada mais é perdido) se ela falhar. */
  private run(action: Observable<void>, fallback: string, onDone?: () => void): void {
    this.saving.set(true);
    this.message.set(null);

    action.subscribe({
      error: (error: unknown) => {
        this.saving.set(false);
        this.message.set(messageFor(readProblem(error), fallback));
      },
      complete: () => {
        this.saving.set(false);
        onDone?.();
      },
    });
  }
}
