import { execFileSync } from 'node:child_process';
import { resolve } from 'node:path';
import { expect, test } from '@playwright/test';

const apiUrl = process.env.E2E_STAFF_API_URL, webUrl = process.env.E2E_STAFF_WEB_URL, password = process.env.E2E_DEMO_PASSWORD;
const schema = process.env.E2E_NOSHOW_SCHEMA, dbUser = process.env.E2E_POSTGRES_USER, dbName = process.env.E2E_POSTGRES_DB;
test.skip(process.env.E2E_STAFF_ISOLATED !== '1' || !apiUrl || !webUrl || !password || !schema || !dbUser || !dbName, 'Requires an explicitly isolated no-show fixture.');

test('real no-show releases its time for another patient while preserving the original record', async ({ page, request }, testInfo) => {
  test.setTimeout(120000); page.setDefaultTimeout(15000);
  expect(schema).toMatch(/^noshow_e2e_20261109_[a-f0-9]{8}$/);
  const checked = async (response: Awaited<ReturnType<typeof request.post>>) => {
    expect(response.ok(), await response.text()).toBeTruthy(); return response.json();
  };
  const session = await checked(await request.post(`${apiUrl}/api/auth/sign-in`, { data: { userName: 'assistant.demo', password } }));
  const headers = { Authorization: `Bearer ${session.accessToken}` }, suffix = crypto.randomUUID().slice(0, 8);
  const patient = (await checked(await request.post(`${apiUrl}/api/patients`, { headers, data: { fullName: `SYNTHETIC original ${suffix}`,
    birthDate: '1990-05-12', gender: 'MALE', phone: '0912345678', duplicatesReviewed: true, requestKey: crypto.randomUUID() } }))).patient;
  const options = await checked(await request.get(`${apiUrl}/api/appointments/options`, { headers }));
  const branch = options.branches.find((value: { name: string }) => value.name === 'S09 - Chi nhánh thử nghiệm');
  const dentist = options.dentists.find((value: { branchId: number; name: string }) => value.branchId === branch.id && value.name === 'Nha sĩ demo');
  const service = options.services.find((value: { branchId: number }) => value.branchId === branch.id);
  const tomorrow = new Date(Date.now() + 7 * 3600000 + 86400000).toISOString().slice(0, 10);
  const availability = await checked(await request.get(`${apiUrl}/api/availability?branchId=${branch.id}&dentistId=${dentist.id}&serviceId=${service.id}&date=${tomorrow}&durationMinutes=30`, { headers }));
  const slot = availability.slots[0]; expect(slot).toBeTruthy();
  const original = await checked(await request.post(`${apiUrl}/api/appointments`, { headers, data: { branchId: branch.id,
    booker: { name: 'SYNTHETIC original booker', phone: '0912345678' }, attendee: { patientId: patient.id, name: patient.fullName }, attendeeRelation: 'Self',
    kind: 'Standard', source: 'InPerson', serviceId: service.id, dentistId: dentist.id, chairId: slot.chairId, startAt: slot.start, durationMinutes: 30,
    requestKey: crypto.randomUUID(), scheduleVersion: slot.scheduleVersion, bookingSettingsVersion: slot.bookingSettingsVersion } }));
  expect(original.appointmentId).toMatch(/^[a-f0-9-]{36}$/);
  const start = new Date(Math.floor(Date.now() / 900000) * 900000 - 900000).toISOString(), end = new Date(Math.floor(Date.now() / 900000) * 900000 + 90 * 60000).toISOString();
  // Only the owned synthetic schema is altered to make an appointment due without backdating the booking API.
  const sql = `SET search_path TO "${schema}"; UPDATE "APPOINTMENT" SET "StartAt"='${start}', "EndAt"='${end}' WHERE "Id"='${original.appointmentId}'; UPDATE "RESOURCE_RESERVATION" SET "Start"='${start}', "End"='${end}' WHERE "Id"='${original.appointmentId}';`;
  execFileSync('docker', ['compose', '--env-file', '.env', '-f', 'infrastructure/docker/compose.yaml', 'exec', '-T', 'postgres', 'psql', '-U', dbUser!, '-d', dbName!, '-v', 'ON_ERROR_STOP=1'],
    { cwd: resolve(process.cwd(), '..'), input: sql, stdio: ['pipe', 'pipe', 'pipe'] });
  const errors: string[] = []; page.on('pageerror', error => errors.push(error.message));
  await page.goto(`${webUrl}/sign-in`); await page.getByLabel('Tên đăng nhập', { exact: true }).fill('assistant.demo');
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password!); await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Tài khoản của tôi', exact: true })).toBeVisible();
  await page.goto(`${webUrl}/patients/${patient.id}?tab=appointments`);
  const row = page.locator('.ant-table-tbody > tr').filter({ hasText: original.code }); await row.getByRole('button', { name: 'Khách không tới', exact: true }).click();
  const modal = page.getByRole('dialog', { name: 'Khách không tới', exact: true });
  await modal.getByRole('button', { name: 'Xác nhận khách không tới', exact: true }).click();
  await expect(modal).not.toBeVisible();
  await expect(row).toContainText('Khách không tới'); await row.getByRole('button', { name: 'Đặt lịch thay thế', exact: true }).click();
  const form = page.getByRole('dialog', { name: 'Đặt lịch thay thế', exact: true });
  await expect(form.getByLabel('Họ tên người khám', { exact: true })).toHaveValue('');
  await form.getByLabel('Họ tên người đặt', { exact: true }).fill('SYNTHETIC new booker');
  await form.getByLabel('SĐT (10 số) hoặc email', { exact: true }).fill('0987654321');
  await form.getByLabel('Họ tên người khám', { exact: true }).fill(`SYNTHETIC replacement ${suffix}`);
  await expect(form.getByRole('button', { name: 'Xem lại', exact: true })).toBeEnabled();
  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: 1000 });
    await expect.poll(async () => (await form.boundingBox())?.x ?? -1000).toBeGreaterThanOrEqual(-1);
    await expect.poll(async () => (await form.boundingBox())?.width ?? 10000).toBeLessThanOrEqual(width + 1);
    await page.screenshot({ path: testInfo.outputPath(`no-show-replacement-${width}.png`), fullPage: true });
  }
  await page.setViewportSize({ width: 1440, height: 1000 });
  await form.getByRole('button', { name: 'Xem lại', exact: true }).click();
  const bookedResponse = page.waitForResponse(response => response.request().method() === 'POST' && response.url().endsWith('/api/appointments'));
  await form.getByRole('button', { name: 'Xác nhận đặt lịch', exact: true }).click();
  const replacement = await checked(await bookedResponse); await expect(form).not.toBeVisible();
  expect(replacement.appointmentId).not.toBe(original.appointmentId); expect(Date.parse(replacement.startAt)).toBeGreaterThan(Date.now() - 5000);
  expect(Date.parse(replacement.endAt)).toBeLessThanOrEqual(Date.parse(end));
  const oldDetail = await checked(await request.get(`${apiUrl}/api/appointments/${original.appointmentId}`, { headers }));
  expect(oldDetail.appointment.status).toBe('NoShow'); expect(oldDetail.appointment.attendeeName).toBe(patient.fullName);
  const newDetail = await checked(await request.get(`${apiUrl}/api/appointments/${replacement.appointmentId}`, { headers }));
  expect(newDetail.appointment.status).toBe('Confirmed'); expect(newDetail.appointment.attendeeName).toBe(`SYNTHETIC replacement ${suffix}`);
  await expect(row).toContainText('Khách không tới'); expect(errors).toEqual([]);
});
