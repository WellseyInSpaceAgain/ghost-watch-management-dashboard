import {test,expect} from '@playwright/test';
test('Track operations select concrete KPIs and prefill execution',async({page,request})=>{
 const name=`Operating Track ${Date.now()}`;const track=await(await request.post('/api/economics/tracks',{data:{name,description:'',notes:'',purpose:'Cashflow',status:'Active'}})).json();
 await page.goto(`/tracks/${track.id}`);
 await expect(page.getByRole('heading',{name:'Capital and performance'})).toBeVisible();
 await page.getByText('Select Track KPIs (up to six)',{exact:true}).click();
 await page.getByRole('checkbox',{name:'Recorded R&D spend',exact:true}).check();
 await page.getByRole('button',{name:'Save selected KPIs'}).click();
 await expect(page.getByRole('status').filter({hasText:'Selected KPIs saved'})).toBeVisible();
 await page.reload();await expect(page.getByRole('heading',{name:'Recorded R&D spend',exact:true})).toBeVisible();
 await page.getByRole('link',{name:'Create Run for Track'}).click();
 await expect(page.getByRole('combobox',{name:'Track',exact:true})).toHaveValue(track.id);
 await page.goto('/');await expect(page.getByRole('row').filter({hasText:name})).toContainText('Unknown');
 await expect(page.getByRole('heading',{name:'Needs Attention'})).toBeVisible();
});
