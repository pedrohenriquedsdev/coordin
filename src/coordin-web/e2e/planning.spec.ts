import { expect, test } from '@playwright/test';
import { loginAs, uniqueSuffix } from './helpers';

test('a demand created with an estimate shows its predicted end date in the schedule', async ({
  page,
}) => {
  const suffix = uniqueSuffix();
  const team = `Time ${suffix}`;
  const person = `Pessoa ${suffix}`;
  const title = `Demanda ${suffix}`;

  await loginAs(page);

  // 1. Um time, com uma pessoa de capacidade.
  await page.getByRole('link', { name: 'Times e pessoas' }).click();
  await page.getByRole('button', { name: '+ Novo time' }).click();
  await page.getByLabel('Nome do novo time').fill(team);
  await page.getByRole('button', { name: 'Salvar time' }).click();
  // Com o time selecionado, a pessoa entra nele (o campo de time só aparece ao editar).
  await page.getByRole('button', { name: new RegExp(team) }).click();

  await page.getByLabel('Nome', { exact: true }).fill(person);
  await page.getByLabel('Horas por dia útil').fill('8');
  await page.getByRole('button', { name: 'Adicionar pessoa' }).click();
  await expect(page.getByText(person)).toBeVisible();

  // 2. Uma demanda com início, estimativa do time e a pessoa alocada.
  await page.getByRole('link', { name: 'Demandas' }).click();
  await page.getByRole('link', { name: 'Nova demanda' }).click();
  await page.getByLabel('Título').fill(title);
  await page.getByLabel('Data de início').fill('2027-01-04');
  await page.getByLabel(`Envolver o time ${team}`).check();
  await page.getByLabel(`Esforço estimado de ${team} (horas)`).fill('16');
  await page.getByLabel(`${person} (${team})`).check();
  await page.getByRole('button', { name: 'Salvar demanda' }).click();
  await expect(page.getByRole('heading', { name: new RegExp(title) })).toBeVisible();

  // 3. O cronograma mostra uma data de fim prevista, no formato dd/MM/aaaa, e nenhum "Sem previsão" para ela.
  await page.getByRole('link', { name: 'Cronograma' }).click();
  const main = page.getByRole('main');
  await expect(main.getByText(title).first()).toBeVisible();
  await expect(main).toContainText(/Com margem: \d{2}\/\d{2}\/\d{4}/);
  await expect(main).not.toContainText('Sem previsão');
});
