import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { formatCalendarDate } from '../core/dates/calendar-date';
import { ChartComponent } from '../charts/chart.component';
import { CountUp, GrowBar, HubTilt, Reveal } from '../core/ui/hub-motion';
import { Icon, type IconName } from '../core/ui/icon';
import { Loader } from '../core/ui/loader';
import { ThemeService } from '../core/ui/theme.service';
import { ValidationAvailability } from '../validation/validation-availability';
import { buildDeliveriesChart, buildLoadChart, DARK_CHART, LIGHT_CHART } from './home-charts';
import { HomeCalendar } from './home-calendar';
import { buildTimeline, teamOccupancy, weeklyDeliveries, weeklyLoad } from './home-data';
import { HomeTimeline } from './home-timeline';
import { HomeStore, Loadable } from './home.store';

interface Shortcut {
  readonly path: string;
  readonly icon: IconName;
  readonly title: string;
  readonly text: string;
  /** O número vivo do cartão; `null` quando o atalho não tem um. */
  readonly metric: string | null;
}

interface Indicator {
  readonly label: string;
  readonly value: string;
  /** Quando o valor é um número, ele "conta" de 0 até aqui; `null` para datas e textos. */
  readonly numeric: number | null;
  readonly hint: string;
  readonly icon: IconName;
  /** Verdadeiro quando o número pede atenção do gestor (sempre também escrito em `hint`). */
  readonly attention: boolean;
}

/**
 * Tela inicial: um painel (HUD) com os números que importam e atalhos para cada área, todos em texto.
 * Os números são só leitura e vêm da API; nenhuma decisão é tomada aqui (o gestor decide).
 */
@Component({
  selector: 'app-home-page',
  imports: [
    RouterLink,
    Icon,
    HubTilt,
    Reveal,
    Loader,
    CountUp,
    GrowBar,
    ChartComponent,
    HomeCalendar,
    HomeTimeline,
  ],
  providers: [HomeStore],
  templateUrl: './home-page.html',
})
export class HomePage {
  protected readonly store = inject(HomeStore);
  private readonly theme = inject(ThemeService);
  private readonly availability = inject(ValidationAvailability);

  protected readonly indicators = computed<readonly Indicator[]>(() => {
    const unpredictable = this.store.unpredictableCount();
    const late = this.store.lateCount();
    const sectorEnd = this.store.sectorEnd();
    return [
      {
        label: 'Prazo do setor',
        value: this.show(this.store.schedule(), sectorEnd === null ? '—' : this.date(sectorEnd)),
        numeric: null,
        hint: 'Última entrega prevista, com margem',
        icon: 'schedule',
        attention: false,
      },
      {
        label: 'Demandas ativas',
        value: this.count(this.store.activeCount()),
        numeric: this.store.activeCount(),
        hint: 'Ainda não concluídas',
        icon: 'demands',
        attention: false,
      },
      {
        label: 'Sem previsão',
        value: this.count(unpredictable),
        numeric: unpredictable,
        hint: unpredictable ? 'Faltam dados para prever' : 'Todas têm previsão',
        icon: 'accuracy',
        attention: (unpredictable ?? 0) > 0,
      },
      {
        label: 'Passam do prazo informado',
        value: this.count(late),
        numeric: late,
        hint: late ? 'A previsão passa do prazo desejado' : 'Nenhuma passa do prazo',
        icon: 'validation',
        attention: (late ?? 0) > 0,
      },
    ];
  });

