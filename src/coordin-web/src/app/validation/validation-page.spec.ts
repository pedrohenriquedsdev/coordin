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

    expect(text()).toContain('Etapa atual: Carregar o conjunto fictício');
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
    expect(text()).toContain('Etapa atual: Comparar');
  });
});
