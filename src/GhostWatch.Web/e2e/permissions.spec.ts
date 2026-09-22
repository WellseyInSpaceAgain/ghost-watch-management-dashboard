import { test, expect } from '@playwright/test';

const missing = ['esi-universe.read_structures.v1', 'esi-assets.read_assets.v1'];
const permissions = (healthy: boolean) => ({ scopesKnown: true, hasAllRequiredScopes: healthy, missingScopeCount: healthy ? 0 : missing.length, missingScopes: healthy ? [] : missing });
const detail = (healthy: boolean) => ({ character: { characterId: 7, characterName: 'Permission pilot' }, permissions: permissions(healthy), progress: { state: 'idle' }, sections: [], jobs: [], capacity: null });

test('compact permission badges use API status and names still link to detail', async ({ page }) => {
  await page.route('**/api/auth/eve/config', route => route.fulfill({ json: { configured: true, scopes: [] } }));
  await page.route('**/api/eve/characters', route => route.fulfill({ json: [7, 8].map(id => ({ characterId: id, characterName: `Pilot ${id}`, permissions: permissions(id === 8) })) }));
  await page.goto('/characters');
  const warning = page.getByRole('row').filter({ hasText: 'Pilot 7' });
  await expect(warning).toContainText('Missing permissions');
  await expect(warning).not.toContainText('esi-');
  await warning.getByText('Missing permissions', { exact: false }).hover();
  await expect(page.locator('.mat-mdc-tooltip')).toBeVisible();
  await expect(page.locator('.mat-mdc-tooltip')).toContainText('Missing 2 required ESI permissions');
  await expect(page.getByRole('row').filter({ hasText: 'Pilot 8' })).toContainText('Permissions OK');
  await expect(warning.getByRole('link')).toHaveAttribute('href', '/characters/7');
});

test('detail lists missing scopes and reloads healthy status after re-authorisation', async ({ page }) => {
  let healthy = false;
  await page.route('**/api/eve/characters/7/data', route => route.fulfill({ json: detail(healthy) }));
  await page.route('**/api/auth/eve/start?characterId=7', route => {
    healthy = true;
    return route.fulfill({ status: 302, headers: { location: '/characters/7?auth=reauthorised' } });
  });
  await page.goto('/characters/7');
  const panel = page.getByRole('region', { name: 'ESI Permissions' });
  await expect(panel).toContainText('2 required permissions missing');
  for (const name of missing) await expect(panel.getByText(name, { exact: true })).toBeVisible();
  await panel.getByRole('link', { name: 'Re-authorise Character' }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Character re-authorised successfully' })).toBeVisible();
  await expect(panel).toContainText('All required permissions granted');
  await expect(panel).not.toContainText('esi-');
});

for (const outcome of ['cancelled', 'wrong-character']) {
  test(`detail retains permission warning and explains ${outcome} re-authorisation`, async ({ page }) => {
    await page.route('**/api/eve/characters/7/data', route => route.fulfill({ json: detail(false) }));
    await page.goto(`/characters/7?auth=${outcome}`);
    await expect(page.getByRole('alert')).toContainText('existing connection and data were retained');
    await expect(page.getByRole('region', { name: 'ESI Permissions' })).toContainText('2 required permissions missing');
    await expect(page.getByRole('link', { name: 'Re-authorise Character' })).toHaveAttribute('href', '/api/auth/eve/start?characterId=7');
  });
}