  protected readonly shortcuts = computed<readonly Shortcut[]>(() => {
    const active = this.store.activeCount();
    const done = this.store.completedCount();
    const teams = this.store.teams();
    const list: Shortcut[] = [
      {
        path: '/',
        icon: 'schedule',
        title: 'Cronograma',
        text: 'Datas de entrega, com e sem margem.',
        metric: active === null ? null : `${active} em andamento`,
      },
      {
        path: '/demands',
        icon: 'demands',
        title: 'Demandas',
        text: 'Cadastre, priorize e conclua demandas.',
        metric: active === null || done === null ? null : `${active} ativas · ${done} concluídas`,
      },
      {
        path: '/scenarios',
        icon: 'scenarios',
        title: 'Cenários',
        text: 'Compare ordens de execução e decida.',
        metric: null,
      },
      {
        path: '/reports/teams',
        icon: 'report',
        title: 'Relatório por time',
        text: 'Ocupação, ociosidade e sobrecarga por semana.',
        metric: teams.state === 'ready' ? `${teams.value.teams} times` : null,
      },
      {
        path: '/reports/accuracy',
        icon: 'accuracy',
        title: 'Precisão',
        text: 'Previsto × realizado nas demandas concluídas.',
        metric: null,
      },
      {
        path: '/teams',
        icon: 'teams',
        title: 'Times e pessoas',
        text: 'Times, pessoas e capacidade diária.',
        metric:
          teams.state === 'ready'
            ? `${teams.value.teams} times · ${teams.value.people} pessoas`
            : null,
      },
      {
        path: '/calendar',
        icon: 'calendar',
        title: 'Calendário',
        text: 'Feriados do setor e ausências das pessoas.',
        metric: this.holidayMetric(),
      },
      {
        path: '/settings',
        icon: 'settings',
        title: 'Configurações',
        text: 'Margem de segurança das previsões.',
        metric: this.marginMetric(),
      },
    ];

    if (this.availability.available()) {
      list.push({
        path: '/validation',
        icon: 'validation',
        title: 'Validação',
        text: 'Simule e compare o planejamento atual com o Coordin.',
        metric: null,
      });
    }

    return list;
  });

  protected readonly today = this.store.today;

  /** O time escolhido no gráfico de carga (`null` = o setor inteiro). */
  protected readonly selectedTeam = signal<string | null>(null);

  protected readonly chartTheme = computed(() => (this.theme.isDark() ? DARK_CHART : LIGHT_CHART));

  protected readonly teamOptions = computed(() => {
    const report = this.store.report();
    return report.state === 'ready'
      ? report.value.teams.map((team) => ({ id: team.teamId, name: team.teamName }))
      : [];
  });

  protected readonly loadWeeks = computed(() => {
    const report = this.store.report();
    return report.state === 'ready' ? weeklyLoad(report.value, this.selectedTeam()) : [];
  });
  protected readonly loadChart = computed(() =>
    buildLoadChart(this.loadWeeks(), this.chartTheme()),
  );

  private readonly scheduleItems = computed(() => {
    const schedule = this.store.schedule();
    return schedule.state === 'ready' ? schedule.value.items : [];
  });

  protected readonly deliveries = computed(() =>
    weeklyDeliveries(this.scheduleItems(), this.today, 8),
  );
  protected readonly deliveriesChart = computed(() =>
    buildDeliveriesChart(this.deliveries(), this.chartTheme()),
  );

  protected readonly timeline = computed(() => buildTimeline(this.scheduleItems(), this.today, 8));

  protected readonly occupancy = computed(() => {
    const report = this.store.report();
    return report.state === 'ready' ? teamOccupancy(report.value) : [];
  });

  protected readonly calendarHolidays = computed(() => {
    const holidays = this.store.holidays();
    return holidays.state === 'ready' ? holidays.value : [];
  });
  protected readonly calendarAbsences = computed(() => {
    const absences = this.store.absences();
    return absences.state === 'ready' ? absences.value : [];
  });
  protected readonly calendarItems = this.scheduleItems;

  protected readonly reportFailed = computed(() => this.store.report().state === 'failed');
  protected readonly scheduleFailed = computed(() => this.store.schedule().state === 'failed');

  protected percent(value: number | null): string {
    return value === null ? 'sem capacidade' : `${Math.round(value * 100)}%`;
  }

  protected barWidth(value: number | null): number {
    return value === null ? 0 : Math.min(value * 100, 100);
  }

  constructor() {
    this.store.load();
  }

  private date(value: string): string {
    return formatCalendarDate(value);
  }

  private count(value: number | null): string {
    return value === null ? this.unavailable() : String(value);
  }

  /** Enquanto carrega mostra reticências; depois, o valor ou "indisponível". */
  private show<T>(data: Loadable<T>, ready: string): string {
    if (data.state === 'ready') {
      return ready;
    }
    return data.state === 'loading' ? '…' : 'indisponível';
  }

  private unavailable(): string {
    return this.store.schedule().state === 'loading' ? '…' : 'indisponível';
  }

  private holidayMetric(): string | null {
    const holidays = this.store.holidays();
    if (holidays.state !== 'ready') {
      return null;
    }
    const count = holidays.value.length;
    return count === 1 ? '1 feriado' : `${count} feriados`;
  }

  private marginMetric(): string | null {
    const margin = this.store.margin();
    return margin.state === 'ready'
      ? `Margem de ${margin.value.toLocaleString('pt-BR', { maximumFractionDigits: 2 })}%`
      : null;
  }
}
