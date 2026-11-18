/** O nome de reserva, se a resposta não trouxer o nome do arquivo (RF-19). */
const FALLBACK_FILE_NAME = 'coordin-cronograma.csv';

/** O BOM UTF-8. O texto já decodificado pelo navegador perdeu o BOM; sem ele, o Excel não reconhece os acentos (DT-20). */
const UTF8_BOM = '﻿';

/**
 * O nome do arquivo que a API informa no cabeçalho `Content-Disposition` (com ou sem aspas). Sem cabeçalho ou sem
 * nome, usa o nome de reserva.
 */
export function fileNameFrom(contentDisposition: string | null): string {
  const match = contentDisposition?.match(/filename="?([^";]+)"?/i);
  return match ? match[1] : FALLBACK_FILE_NAME;
}

/** O texto do CSV como arquivo CSV em UTF-8, com o BOM de volta na frente (DT-20). */
export function csvBlob(text: string): Blob {
  return new Blob([UTF8_BOM, text], { type: 'text/csv;charset=utf-8' });
}

/**
 * Dispara o download no navegador: cria uma URL temporária para o arquivo, aciona um link de download e libera a URL
 * em seguida. A sessão já foi usada pelo pedido, então o link não precisa de token.
 */
export function downloadBlob(blob: Blob, name: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = name;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
