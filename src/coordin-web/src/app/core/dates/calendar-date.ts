import { Injectable } from '@angular/core';

/**
 * Datas de calendário (`2026-12-31`, sem horário nem fuso: prazos, ausências, feriados).
 *
 * Regra do projeto (lição da change 1): uma data de calendário **nunca** passa por `Date`, `DatePipe`
 * ou `new Date(texto)`. Eles leem `2026-12-31` como meia-noite em algum fuso e podem exibir o dia
 * anterior ou seguinte. Aqui tudo é feito por troca de partes do texto, igual em qualquer fuso.
 */
const CALENDAR_DATE = /^(\d{4})-(\d{2})-(\d{2})$/;

/** `2026-12-31` → `31/12/2026`. Um valor que não é uma data de calendário volta como veio. */
export function formatCalendarDate(value: string): string {
  const match = CALENDAR_DATE.exec(value);
  return match ? `${match[3]}/${match[2]}/${match[1]}` : value;
}

/**
 * Um período em poucas palavras: `04–15/01` quando começa e termina no mesmo mês, `21/12–04/01` em
 * meses diferentes e `08/02` quando é um dia só. O ano não aparece: serve a listas curtas.
 */
export function formatPeriod(start: string, end: string): string {
  const from = CALENDAR_DATE.exec(start);
  const to = CALENDAR_DATE.exec(end);
  if (!from || !to) {
    return `${start}–${end}`;
  }

  if (start === end) {
    return `${to[3]}/${to[2]}`;
  }

  return from[1] === to[1] && from[2] === to[2]
    ? `${from[3]}–${to[3]}/${to[2]}`
    : `${from[3]}/${from[2]}–${to[3]}/${to[2]}`;
}

/** O fim é anterior ao início? Datas `yyyy-MM-dd` se comparam como texto, sem `Date`. */
export function isEndBeforeStart(start: string, end: string): boolean {
  return end < start;
}

const SAO_PAULO_OFFSET_MS = 3 * 60 * 60 * 1000;

/** A data de calendário de um instante em São Paulo (UTC-3, o deslocamento fixo do projeto, RNF-07). */
export function saoPauloDate(epochMilliseconds: number): string {
  return new Date(epochMilliseconds - SAO_PAULO_OFFSET_MS).toISOString().slice(0, 10);
}

/** O relógio da aplicação. Existe como serviço para os testes controlarem "hoje". */
@Injectable({ providedIn: 'root' })
export class Clock {
  now(): number {
    return Date.now();
  }

  /** A data de hoje em São Paulo, como `yyyy-MM-dd`. */
  today(): string {
    return saoPauloDate(this.now());
  }
}
