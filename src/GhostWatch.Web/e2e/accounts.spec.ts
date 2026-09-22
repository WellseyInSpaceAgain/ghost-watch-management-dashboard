import { test, expect } from '@playwright/test';

test('accounts persist manual subscription and characters can edit assignments and Track links', async ({ page, request }) => {
  const suffix = Date.now();
  const name = `Main account ${suffix}`;
  const trackResponse = await request.post('/api/economics/tracks', { data: { name: `Research ${suffix}`, description: '', notes: '', status: 'Active', purpose: 'R&D' } });
  const track = await trackResponse.json();
  await page.goto('/accounts');
  await page.getByLabel('Account name', { exact: true }).fill(name);
  await page.getByRole('combobox', { name: 'Subscription', exact: true }).selectOption('Omega');
  await page.getByRole('button', { name: 'Save account' }).click();
  await expect(page.getByRole('status')).toContainText('Account saved');
  await page.reload();
  const row = page.getByRole('row').filter({ hasText: name });
  await expect(row).toContainText('Omega');
  const accounts = await (await request.get('/api/management/accounts')).json();
  const account = accounts.find((x: {name:string}) => x.name === name);
  let saved = { accountId: null as string | null, assignment: '', notes: '', revision: 0, tracks: [] as {trackId:string;name:string}[] };
  await page.route('**/api/eve/characters/7/data', route => route.fulfill({ json: { character: { characterId: 7, characterName: 'Planning pilot' }, progress: { state: 'idle' }, sections: [], jobs: [], capacity: null } }));
  await page.route('**/api/management/characters/7', route => {
    if (route.request().method() === 'PUT') {
      const body = route.request().postDataJSON();
      saved = { ...body, revision: 1, tracks: body.trackIds.map((id: string) => ({ trackId: id, name: track.name })) };
      return route.fulfill({ json: { revision: 1 } });
    }
    return route.fulfill({ json: saved });
  });
  await page.goto('/characters/7');
  await page.getByRole('combobox', { name: 'Account', exact: true }).selectOption({ label: `${name} · Omega` });
  await page.getByLabel('Economic assignment', { exact: true }).fill('Research / Invention');
  await page.getByRole('checkbox', { name: `${track.name} Open Track` }).check();
  await page.getByRole('button', { name: 'Save assignment' }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Assignment saved' })).toBeVisible();
  expect(saved.accountId).toBe(account.id);
  await page.reload();
  await expect(page.getByLabel('Economic assignment', { exact: true })).toHaveValue('Research / Invention');
  await expect(page.getByRole('checkbox', { name: `${track.name} Open Track` })).toBeChecked();
});
