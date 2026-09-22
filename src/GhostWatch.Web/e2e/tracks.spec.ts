import { test, expect } from '@playwright/test';

test('create, edit, reload, archive and restore a Track', async ({ page }) => {
  const browserErrors: string[] = [];
  page.on('pageerror', error => browserErrors.push(error.message));
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Economic operations' })).toBeVisible();
  await page.getByRole('link', { name: 'Create Track', exact: true }).click();
  await page.getByLabel('Name', { exact: true }).fill('T2 Workshop');
  await page.getByLabel('Description').fill('Repeatable small-batch production.');
  await page.getByLabel('Notes').fill('Keep expected and actual results separate.');
  await page.getByRole('combobox', { name: 'Status' }).click();
  await page.getByRole('option', { name: 'Active', exact: true }).click();
  await page.getByRole('combobox', { name: 'Purpose' }).click();
  await page.getByRole('option', { name: 'Cashflow', exact: true }).click();
  await page.getByRole('button', { name: 'Create Track', exact: true }).click();
  await expect(page).toHaveURL(/tracks\/[a-f0-9-]{36}$/);
  await expect(page.getByRole('heading', { name: 'T2 Workshop', exact: true })).toBeVisible();
  const trackUrl = page.url();
  await page.reload();
  await expect(page.getByLabel('Notes')).toHaveValue('Keep expected and actual results separate.');
  await page.getByLabel('Notes').fill('First batch: record all input costs.');
  await page.getByRole('button', { name: 'Save changes' }).click();
  await expect(page.getByRole('status')).toHaveText('Changes saved.');
  await page.getByRole('link', { name: 'Overview', exact: true }).click();
  await expect(page.getByRole('link', { name: 'T2 Workshop', exact: true })).toBeVisible();
  await page.screenshot({ path: '/tmp/ghost-watch-dashboard.png', fullPage: true });
  await page.goto(trackUrl);
  await expect(page.getByLabel('Notes')).toHaveValue('First batch: record all input costs.');
  await page.getByRole('combobox', { name: 'Status' }).click();
  await page.getByRole('option', { name: 'Archived', exact: true }).click();
  await page.getByRole('button', { name: 'Save changes' }).click();
  await expect(page.getByText('History is retained.', { exact: false })).toBeVisible();
  await page.getByRole('link', { name: 'Back to Tracks' }).click();
  await expect(page.getByRole('link', { name: 'T2 Workshop', exact: true })).toHaveCount(0);
  await page.getByRole('checkbox', { name: 'Include archived' }).check();
  await page.getByRole('link', { name: 'T2 Workshop', exact: true }).click();
  await expect(page.getByLabel('Notes')).toHaveValue('First batch: record all input costs.');
  await page.getByRole('combobox', { name: 'Status' }).click();
  await page.getByRole('option', { name: 'Active', exact: true }).click();
  await page.getByRole('button', { name: 'Save changes' }).click();
  await expect(page.getByRole('status')).toHaveText('Changes saved.');
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole('button', { name: 'Save changes' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  expect(browserErrors).toEqual([]);
});

test('API failure is actionable and does not pretend there are no Tracks', async ({ page, request }) => {
  const created = await request.post('/api/economics/tracks', { data: {
    name: 'Recovery check', description: '', status: 'Planning', purpose: 'Other', notes: '',
  } });
  expect(created.ok()).toBe(true);
  await page.route('**/api/economics/tracks?*', route => route.abort());
  await page.goto('/tracks');
  await expect(page.getByRole('alert')).toContainText('local API could not be reached');
  await expect(page.getByRole('heading', { name: 'No Tracks yet' })).toHaveCount(0);
  await page.unroute('**/api/economics/tracks?*');
  await page.getByRole('button', { name: 'Retry' }).click();
  await expect(page.getByRole('alert')).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Recovery check', exact: true })).toBeVisible();
});
