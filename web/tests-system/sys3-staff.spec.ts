import { test, expect, type APIRequestContext } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { staffLogin, password } from './helpers';

const api = process.env.E2E_API_BASE_URL ?? 'http://localhost:5080';
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });

async function futureBooking(request: APIRequestContext) {
  const s = await request.post(`${api}/api/auth/sign-in`, { data: { userName: 'assistant.demo', password } });
  const headers = { Authorization: `Bearer ${(await s.json()).accessToken}` };
  const fullName = `Lịch Nhân Viên ${randomUUID().slice(0, 6)}`;
  const patient = (await (await request.post(`${api}/api/patients`, { headers, data: { fullName, birthDate: '1990-05-12', gender: 'MALE', phone: '0912345678', duplicatesReviewed: true, requestKey: randomUUID() } })).json()).patient;
  const options = await (await request.get(`${api}/api/appointments/options`, { headers })).json();
  const branch = options.branches.find((v: { name: string }) => v.name === 'S09 - Chi nhánh thử nghiệm');
  const dentist = options.dentists.find((v: { branchId: number; name: string }) => v.branchId === branch.id && v.name === 'Nha sĩ demo');
  const service = options.services.find((v: { branchId: number }) => v.branchId === branch.id);
  for (let d = 1; d < 15; d++) {
    const date = new Date(Date.now() + 7 * 3600000 + d * 86400000).toISOString().slice(0, 10);
    const av = await request.get(`${api}/api/availability?branchId=${branch.id}&dentistId=${dentist.id}&serviceId=${service.id}&date=${date}&durationMinutes=30`, { headers });
    if (!av.ok()) continue;
    const slot = (await av.json()).slots[0];
    if (!slot) continue;
    const newDate = new Date(Date.parse(`${date}T00:00:00Z`) + 86400000).toISOString().slice(0, 10);
    const b = await request.post(`${api}/api/appointments`, { headers, data: { branchId: branch.id, booker: { name: 'Lễ tân', phone: '0912345678' },
      attendee: { patientId: patient.id, name: fullName }, attendeeRelation: 'Self', kind: 'Standard', source: 'InPerson', serviceId: service.id, dentistId: dentist.id,
      chairId: slot.chairId, startAt: slot.start, durationMinutes: 30, requestKey: randomUUID(), scheduleVersion: slot.scheduleVersion, bookingSettingsVersion: slot.bookingSettingsVersion } });
    expect(b.ok(), await b.text()).toBeTruthy();
    return { patient, code: (await b.json()).code as string, newDate };
  }
  throw new Error('No future slot');
}

test('SYS3-06/07 staff changes the time then cancels an appointment', async ({ page, request }) => {
  const f = await futureBooking(request);
  await staffLogin(page, 'assistant.demo');
  await page.goto(`/patients/${f.patient.id}?tab=appointments`);
  const row = page.getByRole('row').filter({ hasText: f.code });
  await test.step('SYS3-06 change the time', async () => {
    await row.getByRole('button', { name: 'Chỉnh sửa lịch', exact: true }).click();
    const editor = page.getByRole('dialog', { name: 'Chỉnh sửa lịch hẹn', exact: true });
    await editor.getByLabel('Ngày hẹn mới (giờ Việt Nam)').fill(f.newDate);
    await editor.getByRole('group', { name: 'Khung giờ chỉnh sửa lịch hẹn', exact: true }).getByRole('button').first().click();
    await editor.getByLabel('Lý do chỉnh sửa (bắt buộc)').fill('Bệnh nhân xin đổi giờ');
    const resp = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/reschedule'));
    await editor.getByRole('button', { name: 'Lưu thay đổi', exact: true }).click();
    expect((await resp).status()).toBe(200);
    await expect(editor).not.toBeVisible();
  });
  await test.step('SYS3-07 cancel', async () => {
    await row.getByRole('button', { name: 'Hủy lịch', exact: true }).click();
    const dlg = page.getByRole('dialog', { name: 'Hủy lịch hẹn', exact: true });
    await dlg.getByLabel('Lý do hủy lịch (bắt buộc)').fill('Phòng khám đổi lịch làm việc');
    const resp = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/cancel'));
    await dlg.getByRole('button', { name: 'Xác nhận hủy lịch', exact: true }).click();
    expect((await resp).status()).toBe(200);
    await expect(row).toContainText('Đã hủy');
  });
});

test('SYS3-09 two staff editing the same appointment get a conflict', async ({ browser, request }) => {
  const f = await futureBooking(request);
  const open = async () => {
    const ctx = await browser.newContext({ baseURL: 'http://localhost:5173' });
    const page = await ctx.newPage();
    await staffLogin(page, 'assistant.demo');
    await page.goto(`/patients/${f.patient.id}?tab=appointments`);
    const row = page.getByRole('row').filter({ hasText: f.code });
    await row.getByRole('button', { name: 'Chỉnh sửa lịch', exact: true }).click();
    const editor = page.getByRole('dialog', { name: 'Chỉnh sửa lịch hẹn', exact: true });
    await editor.getByLabel('Ngày hẹn mới (giờ Việt Nam)').fill(f.newDate);
    await editor.getByRole('group', { name: 'Khung giờ chỉnh sửa lịch hẹn', exact: true }).getByRole('button').first().click();
    await editor.getByLabel('Lý do chỉnh sửa (bắt buộc)').fill('Đổi giờ');
    return { ctx, page, editor };
  };
  const a = await open();
  const b = await open();
  const save = async (x: Awaited<ReturnType<typeof open>>) => {
    const resp = x.page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/reschedule'));
    await x.editor.getByRole('button', { name: 'Lưu thay đổi', exact: true }).click();
    return (await resp).status();
  };
  expect(await save(a)).toBe(200);
  expect(await save(b)).toBe(409);
  await b.page.screenshot({ path: 'test-results/sys3-09.png' });
  await a.ctx.close(); await b.ctx.close();
});
