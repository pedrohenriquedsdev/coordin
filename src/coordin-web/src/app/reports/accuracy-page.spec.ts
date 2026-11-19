import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideApiConfiguration } from '../api/api-configuration';
import { CHART_FACTORY } from '../charts/chart.component';
import { createFakeChartFactory } from '../charts/chart-test-double';
import { addDays, Clock } from '../core/dates/calendar-date';
import { AccuracyPage } from './accuracy-page';
import { AccuracyStore } from './accuracy.store';

const indicators = (usedCount: number, overrides: Record<string, unknown> = {}) => ({
  usedCount,
  averageDeviationFromBaselineDays: 1,
  averageDeviationFromCompletionForecastDays: -2,
  onTimePercentage: 66.7,
  averageEstimateDifferencePercent: 12.5,
  excludedWithoutSnapshot: 0,
  excludedWithoutBaseline: 0,
  excludedWithoutActualEffort: 2,
  excludedWithoutEstimate: 0,
  ...overrides,
});

describe('AccuracyPage', () => {
  let fixture: ComponentFixture<AccuracyPage>;
  let http: HttpTestingController;
  let created: ReturnType<typeof createFakeChartFactory>['created'];

  beforeEach(async () => {
    const fake = createFakeChartFactory();
    created = fake.created;
    await TestBed.configureTestingModule({
      imports: [AccuracyPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        { provide: CHART_FACTORY, useValue: fake.factory },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AccuracyPage);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => http.match(() => true));

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
  /** O store é da página (protegido), e o teste precisa mexer no período direto. */
  const storeOf = () => (fixture.componentInstance as unknown as { store: AccuracyStore }).store;
  const submit = () => {
    (fixture.nativeElement as HTMLElement)
      .querySelector('form')
      ?.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  };

  it('refuses an inverted period on the screen, and sends nothing', () => {
    storeOf().from.set('2027-02-01');
    storeOf().to.set('2027-01-01');

    submit();

    expect(text()).toContain('A data inicial não pode ser posterior à final.');
    http.expectNone((request) => request.url.includes('/api/reports/accuracy'));
  });

  it('asks the API for the period and shows the sector and each team, with two charts', () => {
    storeOf().from.set('2027-01-01');
    storeOf().to.set('2027-01-31');

    submit();
    http
      .expectOne(
        (request) =>
          request.url === '/api/reports/accuracy' &&
          request.params.get('from') === '2027-01-01' &&
          request.params.get('to') === '2027-01-31',
      )
      .flush({
        from: '2027-01-01',
        to: '2027-01-31',
        sector: indicators(3),
        teams: [
          {
            teamId: 'a',
            teamName: 'Alfa',
            indicators: indicators(2, { averageEstimateDifferencePercent: 30 }),
            isAboveSector: true,
          },
        ],
      });
    fixture.detectChanges();

    expect(created.length).toBe(2);
    expect(text()).toContain('Setor');
    expect(text()).toContain('Alfa');
    expect(text()).toContain('acima do setor');
    expect(text()).toContain('2 demandas ficaram de fora (sem esforço real)');
  });

  it('shows the same numbers in the table that the charts plot', () => {
    storeOf().from.set('2027-01-01');
    storeOf().to.set('2027-01-31');
    submit();
    http
      .expectOne((request) => request.url === '/api/reports/accuracy')
      .flush({
        from: '2027-01-01',
        to: '2027-01-31',
        sector: indicators(3),
        teams: [],
      });
    fixture.detectChanges();

    const datasets = created[1].data as { datasets: { data: number[] }[] };
    expect(datasets.datasets[0].data).toEqual([12.5]);
    expect(text()).toContain('12,5%');
    expect(text()).toContain('1 dias');
  });

  it('says so when nothing was completed in the period', () => {
    storeOf().from.set('2027-01-01');
    storeOf().to.set('2027-01-31');
    submit();
    http
      .expectOne((request) => request.url === '/api/reports/accuracy')
      .flush({
        from: '2027-01-01',
        to: '2027-01-31',
        sector: indicators(0, { averageEstimateDifferencePercent: null }),
        teams: [],
      });
    fixture.detectChanges();

    expect(text()).toContain('Nenhuma demanda concluída no período.');
    expect(created.length).toBe(0);
  });

  it('shows a failure without losing the period the manager chose', () => {
    storeOf().from.set('2027-01-01');
    storeOf().to.set('2027-01-31');
    submit();
    http
      .expectOne((request) => request.url === '/api/reports/accuracy')
      .flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(text()).toContain('Tente de novo com outro período ou mais tarde.');
    expect(storeOf().from()).toBe('2027-01-01');
  });

  it('asks the manager to choose a period before any search', () => {
    expect(text()).toContain('Escolha o período de conclusão');
    http.expectNone((request) => request.url.includes('/api/reports/accuracy'));
  });

  it('searches the last 30 days from today when the shortcut is used', () => {
    const today = TestBed.inject(Clock).today();
    [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')]
      .find((b) => b.textContent?.trim() === 'Últimos 30 dias')
      ?.click();
    fixture.detectChanges();

    http
      .expectOne(
        (request) =>
          request.url === '/api/reports/accuracy' &&
          request.params.get('to') === today &&
          request.params.get('from') === addDays(today, -30),
      )
      .flush({ from: '', to: '', sector: indicators(0), teams: [] });
  });

  it('summarizes the sector on top: how many were completed and how many were on time', () => {
    storeOf().from.set('2027-01-01');
    storeOf().to.set('2027-01-31');
    submit();
    http
      .expectOne((request) => request.url === '/api/reports/accuracy')
      .flush({
        from: '2027-01-01',
        to: '2027-01-31',
        sector: indicators(3),
        teams: [],
      });
    fixture.detectChanges();

    const summary =
      (fixture.nativeElement as HTMLElement).querySelector('[aria-label="Resumo do setor"]')
        ?.textContent ?? '';
    expect(summary).toContain('3');
    expect(summary).toContain('Demandas concluídas no período');
    expect(summary).toContain('66,7%');
  });
});
