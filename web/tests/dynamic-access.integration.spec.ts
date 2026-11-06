import { readFileSync } from 'node:fs';
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiUrl=process.env.E2E_API_URL,password=process.env.E2E_DEMO_PASSWORD,fixturePath=process.env.E2E_ACCESS_STAFF_FIXTURE;
test.skip(!apiUrl||!password||!fixturePath,'Requires isolated runtime and a dedicated synthetic staff fixture.');
async function ok(response:Awaited<ReturnType<APIRequestContext['post']>>){expect(response.ok(),await response.text()).toBeTruthy();return response.json();}
async function auth(request:APIRequestContext,userName:string){return {Authorization:`Bearer ${(await ok(await request.post(`${apiUrl}/api/auth/sign-in`,{data:{userName,password}}))).accessToken}`};}
async function signIn(page:Page,userName:string){
  await page.goto('/sign-in');await page.getByLabel('Tên đăng nhập',{exact:true}).fill(userName);await page.getByLabel('Mật khẩu',{exact:true}).fill(password!);
  await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();await expect(page.getByRole('heading',{name:'Tài khoản của tôi',exact:true})).toBeVisible();
}

test('owner creates checkbox group, assigns staff, then revokes the action for an existing session',async({page,request},testInfo)=>{
  test.setTimeout(120_000);
  const fixture=JSON.parse(readFileSync(fixturePath!,'utf8').replace(/^\uFEFF/,'')) as {userName:string;fullName:string;employeeId:number;accountId:number;branchId:number;schema:string};
  expect(fixture.schema).toMatch(/^dcms_reception_e2e_[a-z0-9]+$/);
  const suffix=crypto.randomUUID().slice(0,8),groupName='SYNTHETIC Quyền thêm hồ sơ '+suffix;
  const revokeReason='SYNTHETIC thu hồi quyền thêm trong phiên đang dùng '+suffix;
  await signIn(page,'owner.demo');await page.goto('/system-settings?tab=access-groups');
  await page.getByRole('button',{name:'Thêm nhóm quyền',exact:true}).click();let dialog=page.getByRole('dialog',{name:'Thiết lập phân quyền',exact:true});
  await dialog.getByLabel('Tên phân quyền',{exact:true}).fill(groupName);await dialog.getByLabel('Mã phân quyền',{exact:true}).fill('SYN-PERM-'+suffix);
  await dialog.getByRole('checkbox',{name:'Thêm hồ sơ bệnh nhân',exact:true}).check();await expect(dialog.getByRole('checkbox',{name:'Xem bệnh nhân',exact:true})).toBeChecked();
  await expect(dialog.getByRole('checkbox',{name:/Ghi và xác nhận khám/})).toBeDisabled();
  await dialog.locator('.ant-modal-body').evaluate(element=>{element.scrollTop=0;});
  await page.screenshot({path:testInfo.outputPath('dynamic-permissions-desktop.png'),fullPage:true});
  await page.setViewportSize({width:390,height:900});await expect.poll(()=>page.evaluate(()=>document.documentElement.scrollWidth)).toBeLessThanOrEqual(391);
  await page.screenshot({path:testInfo.outputPath('dynamic-permissions-mobile.png'),fullPage:true});await page.setViewportSize({width:1440,height:1000});
  const created=page.waitForResponse(response=>response.url().endsWith('/api/system-settings/access-groups/')&&response.request().method()==='POST');
  await dialog.getByRole('button',{name:'Lưu phân quyền',exact:true}).click();const group=await ok(await created);await expect(dialog).not.toBeVisible();
  expect(group.grants.sort()).toEqual(['patients.create','patients.read']);
  await page.getByRole('tab',{name:'Gán cho nhân viên',exact:true}).click();await page.getByRole('button',{name:`Gán quyền ${fixture.fullName}`,exact:true}).click();
  dialog=page.getByRole('dialog').filter({has:page.getByLabel('Nhóm quyền nhân viên',{exact:true})});
  await dialog.getByRole('combobox',{name:'Nhóm quyền nhân viên',exact:true}).press('ArrowDown');await page.locator('.ant-select-dropdown:visible').getByText(groupName,{exact:true}).click();
  await dialog.getByLabel('Lý do gán nhóm',{exact:true}).fill('SYNTHETIC kiểm chứng phân công chỉ thêm hồ sơ');
  const assigned=page.waitForResponse(response=>response.url().endsWith(`/api/system-settings/staff/${fixture.employeeId}/access-group`)&&response.request().method()==='PUT');
  await dialog.getByRole('button',{name:'Lưu nhóm cho nhân viên',exact:true}).click();expect((await assigned).ok()).toBeTruthy();await expect(dialog).not.toBeVisible();
  const owner=await auth(request,'owner.demo'),staffToken=await auth(request,fixture.userName);
  const memberships=await ok(await request.get(`${apiUrl}/api/system-settings/access-groups/staff`,{headers:owner}));
  expect(memberships.find((staff:{accountId:number})=>staff.accountId===fixture.accountId)).toMatchObject({groupId:group.id,branchIds:[fixture.branchId]});
  const staffContext=await page.context().browser()!.newContext();const staffPage=await staffContext.newPage();
  try {
    await signIn(staffPage,fixture.userName);await staffPage.goto('/patients');await staffPage.getByRole('button',{name:'Thêm hồ sơ',exact:true}).click();
    const patientDrawer=staffPage.getByRole('dialog');await patientDrawer.getByLabel('Họ tên',{exact:true}).fill('SYNTHETIC bệnh nhân từ quyền động '+suffix);
    const savedPatient=staffPage.waitForResponse(response=>new URL(response.url()).pathname==='/api/patients'&&response.request().method()==='POST');
    await patientDrawer.getByRole('button',{name:'Lưu',exact:true}).click();const patient=(await ok(await savedPatient)).patient;
    await expect(staffPage).toHaveURL(new RegExp(`/patients/${patient.id}$`));
    await expect(staffPage.getByRole('button',{name:'Sửa thông tin',exact:true})).toHaveCount(0);await expect(staffPage.getByRole('button',{name:'Ngừng sử dụng',exact:true})).toHaveCount(0);
    const forbiddenUpdate=await request.put(`${apiUrl}/api/patients/${patient.id}`,{headers:staffToken,data:{version:patient.version,fullName:patient.fullName,duplicatesReviewed:true}});expect(forbiddenUpdate.status()).toBe(403);
    await page.bringToFront();await page.getByRole('tab',{name:'Nhóm quyền',exact:true}).click();
    await expect(page.getByRole('button',{name:`Ngừng nhóm ${groupName}`,exact:true})).toBeDisabled();
    await page.getByRole('button',{name:`Thiết lập ${groupName}`,exact:true}).click();dialog=page.getByRole('dialog',{name:'Thiết lập phân quyền',exact:true});
    await dialog.getByRole('checkbox',{name:'Thêm hồ sơ bệnh nhân',exact:true}).uncheck();await dialog.getByLabel('Lý do thay đổi phân quyền',{exact:true}).fill(revokeReason);
    await dialog.getByRole('button',{name:'Xem trước thay đổi',exact:true}).click();await expect(dialog.getByText('Ảnh hưởng 1 nhân viên',{exact:true})).toBeVisible();
    const changed=page.waitForResponse(response=>response.url().endsWith(`/api/system-settings/access-groups/${group.id}`)&&response.request().method()==='PUT');
    await dialog.getByRole('button',{name:'Lưu phân quyền',exact:true}).click();expect((await changed).ok()).toBeTruthy();await expect(dialog).not.toBeVisible();
    const denied=await request.post(`${apiUrl}/api/patients`,{headers:staffToken,data:{fullName:'SYNTHETIC phải bị từ chối '+suffix,duplicatesReviewed:true}});expect(denied.status()).toBe(403);
    expect((await request.get(`${apiUrl}/api/patients/${patient.id}`,{headers:staffToken})).ok()).toBeTruthy();
    await staffPage.goto('/patients');await expect(staffPage.getByRole('heading',{name:'Bệnh nhân',exact:true})).toBeVisible();await expect(staffPage.getByRole('button',{name:'Thêm hồ sơ',exact:true})).toHaveCount(0);
    await page.getByRole('button',{name:'Lịch sử phân quyền',exact:true}).click();const history=page.getByRole('dialog',{name:'Lịch sử phân quyền',exact:true});
    await expect(history.getByText(revokeReason,{exact:true})).toBeVisible();
    await page.screenshot({path:testInfo.outputPath('dynamic-permissions-audit.png'),fullPage:true});
  } finally {await staffContext.close();}
});
