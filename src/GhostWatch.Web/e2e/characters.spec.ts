import { test, expect } from '@playwright/test';

test('unconfigured SSO explains setup and callback errors are actionable', async ({ page }) => {
  await page.goto('/characters');
  await expect(page.getByRole('heading', { name: 'Characters', exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Set up EVE authentication' })).toBeVisible();
  await expect(page.getByText('http://localhost:4200/api/auth/eve/callback', { exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Log in with EVE Online' })).toHaveCount(0);
  await page.goto('/api/auth/eve/callback?state=untrusted&code=untrusted');
  await expect(page).toHaveURL('/characters?auth=invalid-state');
  await expect(page.getByRole('alert')).toContainText('Start a new login here');
});

test('configured connections list only public character information', async ({ page }) => {
  await page.route('**/api/auth/eve/config', route => route.fulfill({ json: {
    configured: true, callbackUrl: 'http://localhost:4200/api/auth/eve/callback', scopes: ['esi-skills.read_skills.v1'],
  } }));
  await page.route('**/api/eve/characters', route => route.fulfill({ json: [
    { characterId: 90000001, characterName: 'Test pilot', connectedAt: '2026-09-22T00:00:00Z', lastAuthenticatedAt: '2026-09-22T00:00:00Z' },
  ] }));
  await page.goto('/characters?auth=connected');
  await expect(page.getByRole('status').filter({ hasText: 'Character connected successfully' })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Test pilot', exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Log in with EVE Online' })).toHaveAttribute('href', '/api/auth/eve/start');
  await expect(page.getByRole('heading', { name: 'Set up EVE authentication' })).toHaveCount(0);
});

test('refresh all queues every character and reports existing refreshes and failures', async ({ page }) => {
  await page.route('**/api/auth/eve/config', route => route.fulfill({ json: { configured: true, callbackUrl: '', scopes: [] } }));
  await page.route('**/api/eve/characters', route => route.fulfill({ json: [1, 2, 3].map(id => ({
    characterId: id, characterName: `Pilot ${id}`, connectedAt: '2026-09-22T00:00:00Z', lastAuthenticatedAt: '2026-09-22T00:00:00Z',
  })) }));
  const requested: string[] = [];
  await page.route('**/api/eve/characters/*/refresh', route => {
    expect(route.request().headers()['x-ghost-watch']).toBe('1');
    expect(route.request().method()).toBe('POST');
    const id = route.request().url().split('/').at(-2)!;
    requested.push(id);
    return route.fulfill({ status: id === '1' ? 202 : id === '2' ? 409 : 500, json: {} });
  });
  await page.goto('/characters');
  const button = page.getByRole('button', { name: 'Refresh all characters' });
  await button.click();
  await expect(page.getByRole('status')).toContainText('1 queued; 1 already queued or refreshing');
  await expect(page.getByRole('alert')).toContainText('Could not queue: Pilot 3');
  expect(requested.sort()).toEqual(['1', '2', '3']);
  await expect(button).toBeEnabled();
});
