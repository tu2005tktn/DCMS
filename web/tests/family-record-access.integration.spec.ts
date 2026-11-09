import { expect as baseExpect, test, type APIRequestContext, type Page } from '@playwright/test';

const expect = baseExpect.configure({ timeout: 15000 });

const apiUrl = process.env.E2E_FAMILY_API_URL, webUrl = process.env.E2E_FAMILY_WEB_URL, password = process.env.E2E_DEMO_PASSWORD;
test.skip(process.env.E2E_FAMILY_ISOLATED !== '1' || !apiUrl || !webUrl || !password, 'Requires explicitly isolated family fixture API/web and demo password.');

async function auth(request: APIRequestContext, userName: string) {
  const response = await request.post(`${apiUrl}/api/auth/sign-in`, { data: { userName, password } });
  expect(response.ok(), `Sign-in failed for ${userName}: ${response.status()}`).toBeTruthy();
  return { Authorization: `Bearer ${(await response.json()).accessToken}` };
}
async function ok(response: Awaited<ReturnType<APIRequestContext['post']>>) {
  expect(response.ok(), await response.text()).toBeTruthy(); return response.json();
}
async function choose(page: Page, label: string, option: string) {
  const box = page.getByRole('combobox', { name: new RegExp(`${label}$`) }); await box.click();
  const list = await box.getAttribute('aria-controls');
  await page.locator('.ant-select-dropdown').filter({ has: page.locator(`[id="${list}"]`) }).getByText(option, { exact: true }).click();
}

