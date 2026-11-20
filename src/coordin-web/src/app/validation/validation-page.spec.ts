import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideApiConfiguration } from '../api/api-configuration';
import { ValidationPage } from './validation-page';

describe('ValidationPage', () => {
  let fixture: ComponentFixture<ValidationPage>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ValidationPage],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideApiConfiguration('')],
    }).compileComponents();

    fixture = TestBed.createComponent(ValidationPage);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    // Algumas provas param com a requisição de status ainda em voo; ela é descartada aqui.
    http.match(() => true);
  });

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
  const statusRequest = () => http.expectOne((req) => req.url === '/api/validation/status');

  it('shows loading while the status is on its way', () => {
    expect(text()).toContain('Carregando…');
  });

  it('shows the current step once the status arrives', () => {
    statusRequest().flush({
      demandCount: 0,
      baselineCount: 0,
      currentPlanCaptured: false,
      currentPlanCapturedAt: null,
    });
    fixture.detectChanges();

    expect(text()).toContain('Etapa atual');
    expect(text()).toContain('Carregar o conjunto fictício');
  });

  it('says the area is not available when the switch is off', () => {
    statusRequest().flush(null, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(text()).toContain('A validação não está disponível neste ambiente.');
    expect(text()).not.toContain('Carregando…');
  });

  it('offers to try again after a failure, and trying again asks the API once more', () => {
    statusRequest().flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(text()).toContain('Não foi possível carregar a validação.');
    const button = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')].find(
      (b) => b.textContent?.includes('Tentar de novo'),
    );
    button?.click();

    statusRequest().flush({
      demandCount: 30,
      baselineCount: 30,
      currentPlanCaptured: true,
      currentPlanCapturedAt: null,
    });
    fixture.detectChanges();
    expect(text()).toContain('Etapa atual');
    expect(text()).toContain('Comparar');
  });

  describe('the steps', () => {
    const buttonWith = (label: string) =>
      [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')].find((b) =>
        b.textContent?.includes(label),
      ) as HTMLButtonElement | undefined;

    it('keeps the load button disabled until the confirmation is checked', () => {
      statusRequest().flush({
        demandCount: 0,
        baselineCount: 0,
        currentPlanCaptured: false,
        currentPlanCapturedAt: null,
      });
      fixture.detectChanges();

      expect(buttonWith('Carregar conjunto fictício')?.disabled).toBe(true);

      const checkbox = (fixture.nativeElement as HTMLElement).querySelector(
        'input[type="checkbox"]',
      ) as HTMLInputElement;
      checkbox.checked = true;
      checkbox.dispatchEvent(new Event('change'));
      fixture.detectChanges();

      expect(buttonWith('Carregar conjunto fictício')?.disabled).toBe(false);
    });

    it('counts the demands still without a baseline date', () => {
      statusRequest().flush({
        demandCount: 2,
        baselineCount: 0,
        currentPlanCaptured: false,
        currentPlanCapturedAt: null,
      });
      fixture.detectChanges();
      TestBed.flushEffects();

      http
        .expectOne((req) => req.url === '/api/validation/baseline')
        .flush([
          { demandId: 'a', title: 'Alfa', baselineDate: null },
          { demandId: 'b', title: 'Beta', baselineDate: null },
        ]);
      fixture.detectChanges();

      expect(text()).toContain('Faltam 2 de 2.');
      expect(buttonWith('Salvar linha de base')?.disabled).toBe(true);
    });

    it('shows the comparison form with the suggested factors and seed', () => {
      statusRequest().flush({
        demandCount: 2,
        baselineCount: 2,
        currentPlanCaptured: true,
        currentPlanCapturedAt: '2027-06-30T12:00:00Z',
      });
      fixture.detectChanges();

      const inputs = [
        ...(fixture.nativeElement as HTMLElement).querySelectorAll('input[type="number"]'),
      ] as HTMLInputElement[];
      expect(inputs.map((input) => input.value)).toEqual(['0.9', '1.1', '20261126']);
      expect(text()).toContain('A comparação não grava nada');
      expect(text()).toContain('Planejamento guardado em 30/06/2027');
    });
  });

  describe('progress indicator', () => {
    const stepItems = () => [
      ...(fixture.nativeElement as HTMLElement).querySelectorAll(
        'ol[aria-label="Etapas da validação"] li',
      ),
    ];

    it('marks the earlier steps as done, the current one, and the rest as pending, in words', () => {
      statusRequest().flush({
        demandCount: 2,
        baselineCount: 2,
        currentPlanCaptured: false,
        currentPlanCapturedAt: null,
      });
      fixture.detectChanges();

      const items = stepItems();
      expect(items).toHaveLength(4);
      expect(items[0].textContent).toContain('Concluída');
      expect(items[1].textContent).toContain('Concluída');
      expect(items[2].textContent).toContain('Em andamento');
      expect(items[2].getAttribute('aria-current')).toBe('step');
      expect(items[3].textContent).toContain('Pendente');
    });

    it('starts at the first step when nothing was loaded', () => {
      statusRequest().flush({
        demandCount: 0,
        baselineCount: 0,
        currentPlanCaptured: false,
        currentPlanCapturedAt: null,
      });
      fixture.detectChanges();

      expect(stepItems()[0].getAttribute('aria-current')).toBe('step');
      expect(stepItems()[1].textContent).toContain('Pendente');
    });
  });

  it('loads the demand names on the compare step too, so the result shows names instead of codes', () => {
    statusRequest().flush({
      demandCount: 2,
      baselineCount: 2,
      currentPlanCaptured: true,
      currentPlanCapturedAt: '2027-06-30T12:00:00Z',
    });
    fixture.detectChanges();
    TestBed.flushEffects();

    http
      .expectOne((req) => req.url === '/api/validation/baseline')
      .flush([
        { demandId: 'a', title: 'Alfa', baselineDate: '2027-06-30' },
        { demandId: 'b', title: 'Beta', baselineDate: '2027-07-01' },
      ]);
  });
});
