import {test,expect} from '@playwright/test';

test('Needs Attention acknowledgement survives reload, stays inspectable and restores without editing the subject',async({page,request})=>{
 const name=`Accepted readiness ${Date.now()}`;
 const input={name,description:'',type:'Objective',status:'Active',targetDate:null,manualProgress:null,conditions:[{label:'Facility eligibility',done:false}],notes:'Deliberately outstanding'};
 const create=await request.post('/api/economics/objectives',{data:input});expect(create.ok()).toBeTruthy();
 const objective=await create.json();
 const other=await request.post('/api/economics/objectives',{data:{...input,name:name+' other'}});expect(other.ok()).toBeTruthy();
 const before=await(await request.get(`/api/economics/objectives/${objective.id}`)).json();
 await page.goto('/');
 const active=page.getByRole('list',{name:'Active findings',exact:true});
 const finding=active.getByRole('listitem').filter({hasText:name+' has incomplete checklist conditions.'});
 await expect(finding).toBeVisible();
 await finding.getByRole('button',{name:'Acknowledge',exact:true}).click();
 await expect(finding).toHaveCount(0);
 await expect(active).toContainText(name+' other');
 expect(await(await request.get(`/api/economics/objectives/${objective.id}`)).json()).toEqual(before);
 await page.reload();await expect(active).toBeVisible();await expect(finding).toHaveCount(0);
 await page.getByRole('button',{name:/Show acknowledged/}).click();
 const acknowledged=page.getByRole('list',{name:'Acknowledged findings',exact:true});
 const accepted=acknowledged.getByRole('listitem').filter({hasText:name+' has incomplete checklist conditions.'});
 await expect(accepted).toContainText('Condition still matches');await expect(accepted).toContainText('Acknowledged');
 await accepted.getByRole('button',{name:'Restore',exact:true}).click();
 await expect(finding).toBeVisible();await expect(accepted).toHaveCount(0);
 await page.reload();await expect(finding).toBeVisible();
 expect(await(await request.get(`/api/economics/objectives/${objective.id}`)).json()).toEqual(before);
 // A cleared accepted item remains inspectable, without suggesting its condition still matches.
 await finding.getByRole('button',{name:'Acknowledge',exact:true}).click();await expect(finding).toHaveCount(0);
 expect((await request.put(`/api/economics/objectives/${objective.id}`,{data:{...input,status:'Completed',revision:before.revision}})).ok()).toBeTruthy();
 await page.reload();await page.getByRole('button',{name:/Show acknowledged/}).click();
 await expect(accepted).toContainText('Condition no longer matches');await expect(accepted.getByRole('link')).toHaveCount(0);
 await accepted.getByRole('button',{name:'Restore',exact:true}).click();await expect(accepted).toHaveCount(0);await expect(finding).toHaveCount(0);
});
