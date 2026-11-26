import { test, expect, type APIRequestContext, type Page } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { staffLogin, password, suffix } from './helpers';

const api = process.env.E2E_API_BASE_URL ?? 'http://localhost:5080';
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });

async function auth(request: APIRequestContext, name: string) {
  const r = await request.post(`${api}/api/auth/sign-in`, { data: { userName: name, password } });
  expect(r.ok(), await r.text()).toBeTruthy();
  return { Authorization: `Bearer ${(await r.json()).accessToken}` };
}

async function checkedInVisit(request: APIRequestContext) {
  const headers = await auth(request, 'assistant.demo');
  const options = await (await request.get(`${api}/api/reception/options`, { headers })).json();
  const branchId = options.calendar.branches[0].id;
  const dentistId = options.calendar.dentists.find((x: { name: string }) => x.name === 'Nha sĩ demo').id;
  const serviceId = options.calendar.services[0].id;
  const p = await request.post(`${api}/api/patients`, { headers, data: { fullName: `Khám Hệ Thống ${randomUUID().slice(0, 6)}`, birthDate: '2000-01-01', gender: 'FEMALE', duplicatesReviewed: true } });
  expect(p.ok(), await p.text()).toBeTruthy();
  const patient = (await p.json()).patient;
  const av = await (await request.get(`${api}/api/availability?branchId=${branchId}&dentistId=${dentistId}&serviceId=${serviceId}&date=${options.clinicDate}&durationMinutes=1`, { headers })).json();
  const slot = av.slots.find((x: { start: string }) => Date.parse(x.start) > Date.now() + 10000);
  expect(slot, 'a bookable slot must remain today').toBeTruthy();
  const booked = await request.post(`${api}/api/appointments`, { headers, data: { branchId, booker: { name: 'Người đặt', phone: '0912345678' },
    attendee: { patientId: patient.id, name: patient.fullName }, attendeeRelation: 'Relative', kind: 'WalkIn', source: 'InPerson', serviceId, dentistId,
    chairId: slot.chairId, startAt: slot.start, durationMinutes: 1, exceptionReason: 'System test walk-in', requestKey: randomUUID(),
    scheduleVersion: slot.scheduleVersion, bookingSettingsVersion: slot.bookingSettingsVersion } });
  expect(booked.ok(), await booked.text()).toBeTruthy();
  const appt = await booked.json();
  const checked = await request.post(`${api}/api/reception/appointments/${appt.appointmentId}/check-in`, { headers, data: { patientId: patient.id,
    expectedVersion: appt.version, requestKey: randomUUID(), identityConfirmed: true, purpose: 'InitialExamination', note: 'Đau răng' } });
  expect(checked.ok(), await checked.text()).toBeTruthy();
  return { patient, visitId: (await checked.json()).visitId as number };
}

