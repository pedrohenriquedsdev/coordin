import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ChartComponent } from '../charts/chart.component';
import { CalendarDatePipe } from '../core/dates/calendar-date.pipe';
import { Clock, weeksBetween } from '../core/dates/calendar-date';
import { buildTeamChart } from './team-report-chart';
import {
  defaultPeriod,
  ReportTeam,
  ReportWeek,
  summarizeTeam,
  TeamSummary,
  weekLabel,
} from './team-report';
import { TeamReportStore } from './team-report.store';

/** Limite do período (DT-18): o mesmo da API. */
const MAX_WEEKS = 52;

/** Valida o período na tela, antes de chamar a API. Devolve a mensagem em português, ou `null` se estiver certo. */
export function validatePeriod(from: string, to: string): string | null {
  if (from === '' || to === '') {
    return 'Informe as duas datas do período.';
  }

  if (to < from) {
    return 'A data de fim não pode ser anterior à data de início.';
  }

  if (weeksBetween(from, to) > MAX_WEEKS) {
    return `O período pode ter no máximo ${MAX_WEEKS} semanas.`;
  }

  return null;
}

/** Ocupação em porcentagem inteira; sem capacidade, um travessão (não há o que medir). */
function percentOf(value: number | null): string {
  return value === null ? '—' : `${Math.round(value * 100)}%`;
}

/** Horas em pt-BR. */
function hoursOf(value: number): string {
  return `${value.toLocaleString('pt-BR', { maximumFractionDigits: 2 })} h`;
}

/**
 * Tela "Relatório por time" (T09, M03; RF-17): capacidade, horas alocadas, ocupação, ociosidade e sobrecarga
 * por time e por semana, com o gráfico e a tabela com os mesmos valores (DT-36).
 */
@Component({
  selector: 'app-team-report-page',
  imports: [RouterLink, ChartComponent, CalendarDatePipe],
  providers: [TeamReportStore],
  templateUrl: './team-report-page.html',
})
export class TeamReportPage {
  protected readonly store = inject(TeamReportStore);
  private readonly clock = inject(Clock);

  private readonly initial = defaultPeriod(this.clock.today());
  protected readonly from = signal(this.initial.from);
  protected readonly to = signal(this.initial.to);
  protected readonly message = signal<string | null>(null);
  protected readonly selectedTeamId = signal<string | null>(null);

  protected readonly rows = computed(() =>
    (this.store.report()?.teams ?? []).map((team) => ({
      teamId: team.teamId,
      summary: summarizeTeam(team),
    })),
  );

  protected readonly selectedTeam = computed<ReportTeam | null>(
    () => this.store.report()?.teams.find((team) => team.teamId === this.selectedTeamId()) ?? null,
  );

  /** A configuração do gráfico só muda quando o time escolhido ou o relatório muda. */
  protected readonly chart = computed(() => {
    const team = this.selectedTeam();
    return team === null ? null : buildTeamChart(team);
  });

  constructor() {
    this.search();
  }

  protected search(): void {
    const error = validatePeriod(this.from(), this.to());
    if (error !== null) {
      this.message.set(error);
      return;
    }

    this.message.set(null);
    this.store.load(this.from(), this.to()).subscribe({
      // A falha aparece no bloco de estado da tela, com "Tentar de novo": sem mensagem duplicada aqui.
      error: () => this.store.fail(),
    });
  }

  protected select(teamId: string): void {
    this.selectedTeamId.set(teamId);
  }

  protected setFrom(event: Event): void {
    this.from.set((event.target as HTMLInputElement).value);
  }

  protected setTo(event: Event): void {
    this.to.set((event.target as HTMLInputElement).value);
  }

  protected percent(value: number | null): string {
    return percentOf(value);
  }

  protected hours(value: number): string {
    return hoursOf(value);
  }

  protected labelOf(week: ReportWeek): string {
    return weekLabel(week);
  }

  protected unpredictableText(summary: TeamSummary): string {
    return summary.unpredictableDemandCount === 1
      ? '1 demanda não previsível (não entra nas horas alocadas)'
      : `${summary.unpredictableDemandCount} demandas não previsíveis (não entram nas horas alocadas)`;
  }
}
