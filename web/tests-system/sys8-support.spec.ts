import { test, expect } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { staffLogin, customerLogin, password, phone } from './helpers';

const api = process.env.E2E_API_BASE_URL ?? 'http://localhost:5080';
const ymd = (ms: number) => new Date(ms + 7 * 3600_000).toISOString().slice(0, 10);

test('SYS8-04 customer support request reaches the staff inbox', async ({ page, request, browser }) => {
  const opt = await (await request.get(`${api}/api/availability/options`)).json();
  let booked: { code: string; p: string; pass: string } | null = null;
  for (const branch of opt.branches) {
    const dentist = opt.employees.find((e: any) => e.branchId === branch.id && e.canDentist);
    const service = opt.services.find((s: any) => s.branchId === branch.id);
    if (!dentist || !service || booked) continue;
    const q = new URLSearchParams({ branchId: branch.id, dentistId: dentist.id, serviceId: service.id, date: ymd(Date.now()), durationMinutes: service.publicDurationMinutes });
    const av = await request.get(`${api}/api/availability?${q}`);
    if (!av.ok()) continue;
    const slot = (await av.json()).slots.find((x: any) => Date.parse(x.start) > Date.now() + 60_000 && Date.parse(x.start) < Date.now() + 11 * 3600_000);
    if (!slot) continue;
    const pass = `Aa1!${randomUUID()}`; const p = phone();
    await request.post(`${api}/api/auth/register`, { data: { fullName: 'Khách Hỗ Trợ', phoneNumber: p, email: `${randomUUID()}@example.invalid`, password: pass, confirmPassword: pass } });
    const session = await (await request.post(`${api}/api/auth/sign-in`, { data: { userName: p, password: pass } })).json();
    const b = await request.post(`${api}/api/appointments`, { headers: { Authorization: `Bearer ${session.accessToken}` }, data: { branchId: branch.id, booker: { name: 'Khách Hỗ Trợ', phone: p },
      attendee: { name: 'Khách Hỗ Trợ' }, attendeeRelation: 'Self', kind: 'Standard', serviceId: service.id, dentistId: dentist.id, startAt: slot.start,
      durationMinutes: service.publicDurationMinutes, scheduleVersion: slot.scheduleVersion, requestKey: randomUUID() } });
    if (b.status() === 201) booked = { code: (await b.json()).code, p, pass };
  }
  test.skip(!booked, 'No slot within the next 11 hours today');
  await customerLogin(page, booked!.p, booked!.pass);
  await page.getByRole('link', { name: 'Bắt đầu đặt lịch' }).waitFor();
  await page.getByRole('link', { name: 'Lịch hẹn' }).first().click();
  await page.getByRole('link', { name: `Xem lịch ${booked!.code}`, exact: true }).click();
  await page.getByRole('button', { name: 'Yêu cầu hủy lịch', exact: true }).click();
  const dlg = page.getByRole('dialog');
  await dlg.getByLabel('Lý do', { exact: true }).fill('Có việc đột xuất');
  await page.screenshot({ path: 'test-results/sys8-04-customer.png' });
  console.log('DLG BTN', JSON.stringify(await dlg.getByRole('button').allInnerTexts()));
  await dlg.getByRole('button', { name: /Xem lại|Gửi/ }).first().click();
  await page.waitForTimeout(800);
  const send = dlg.getByRole('button', { name: /Gửi yêu cầu/ });
  if (await send.count()) await send.click();
  await page.waitForTimeout(1500);
  const ctx = await browser.newContext({ baseURL: 'http://localhost:5173' });
  const staff = await ctx.newPage();
  await staffLogin(staff, 'assistant.demo');
  await staff.goto('/support-inbox');
  await staff.waitForTimeout(3000);
  await staff.screenshot({ path: 'test-results/sys8-04-inbox.png', fullPage: true });
  await expect(staff.getByText(booked!.code).first()).toBeVisible();
  await ctx.close();
});
