import { test, expect } from '@playwright/test';
test('named skills, orders, standings, loyalty and PI are readable and searchable', async ({ page }) => {
  await page.route('**/api/management/characters/7', route => route.fulfill({ json: { accountId: null, assignment: '', notes: '', revision: 0, tracks: [] } }));
  await page.route('**/api/eve/characters/7/data', route => route.fulfill({ json: {
    character: { characterId: 7, characterName: 'Economics pilot' }, progress: { state: 'idle' }, sections: [], jobs: [], capacity: null,
    facts: { skills: { skills: [{ skill_name: 'Science', group_name: 'Science', trained_skill_level: 5, active_skill_level: null }] },
      skillQueue: [{ skill_name: 'Advanced Laboratory Operation', finished_level: 4 }],
      marketOrders: [{ type_name: 'Tritanium', location_name: 'Jita station', side: 'Buy', price: 4.5, volume_remain: 20, remaining_value: 90 }],
      standings: [{ from_name: 'Caldari State', from_type: 'faction', standing: 4.1 }],
      loyalty: [{ corporation_name: 'Caldari Navy', loyalty_points: 2500 }],
      planets: [{ planet_name: 'Jita IV', solar_system_name: 'Jita', num_pins: 12, planet_type: 'temperate' }] },
  } }));
  await page.goto('/characters/7');
  await expect(page.getByRole('region', { name: 'Skills', exact: true })).toContainText('Unknown');
  await expect(page.getByRole('region', { name: 'Market orders', exact: true })).toContainText('90.00 ISK');
  await expect(page.getByRole('region', { name: 'Planetary Interaction', exact: true })).toContainText('Jita IV');
  await expect(page.getByRole('region', { name: 'Loyalty points', exact: true })).toContainText('Caldari Navy');
  await expect(page.getByRole('region', { name: 'Standings', exact: true })).toContainText('Caldari State');
  await page.getByRole('textbox', { name: 'Search Skills', exact: true }).fill('nonexistent');
  await expect(page.getByRole('region', { name: 'Skills', exact: true })).toContainText('No records match');
});
