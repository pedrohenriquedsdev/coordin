import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { formatCalendarDate } from '../core/dates/calendar-date';
import { Icon } from '../core/ui/icon';
import { Loader } from '../core/ui/loader';
import { PageHeader } from '../core/ui/page-header';
import { Reveal } from '../core/ui/hub-motion';
import { hoursText } from './demand-history';
import { DemandHistoryStore } from './demand-history.store';

export type TimelineFilter = 'all' | 'estimate' | 'forecast';

/**
 * Tela de detalhe de uma demanda (RF-20, RF-21): os dois históricos em ordem cronológica. Só lê; não há edição de
 * histórico, porque ele é somente de inclusão (DT-14). Reage a troca de demanda na mesma tela (voltar e avançar do
 * navegador entre dois detalhes), porque o Angular reaproveita o componente quando só o parâmetro muda.
 */
@Component({
  selector: 'app-demand-history-page',
  imports: [RouterLink, PageHeader, Icon, Loader, Reveal],
  providers: [DemandHistoryStore],
  templateUrl: './demand-history-page.html',
})
export class DemandHistoryPage {
  protected readonly store = inject(DemandHistoryStore);

  protected readonly filters: readonly { id: TimelineFilter; label: string }[] = [
    { id: 'all', label: 'Todos os eventos' },
    { id: 'estimate', label: 'Estimativas' },
    { id: 'forecast', label: 'Previsões' },
  ];
  protected readonly filter = signal<TimelineFilter>('all');

  protected readonly events = computed(() =>
    this.store
      .timeline()
      .filter((event) => this.filter() === 'all' || event.kind === this.filter()),
  );

  /** O resumo da demanda, só com o que a API trouxe (campo ausente vira travessão). */
  protected readonly facts = computed(() => {
    const d = this.store.summary();
    const teams = d.teams ?? [];
    const hours = teams
      .map((team) => team.estimatedEffortHours)
      .filter((value): value is number | string => value !== null);
    return {
      priority: d.priorityRank == null ? '—' : String(d.priorityRank),
      teams: teams.length === 0 ? '—' : teams.map((team) => team.teamName).join(', '),
      effort:
        hours.length === 0
          ? '—'
          : `${hoursText(hours.reduce<number>((sum, h) => sum + Number(h), 0))} h`,
      missing: teams.length - hours.length,
      start: d.startDate ? formatCalendarDate(d.startDate) : '—',
      end: d.informedEndDate ? formatCalendarDate(d.informedEndDate) : '—',
      completed: d.completedOn ? formatCalendarDate(d.completedOn) : null,
    };
  });

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
