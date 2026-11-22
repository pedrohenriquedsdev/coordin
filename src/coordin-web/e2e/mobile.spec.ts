import { expect, test } from '@playwright/test';
import { apiToken, loginAs, navigateInApp, uniqueSuffix } from './helpers';

test('the schedule fits a 360 px phone screen without horizontal scrolling', async ({
  page,
  request,
}) => {
  const suffix = uniqueSuffix();
  const title = `Demanda celular ${suffix}`;

  // Os dados entram pela API: no celular o produto só consulta (RNF-03).
  const token = await apiToken(request);
  const headers = { Authorization: `Bearer ${token}` };
  const team = await (
    await request.post('/api/teams', { headers, data: { name: `Time celular ${suffix}` } })
  ).json();
  const person = await (
    await request.post('/api/people', {
      headers,
      data: { name: `Pessoa celular ${suffix}`, teamId: team.id, dailyCapacityHours: 8 },
    })
  ).json();
  const created = await request.post('/api/demands', {
    headers,
    data: {
      title,
      startDate: '2027-01-04',
      informedEndDate: null,
      teams: [{ teamId: team.id, estimatedEffortHours: 16 }],
      assignedPersonIds: [person.id],
    },
  });
  expect(created.status()).toBe(201);

  await loginAs(page);
  // O cronograma fica em uma lista suspensa do menu: abrir pelo endereço vale para qualquer largura.
  await navigateInApp(page, '/');
  // Só o cartão visível conta: em 360 px a tabela do computador fica escondida.
  await expect(
    page.getByRole('main').getByText(title).filter({ visible: true }).first(),
  ).toBeVisible();

  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - window.innerWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
});
