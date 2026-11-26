import { test, expect, type APIRequestContext, type Page } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { staffLogin, customerLogin, password, suffix, phone } from './helpers';

const api = process.env.E2E_API_BASE_URL ?? 'http://localhost:5080';
const ymd = (ms: number) => new Date(ms + 7 * 3600_000).toISOString().slice(0, 10);

test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });

async function bookedCustomer(request: APIRequestContext) {
  const opt = await (await request.get(`${api}/api/availability/options`)).json();
  let setup: any = null;
  for (const branch of opt.branches) {
    const dentist = opt.employees.find((e: any) => e.branchId === branch.id && e.canDentist);
    const service = opt.services.find((s: any) => s.branchId === branch.id);
    if (!dentist || !service) continue;
    for (let d = 2; d < 23 && !setup; d++) {
      const day = ymd(Date.now() + d * 86400_000);
      const q = new URLSearchParams({ branchId: branch.id, dentistId: dentist.id, serviceId: service.id, date: day, durationMinutes: service.publicDurationMinutes });
      const r = await request.get(`${api}/api/availability?${q}`);
      if (!r.ok()) continue;
      const { slots } = await r.json();
      if (slots.length >= 2) setup = { branch, dentist, service, slot: slots[0] };
    }
    if (setup) break;
  }
  expect(setup, 'a future demo schedule must exist').toBeTruthy();
  const pass = `Aa1!${randomUUID()}`; const p = phone();
  expect((await request.post(`${api}/api/auth/register`, { data: { fullName: 'Khách Hệ Thống', phoneNumber: p, email: `${randomUUID()}@example.invalid`, password: pass, confirmPassword: pass } })).status()).toBe(201);
  const session = await (await request.post(`${api}/api/auth/sign-in`, { data: { userName: p, password: pass } })).json();
  const headers = { Authorization: `Bearer ${session.accessToken}` };
  const booked = await request.post(`${api}/api/appointments`, { headers, data: { branchId: setup.branch.id, booker: { name: 'Khách Hệ Thống', phone: p },
    attendee: { name: 'Khách Hệ Thống' }, attendeeRelation: 'Self', kind: 'Standard', serviceId: setup.service.id, dentistId: setup.dentist.id,
    startAt: setup.slot.start, durationMinutes: setup.service.publicDurationMinutes, scheduleVersion: setup.slot.scheduleVersion, requestKey: randomUUID() } });
  expect(booked.status()).toBe(201);
  return { p, pass, code: (await booked.json()).code as string };
}

test('SYS2-05 customer views list and detail of own appointments', async ({ page, request }) => {
  const c = await bookedCustomer(request);
  await customerLogin(page, c.p, c.pass);
  await page.getByRole('link', { name: 'Lịch hẹn của tôi', exact: true }).click();
  await page.getByRole('link', { name: `Xem lịch ${c.code}`, exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Chi tiết lịch hẹn', exact: true })).toBeVisible();
  await expect(page.getByText(c.code).first()).toBeVisible();
});

test('SYS2-08 another customer cannot open the appointment', async ({ page, request }) => {
  const a = await bookedCustomer(request);
  const b = await bookedCustomer(request);
  await customerLogin(page, b.p, b.pass);
  await expect(page.getByRole('link', { name: 'Lịch hẹn của tôi', exact: true })).toBeVisible();
  await page.goto(`/customer/appointments/${a.code}`);
  await page.waitForTimeout(1500);
  await expect(page.getByText('Bạn chưa có quyền xem thông tin này.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Hủy lịch', exact: true })).toHaveCount(0);
  await page.screenshot({ path: 'test-results/sys2-08.png' });
});

test('SYS7-02 owner creates a material group and a material', async ({ page }) => {
  const s = suffix(); const group = `Nhóm HT ${s}`; const mat = `Vật liệu HT ${s}`;
  await staffLogin(page, 'owner.demo');
  await page.goto('/settings/materials');
  await page.getByRole('button', { name: 'Nhóm dụng cụ / vật liệu', exact: true }).click();
  await page.getByRole('button', { name: /Thêm mới/ }).first().click();
  await page.getByLabel('Tên', { exact: true }).fill(group);
  await page.getByRole('button', { name: 'Đồng ý', exact: true }).click();
  await expect(page.getByText(group, { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Dụng cụ / Vật liệu', exact: true }).click();
  const add = async (name: string) => {
    await page.getByRole('button', { name: /Thêm mới/ }).first().click();
    const dlg = page.getByRole('dialog');
    await dlg.getByLabel('Tên', { exact: true }).fill(name);
    await dlg.getByLabel('Nhóm', { exact: true }).click();
    await page.locator('.ant-select-dropdown:visible').getByText(group, { exact: true }).click();
    await dlg.getByLabel('Đơn vị sử dụng', { exact: true }).fill('cái');
    await dlg.getByRole('button', { name: 'Đồng ý', exact: true }).click();
    return dlg;
  };
  await add(mat);
  await page.getByPlaceholder('Tìm theo tên...').fill(mat);
  await page.getByPlaceholder('Tìm theo tên...').press('Enter');
  await expect(page.getByText(mat, { exact: true })).toBeVisible();
  const dlg = await add(mat);
  await expect(dlg).toBeVisible();
  await expect(page.getByText(/đã tồn tại/i).first()).toBeVisible();
});

test('SYS7-08 assistant cannot open the system settings', async ({ page }) => {
  await staffLogin(page, 'assistant.demo');
  await expect(page.getByRole('link', { name: 'Thiết lập hệ thống', exact: true })).toHaveCount(0);
  await page.goto('/system-settings');
  await expect(page.getByText('Bạn không có quyền thực hiện thao tác này.')).toBeVisible();
});
