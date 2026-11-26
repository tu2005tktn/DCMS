import { test, expect, type Page } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { customerLogin, phone } from './helpers';

const api = process.env.E2E_API_BASE_URL ?? 'http://localhost:5080';
const ymd = (ms: number) => new Date(ms + 7 * 3600_000).toISOString().slice(0, 10);

async function findSetup(request: import('@playwright/test').APIRequestContext) {
  const opt = await (await request.get(`${api}/api/availability/options`)).json();
  for (const branch of opt.branches) {
    const dentist = opt.employees.find((e: any) => e.branchId === branch.id && e.canDentist);
    const service = opt.services.find((s: any) => s.branchId === branch.id);
    if (!dentist || !service) continue;
    for (let d = 2; d < 23; d++) {
      const day = ymd(Date.now() + d * 86400_000);
      const q = new URLSearchParams({ branchId: branch.id, dentistId: dentist.id, serviceId: service.id, date: day, durationMinutes: service.publicDurationMinutes });
      const r = await request.get(`${api}/api/availability?${q}`);
      if (!r.ok()) continue;
      if ((await r.json()).slots.length >= 2) return { branch, dentist, service, day };
    }
  }
  throw new Error('No future schedule');
}

async function newCustomer(page: Page, request: import('@playwright/test').APIRequestContext) {
  const pass = `Aa1!${randomUUID()}`; const p = phone();
  expect((await request.post(`${api}/api/auth/register`, { data: { fullName: 'Khách Đặt Lịch', phoneNumber: p, email: `${randomUUID()}@example.invalid`, password: pass, confirmPassword: pass } })).status()).toBe(201);
  await customerLogin(page, p, pass);
  await page.getByRole('link', { name: 'Bắt đầu đặt lịch' }).click();
  await page.getByRole('button', { name: 'Tiếp tục' }).click();
  return { p, pass };
}

async function pickSlot(page: Page, setup: any) {
  const pick = async (idx: number, text: string) => {
    await page.locator('.ant-select').nth(idx).click();
    await page.locator('.ant-select-dropdown:visible').getByText(text, { exact: false }).first().click();
  };
  await pick(0, setup.branch.name);
  await page.waitForTimeout(800);
  await pick(1, setup.dentist.name);
  await page.locator('input[type=date]').fill(setup.day);
  await page.waitForTimeout(1500);
  const slotBtn = page.getByRole('button', { name: /^\d{2}:\d{2}/ }).first();
  await slotBtn.click();
}

