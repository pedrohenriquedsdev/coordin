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

/*
 * Semanas e intervalos de datas de calendário. Usam aritmética em UTC: `Date.UTC` e os métodos `getUTC*`
 * não dependem do fuso do navegador, então a mesma data gera o mesmo resultado em qualquer lugar.
 */
const DAY_MS = 24 * 60 * 60 * 1000;

function fromCalendar(value: string): Date {
  const match = CALENDAR_DATE.exec(value);
  if (!match) {
    throw new RangeError(`Não é uma data de calendário: ${value}`);
  }

  return new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3])));
}

function toCalendar(date: Date): string {
  return date.toISOString().slice(0, 10);
}

/** Soma dias corridos a uma data de calendário (negativo volta no tempo). */
export function addDays(value: string, days: number): string {
  const date = fromCalendar(value);
  date.setUTCDate(date.getUTCDate() + days);
  return toCalendar(date);
}

/** A segunda-feira da semana (segunda a domingo) de uma data de calendário. */
export function mondayOf(value: string): string {
  const weekday = fromCalendar(value).getUTCDay(); // domingo = 0
  return addDays(value, -((weekday + 6) % 7));
}

/** O domingo da semana de uma data de calendário. */
export function sundayOf(value: string): string {
  return addDays(mondayOf(value), 6);
}

/** Quantas semanas completas o período cobre, do início da semana de `from` ao fim da semana de `to`. */
export function weeksBetween(from: string, to: string): number {
  const days =
    (fromCalendar(sundayOf(to)).getTime() - fromCalendar(mondayOf(from)).getTime()) / DAY_MS;
  return (days + 1) / 7;
}

/** `2026-12-31` → `31/12`: o dia e o mês de uma data, para os rótulos curtos das semanas. */
export function formatShortCalendarDate(value: string): string {
  const match = CALENDAR_DATE.exec(value);
  return match ? `${match[3]}/${match[2]}` : value;
}

/** Quantos dias corridos há de `from` até `to` (negativo se `to` for anterior). */
export function daysBetween(from: string, to: string): number {
  return Math.round((fromCalendar(to).getTime() - fromCalendar(from).getTime()) / DAY_MS);
}

/** O primeiro dia do mês de uma data de calendário. */
export function startOfMonth(value: string): string {
  const date = fromCalendar(value);
  return toCalendar(new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), 1)));
}

/** O primeiro dia do mês que fica `months` meses depois (ou antes, se negativo) do mês da data. */
export function addMonths(value: string, months: number): string {
  const date = fromCalendar(value);
  return toCalendar(new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + months, 1)));
}

/** As 42 datas (6 semanas, de segunda a domingo) que desenham o mês de uma data em um calendário. */
export function monthGrid(value: string): string[] {
  const first = mondayOf(startOfMonth(value));
  return Array.from({ length: 42 }, (_, index) => addDays(first, index));
}

export const MONTH_NAMES = [
  'janeiro',
  'fevereiro',
  'março',
  'abril',
  'maio',
  'junho',
  'julho',
  'agosto',
  'setembro',
  'outubro',
  'novembro',
  'dezembro',
] as const;

export const WEEKDAY_SHORT = ['seg', 'ter', 'qua', 'qui', 'sex', 'sáb', 'dom'] as const;

/** `2026-12-01` → `dezembro de 2026`. */
export function formatMonth(value: string): string {
  const match = CALENDAR_DATE.exec(value);
  return match ? `${MONTH_NAMES[Number(match[2]) - 1]} de ${match[1]}` : value;
}

/** `2026-12-25` → `25 de dezembro de 2026`. */
export function formatLongCalendarDate(value: string): string {
  const match = CALENDAR_DATE.exec(value);
  return match
    ? `${Number(match[3])} de ${MONTH_NAMES[Number(match[2]) - 1]} de ${match[1]}`
    : value;
}

/** O mesmo mês (`aaaa-mm`) para duas datas? */
export function isSameMonth(a: string, b: string): boolean {
  return a.slice(0, 7) === b.slice(0, 7);
}
