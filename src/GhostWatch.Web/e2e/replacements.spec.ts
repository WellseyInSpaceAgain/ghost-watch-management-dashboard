import {test,expect} from '@playwright/test';
test('replacement estimates and default selection persist',async({page})=>{
 const name=`Doctrine ${Date.now()}`;await page.goto('/replacement-packages');
 await page.getByLabel('Package name',{exact:true}).fill(name);
 await page.getByLabel('Estimated replacement value (ISK)',{exact:true}).fill('800000000');
 await page.getByLabel('Default programme package',{exact:true}).check();
 await page.getByRole('button',{name:'Save package',exact:true}).click();
 await expect(page.getByRole('status')).toContainText('Package saved');
 await page.reload();const row=page.getByRole('row').filter({hasText:name});
 await expect(row).toContainText('800,000,000 ISK');await expect(row).toContainText('Default');
 await page.getByRole('button',{name:`Edit ${name}`,exact:true}).click();
 await expect(page.getByLabel('Estimated replacement value (ISK)',{exact:true})).toHaveValue('800000000');
});
