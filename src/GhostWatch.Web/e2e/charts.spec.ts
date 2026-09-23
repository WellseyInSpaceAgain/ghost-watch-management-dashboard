import {test,expect} from '@playwright/test';
test('chart definitions preview, share across pages and retain dragged layout widths',async({page,request})=>{
 test.setTimeout(60000);const suffix=Date.now();const chartName=`Profit ${suffix}`,secondName=`Count ${suffix}`;
 const track=await(await request.post('/api/economics/tracks',{data:{name:`Chart Track ${suffix}`,description:'',notes:'',purpose:'Cashflow',status:'Active'}})).json();
 const run=await request.post('/api/economics/runs',{data:{run:{name:'Chart batch',trackId:track.id,runType:'Manufacturing',purpose:'Commercial',status:'Completed',startedAt:new Date(Date.now()-86400000).toISOString(),completedAt:new Date().toISOString(),actualInputCost:100,actualOtherCost:0,actualRevenue:150,expectedInputCost:100,expectedOtherCost:0,expectedRevenue:175,productName:'Shield',verdict:'Scale',notes:''}}});expect(run.ok()).toBe(true);
 const config={title:chartName,type:'bar',dataSource:'runs',filters:{trackId:track.id},x:{field:'productName',label:'Product'},series:[{field:'expectedProfit',label:'Expected',aggregation:'sum',format:'isk'},{field:'actualProfit',label:'Actual',aggregation:'sum',format:'isk'}],timeRange:'all'};
 await page.goto('/charts');await expect(page.getByLabel('Chart configuration JSON')).toHaveValue(/Expected and actual profit/);
 await page.getByLabel('Definition name',{exact:true}).fill(chartName);await page.getByLabel('Chart configuration JSON').fill(JSON.stringify(config,null,2));
 await expect(page.getByRole('heading',{name:chartName,exact:true})).toBeVisible();await expect(page.locator('canvas')).toBeVisible();
 await page.getByText('Chart data (1 source records)',{exact:true}).click();await expect(page.getByRole('row').filter({hasText:'Shield'})).toContainText('75 ISK');await expect(page.getByRole('row').filter({hasText:'Shield'})).toContainText('50 ISK');
 await page.getByRole('button',{name:'Save definition',exact:true}).click();await expect(page.getByRole('status')).toContainText('Chart definition saved');
 const defs=await(await request.get('/api/economics/charts/definitions')).json();const definition=defs.find((x:{name:string})=>x.name===chartName);
 const secondConfig={...config,title:secondName,type:'kpi',x:{field:'all'},series:[{field:'actualProfit',label:'Runs',aggregation:'count',format:'count'}]};
 const second=await request.post('/api/economics/charts/definitions',{data:{name:secondName,configJson:JSON.stringify(secondConfig)}});expect(second.ok()).toBe(true);const secondDefinition=await second.json();
 await page.goto('/');await page.getByRole('heading',{name:'Dashboard charts',exact:true}).scrollIntoViewIfNeeded();
 await page.getByRole('combobox',{name:'Chart definition',exact:true}).selectOption(definition.id);await page.getByRole('button',{name:'Add chart to page'}).click();
 await expect(page.locator('[data-testid="chart-placement"]')).toHaveCount(1);
 await page.getByRole('combobox',{name:'Chart definition',exact:true}).selectOption(secondDefinition.id);await page.getByRole('button',{name:'Add chart to page'}).click();await expect(page.locator('[data-testid="chart-placement"]')).toHaveCount(2);
 await page.getByRole('button',{name:'Edit chart layout'}).click();
 const handle=page.getByRole('button',{name:`Drag ${chartName}`,exact:true});const target=page.getByRole('button',{name:`Drag ${secondName}`,exact:true});
 await handle.scrollIntoViewIfNeeded();const start=(await handle.boundingBox())!,end=(await target.boundingBox())!;
 await page.mouse.move(start.x+15,start.y+15);await page.mouse.down();await page.mouse.move(start.x+25,start.y+25,{steps:5});await page.mouse.move(end.x+end.width/2,end.y+end.height/2,{steps:20});await page.mouse.up();
 await expect(page.locator('[data-testid="chart-placement"]').first()).toHaveAttribute('data-definition',secondName);
 await page.getByRole('combobox',{name:`Width for ${chartName}`}).selectOption('Wide');await page.getByRole('combobox',{name:`Width for ${secondName}`}).selectOption('Small');
 await page.getByRole('button',{name:'Save layout',exact:true}).click();await expect(page.getByRole('status')).toContainText('Chart layout saved');
 await page.reload();await page.getByRole('heading',{name:'Dashboard charts',exact:true}).scrollIntoViewIfNeeded();await expect(page.locator('[data-testid="chart-placement"]').first()).toHaveAttribute('data-definition',secondName);await expect(page.locator('[data-testid="chart-placement"]').last()).toHaveAttribute('data-width','Wide');
 await page.goto(`/tracks/${track.id}`);await page.getByRole('heading',{name:'Track charts',exact:true}).scrollIntoViewIfNeeded();await page.getByRole('combobox',{name:'Chart definition',exact:true}).selectOption(definition.id);await page.getByRole('button',{name:'Add chart to page'}).click();await expect(page.getByRole('heading',{name:chartName,exact:true})).toBeVisible();
 await page.goto(`/charts?edit=${definition.id}`);await expect(page.getByLabel('Definition name',{exact:true})).toHaveValue(chartName);await page.getByLabel('Chart configuration JSON').fill(JSON.stringify({...config,title:`Updated ${chartName}`}));await page.getByRole('button',{name:'Save definition',exact:true}).click();await expect(page.getByRole('status')).toContainText('saved');
 await page.goto(`/tracks/${track.id}`);await page.getByRole('heading',{name:'Track charts',exact:true}).scrollIntoViewIfNeeded();await expect(page.getByRole('heading',{name:`Updated ${chartName}`,exact:true})).toBeVisible();await page.getByRole('button',{name:'Edit chart layout'}).click();await page.getByRole('button',{name:'Remove placement',exact:true}).click();await expect(page.locator('[data-testid="chart-placement"]')).toHaveCount(0);
 await page.goto('/');await page.getByRole('heading',{name:'Dashboard charts',exact:true}).scrollIntoViewIfNeeded();await expect(page.getByRole('heading',{name:`Updated ${chartName}`,exact:true})).toBeVisible();
 await page.setViewportSize({width:390,height:844});await expect.poll(()=>page.evaluate(()=>document.documentElement.scrollWidth)).toBe(390);
});
test('supported renderers and invalid JSON preview recover without running code',async({page,request})=>{
 const track=await(await request.post('/api/economics/tracks',{data:{name:`Renderer ${Date.now()}`,description:'',notes:'',purpose:'Other',status:'Active'}})).json();
 await page.goto('/charts');await expect(page.getByLabel('Chart configuration JSON')).toHaveValue(/Expected and actual profit/);
 await page.getByLabel('Chart configuration JSON').fill('{"javascript":"window.unsafeChart=true"}');await expect(page.getByRole('alert')).toContainText('Invalid chart JSON');expect(await page.evaluate(()=>('unsafeChart' in window))).toBe(false);
 for(const type of ['line','bar','stackedBar','pie','donut','kpi']){
  const config={title:`Renderer ${type}`,type,dataSource:'tracks',filters:{trackId:track.id},x:{field:type==='kpi'?'all':'name'},series:[{field:'activeRuns',label:'Tracks',aggregation:'count',format:'count'}]};
  await page.getByLabel('Chart configuration JSON').fill(JSON.stringify(config));await expect(page.getByRole('heading',{name:`Renderer ${type}`,exact:true})).toBeVisible();await expect(page.getByRole('alert')).toHaveCount(0);
  if(type==='kpi')await expect(page.locator('app-chart-renderer .kpi')).toHaveText('1');else await expect(page.locator('canvas')).toBeVisible();
 }
});
