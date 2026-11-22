import { expect, type APIRequestContext, type Page } from '@playwright/test';

/** O e-mail e a senha do gestor da execução, preparados por `playwright.config.ts` (nunca gravados em arquivo). */
export function managerCredentials(): { email: string; password: string } {
  const email = process.env.E2E_MANAGER_EMAIL;
  const password = process.env.E2E_MANAGER_PASSWORD;
  if (!email || !password) {
    throw new Error('Credenciais do smoke ausentes: rode com `npx playwright test`.');
  }

  return { email, password };
}

/** Um sufixo único por teste, para nomes que não colidem com os de outra execução ou de outro cenário. */
export function uniqueSuffix(): string {
  return `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

/**
 * Entra pela tela de login e espera o botão "Sair", que só existe com sessão e fica visível em qualquer
 * largura (o menu principal fica recolhido em 360 px, então ele não serve de sinal).
 */
export async function loginAs(page: Page): Promise<void> {
  const { email, password } = managerCredentials();
  await page.goto('/login');
  await page.getByLabel('E-mail').fill(email);
  // `exact`: o botão "Mostrar senha" também traz "senha" no rótulo, e a busca por trecho acharia dois elementos.
  await page.getByLabel('Senha', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Entrar' }).click();
  await expect(page.getByRole('button', { name: 'Sair' })).toBeVisible();
}

/**
 * Troca de tela sem recarregar a página. Um `page.goto` perde a sessão (que só vive na memória) e a
 * restaura pelo refresh token, e a renovação tem limite de 10 por minuto: vários `goto` seguidos tomam 429.
 * O roteador do Angular escuta o `popstate`, então basta empurrar o endereço e avisá-lo.
 */
export async function navigateInApp(page: Page, path: string): Promise<void> {
  await page.evaluate((target) => {
    history.pushState({}, '', target);
    dispatchEvent(new PopStateEvent('popstate'));
  }, path);
}

/** Token de acesso pela API, para criar dados sem a tela (o celular só consulta, RNF-03). */
export async function apiToken(request: APIRequestContext): Promise<string> {
  const { email, password } = managerCredentials();
  const response = await request.post('/api/auth/login', { data: { email, password } });
  expect(response.ok()).toBeTruthy();
  return ((await response.json()) as { accessToken: string }).accessToken;
}
