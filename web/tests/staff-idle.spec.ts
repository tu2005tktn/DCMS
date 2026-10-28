import { expect, test } from '@playwright/test';

const password = process.env.E2E_DEMO_PASSWORD;
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD from ignored .env.'); });

async function login(page: import('@playwright/test').Page, name:string) {
  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact:true }).fill(name);
  await page.getByLabel('Mật khẩu', { exact:true }).fill(password!);
  await page.getByRole('button', { name:'Đăng nhập', exact:true }).click();
  await expect(page.getByRole('heading', { name:'Tài khoản của tôi' })).toBeVisible();
}

test('S58 staff idle settings preview uses the real API without saving', async ({ page, request }) => {
  const signIn = await request.post('http://localhost:5080/api/auth/sign-in',
    { data:{userName:'owner.demo',password} });
  expect(signIn.ok()).toBe(true);
  const access = (await signIn.json()).accessToken as string;
  const before = await request.get('http://localhost:5080/api/settings/staff-idle',
    {headers:{Authorization:`Bearer ${access}`}});
  expect(before.ok()).toBe(true);
  await login(page, 'owner.demo');
  await page.goto('/staff-idle-settings');
  await expect(page.getByRole('heading', { name:'Tự đăng xuất staff web' })).toBeVisible();
  await expect(page.getByRole('combobox', {name:'Thời gian tự đăng xuất'})).toBeVisible();
  const preview = page.waitForResponse(response => response.url().endsWith('/api/settings/staff-idle/preview'));
  await page.getByRole('button', {name:'Xem trước ảnh hưởng'}).click();
  expect((await preview).status()).toBe(200);
  await expect(page.getByText(/Sẽ ảnh hưởng \d+ phiên staff web/)).toBeVisible();
  const after = await request.get('http://localhost:5080/api/settings/staff-idle',
    {headers:{Authorization:`Bearer ${access}`}});
  expect((await after.json()).version).toBe((await before.json()).version);
});

test('S58 mock idle warning is shown without writing a clinical draft', async ({ page }) => {
  await login(page, 'assistant.demo');
  await page.route('**/api/auth/session/idle', async route => {
    const now = new Date();
    await route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({
      staffIdleTimeoutMinutes:5, lastInteractiveActivityAt:new Date(now.getTime()-270_000).toISOString(),
      deadlineAt:new Date(now.getTime()+30_000).toISOString(), serverNow:now.toISOString(),
    })});
  });
  await page.reload();
  await expect(page.getByText(/Phiên staff sẽ hết hạn sau/)).toBeVisible();
  await expect(page.getByText(/Phiên Patient dùng chính sách riêng/)).toHaveCount(0);
});

test('S58 expired mock session drops an unsubmitted receipt draft before another actor signs in', async ({ page, request }) => {
  const signIn = await request.post('http://localhost:5080/api/auth/sign-in',
    { data:{userName:'assistant.demo',password} });
  const token = (await signIn.json()).accessToken as string;
  const headers = { Authorization:`Bearer ${token}` };
  const patientResponse = await request.post('http://localhost:5080/api/patients', {headers,data:{
    patientCode:null, fullName:`SYNTHETIC idle draft ${Date.now()}`, birthDate:null, gender:null,
    phone:null, address:null, guardianName:null, guardianRelationship:null, guardianPhone:null,
    note:null, duplicatesReviewed:true,
  }});
  expect(patientResponse.status()).toBe(201);
  const patient = (await patientResponse.json()).patient;
  await login(page, 'assistant.demo');
  await page.goto(`/patients/${patient.id}`);
  await page.getByRole('tab', {name:'Điều trị & Thanh toán'}).click();
  await page.getByRole('tab', {name:'Bảng kê',exact:true}).click();
  await page.getByRole('button', {name:'Ghi nhận tiền thực thu'}).click();
  const dialog = page.getByRole('dialog', {name:/Thu tiền/});
  await dialog.getByRole('spinbutton', {name:'Số tiền thực nhận'}).fill('1000');
  await page.clock.install();
  await page.route('**/api/auth/session/activity', route => route.fulfill({status:401,contentType:'application/json',
    body:JSON.stringify({code:'MSG07',message:'Phiên hết hạn, vui lòng đăng nhập lại.'})}));
  await page.route('**/api/auth/refresh', route => route.fulfill({status:401,contentType:'application/json',
    body:JSON.stringify({code:'MSG07',message:'Phiên hết hạn, vui lòng đăng nhập lại.'})}));
  await page.clock.fastForward(31_000);
  await dialog.getByRole('spinbutton', {name:'Số tiền thực nhận'}).press('Tab');
  await expect(page.getByRole('heading', {name:'Đăng nhập'})).toBeVisible();
  const context = await request.get(`http://localhost:5080/api/patients/${patient.id}/billing-context`, {headers});
  expect((await context.json()).receipts).toHaveLength(0);
  await page.unrouteAll();
  await page.getByLabel('Tên đăng nhập', {exact:true}).fill('dentist.demo');
  await page.getByLabel('Mật khẩu', {exact:true}).fill(password!);
  await page.getByRole('button', {name:'Đăng nhập',exact:true}).click();
  await expect(page.getByRole('heading', {name:'Tài khoản của tôi'})).toBeVisible();
  await expect(page.getByRole('dialog', {name:/Thu tiền/})).toHaveCount(0);
});