test('SYS5-01/02/03/05 dentist examines a checked-in patient and ends the visit', async ({ page, request }) => {
  const f = await checkedInVisit(request);
  await staffLogin(page, 'dentist.demo');
  await page.goto(`/visits/${f.visitId}`);
  const chart = page.getByRole('region', { name: 'Sơ đồ răng', exact: true });
  await test.step('SYS5-01 start the examination', async () => {
    await page.getByRole('button', { name: 'Bắt đầu khám', exact: true }).click();
    await expect(page.getByLabel('Lý do khám / triệu chứng', { exact: true })).toBeEnabled();
  });
  await test.step('SYS5-02 record findings on the tooth chart and keep them after reload', async () => {
    await page.getByLabel('Lý do khám / triệu chứng', { exact: true }).fill('Đau răng 16');
    await chart.getByRole('button', { name: 'Răng 16', exact: true }).click();
    await chart.getByRole('textbox', { name: 'Ghi nhanh kết quả', exact: true }).fill('Sâu mặt nhai răng 16');
    await chart.getByRole('button', { name: 'Áp dụng cho 1 răng', exact: true }).click();
    await page.getByRole('button', { name: 'Lưu nháp', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Lưu nháp', exact: true })).toBeEnabled();
    await page.reload();
    await expect(page.getByLabel('Lý do khám / triệu chứng', { exact: true })).toHaveValue('Đau răng 16');
    await expect(page.getByLabel('Tình trạng 1', { exact: true })).toHaveValue('Sâu mặt nhai răng 16');
  });
  await test.step('SYS5-03 diagnosis and handling direction', async () => {
    await page.getByRole('tab', { name: 'Tổng kết khám', exact: true }).click();
    const field = page.locator('.ant-form-item').filter({ has: page.getByRole('combobox', { name: 'Chẩn đoán', exact: true }) });
    await field.getByRole('combobox', { name: 'Chẩn đoán', exact: true }).fill('Sâu răng');
    await page.keyboard.press('Enter');
    await expect(field.getByTitle('Sâu răng', { exact: true })).toBeVisible();
    await page.keyboard.press('Escape');
    await page.getByLabel('Dặn dò / lời khuyên', { exact: true }).fill('Vệ sinh răng miệng sau ăn');
    await page.getByRole('combobox', { name: 'Hướng xử trí', exact: true }).click();
    await page.getByText('Tiếp tục điều trị', { exact: true }).click();
    await page.getByRole('button', { name: 'Hoàn tất khám', exact: true }).click();
    await expect(page.getByText('Phiếu khám đã hoàn tất.', { exact: false })).toBeVisible();
  });
  await test.step('SYS5-05 end the visit', async () => {
    await page.getByRole('button', { name: 'Kết thúc lượt khám', exact: true }).click();
    const ending = page.getByRole('dialog');
    await ending.getByRole('checkbox').check();
    await ending.getByRole('button', { name: 'Xác nhận kết thúc lượt', exact: true }).click();
    await expect(ending).not.toBeVisible();
    await expect(page.getByRole('link', { name: 'Lập kế hoạch điều trị', exact: true })).toBeVisible();
  });
});

test('SYS5-10 assistant cannot write the examination', async ({ page, request }) => {
  const f = await checkedInVisit(request);
  await staffLogin(page, 'assistant.demo');
  await page.goto(`/visits/${f.visitId}`);
  await page.waitForTimeout(2500);
  await page.screenshot({ path: 'test-results/sys5-10.png', fullPage: true });
  await expect(page.getByRole('button', { name: 'Bắt đầu khám', exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Lưu nháp', exact: true })).toHaveCount(0);
});

test('SYS5-06/07 prescription: invalid line refused, valid prescription saved', async ({ page }) => {
  const s = suffix().slice(0, 7).toUpperCase(); const medName = `Thuốc KD ${s}`;
  await staffLogin(page, 'owner.demo');
  await page.goto('/settings/medicines');
  await page.getByRole('button', { name: 'Thêm mới', exact: true }).click();
  const drawer = page.getByRole('dialog');
  await drawer.getByLabel('Mã', { exact: true }).fill(`KD${s}`);
  await drawer.getByLabel('Tên', { exact: true }).fill(medName);
  await drawer.getByLabel('Đơn vị gốc', { exact: true }).fill('viên');
  await drawer.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(drawer).not.toBeVisible();
  await page.getByRole('button', { name: 'Đăng xuất' }).click();
  await expect(page).toHaveURL(/sign-in/);
  await staffLogin(page, 'dentist.demo');
  await page.goto('/patients/38');
  await page.getByRole('tab', { name: 'Đơn thuốc' }).click();
  await page.getByRole('button', { name: '+ Kê đơn thuốc' }).click();
  const dlg = page.getByRole('dialog');
  await test.step('SYS5-07 empty medicine or zero quantity is refused', async () => {
    await dlg.getByRole('spinbutton').first().fill('0');
    await dlg.getByRole('button', { name: 'Lưu đơn thuốc', exact: true }).click();
    await page.waitForTimeout(1000);
    await expect(dlg).toBeVisible();
    await page.screenshot({ path: 'test-results/sys5-07.png' });
  });
  await test.step('SYS5-06 valid prescription is saved', async () => {
    await dlg.locator('.ant-select').nth(1).click();
    await page.keyboard.type(medName);
    await page.waitForTimeout(1500);
    await page.locator('.ant-select-dropdown:visible .ant-select-item-option').first().click({ force: true });
    await dlg.getByPlaceholder(/Đơn vị thuốc/).fill('viên');
    await dlg.getByRole('spinbutton').first().fill('2');
    await dlg.getByPlaceholder('Hướng dẫn sử dụng').fill('Uống sau ăn');
    await dlg.getByRole('button', { name: 'Lưu đơn thuốc', exact: true }).click();
    await expect(dlg).not.toBeVisible();
    await expect(page.getByText(medName).first()).toBeVisible();
  });
});
