import { Pipe, PipeTransform } from '@angular/core';
import { formatCalendarDate } from './calendar-date';

/** `{{ '2026-12-31' | calendarDate }}` → `31/12/2026`, sem fuso (ver `calendar-date.ts`). */
@Pipe({ name: 'calendarDate' })
export class CalendarDatePipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return value ? formatCalendarDate(value) : '';
  }
}
