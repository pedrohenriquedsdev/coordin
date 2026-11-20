import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideApiConfiguration } from '../api/api-configuration';
import { CHART_FACTORY } from '../charts/chart.component';
import { createFakeChartFactory } from '../charts/chart-test-double';
import { Clock } from '../core/dates/calendar-date';
import { provideLocalePtBr } from '../core/locale';
import { TeamReportPage, validatePeriod } from './team-report-page';

/** Uma semana como a API a devolve (números em texto onde o cliente gerado os tipa assim). */
function week(start: string, end: string, overrides: Record<string, unknown> = {}) {
  return {
    weekStart: start,
    weekEnd: end,
    capacityHours: '40',
    allocatedHours: '0',
    occupancy: '0',
    idleHours: '40',
    isOverloaded: false,
    overloadedDemands: [],
    ...overrides,
  };
}

function team(id: string, name: string, weeks: unknown[], unpredictable = 0) {
  return { teamId: id, teamName: name, unpredictableDemandCount: String(unpredictable), weeks };
}

const REPORT = {
  periodStart: '2026-11-02',
  periodEnd: '2026-11-15',
  teams: [
    team(
      't1',
      'Web',
      [
        week('2026-11-02', '2026-11-08', {
          capacityHours: '80',
          allocatedHours: '60',
          occupancy: '0.75',
          idleHours: '20',
        }),
        week('2026-11-09', '2026-11-15', {
          capacityHours: '80',
          allocatedHours: '60',
          occupancy: '0.75',
          idleHours: '20',
        }),
      ],
      1,
    ),
    team('t2', 'Dados', [
      week('2026-11-02', '2026-11-08', { capacityHours: '0', occupancy: null, idleHours: '0' }),
    ]),
  ],
};

