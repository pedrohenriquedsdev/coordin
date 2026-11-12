import { DATE_PIPE_DEFAULT_OPTIONS, registerLocaleData } from '@angular/common';
import localePt from '@angular/common/locales/pt';
import { LOCALE_ID, Provider } from '@angular/core';

/**
 * Deslocamento de America/Sao_Paulo (RNF-07). O DatePipe aceita só deslocamentos fixos, não nomes
 * de fuso IANA, e o Brasil não tem horário de verão desde 2019. Se isso mudar, só esta constante muda.
 */
export const SAO_PAULO_UTC_OFFSET = '-0300';

/** Português do Brasil: números como 1.234,5 e datas como 28/11/2026, no fuso de São Paulo. */
export function provideLocalePtBr(): Provider[] {
  registerLocaleData(localePt);

  return [
    { provide: LOCALE_ID, useValue: 'pt-BR' },
    { provide: DATE_PIPE_DEFAULT_OPTIONS, useValue: { timezone: SAO_PAULO_UTC_OFFSET } },
  ];
}
