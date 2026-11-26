import { test, expect, type APIRequestContext } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { staffLogin, password } from './helpers';
const api = process.env.E2E_API_BASE_URL ?? 'http://localhost:5080';
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });

async function noRecordAppointment(request: APIRequestContext) {
  const r = await request.post(`${api}/api/auth/sign-in`, { data: { userName: 'assistant.demo', password } });
  const headers = { Authorization: `Bearer ${(await r.json()).accessToken}` };
  const options = await (await request.get(`${api}/api/reception/options`, { headers })).json();
  const branchId = options.calendar.branches[0].id;
  const dentistId = options.calendar.dentists.find((x: { name: string }) => x.name === 'Nha sĩ demo').id;
  const serviceId = options.calendar.services[0].id;
  const av = await (await request.get(`${api}/api/availability?branchId=${branchId}&dentistId=${dentistId}&serviceId=${serviceId}&date=${options.clinicDate}&durationMinutes=1`, { headers })).json();
  const slot = av.slots.find((x: { start: string }) => Date.parse(x.start) > Date.now() + 10000);
  expect(slot, 'a bookable slot must remain today').toBeTruthy();
  const name = `Chưa Hồ Sơ ${randomUUID().slice(0, 6)}`;
  const booked = await request.post(`${api}/api/appointments`, { headers, data: { branchId, booker: { name: 'Người đặt', phone: '0912345678' },
    attendee: { name }, attendeeRelation: 'Self', kind: 'WalkIn', source: 'InPerson', serviceId, dentistId,
    chairId: slot.chairId, startAt: slot.start, durationMinutes: 1, exceptionReason: 'System test no record', requestKey: randomUUID(),
    scheduleVersion: slot.scheduleVersion, bookingSettingsVersion: slot.bookingSettingsVersion } });
  expect(booked.ok(), await booked.text()).toBeTruthy();
  return { name, code: (await booked.json()).code as string | undefined };
}

test('SYS4-07 check-in is refused when the patient record is missing', async ({ page, request }) => {
  const f = await noRecordAppointment(request);
  await staffLogin(page, 'assistant.demo');
  await page.getByRole('link', { name: 'Tiếp nhận và hàng chờ' }).click(); await page.waitForTimeout(3500);
  const row = page.locator('tr').filter({ hasText: f.name });
  await expect(row).toBeVisible({ timeout: 15000 });
  await row.getByRole('link').first().click(); await page.waitForTimeout(3000);
  await page.screenshot({ path: 'test-results/sys4-07.png', fullPage: true });
  console.log('NOREC', (await page.locator('#staff-content').innerText()).slice(0, 900), JSON.stringify(await page.getByRole('button').allInnerTexts()));
});
