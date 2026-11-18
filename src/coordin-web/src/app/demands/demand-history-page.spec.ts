import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { provideApiConfiguration } from '../api/api-configuration';
import { DemandHistoryPage } from './demand-history-page';

describe('DemandHistoryPage', () => {
  /** Os parâmetros da rota: o teste troca o identificador como o navegador faz ao voltar e avançar. */
  const params$ = new BehaviorSubject(convertToParamMap({ id: 'demand-1' }));
  let fixture: ComponentFixture<DemandHistoryPage>;
  let http: HttpTestingController;

  beforeEach(async () => {
    // Cada teste começa com a rota na demanda 1: o teste de troca deixa a rota na 2.
    params$.next(convertToParamMap({ id: 'demand-1' }));
    await TestBed.configureTestingModule({
      imports: [DemandHistoryPage],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: convertToParamMap({ id: 'demand-1' }) },
            paramMap: params$,
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DemandHistoryPage);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => http.match(() => true));

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  /** Responde as três requisições da tela: a demanda e os dois históricos. */
  function answer(demand: object, estimates: object[], forecasts: object[]): void {
    http.expectOne('/api/demands/demand-1').flush(demand);
    http.expectOne('/api/demands/demand-1/estimate-history').flush(estimates);
    http.expectOne('/api/demands/demand-1/forecast-history').flush(forecasts);
    fixture.detectChanges();
  }

  it('shows loading while the demand and its histories are on their way', () => {
    expect(text()).toContain('Carregando…');
  });

  it('shows the two histories in order, with the team, the values and the cause', () => {
    answer(
      { id: 'demand-1', title: 'Alterar escopo' },
      [
        {
          teamId: 'a',
          teamName: 'Alfa',
          previousHours: 40,
          newHours: 60,
          reason: 'escopo maior',
          changedAt: '2027-03-01T12:00:00Z',
        },
      ],
      [
        {
          previousEndDate: '2027-03-10',
          newEndDate: '2027-03-17',
          cause: 'Dependency',
          changedAt: '2027-03-02T12:00:00Z',
        },
      ],
    );

    expect(text()).toContain('Alterar escopo');
    expect(text()).toContain('Alfa: 40 → 60 h');
    expect(text()).toContain('escopo maior');
    expect(text()).toContain('10/03/2027 → 17/03/2027');
    expect(text()).toContain('Dependência');
  });

  it('loads the new demand when the route changes to another id on the same screen', () => {
    answer({ id: 'demand-1', title: 'Primeira' }, [], []);

    params$.next(convertToParamMap({ id: 'demand-2' }));

    http.expectOne('/api/demands/demand-2').flush({ id: 'demand-2', title: 'Segunda' });
    http.expectOne('/api/demands/demand-2/estimate-history').flush([]);
    http.expectOne('/api/demands/demand-2/forecast-history').flush([]);
    fixture.detectChanges();
    expect(text()).toContain('Segunda');
    expect(text()).not.toContain('Primeira');
  });

  it('says the demand does not exist when the API answers 404', () => {
    http.expectOne('/api/demands/demand-1').flush(null, { status: 404, statusText: 'Not Found' });
    // A falha da demanda cancela os históricos que saíram junto: não há o que responder para eles.
    http.match(() => true);
    fixture.detectChanges();

    expect(text()).toContain('Demanda não encontrada.');
  });

  it('says there is no change when a history is empty', () => {
    answer({ id: 'demand-1', title: 'Parada' }, [], []);

    expect(text()).toContain('Nenhuma alteração de estimativa registrada.');
    expect(text()).toContain('Nenhuma previsão registrada ainda.');
  });
});
