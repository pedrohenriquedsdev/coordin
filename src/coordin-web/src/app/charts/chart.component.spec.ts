import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChartComponent, CHART_FACTORY } from './chart.component';
import { createFakeChartFactory } from './chart-test-double';
import type { ChartConfiguration } from './chart-setup';

const config = (label: string): ChartConfiguration<'bar'> => ({
  type: 'bar',
  data: { labels: ['06/01'], datasets: [{ label, data: [8] }] },
});

@Component({
  imports: [ChartComponent],
  template: `<app-chart [configuration]="configuration()" label="Gráfico de teste" />`,
})
class HostComponent {
  readonly configuration = signal(config('Horas'));
}

describe('ChartComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let created: ReturnType<typeof createFakeChartFactory>['created'];

  beforeEach(() => {
    const fake = createFakeChartFactory();
    created = fake.created;
    TestBed.configureTestingModule({
      providers: [{ provide: CHART_FACTORY, useValue: fake.factory }],
    });
    fixture = TestBed.createComponent(HostComponent);
  });

  it('cria, atualiza e destrói o gráfico uma vez cada, ao sair da tela', () => {
    fixture.detectChanges();
    expect(created).toHaveLength(1);
    expect(created[0].canvas.tagName).toBe('CANVAS');

    // Uma configuração nova atualiza o mesmo gráfico, sem criar outro.
    fixture.componentInstance.configuration.set(config('Ociosas'));
    fixture.detectChanges();

    expect(created).toHaveLength(1);
    expect(created[0].updates).toBe(1);
    expect(created[0].data).toEqual(config('Ociosas').data);

    // Sair da tela destrói o gráfico.
    fixture.destroy();

    expect(created[0].destroys).toBe(1);
  });

  it('expõe o rótulo para leitores de tela no canvas', () => {
    fixture.detectChanges();

    const canvas = fixture.nativeElement.querySelector('canvas') as HTMLCanvasElement;
    expect(canvas.getAttribute('role')).toBe('img');
    expect(canvas.getAttribute('aria-label')).toBe('Gráfico de teste');
  });
});
