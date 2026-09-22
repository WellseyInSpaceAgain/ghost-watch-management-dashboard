import { test, expect } from '@playwright/test';

test('inventory separates available stock and copies and supports filters and pagination', async ({ page }) => {
  const available = Array.from({ length: 28 }, (_, index) => ({ typeId: 100 + index, name: `Material ${String(index + 1).padStart(2, '0')}`, category: 'Minerals', quantity: 100, availability: 'Available stock', itemId: 1000 + index, locationId: 60000001, locationFlag: 'Hangar' }));
  const fitted = { typeId: 200, name: 'Fitted module', category: 'Modules', quantity: 1, availability: 'Fitted / contained assets', itemId: 2000, locationId: 9999, locationFlag: 'HiSlot0' };
  await page.route('**/api/eve/characters/7/data', route => route.fulfill({ json: {
    character: { characterId: 7, characterName: 'Inventory pilot' }, progress: { state: 'partial', section: null, error: null }, jobs: [], capacity: null,
    sections: ['assets', 'blueprints'].map(name => ({ name, attemptedAt: '2026-09-22T12:00:00Z', updatedAt: '2026-09-21T12:00:00Z', error: name === 'assets' ? 'Refresh failed on a later page.' : null, warning: name === 'blueprints' ? 'Some names could not be refreshed.' : null, data: [] })),
    inventory: { assets: [...available, fitted], stock: [...available, fitted], blueprints: [
      { itemId: 3000, typeId: 300, name: 'Module Blueprint', kind: 'Original', quantity: 1, materialEfficiency: 10, timeEfficiency: 20, runsRemaining: null, locationId: 60000001, locationFlag: 'Hangar' },
      { itemId: 3001, typeId: 300, name: 'Module Blueprint', kind: 'Copy', quantity: 1, materialEfficiency: 2, timeEfficiency: 4, runsRemaining: 7, locationId: 60000001, locationFlag: 'Hangar' },
    ] },
  } }));
  await page.goto('/characters/7');
  const assets = page.getByRole('region', { name: 'Assets', exact: true });
  const blueprints = page.getByRole('region', { name: 'Blueprints', exact: true });
  await expect(assets.getByRole('alert')).toContainText('last successful collection');
  await expect(assets.getByRole('cell', { name: 'Material 01', exact: false })).toBeVisible();
  await expect(assets.getByRole('cell', { name: 'Material 28', exact: false })).toHaveCount(0);
  await assets.getByRole('button', { name: 'Next page' }).click();
  await expect(assets.getByRole('cell', { name: 'Material 28', exact: false })).toBeVisible();
  await assets.getByRole('combobox', { name: 'Availability', exact: true }).click();
  await page.getByRole('option', { name: 'Fitted / contained assets', exact: true }).click();
  await expect(assets.getByRole('cell', { name: 'Fitted module', exact: false })).toBeVisible();
  await expect(assets.getByRole('cell', { name: 'Material 01', exact: false })).toHaveCount(0);
  await assets.getByRole('combobox', { name: 'Asset view' }).click();
  await page.getByRole('option', { name: 'Item locations' }).click();
  await expect(assets.getByRole('cell', { name: '9999 HiSlot0' })).toBeVisible();
  await assets.getByRole('textbox', { name: 'Search assets' }).fill('absent');
  await expect(assets.getByText('No assets match these filters.')).toBeVisible();
  await expect(blueprints.getByRole('cell', { name: 'Unlimited', exact: true })).toBeVisible();
  await blueprints.getByRole('combobox', { name: 'Blueprint kind' }).click();
  await page.getByRole('option', { name: 'Copies', exact: true }).click();
  await expect(blueprints.getByRole('cell', { name: 'Unlimited', exact: true })).toHaveCount(0);
  await expect(blueprints.getByRole('cell', { name: '7', exact: true })).toBeVisible();
  await expect(blueprints.getByText('Some names could not be refreshed.')).toBeVisible();
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});
