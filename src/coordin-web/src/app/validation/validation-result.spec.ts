import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CHART_FACTORY } from '../charts/chart.component';
import { createFakeChartFactory } from '../charts/chart-test-double';
import { ValidationResult } from '../api/models/validation-result';
import { ValidationResultView } from './validation-result';

const result: ValidationResult = {
  periodStart: '2027-01-04',
  periodEnd: '2027-03-14',
  efficiency: {
    currentProcess: {
      idleHours: 412.5,
      occupancySpreadPoints: 38,
      overloadedPeriods: 9,
      sectorEndDate: '2027-03-12',
    },
    withCoordin: {
      idleHours: 296,
      occupancySpreadPoints: 21.5,
      overloadedPeriods: 4,
      sectorEndDate: null,
    },
  },
  deviation: {
    averageBaselineDeviationDays: 9.4,
    averageCoordinDeviationDays: 2.8,
    averageBaselineAbsoluteDeviationDays: 11.2,
    averageCoordinAbsoluteDeviationDays: 4.1,
    items: [
      {
        demandId: 'a',
        baselineDate: '2027-06-30',
        coordinForecastEndDate: '2027-02-03',
        simulatedActualEndDate: '2027-02-05',
        baselineDeviationDays: -146,
        coordinDeviationDays: 2,
      },
    ],
  },
  priorityCheck: {
    worsenedCount: 1,
    items: [
      {
        demandId: 'a',
        priorityRank: 1,
        currentForecastEndDate: '2027-02-01',
        coordinForecastEndDate: '2027-02-03',
        deltaDays: 2,
      },
    ],
  },
};

describe('ValidationResultView', () => {
  let fixture: ComponentFixture<ValidationResultView>;
  let created: ReturnType<typeof createFakeChartFactory>['created'];

  beforeEach(async () => {
    const fake = createFakeChartFactory();
    created = fake.created;
    await TestBed.configureTestingModule({
      imports: [ValidationResultView],
      providers: [{ provide: CHART_FACTORY, useValue: fake.factory }],
    }).compileComponents();

    fixture = TestBed.createComponent(ValidationResultView);
    fixture.componentRef.setInput('result', result);
    fixture.componentRef.setInput('titles', new Map([['a', 'Alfa']]));
    fixture.detectChanges();
  });

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('draws the efficiency and the deviation charts', () => {
    expect(created.length).toBe(2);
  });

  it('shows the period and the same efficiency numbers in the table', () => {
    expect(text()).toContain('Período comparado: 04/01/2027 a 14/03/2027');
    expect(text()).toContain('412,5');
    expect(text()).toContain('296');
  });

  it('shows the averages in the table, with the same numbers the chart plots', () => {
    expect(text()).toContain('9,4');
    expect(text()).toContain('11,2');
    expect(text()).toContain('2,8');
    expect(text()).toContain('4,1');
  });

  it('shows the per-demand deviations twice: as a table for wider screens and as cards for phones', () => {
    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll(
      'tbody tr th[scope="row"]',
    );
    const cards = (fixture.nativeElement as HTMLElement).querySelectorAll('ul > li');

    expect(text()).toContain('146 dias antes');
    expect(rows.length).toBeGreaterThan(0);
    expect(cards.length).toBe(1);
  });

  it('marks the worsened priority with its text, not only with colour', () => {
    expect(text()).toContain('2 dias de atraso (piora)');
    expect(text()).toContain('1 demanda prioritária piora com o Coordin.');
  });
});
