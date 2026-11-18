import { expect, test } from '@playwright/test';
import { loginAs } from './helpers';

test('login leads to the schedule, and logout closes the session', async ({ page }) => {
  await loginAs(page);
  await expect(page.getByRole('heading', { name: 'Cronograma' })).toBeVisible();

  await page.getByRole('button', { name: 'Sair' }).click();
  await expect(page).toHaveURL(/\/login(\?|$)/);
  await expect(page.getByRole('heading', { name: 'Entrar' })).toBeVisible();

  // Depois do logout, uma tela protegida volta ao login e não mostra dados (RNF-04).
  await page.goto('/teams');
  await expect(page).toHaveURL(/\/login(\?|$)/);
  await expect(page.getByRole('navigation', { name: 'Principal' })).toHaveCount(0);
});
