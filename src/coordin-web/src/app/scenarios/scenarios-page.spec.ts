import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideApiConfiguration } from '../api/api-configuration';
import { CHART_FACTORY } from '../charts/chart.component';
import { createFakeChartFactory } from '../charts/chart-test-double';
import { ScenariosPage } from './scenarios-page';

function item(demandId: string, title: string, completedOn: string | null = null) {
  return {
    demandId,
    title,
    priorityRank: 1,
    executionRank: 1,
    isOutOfPriorityOrder: false,
    teams: [],
    totalEstimatedEffortHours: '0',
    startDate: null,
    isPredictable: true,
    unpredictableReason: null,
    forecastStartDate: null,
    endDateWithoutMargin: null,
    forecastEndDate: null,
    informedEndDate: null,
    exceedsInformedEndDate: false,
    completedOn,
    baselineEndDate: null,
    baselineDeviationDays: null,
    forecastDeviationDays: null,
    prerequisites: [],
  };
}

function scenario(executionOrder: string[]) {
  return {
    executionOrder,
    isComparable: true,
    items: [
      {
        demandId: executionOrder[0],
        title: 'Alfa',
        priorityRank: '1',
        newExecutionRank: '1',
        currentForecastEndDate: '2027-03-01',
        forecastStartDate: '2027-02-01',
        forecastEndDate: '2027-03-04',
        deltaDays: '3',
        weight: '10',
        scoreContributionDelta: '-10',
      },
    ],
    score: { current: '100', scenario: '90', delta: '-10' },
    sectorDeltaDays: '0',
    sectorEndDate: '2027-03-04',
  };
}

describe('ScenariosPage', () => {
  let fixture: ComponentFixture<ScenariosPage>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideRouter([]),
        { provide: CHART_FACTORY, useValue: createFakeChartFactory().factory },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ScenariosPage);
  });

  afterEach(() => http.verify());

  const element = (): HTMLElement => fixture.nativeElement as HTMLElement;

  const settle = async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const button = (label: string): HTMLButtonElement | undefined =>
    [...element().querySelectorAll('button')].find((b) => b.textContent?.trim() === label);

  const openList = (): string[] =>
    [...element().querySelectorAll('ol li > span:first-child')].map(
      (span) => span.textContent?.trim() ?? '',
    );

  const answerSchedule = async (items: unknown[]) => {
    http
      .expectOne({ method: 'GET', url: '/api/schedule' })
      .flush({ executionOrderMode: 'FollowPriority', items });
    await settle();
  };

  it('loads the schedule on open and asks for no recommendation', async () => {
    fixture.detectChanges();
    await answerSchedule([item('a', 'Alfa')]);

    http.expectNone((req) => req.url.includes('scenarios'));
    expect(element().textContent).toContain('Cenários');
  });

  it('asks for recommendations only when the button is pressed, and shows one card each', async () => {
    fixture.detectChanges();
    await answerSchedule([item('a', 'Alfa')]);

    button('Buscar recomendações')?.click();
    await settle();
    http.expectOne('/api/scenarios/recommendations').flush([scenario(['a']), scenario(['a'])]);
    await settle();

    expect(element().querySelectorAll('app-scenario-card')).toHaveLength(2);
    expect(element().textContent).toContain('Cenário recomendado 1');
  });

  it('moves open demands with Subir and Descer, and sends the full order with completed ones in place', async () => {
    fixture.detectChanges();
    await answerSchedule([
      item('a', 'Alfa'),
      item('done', 'Feita', '2027-01-05'),
      item('b', 'Bravo'),
    ]);

    expect(openList()).toEqual(['1. Alfa', '2. Bravo']);

    const bravo = [...element().querySelectorAll('li')].find((li) =>
      li.textContent?.includes('Bravo'),
    );
    bravo?.querySelectorAll('button')[0].click();
    await settle();
    expect(openList()).toEqual(['1. Bravo', '2. Alfa']);

    button('Simular')?.click();
    const request = http.expectOne('/api/scenarios/simulate');
    expect(request.request.body).toEqual({ executionOrder: ['b', 'done', 'a'] });
    request.flush(scenario(['b', 'done', 'a']));
    await settle();

    expect(element().textContent).toContain('Ordem simulada');
  });

  it('restores the current order and removes the simulated result', async () => {
    fixture.detectChanges();
    await answerSchedule([item('a', 'Alfa'), item('b', 'Bravo')]);

    [...element().querySelectorAll('li')][1].querySelectorAll('button')[0].click();
    await settle();
    button('Simular')?.click();
    http.expectOne('/api/scenarios/simulate').flush(scenario(['b', 'a']));
    await settle();

    button('Restaurar a ordem atual')?.click();
    await settle();

    expect(openList()).toEqual(['1. Alfa', '2. Bravo']);
    expect(element().textContent).not.toContain('Ordem simulada');
  });

  it('approves a recommended scenario only after the confirmation, and never before', async () => {
    fixture.detectChanges();
    await answerSchedule([item('a', 'Alfa')]);
    button('Buscar recomendações')?.click();
    await settle();
    http.expectOne('/api/scenarios/recommendations').flush([scenario(['a'])]);
    await settle();

    button('Aprovar este cenário')?.click();
    await settle();
    http.expectNone('/api/scenarios/apply');

    button('Confirmar aprovação')?.click();
    const apply = http.expectOne('/api/scenarios/apply');
    expect(apply.request.body).toEqual({ executionOrder: ['a'] });
    apply.flush(scenario(['a']));
    await settle();
    // depois de aprovar, a tela recarrega o cronograma
    await answerSchedule([item('a', 'Alfa')]);

    expect(element().textContent).toContain('A prioridade registrada não mudou.');
  });
});
