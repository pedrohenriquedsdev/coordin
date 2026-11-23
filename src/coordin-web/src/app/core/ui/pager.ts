import { signal } from '@angular/core';

/** Quantos itens uma lista longa mostra de cada vez. */
export const PAGE_SIZE = 10;

/**
 * "Mostrar mais" de uma lista: guarda quantos itens estão à mostra e aumenta de `size` em `size`.
 * A lista inteira continua na tela que a usa (busca, filtros, contagens, Subir e Descer olham a lista
 * completa); o `Pager` só decide quantos dos primeiros itens aparecem.
 *
 * `slice` lê um signal, então, chamado dentro de um `computed`, a lista se atualiza quando `more` ou
 * `reset` são chamados. Quem muda a lista (busca, filtro, aba) deve chamar `reset`, para voltar aos
 * primeiros itens em vez de herdar a quantidade da lista anterior.
 */
export class Pager {
  private readonly limit = signal(PAGE_SIZE);

  constructor(private readonly size: number = PAGE_SIZE) {
    this.limit.set(size);
  }

  /** Os primeiros itens da lista, até o limite atual. */
  slice<T>(list: readonly T[]): T[] {
    return list.slice(0, this.limit());
  }

  /** Mostra mais uma página. */
  more(): void {
    this.limit.update((current) => current + this.size);
  }

  /** Volta à primeira página. */
  reset(): void {
    this.limit.set(this.size);
  }
}
