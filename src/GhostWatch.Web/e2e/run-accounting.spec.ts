import {test, expect} from '@playwright/test';

test('T2 accounting persists through editing and reports capital efficiency in Runs, Track KPIs, charts and snapshots', async ({page, request}) => {
  test.setTimeout(90000);
  const suffix = Date.now();
  const track = await (await request.post('/api/economics/tracks', {data:{name:`T2 accounting ${suffix}`, description:'', notes:'', status:'Active', purpose:'Cashflow'}})).json();
  const ratioLabel = 'Profit / Slot-Day / ISK Tied Up';
  await page.goto(`/runs/new?trackId=${track.id}`);
  await page.getByLabel('Run name', {exact:true}).fill(`T2 product test ${suffix}`);
  await page.getByRole('combobox', {name:'Run type', exact:true}).selectOption('Manufacturing');
  for (const [label, value] of [
    ['Expected input cost (ISK)', '100'], ['Expected job cost (ISK)', '10'], ['Expected other cost (ISK)', '20'], ['Expected revenue (ISK)', '200'],
    ['Actual input cost (ISK)', '110'], ['Actual job cost (ISK)', '15'], ['Actual other cost (ISK)', '25'], ['Actual revenue (ISK)', '250'], ['Capital tied up (ISK)', '200000']
  ]) await page.getByLabel(label, {exact:true}).fill(value);
  await page.getByText('Duration and efficiency', {exact:true}).click();
  await page.getByLabel('Manufacturing duration (hours)', {exact:true}).fill('24');
  await page.getByLabel('Concurrent slots', {exact:true}).fill('2');
  await page.getByRole('button', {name:'Create Run', exact:true}).click();
  await expect(page).toHaveURL(/\/runs\/[a-f0-9-]{36}$/);
  const runUrl = page.url(); const runId = runUrl.split('/').pop()!;
  await expect(page.getByRole('row').filter({hasText:ratioLabel})).toContainText('0.00025 ISK/slot-day/ISK');
  await expect(page.getByRole('row').filter({hasText:'Expected cost (ISK)'})).toContainText('130.00');
  await expect(page.getByRole('row').filter({hasText:'Actual cost (ISK)'})).toContainText('150.00');
  await page.reload();
  await expect(page.getByLabel('Expected job cost (ISK)', {exact:true})).toHaveValue('10');
  await expect(page.getByLabel('Actual job cost (ISK)', {exact:true})).toHaveValue('15');
  await expect(page.getByLabel('Capital tied up (ISK)', {exact:true})).toHaveValue('200000');
  await page.getByLabel('Expected job cost (ISK)', {exact:true}).fill('20');
  await page.getByLabel('Actual job cost (ISK)', {exact:true}).fill('25');
  await page.getByLabel('Capital tied up (ISK)', {exact:true}).fill('90000');
  await page.getByRole('combobox', {name:'Status', exact:true}).selectOption('Evaluated');
  await page.getByRole('combobox', {name:'Verdict', exact:true}).selectOption('Scale');
  await page.getByLabel('Started at (UTC)', {exact:true}).fill(new Date(Date.now()-86400000).toISOString().slice(0,16));
  await page.getByLabel('Completed at (UTC)', {exact:true}).fill(new Date().toISOString().slice(0,16));
  await page.getByRole('button', {name:'Save Run', exact:true}).click();
  await expect(page.getByRole('status')).toContainText('Run saved');
  await page.reload();
  await expect(page.getByRole('row').filter({hasText:ratioLabel})).toContainText('0.0005 ISK/slot-day/ISK');
  await expect(page.getByRole('row').filter({hasText:'Actual profit (ISK)'})).toContainText('90.00');
  const saved = await (await request.get(`/api/economics/runs/${runId}`)).json();
  expect(saved.run).toMatchObject({expectedJobCost:20, actualJobCost:25, capitalTiedUp:90000, expectedOtherCost:20, actualOtherCost:25});

  await page.goto(`/tracks/${track.id}`);
  await page.getByText('Select Track KPIs (up to six)', {exact:true}).click();
  await page.getByRole('checkbox', {name:ratioLabel, exact:true}).check();
  await page.getByRole('button', {name:'Save selected KPIs'}).click();
  await expect(page.getByRole('status').filter({hasText:'Selected KPIs saved'})).toBeVisible();
  await page.reload();
  await expect(page.getByRole('article').filter({has:page.getByRole('heading', {name:ratioLabel, exact:true})})).toContainText('0.0005 ISK/slot-day/ISK');

  const snapshotName = `Accounting capture ${suffix}`;
  const snapshot = await (await request.post('/api/economics/snapshots', {data:{name:snapshotName}})).json();
  const history = await (await request.get(`/api/economics/snapshots/${snapshot.id}`)).json();
  expect(history.schemaVersion).toBe(2);
  expect(history.values.tracks.find((x:{id:string})=>x.id===track.id).metrics.capitalEfficiency).toBe(.0005);
  const config = {title:`Capital efficiency ${suffix}`, type:'bar', dataSource:'runs', filters:{trackId:track.id}, x:{field:'name'}, series:[{field:'capitalEfficiency', label:ratioLabel, aggregation:'latest', format:'ratio'}], timeRange:'all'};
  await page.goto('/charts');
  await page.getByLabel('Definition name', {exact:true}).fill(config.title);
  await page.getByLabel('Chart configuration JSON').fill(JSON.stringify(config));
  await expect(page.getByRole('heading', {name:config.title, exact:true})).toBeVisible();
  await page.getByText('Chart data (1 source records)', {exact:true}).click();
  await expect(page.getByRole('row').filter({hasText:`T2 product test ${suffix}`})).toContainText('0.0005 ISK/slot-day/ISK');
  await page.getByRole('button', {name:'Save definition', exact:true}).click();
  await expect(page.getByRole('status')).toContainText('Chart definition saved');
  const definitions = await (await request.get('/api/economics/charts/definitions')).json();
  const definition = definitions.find((x:{name:string})=>x.name===config.title);
  await page.goto(`/charts?edit=${definition.id}`);
  await expect(page.getByLabel('Chart configuration JSON')).toHaveValue(/capitalEfficiency/);

  await page.goto(runUrl);
  for (const label of ['Expected job cost (ISK)', 'Actual job cost (ISK)', 'Capital tied up (ISK)']) await page.getByLabel(label, {exact:true}).fill('');
  await page.getByRole('button', {name:'Save Run', exact:true}).click();
  await expect(page.getByRole('status')).toContainText('Run saved');
  await page.reload();
  for (const label of ['Expected job cost (ISK)', 'Actual job cost (ISK)', 'Capital tied up (ISK)']) await expect(page.getByLabel(label, {exact:true})).toHaveValue('');
  await expect(page.getByRole('row').filter({hasText:ratioLabel})).toContainText('Unknown');
  await expect(page.getByRole('row').filter({hasText:'Actual profit (ISK)'})).toContainText('Unknown');
  await page.goto(`/charts?edit=${definition.id}`);
  await expect(page.getByText('Chart unavailable until its values are recorded.')).toBeVisible();
  await page.getByText('Chart data (1 source records)', {exact:true}).click();
  await expect(page.getByRole('row').filter({hasText:`T2 product test ${suffix}`})).toContainText('Unknown');
  await page.goto('/snapshots');
  await page.getByRole('button', {name:`View ${snapshotName}`, exact:true}).click();
  await page.getByText(`${track.name} · Active · Cashflow`, {exact:true}).click();
  await expect(page.getByRole('row').filter({hasText:ratioLabel})).toContainText('0.0005 ISK/slot-day/ISK');
  expect((await (await request.get(`/api/economics/snapshots/${snapshot.id}`)).json()).values).toEqual(history.values);
});
