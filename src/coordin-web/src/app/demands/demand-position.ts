/**
 * Lê a posição digitada na nova demanda (RF-16). Vazio é válido e significa "no fim da fila" (`null`); senão,
 * precisa ser um inteiro de 1 até `max`, que é o número de demandas mais uma (a posição do fim).
 */
export function parsePosition(text: string, max: number): { value: number | null; valid: boolean } {
  const trimmed = text.trim();
  if (trimmed === '') {
    return { value: null, valid: true };
  }

  if (!/^\d+$/.test(trimmed)) {
    return { value: null, valid: false };
  }

  const value = Number(trimmed);
  return value >= 1 && value <= max ? { value, valid: true } : { value: null, valid: false };
}
