import { Component, computed, inject, signal } from '@angular/core';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { AbsenceResponse } from '../api/models/absence-response';
import { AbsenceType } from '../api/models/absence-type';
import { CalendarDatePipe } from '../core/dates/calendar-date.pipe';
import { isEndBeforeStart } from '../core/dates/calendar-date';
import { messageFor } from '../core/problems/messages';
import { readProblem } from '../core/problems/problem';
import { absenceTypeLabel } from './absence-type-label';
import { CalendarStore } from './calendar.store';

/** O fim de uma ausência não pode ser anterior ao início. As datas são texto `yyyy-MM-dd`, sem `Date`. */
function periodValidator(group: AbstractControl): ValidationErrors | null {
  const start = group.get('startDate')?.value as string;
  const end = group.get('endDate')?.value as string;
  return start && end && isEndBeforeStart(start, end) ? { endBeforeStart: true } : null;
}

/**
 * As ausências das pessoas (RF-03; T08): tabela com filtro por time e formulário de registro. Só o tipo
 * e o período são pedidos, nunca o motivo (RNF-05).
 */
@Component({
  selector: 'app-absences-section',
  imports: [ReactiveFormsModule, CalendarDatePipe],
  templateUrl: './absences-section.html',
})
export class AbsencesSection {
  protected readonly store = inject(CalendarStore);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly types: readonly AbsenceType[] = ['Vacation', 'Other'];
  protected readonly typeLabel = absenceTypeLabel;

  protected readonly teamFilter = signal('');
  protected readonly visible = computed(() => {
    const team = this.teamFilter();
    return team === ''
      ? this.store.absences()
      : this.store.absences().filter((item) => item.teamId === team);
  });

  protected readonly form = this.fb.group(
    {
      personId: ['', Validators.required],
      type: this.fb.control<AbsenceType>('Vacation', Validators.required),
      startDate: ['', Validators.required],
      endDate: ['', Validators.required],
    },
    { validators: periodValidator },
  );

  protected readonly editingId = signal<string | null>(null);
  protected readonly deletingId = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly message = signal<string | null>(null);

  protected setFilter(event: Event): void {
    this.teamFilter.set((event.target as HTMLSelectElement).value);
  }

  protected edit(absence: AbsenceResponse): void {
    this.message.set(null);
    this.deletingId.set(null);
    this.editingId.set(absence.id);
    this.form.setValue({
      personId: absence.personId,
      type: absence.type,
      startDate: absence.startDate,
      endDate: absence.endDate,
    });
    this.form.controls.personId.disable(); // a ausência é de uma pessoa; editar não a troca
  }

  protected cancelEdit(): void {
    this.reset();
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const body = { type: value.type, startDate: value.startDate, endDate: value.endDate };
    const editing = this.editingId();

    this.saving.set(true);
    this.message.set(null);
    const action =
      editing === null
        ? this.store.createAbsence(value.personId, body)
        : this.store.updateAbsence(editing, body);

    action.subscribe({
      error: (error: unknown) => {
        this.saving.set(false);
        this.message.set(messageFor(readProblem(error), 'Não foi possível salvar a ausência.'));
      },
      complete: () => {
        this.saving.set(false);
        this.reset();
      },
    });
  }

  protected confirmDelete(absence: AbsenceResponse): void {
    this.saving.set(true);
    this.message.set(null);

    this.store.deleteAbsence(absence.id).subscribe({
      error: (error: unknown) => {
        this.saving.set(false);
        this.message.set(messageFor(readProblem(error), 'Não foi possível excluir a ausência.'));
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
    this.form.controls.personId.enable();
    this.form.reset({ personId: '', type: 'Vacation', startDate: '', endDate: '' });
  }
}
