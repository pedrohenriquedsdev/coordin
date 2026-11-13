import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HolidayResponse } from '../api/models/holiday-response';
import { CalendarDatePipe } from '../core/dates/calendar-date.pipe';
import { messageFor } from '../core/problems/messages';
import { readProblem } from '../core/problems/problem';
import { CalendarStore } from './calendar.store';

/** Os feriados do setor (RF-03; T08): em um feriado ninguém contribui com capacidade; a data é única. */
@Component({
  selector: 'app-holidays-section',
  imports: [ReactiveFormsModule, CalendarDatePipe],
  templateUrl: './holidays-section.html',
})
export class HolidaysSection {
  protected readonly store = inject(CalendarStore);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly form = this.fb.group({
    date: ['', Validators.required],
    description: ['', Validators.maxLength(200)],
  });

  protected readonly deletingId = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly message = signal<string | null>(null);

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { date, description } = this.form.getRawValue();
    this.saving.set(true);
    this.message.set(null);

    this.store.createHoliday({ date, description: description.trim() || null }).subscribe({
      error: (error: unknown) => {
        this.saving.set(false);
        this.message.set(messageFor(readProblem(error), 'Não foi possível salvar o feriado.'));
      },
      complete: () => {
        this.saving.set(false);
        this.form.reset({ date: '', description: '' });
      },
    });
  }

  protected confirmDelete(holiday: HolidayResponse): void {
    this.saving.set(true);
    this.message.set(null);

    this.store.deleteHoliday(holiday.id).subscribe({
      error: (error: unknown) => {
        this.saving.set(false);
        this.message.set(messageFor(readProblem(error), 'Não foi possível remover o feriado.'));
      },
      complete: () => {
        this.saving.set(false);
        this.deletingId.set(null);
      },
    });
  }
}
