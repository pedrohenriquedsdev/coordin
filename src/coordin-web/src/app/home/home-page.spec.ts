import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideApiConfiguration } from '../api/api-configuration';
import { CHART_FACTORY } from '../charts/chart.component';
import type { Chart } from '../charts/chart-setup';
import { provideLocalePtBr } from '../core/locale';
import { HomePage } from './home-page';

function item(overrides: Record<string, unknown> = {}) {
  return {
    demandId: 'd1',
    title: 'Checkout',
    priorityRank: 1,
    executionRank: 1,
    isOutOfPriorityOrder: false,
    teams: [],
    totalEstimatedEffortHours: '80',
    startDate: '2026-12-01',
    isPredictable: true,
    unpredictableReason: null,
    forecastStartDate: '2026-12-01',
    endDateWithoutMargin: '2026-12-08',
    forecastEndDate: '2026-12-10',
    informedEndDate: null,
    exceedsInformedEndDate: false,
    completedOn: null,
    baselineEndDate: null,
    baselineDeviationDays: null,
    forecastDeviationDays: null,
    prerequisites: [],
    ...overrides,
  };
}

describe('HomePage', () => {
  let fixture: ComponentFixture<HomePage>;
  let http: HttpTestingController;

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideLocalePtBr(),
        provideRouter([]),
        // Sem canvas de verdade nos testes: um gráfico de mentira.
        {
          provide: CHART_FACTORY,
          useValue: () =>
            ({ destroy: () => undefined, update: () => undefined }) as unknown as Chart<'bar'>,
        },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(HomePage);
    fixture.detectChanges();
  });

  function answer(schedule: unknown[]) {
    http
      .expectOne('/api/schedule')
      .flush({ executionOrderMode: 'FollowPriority', items: schedule });
    http.expectOne('/api/teams').flush([
      { id: 't1', name: 'Web', peopleCount: '3', dailyCapacityHours: '24' },
      { id: 't2', name: 'App', peopleCount: 2, dailyCapacityHours: 16 },
    ]);
    http.expectOne('/api/holidays').flush([{ id: 'h1', date: '2026-12-25', description: null }]);
    http
      .expectOne('/api/settings')
      .flush({ executionOrderMode: 'FollowPriority', safetyMarginPercent: '20' });
    fixture.detectChanges();
  }

  it('shows the sector deadline and the counts from the schedule', () => {
    answer([
      item(),
      item({ demandId: 'd2', forecastEndDate: '2026-12-20', exceedsInformedEndDate: true }),
      item({ demandId: 'd3', isPredictable: false, forecastEndDate: null }),
      item({ demandId: 'd4', completedOn: '2026-11-01' }),
    ]);

    expect(text()).toContain('20/12/2026');
    expect(text()).toContain('3 ativas · 1 concluída');
    expect(text()).toContain('Faltam dados para prever');
    expect(text()).toContain('A previsão passa do prazo desejado');
  });

  it('shows team, holiday and margin numbers on the shortcuts', () => {
    answer([item()]);

    expect(text()).toContain('5 pessoas em 2 times');
    expect(text()).toContain('1 feriado');
    expect(text()).toContain('de margem de segurança');
  });

  it('links every area of the system', () => {
    answer([item()]);

    const hrefs = [...(fixture.nativeElement as HTMLElement).querySelectorAll('nav a')].map((a) =>
      a.getAttribute('href'),
    );
    expect(hrefs).toEqual([
      '/',
      '/demands',
      '/scenarios',
      '/reports/teams',
      '/reports/accuracy',
      '/teams',
      '/calendar',
      '/settings',
    ]);
  });

  it('marks only the card whose request failed as unavailable', () => {
    http.expectOne('/api/schedule').flush(null, { status: 500, statusText: 'Erro' });
    http.expectOne('/api/teams').flush([]);
    http.expectOne('/api/holidays').flush([]);
    http
      .expectOne('/api/settings')
      .flush({ executionOrderMode: 'FollowPriority', safetyMarginPercent: 20 });
    fixture.detectChanges();

    expect(text()).toContain('indisponível');
    expect(text()).toContain('0 pessoas em 0 times');
  });
});