test('SYS2-01 customer books an appointment for themselves', async ({ page, request }) => {
  const setup = await findSetup(request);
  await newCustomer(page, request);
  await pickSlot(page, setup);
  await page.getByRole('button', { name: 'Tiếp tục' }).click();
  await page.getByRole('button', { name: 'Xem lại', exact: true }).click();
  const resp = page.waitForResponse(r => r.url().endsWith('/api/appointments') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Xác nhận đặt lịch' }).click();
  expect((await resp).status()).toBe(201);
});

test('SYS2-03 booking cannot continue with invalid contact data', async ({ page, request }) => {
  const setup = await findSetup(request);
  await newCustomer(page, request);
  await pickSlot(page, setup);
  await page.getByRole('button', { name: 'Tiếp tục' }).click();
  await page.getByLabel('Họ tên người đặt').fill('');
  await page.getByLabel(/Số điện thoại Việt Nam hoặc email/).fill('12345');
  await page.getByRole('button', { name: 'Xem lại', exact: true }).click();
  await page.waitForTimeout(800);
  await expect(page.getByRole('button', { name: 'Xác nhận đặt lịch' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Xem lại', exact: true })).toBeVisible();
  await page.screenshot({ path: 'test-results/sys2-03.png', fullPage: true });
});

async function bookViaUi(page: Page, request: import('@playwright/test').APIRequestContext) {
  const setup = await findSetup(request);
  await newCustomer(page, request);
  await pickSlot(page, setup);
  await page.getByRole('button', { name: 'Tiếp tục' }).click();
  await page.getByRole('button', { name: 'Xem lại', exact: true }).click();
  const resp = page.waitForResponse(r => r.url().endsWith('/api/appointments') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Xác nhận đặt lịch' }).click();
  const body = await (await resp).json();
  await page.getByRole('link', { name: 'Xem và quản lý lịch hẹn' }).click();
  await expect(page.getByRole('heading', { name: 'Chi tiết lịch hẹn', exact: true })).toBeVisible();
  return body.code as string;
}

test('SYS2-06 customer reschedules an upcoming appointment', async ({ page, request }) => {
  await bookViaUi(page, request);
  await page.getByRole('button', { name: 'Đổi lịch', exact: true }).click();
  const dlg = page.getByRole('dialog', { name: 'Đổi lịch hẹn' });
  await dlg.getByRole('group', { name: 'Khung giờ mới' }).getByRole('button').first().click();
  await dlg.getByLabel('Lý do', { exact: true }).fill('Bận việc');
  await dlg.getByRole('button', { name: 'Xem lại', exact: true }).click();
  const resp = page.waitForResponse(r => /\/reschedule$/.test(r.url()) && r.request().method() === 'POST');
  await dlg.getByRole('button', { name: 'Xác nhận đổi lịch', exact: true }).click();
  expect((await resp).status()).toBe(200);
  await expect(dlg).not.toBeVisible();
});

test('SYS2-07 customer cancels an upcoming appointment', async ({ page, request }) => {
  await bookViaUi(page, request);
  await page.getByRole('button', { name: 'Hủy lịch', exact: true }).click();
  const dlg = page.getByRole('dialog', { name: 'Hủy lịch hẹn' });
  await dlg.getByLabel('Lý do', { exact: true }).fill('Không sắp xếp được');
  await dlg.getByRole('button', { name: 'Xem lại', exact: true }).click();
  const resp = page.waitForResponse(r => /\/cancel$/.test(r.url()) && r.request().method() === 'POST');
  await dlg.getByRole('button', { name: 'Xác nhận hủy lịch', exact: true }).click();
  expect((await resp).status()).toBe(200);
  await expect(dlg).not.toBeVisible();
});

test('SYS2-04 a slot taken by another customer is refused', async ({ request }) => {
  const setup = await findSetup(request);
  const q = new URLSearchParams({ branchId: setup.branch.id, dentistId: setup.dentist.id, serviceId: setup.service.id, date: setup.day, durationMinutes: setup.service.publicDurationMinutes });
  const slot = (await (await request.get(`${api}/api/availability?${q}`)).json()).slots[0];
  const book = async (label: string) => {
    const pass = `Aa1!${randomUUID()}`; const p = phone();
    await request.post(`${api}/api/auth/register`, { data: { fullName: label, phoneNumber: p, email: `${randomUUID()}@example.invalid`, password: pass, confirmPassword: pass } });
    const session = await (await request.post(`${api}/api/auth/sign-in`, { data: { userName: p, password: pass } })).json();
    return request.post(`${api}/api/appointments`, { headers: { Authorization: `Bearer ${session.accessToken}` }, data: { branchId: setup.branch.id,
      booker: { name: label, phone: p }, attendee: { name: label }, attendeeRelation: 'Self', kind: 'Standard', serviceId: setup.service.id, dentistId: setup.dentist.id,
      startAt: slot.start, durationMinutes: setup.service.publicDurationMinutes, scheduleVersion: slot.scheduleVersion, requestKey: randomUUID() } });
  };
  expect((await book('Khách A')).status()).toBe(201);
  const second = await book('Khách B');
  expect(second.status()).toBe(409);
  console.log('SYS2-04 second response', await second.text());
});

test('SYS2-02 customer adds a family member and books for them', async ({ page, request }) => {
  const setup = await findSetup(request);
  const pass = `Aa1!${randomUUID()}`; const p = phone(); const rel = `Người Thân ${randomUUID().slice(0, 5)}`;
  expect((await request.post(`${api}/api/auth/register`, { data: { fullName: 'Khách Có Người Thân', phoneNumber: p, email: `${randomUUID()}@example.invalid`, password: pass, confirmPassword: pass } })).status()).toBe(201);
  await customerLogin(page, p, pass);
  await page.getByRole('link', { name: 'Bắt đầu đặt lịch' }).waitFor();
  await page.getByRole('link', { name: 'Người thân' }).first().click();
  await page.getByRole('button', { name: 'Thêm người thân', exact: true }).click();
  const dlg = page.getByRole('dialog');
  await dlg.getByLabel('Họ tên', { exact: true }).fill(rel);
  const pickSel = async (text: string) => {
    await dlg.locator('.ant-form-item').filter({ hasText: text }).locator('.ant-select').first().click();
    await page.waitForTimeout(400);
    await page.locator('.ant-select-dropdown:visible .ant-select-item-option').first().click({ force: true });
  };
  await pickSel('Quan hệ');
  await dlg.getByLabel('Ngày sinh (không bắt buộc)').fill('2015-03-04');
  await pickSel('Giới tính');
  await dlg.getByLabel('Số điện thoại liên hệ').fill(phone());
  await dlg.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(dlg).not.toBeVisible();
  await expect(page.getByText(rel).first()).toBeVisible();
  await page.getByRole('link', { name: 'Đặt lịch' }).first().click();
  await page.getByRole('button', { name: /Người thân/ }).first().click();
  await page.locator('.ant-select').first().click();
  await page.locator('.ant-select-dropdown:visible').getByText(rel, { exact: false }).first().click();
  await page.getByRole('button', { name: 'Tiếp tục' }).click();
  await pickSlot(page, setup);
  await page.getByRole('button', { name: 'Tiếp tục' }).click();
  await page.getByRole('button', { name: 'Xem lại', exact: true }).click();
  const resp = page.waitForResponse(r => r.url().endsWith('/api/appointments') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Xác nhận đặt lịch' }).click();
  expect((await resp).status()).toBe(201);
});
