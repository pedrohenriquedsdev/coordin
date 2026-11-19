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
  /** O número em destaque do cartão (`null` quando a área não tem um número vivo). */
  readonly stat: string | null;
  readonly statLabel: string | null;
  /** Qual par de cores de destaque (índice em `ACCENTS`). */
  readonly accent: number;
  /** Ocupa duas colunas nas telas largas. */
  readonly wide: boolean;
  /** Um mini-gráfico decorativo (sempre acompanhado do número e do texto do cartão). */
  readonly visual: 'bars' | 'progress' | 'occupancy' | null;
}

/** Pares de cores dos cartões (ciano e azul do sistema). */
const ACCENTS: readonly (readonly [string, string])[] = [
  ['#1ec1cb', '#2f8cff'],
  ['#66d9df', '#1ec1cb'],
  ['#4aa8ff', '#1d6fe0'],
  ['#1ec1cb', '#0d8a93'],
];

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

  protected readonly accents = ACCENTS;

  protected readonly shortcuts = computed<readonly Shortcut[]>(() => {
    const active = this.store.activeCount();
    const done = this.store.completedCount();
    const teams = this.store.teams();
    const holidays = this.store.holidays();
    const margin = this.store.margin();
    const plural = (n: number, one: string, many: string) => (n === 1 ? one : many);

    const list: Shortcut[] = [
      {
        path: '/',
        icon: 'schedule',
        title: 'Cronograma',
        text: 'Datas de entrega de cada demanda, com e sem margem de segurança.',
        stat: active === null ? null : String(active),
        statLabel:
          active === null ? null : plural(active, 'demanda em andamento', 'demandas em andamento'),
        accent: 0,
        wide: true,
        visual: 'bars',
      },
      {
        path: '/demands',
        icon: 'demands',
        title: 'Demandas',
        text: 'Cadastre, priorize na fila e conclua demandas.',
        stat: active === null ? null : String(active),
        statLabel:
          active === null || done === null
            ? null
            : `${plural(active, 'ativa', 'ativas')} · ${done} ${plural(done, 'concluída', 'concluídas')}`,
        accent: 3,
        wide: true,
        visual: 'progress',
      },
      {
        path: '/scenarios',
        icon: 'scenarios',
        title: 'Cenários',
        text: 'Compare ordens de execução e aprove só o que fizer sentido.',
        stat: null,
        statLabel: null,
        accent: 2,
        wide: false,
        visual: null,
      },
      {
        path: '/reports/teams',
        icon: 'report',
        title: 'Relatório por time',
        text: 'Ocupação, ociosidade e sobrecarga por semana.',
        stat: teams.state === 'ready' ? String(teams.value.teams) : null,
        statLabel: teams.state === 'ready' ? plural(teams.value.teams, 'time', 'times') : null,
        accent: 1,
        wide: false,
        visual: 'occupancy',
      },
      {
        path: '/reports/accuracy',
        icon: 'accuracy',
        title: 'Precisão',
        text: 'Previsto × realizado nas demandas concluídas.',
        stat: null,
        statLabel: null,
        accent: 0,
        wide: false,
        visual: null,
      },
      {
        path: '/teams',
        icon: 'teams',
        title: 'Times e pessoas',
        text: 'Times, pessoas e a capacidade diária de cada um.',
        stat: teams.state === 'ready' ? String(teams.value.people) : null,
        statLabel:
          teams.state === 'ready'
            ? `${plural(teams.value.people, 'pessoa', 'pessoas')} em ${teams.value.teams} ${plural(teams.value.teams, 'time', 'times')}`
            : null,
        accent: 2,
        wide: false,
        visual: null,
      },
      {
        path: '/calendar',
        icon: 'calendar',
        title: 'Calendário',
        text: 'Feriados do setor e ausências das pessoas.',
        stat: holidays.state === 'ready' ? String(holidays.value.length) : null,
        statLabel:
          holidays.state === 'ready'
            ? plural(holidays.value.length, 'feriado cadastrado', 'feriados cadastrados')
            : null,
        accent: 1,
        wide: true,
        visual: null,
      },
      {
        path: '/settings',
        icon: 'settings',
        title: 'Configurações',
        text: 'A margem de segurança aplicada às previsões.',
        stat:
          margin.state === 'ready'
            ? `${margin.value.toLocaleString('pt-BR', { maximumFractionDigits: 2 })}%`
            : null,
        statLabel: margin.state === 'ready' ? 'de margem de segurança' : null,
        accent: 3,
        wide: true,
        visual: null,
      },
    ];

    if (this.availability.available()) {
      list.push({
        path: '/validation',
        icon: 'validation',
        title: 'Validação',
        text: 'Simule e compare o planejamento atual com o Coordin.',
        stat: null,
        statLabel: null,
        accent: 0,
        wide: false,
        visual: null,
      });
    }

    return list;
  });

  /** Barras do mini-gráfico do cronograma: entregas por semana, de 0 a 100% da maior. */
  protected readonly miniBars = computed(() => {
    const d = this.deliveries();
    const totals = d.onTime.map((count, index) => count + d.late[index]);
    const max = Math.max(...totals, 1);
    return totals.map((total) => Math.max((total / max) * 100, total > 0 ? 18 : 6));
  });

  /** Fatia das demandas ativas no total (ativas + concluídas), para a barra segmentada. */
  protected readonly activeShare = computed(() => {
    const active = this.store.activeCount() ?? 0;
    const done = this.store.completedCount() ?? 0;
    return active + done === 0 ? 0 : (active / (active + done)) * 100;
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
}
