import { DatePipe, DecimalPipe } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { provideLocalePtBr } from './locale';

describe('provideLocalePtBr', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideLocalePtBr(), DatePipe, DecimalPipe],
    });
  });

  it('formats numbers in the pt-BR pattern', () => {
    const decimal = TestBed.inject(DecimalPipe);

    expect(decimal.transform(1234.5)).toBe('1.234,5');
  });

  it('formats dates as dd/MM/yyyy', () => {
    const date = TestBed.inject(DatePipe);

    // Instante fixo (meio-dia UTC): o resultado não depende do fuso da máquina que roda o teste.
    // Uma string só com a data ('2026-11-28') seria lida como meia-noite no fuso local da máquina.
    expect(date.transform('2026-11-28T12:00:00Z', 'dd/MM/yyyy')).toBe('28/11/2026');
  });

  it('shows instants in the America/Sao_Paulo offset', () => {
    const date = TestBed.inject(DatePipe);

    // 02:00 UTC de 28/11 ainda é 23:00 de 27/11 em São Paulo (UTC-3).
    expect(date.transform('2026-11-28T02:00:00Z', 'dd/MM/yyyy HH:mm')).toBe('27/11/2026 23:00');
  });
});
