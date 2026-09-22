import { test, expect } from '@playwright/test';

test('refresh shows progress and retains previous wallet on partial failure', async ({ page }) => {
  let refreshed = false;
  let requestedHeader: string | undefined;
  await page.route('**/api/eve/characters/7/data', route => route.fulfill({ json: {
    character: { characterId: 7, characterName: 'Refresh pilot' },
    progress: { state: refreshed ? 'partial' : 'idle', section: null, error: null },
    sections: ['wallet', 'skills', 'skillQueue', 'industryJobs', 'marketOrders'].map(name => ({
      name, attemptedAt: '2026-09-22T12:00:00Z', updatedAt: name === 'wallet' ? '2026-09-21T12:00:00Z' : null,
      error: name === 'wallet' && refreshed ? 'Access denied or scope missing. Reconnect to grant access.' : null,
      data: name === 'wallet' ? 123456.78 : null,
    })),
    capacity: { trained: { manufacturingJobs: 6, researchJobs: 1, reactionJobs: 0, marketOrders: 5, piColonies: 1 }, active: null },
    jobs: [],
  } }));
  await page.route('**/api/eve/characters/7/refresh', route => {
    refreshed = true;
    requestedHeader = route.request().headers()['x-ghost-watch'];
    return route.fulfill({ status: 202, json: { state: 'queued' } });
  });
  await page.goto('/characters/7');
  await expect(page.getByRole('heading', { name: 'Refresh pilot' })).toBeVisible();
  await expect(page.getByText('123,456.78 ISK')).toBeVisible();
  await expect(page.getByRole('row').filter({ hasText: 'Manufacturing jobs' })).toContainText('Unknown');
  await page.getByRole('button', { name: 'Refresh EVE data' }).click();
  await expect(page.getByRole('status')).toContainText('partial');
  expect(requestedHeader).toBe('1');
  await expect(page.getByText('123,456.78 ISK')).toBeVisible();
  await page.getByText('Wallet · Collected · Refresh failed', { exact: true }).click();
  await expect(page.getByText('Access denied or scope missing.', { exact: false })).toBeVisible();
  await expect(page.getByText('Some sections could not be refreshed', { exact: false })).toBeVisible();
});
