import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CHART_FACTORY } from '../charts/chart.component';
import { createFakeChartFactory } from '../charts/chart-test-double';
import { ScenarioCard } from './scenario-card';
import { Scenario } from './scenarios';

function scenario(overrides: Partial<Scenario> = {}): Scenario {
  return {
    executionOrder: ['a', 'b'],
    isComparable: true,
    items: [
      {
        demandId: 'a',
        title: 'Alfa',
        priorityRank: 1,
        newExecutionRank: 2,
        currentForecastEndDate: '2027-03-01',
        forecastStartDate: '2027-02-01',
        forecastEndDate: '2027-03-04',
        deltaDays: 3,
        weight: 10,
        scoreContributionDelta: 40,
      },
      {
        demandId: 'b',
        title: 'Bravo',
        priorityRank: 2,
        newExecutionRank: 1,
        currentForecastEndDate: '2027-03-10',
        forecastStartDate: '2027-02-05',
        forecastEndDate: '2027-03-05',
        deltaDays: -5,
        weight: 9,
        scoreContributionDelta: -120,
      },
    ],
    sectorEndDate: '2027-03-10',
    sectorDeltaDays: -6,
    score: { current: 15840, scenario: 15210, delta: -630 },
    ...overrides,
  };
}

describe('ScenarioCard', () => {
  let fixture: ComponentFixture<ScenarioCard>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [{ provide: CHART_FACTORY, useValue: createFakeChartFactory().factory }],
    });
    fixture = TestBed.createComponent(ScenarioCard);
  });

  const render = (value: Scenario = scenario()) => {
    fixture.componentRef.setInput('scenario', value);
    fixture.componentRef.setInput('title', 'Cenário recomendado 1');
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  const button = (root: HTMLElement, label: string): HTMLButtonElement | undefined =>
    [...root.querySelectorAll('button')].find((b) => b.textContent?.trim() === label);

  it('shows the criterion, the sector date in dd/MM/yyyy and the day difference in words', () => {
    const root = render();

    expect(root.textContent).toContain('15.840');
    expect(root.textContent).toContain('10/03/2027');
    expect(root.textContent).toContain('6 dias antes');
  });

  it('shows the justification with a table of the same values as the chart', () => {
    const root = render();

    expect(root.querySelector('app-chart')).not.toBeNull();
    const rows = [...root.querySelectorAll('table')[0].querySelectorAll('tbody tr')];
    expect(rows.map((row) => row.textContent?.replace(/\s+/g, ' ').trim())).toEqual([
      expect.stringContaining('Bravo'),
      expect.stringContaining('Alfa'),
    ]);
    expect(root.textContent).toContain('melhora');
    expect(root.textContent).toContain('piora');
  });

  it('shows the full table with the registered priority beside the new position', () => {
    const root = render();
    const rows = [...root.querySelectorAll('section[aria-label="Demandas do cenário"] tbody tr')];

    expect(rows[0].textContent).toContain('Alfa');
    expect(rows[0].textContent).toContain('2');
    expect(rows[0].textContent).toContain('04/03/2027');
  });

  it('asks for confirmation before approving, and emits only on the second click', () => {
    const root = render();
    const approved: Scenario[] = [];
    fixture.componentInstance.approve.subscribe((value) => approved.push(value));

    button(root, 'Aprovar este cenário')?.click();
    fixture.detectChanges();
    expect(approved).toHaveLength(0);
    expect(root.textContent).toContain('A prioridade registrada não muda.');

    button(root, 'Confirmar aprovação')?.click();
    expect(approved).toHaveLength(1);
  });

  it('does not emit anything when the approval is cancelled', () => {
    const root = render();
    const approved: Scenario[] = [];
    fixture.componentInstance.approve.subscribe((value) => approved.push(value));

    button(root, 'Aprovar este cenário')?.click();
    fixture.detectChanges();
    button(root, 'Cancelar')?.click();
    fixture.detectChanges();

    expect(approved).toHaveLength(0);
    expect(button(root, 'Aprovar este cenário')).toBeDefined();
  });

  it('warns when the scenario is not comparable, and rejecting emits the scenario', () => {
    const root = render(scenario({ isComparable: false }));
    const rejected: Scenario[] = [];
    fixture.componentInstance.reject.subscribe((value) => rejected.push(value));

    expect(root.textContent).toContain('deixaria uma demanda sem previsão');
    button(root, 'Rejeitar')?.click();
    expect(rejected).toHaveLength(1);
  });
});
