/**
 * Destino a abrir depois do login, a partir do parâmetro `returnUrl` da URL (que qualquer pessoa pode
 * forjar). Só aceita um caminho interno da própria aplicação; qualquer outra coisa vira a página inicial.
 *
 * Sem essa regra, um link como `/login?returnUrl=//site-falso.com` mandaria o gestor, logo depois de
 * digitar a senha, para outro site (redirecionamento aberto).
 */
export function safeReturnUrl(candidate: unknown): string {
  if (typeof candidate !== 'string') {
    return '/';
  }

  const isInternalPath =
    candidate.startsWith('/') &&
    !candidate.startsWith('//') && // "//host" é um endereço de outro site, sem esquema
    !candidate.includes('\\') && // os navegadores tratam "/\host" como "//host"
    !candidate.startsWith('/login') && // voltar ao login depois de entrar seria um laço
    // eslint-disable-next-line no-control-regex -- barra caracteres de controle em um destino
    !/[\u0000-\u001f]/.test(candidate);

  return isInternalPath ? candidate : '/';
}
