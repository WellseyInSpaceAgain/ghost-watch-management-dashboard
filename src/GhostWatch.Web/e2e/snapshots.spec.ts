import {test,expect} from '@playwright/test';
test('manual snapshot retains captured values after current allocations change',async({page,request})=>{
 const suffix=Date.now();const created=await request.post('/api/economics/capital/pools',{data:{name:`Snapshot pool ${suffix}`,description:'',role:'Other',targetCapital:null}});const pool=await created.json();
 await request.post('/api/economics/capital/adjustments',{data:{fromPoolId:null,toPoolId:pool.id,amount:123456,reason:'Snapshot test'}});
 await page.goto('/snapshots');await page.getByLabel('Snapshot name (optional)',{exact:true}).fill(`Before change ${suffix}`);
 await page.getByRole('button',{name:'Take Snapshot',exact:true}).click();await expect(page.getByRole('status')).toContainText('Snapshot captured');
 await request.post('/api/economics/capital/adjustments',{data:{fromPoolId:null,toPoolId:pool.id,amount:100,reason:'Later change'}});
 await page.reload();await page.getByRole('button',{name:`View Before change ${suffix}`,exact:true}).click();
 const row=page.getByRole('row').filter({hasText:`Snapshot pool ${suffix}`});await expect(row).toContainText('123,456 ISK');await expect(row).not.toContainText('123,556');
 await expect(page.getByRole('heading',{name:'Captured Tracks and KPIs'})).toBeVisible();
});