test('web: book for a relative without an account, verify consent, read official records, then revoke', async ({ page, browser, request }, testInfo) => {
  test.setTimeout(120000);
  page.setDefaultTimeout(15000);
  const suffix = crypto.randomUUID().slice(0, 8), name = `SYNTHETIC người thân ${suffix}`;
  const assistant = await auth(request, 'assistant.demo'), dentist = await auth(request, 'dentist.demo');
  const customerPhone = '09' + String(Math.floor(Math.random() * 100000000)).padStart(8, '0');
  const customerPassword = 'Aa1!' + crypto.randomUUID();
  await ok(await request.post(`${apiUrl}/api/auth/register`, { data: { fullName: `SYNTHETIC người đặt ${suffix}`, phoneNumber: customerPhone, email: `family-${suffix}@example.invalid`, password: customerPassword, confirmPassword: customerPassword } }));
  const runtimeErrors: string[] = []; page.on('pageerror', error => runtimeErrors.push(error.message));
  await page.goto(`${webUrl}/customer/sign-in`);
  await page.getByLabel('Số điện thoại', { exact: true }).fill(customerPhone);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(customerPassword);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/\/customer\/home/);
  const owner = { Authorization: `Bearer ${await page.evaluate(() => sessionStorage.getItem('dcms.customer.accessToken'))}` };
  await page.goto(`${webUrl}/customer/family-members`);
  await page.getByRole('button', { name: 'Thêm người thân', exact: true }).click();
  const memberDrawer = page.getByRole('dialog');
  await memberDrawer.getByLabel(/Họ tên$/).fill(name);
  await choose(page, 'Quan hệ', 'Con'); await memberDrawer.getByLabel('Ngày sinh (không bắt buộc)').fill('2015-03-12');
  await choose(page, 'Giới tính', 'Nam'); await memberDrawer.getByLabel('Số điện thoại liên hệ').fill('0912345678');
  await memberDrawer.getByRole('button', { name: 'Lưu', exact: true }).click(); await expect(memberDrawer).not.toBeVisible();
  const relative = (await ok(await request.get(`${apiUrl}/api/me/family-members?keyword=${encodeURIComponent(name)}`, { headers: owner }))).items[0];
  expect(relative.fullName).toBe(name);
  console.log('Family flow: created relative through customer UI.');
  const row = page.getByRole('row').filter({ hasText: name }); await row.getByRole('link', { name: 'Đặt lịch', exact: true }).click();
  await expect(page.getByText(new RegExp(`${suffix} · Con`))).toBeVisible();
  await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
  const options = await ok(await request.get(`${apiUrl}/api/availability/options`, { headers: owner }));
  const reception = await ok(await request.get(`${apiUrl}/api/reception/options`, { headers: assistant }));
  const branch = options.branches.find((b: { name: string }) => b.name === 'S09 - Chi nhánh thử nghiệm');
  const doctor = options.employees.find((d: { branchId: number; name: string }) => d.branchId === branch.id && d.name === 'Nha sĩ demo');
  await choose(page, 'Chi nhánh', branch.name); await choose(page, 'Bác sĩ', doctor.name);
  await page.getByLabel('Ngày khám', { exact: true }).fill(reception.clinicDate);
  await page.locator('.customer-slots button').first().click(); await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
  await expect(page.getByLabel('Họ tên người khám', { exact: true })).toHaveValue(name);
  await page.getByLabel('Số điện thoại Việt Nam hoặc email', { exact: true }).fill('0912345678');
  await page.getByRole('button', { name: 'Xem lại', exact: true }).click();
  const bookingResponse = page.waitForResponse(r => r.url().endsWith('/api/appointments') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Xác nhận đặt lịch', exact: true }).click();
  const booked = await ok(await bookingResponse); await expect(page.getByText(booked.code, { exact: true })).toBeVisible();
  console.log('Family flow: booked relative through customer UI.');
  expect((await request.get(`${apiUrl}/api/me/family-members/${relative.id}/record`, { headers: owner })).status()).toBe(403);

  // Reception creates/chooses the Patient explicitly. The relative never receives an account.
  const patient = (await ok(await request.post(`${apiUrl}/api/patients`, { headers: assistant, data: {
    fullName: name, birthDate: '2015-03-12', gender: 'MALE', phone: '0912345678', duplicatesReviewed: true, requestKey: crypto.randomUUID(),
  } }))).patient;
  const checked = await ok(await request.post(`${apiUrl}/api/reception/appointments/${booked.appointmentId}/check-in`, { headers: assistant, data: {
    patientId: patient.id, expectedVersion: booked.version, requestKey: crypto.randomUUID(), identityConfirmed: true, purpose: 'InitialExamination',
  } }));
  const started = await ok(await request.post(`${apiUrl}/api/visits/${checked.visitId}/start`, { headers: dentist, data: { patientId: patient.id, expectedVersion: checked.visitVersion, requestKey: crypto.randomUUID() } }));
  await ok(await request.put(`${apiUrl}/api/visits/${checked.visitId}/examination`, { headers: dentist, data: {
    patientId: patient.id, expectedVersion: started.visit.visitVersion, requestKey: crypto.randomUUID(), confirm: true,
    content: { ...started.initialContent, symptoms: 'SYNTHETIC kiểm tra người thân', diagnoses: ['Chưa phát hiện bất thường'], conclusion: 'SYNTHETIC kết quả chính thức', advice: 'SYNTHETIC hướng dẫn chăm sóc', notes: 'SYNTHETIC INTERNAL NOTE', disposition: 'ConsultationOnly' },
  } }));
  console.log('Family flow: reception and confirmed examination created through real APIs.');
  await page.goto(`${webUrl}/customer/family-members`); await page.getByRole('row').filter({ hasText: name }).getByRole('button', { name: 'Xem hồ sơ', exact: true }).click();
  const records = page.getByRole('dialog', { name: `Hồ sơ của ${name}`, exact: true });
  await expect(records.getByText('Chưa yêu cầu quyền xem hồ sơ', { exact: true })).toBeVisible();
  await records.getByLabel('Mã hồ sơ người thân (nếu biết)').fill(patient.patientCode);
  await records.getByLabel('Thông tin giúp phòng khám đối chiếu').fill('SYNTHETIC đã đến khám cùng người thân và có đồng ý chia sẻ.');
  await records.getByRole('button', { name: 'Yêu cầu quyền xem hồ sơ', exact: true }).click();
  await expect(records.getByText('Chờ phòng khám xác minh', { exact: true }).first()).toBeVisible();
  await expect(records.getByRole('tab', { name: 'Lịch sử khám', exact: true })).toHaveCount(0);
  console.log('Family flow: requested access and pending access cannot read records.');

  const staffContext = await browser.newContext(); const staffPage = await staffContext.newPage();
  try {
    await staffPage.goto(`${webUrl}/sign-in`); await staffPage.getByLabel('Tên đăng nhập', { exact: true }).fill('assistant.demo');
    await staffPage.getByLabel('Mật khẩu', { exact: true }).fill(password!); await staffPage.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
    await expect(staffPage.getByRole('heading', { name: 'Tài khoản của tôi', exact: true })).toBeVisible({ timeout: 15000 });
    await staffPage.goto(`${webUrl}/link-verification`); await staffPage.getByRole('tab', { name: 'Hồ sơ người thân', exact: true }).click();
    await staffPage.getByRole('row').filter({ hasText: name }).getByRole('button', { name: 'Đối chiếu', exact: true }).click();
    const review = staffPage.getByRole('dialog');
    await review.getByRole('button', { name: 'Tra cứu hồ sơ', exact: true }).click();
    await review.getByRole('row').filter({ hasText: patient.patientCode }).getByRole('radio').check();
    await review.getByRole('checkbox', { name: 'Lịch sử khám', exact: true }).check();
    await choose(staffPage, 'Căn cứ chia sẻ', 'Sự đồng ý của bệnh nhân');
    await review.getByLabel('Kết quả đối chiếu và bằng chứng đã kiểm tra').fill('SYNTHETIC danh tính và đồng ý chia sẻ đã được kiểm tra tại tiếp nhận.');
    await review.getByRole('checkbox', { name: 'Đã đối chiếu danh tính và kiểm tra sự đồng ý/căn cứ đại diện', exact: true }).check();
    await review.getByRole('button', { name: 'Cấp quyền xem hồ sơ', exact: true }).click();
    await expect(review.getByText('Đã được cấp quyền', { exact: true })).toBeVisible();
    console.log('Family flow: assistant verified access through staff UI.');
    await records.getByRole('button', { name: 'Tải lại trạng thái', exact: true }).click();
    await records.getByRole('tab', { name: 'Lịch sử khám', exact: true }).click();
    await records.locator('.family-record-section .ant-collapse-header').first().click();
    await expect(records.getByText('SYNTHETIC hướng dẫn chăm sóc', { exact: true })).toBeVisible();
    await expect(records.getByText('SYNTHETIC INTERNAL NOTE', { exact: true })).toHaveCount(0);
    await expect(records.getByRole('tab', { name: 'Đơn thuốc', exact: true })).toHaveCount(0);
    for (const width of [1440, 390]) {
      await page.setViewportSize({ width, height: 1000 });
      await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width + 1);
      await page.screenshot({ path: testInfo.outputPath(`family-record-${width}.png`), fullPage: true });
    }
    await review.getByLabel('Lý do quyết định quyền người thân').fill('SYNTHETIC người thân yêu cầu dừng chia sẻ.');
    await review.getByRole('button', { name: 'Thu hồi quyền', exact: true }).click();
    await expect(review.getByText('Đã thu hồi', { exact: true })).toBeVisible();
    await page.evaluate(() => window.dispatchEvent(new Event('focus')));
    await expect(records.getByRole('tab', { name: 'Lịch sử khám', exact: true })).toHaveCount(0);
    await expect(records.getByText('SYNTHETIC hướng dẫn chăm sóc', { exact: true })).toHaveCount(0);
    expect((await request.get(`${apiUrl}/api/me/family-members/${relative.id}/record/examinations`, { headers: owner })).status()).toBe(403);
    expect(runtimeErrors).toEqual([]);
    console.log('Family flow: scoped record reads, responsive display and revoked access passed.');
  } finally { await staffContext.close(); }
});