/** Estes testes rodam também com TZ=Asia/Tokyo e America/Los_Angeles: nenhuma data muda de dia (RNF-07). */
describe('TeamReportPage', () => {
  let fixture: ComponentFixture<TeamReportPage>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideLocalePtBr(),
        provideRouter([]),
        // O gráfico desenha em um dublê: jsdom não desenha em canvas.
        { provide: CHART_FACTORY, useValue: createFakeChartFactory().factory },
        // "Hoje" fixo: uma quarta-feira, para o período padrão ser sempre o mesmo.
        { provide: Clock, useValue: { now: () => 0, today: () => '2026-11-04' } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TeamReportPage);
  });

  afterEach(() => http.verify());

  const element = (): HTMLElement => fixture.nativeElement as HTMLElement;

  const request = (): TestRequest =>
    http.expectOne((req) => req.method === 'GET' && req.url === '/api/reports/teams');

  async function answer(body: object): Promise<void> {
    fixture.detectChanges();
    request().flush(body);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('opens on the period from the Monday of this week to the Sunday of the eighth week', () => {
    fixture.detectChanges();
    request().flush(REPORT);

    expect((element().querySelector('#report-from') as HTMLInputElement).value).toBe('2026-11-02');
    expect((element().querySelector('#report-to') as HTMLInputElement).value).toBe('2026-12-27');
  });

  it('refuses an end before the start, with the message, and calls nothing', () => {
    fixture.detectChanges();
    request().flush(REPORT);

    expect(validatePeriod('2026-11-10', '2026-11-03')).toBe(
      'A data de fim não pode ser anterior à data de início.',
    );
    expect(validatePeriod('2026-11-02', '')).toBe('Informe as duas datas do período.');
    expect(validatePeriod('2026-01-01', '2027-12-31')).toBe(
      'O período pode ter no máximo 52 semanas.',
    );
  });

  it('lists each team with its period occupancy as a percentage', async () => {
    await answer(REPORT);

    const table =
      element().querySelector('[aria-label="Resumo por time em tabela"]')?.textContent ?? '';
    expect(table).toContain('Web');
    expect(table).toContain('75%');
    expect(table).toContain('1 demanda não previsível');
  });

  it('shows a dash instead of a percentage for a team with no capacity', async () => {
    await answer(REPORT);

    const row = [
      ...element().querySelectorAll('[aria-label="Resumo por time em tabela"] tbody tr'),
    ].find((r) => r.textContent?.includes('Dados'));
    expect(row?.textContent).toContain('—');
  });

  it('shows the weeks of the chosen team, with the idle hours and the overload in words', async () => {
    await answer({
      ...REPORT,
      teams: [
        team('t1', 'Web', [
          week('2026-11-02', '2026-11-08', {
            capacityHours: '40',
            allocatedHours: '10',
            occupancy: '0.25',
            idleHours: '30',
          }),
          week('2026-11-09', '2026-11-15', {
            capacityHours: '40',
            allocatedHours: '40',
            occupancy: '1',
            idleHours: '0',
            isOverloaded: true,
            overloadedDemands: [{ id: 'd1', title: 'Checkout' }],
          }),
        ]),
      ],
    });

    const select = [...element().querySelectorAll('button')].find(
      (b) => b.textContent?.trim() === 'Ver semanas',
    );
    select?.click();
    fixture.detectChanges();

    const weeks = element().querySelector('[aria-label="Semanas em tabela"]')?.textContent ?? '';
    expect(weeks).toContain('25%');
    expect(weeks).toContain('30 h');
    expect(weeks).toContain('Sobrecarga');
    expect(weeks).toContain('Checkout');
  });

  it('keeps the chart and the week table on the same screen', async () => {
    await answer(REPORT);
    [...element().querySelectorAll('button')]
      .find((b) => b.textContent?.trim() === 'Ver semanas')
      ?.click();
    fixture.detectChanges();

    expect(element().querySelector('app-chart canvas')).not.toBeNull();
    expect(element().querySelector('[aria-label="Semanas em tabela"]')).not.toBeNull();
    expect(element().querySelector('[aria-label="Semanas em cartões"]')).not.toBeNull();
  });

  it('shows the dates of the report in the Brazilian format, the same in any zone', async () => {
    await answer(REPORT);

    expect(element().textContent).toContain('02/11/2026');
    expect(element().textContent).toContain('15/11/2026');
  });

  it('tells the manager when there are no teams, with a link to register them', async () => {
    await answer({ ...REPORT, teams: [] });

    expect(element().textContent).toContain('Ainda não há times para o relatório.');
    expect(element().querySelector('a[href="/teams"]')).not.toBeNull();
  });

  it('shows the failure with "Tentar de novo", which asks again', async () => {
    fixture.detectChanges();
    request().flush(null, { status: 500, statusText: 'Erro' });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(element().querySelector('[role="alert"]')?.textContent).toContain(
      'Não foi possível carregar o relatório.',
    );
    [...element().querySelectorAll('button')]
      .find((b) => b.textContent?.includes('Tentar de novo'))
      ?.click();
    request().flush(REPORT);
  });

  describe('presets, totals and the occupancy map', () => {
    it('sums every team for the period totals', async () => {
      await answer(REPORT);

      const totals = element().querySelector('[aria-label="Totais do período"]')?.textContent ?? '';
      expect(totals).toContain('160 h'); // 80 + 80 de capacidade do Web, 0 do Dados
      expect(totals).toContain('120 h'); // horas alocadas
      expect(totals).toContain('75%'); // 120 / 160
      expect(totals).toContain('Nenhuma semana com sobrecarga');
    });

    it('draws one button per team and week, described in words, and opens the team on click', async () => {
      await answer(REPORT);

      const cells = [
        ...element().querySelectorAll<HTMLButtonElement>('button[aria-label*="semana"]'),
      ];
      expect(cells).toHaveLength(3);
      expect(cells[0].getAttribute('aria-label')).toBe(
        'Web, semana 02/11 a 08/11: 75% de ocupação',
      );
      expect(cells[2].getAttribute('aria-label')).toContain('sem capacidade');

      cells[0].click();
      fixture.detectChanges();
      expect(element().textContent).toContain('Semanas de Web');
    });

    it('marks an overloaded week with text, not only with color', async () => {
      await answer({
        ...REPORT,
        teams: [
          team('t1', 'Web', [
            week('2026-11-02', '2026-11-08', {
              capacityHours: '40',
              allocatedHours: '40',
              occupancy: '1',
              idleHours: '0',
              isOverloaded: true,
            }),
          ]),
        ],
      });

      const cell = element().querySelector('button[aria-label*="semana"]');
      expect(cell?.getAttribute('aria-label')).toContain('com sobrecarga');
      expect(element().querySelector('[aria-label="Totais do período"]')?.textContent).toContain(
        'Semanas com sobrecarga',
      );
      expect(element().textContent).toContain('(sobrecarga)');
    });

    it('asks again for the period of a shortcut, from this Monday', async () => {
      await answer(REPORT);

      [...element().querySelectorAll('button')]
        .find((b) => b.textContent?.trim() === '4 semanas')
        ?.click();
      fixture.detectChanges();

      expect((element().querySelector('#report-from') as HTMLInputElement).value).toBe(
        '2026-11-02',
      );
      expect((element().querySelector('#report-to') as HTMLInputElement).value).toBe('2026-11-29');
      request().flush(REPORT);
    });
  });
});
