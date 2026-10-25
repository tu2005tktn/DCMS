import { test, expect, type APIRequestContext, type Page } from '@playwright/test';

// Actual HTTP APIs + isolated PostgreSQL schema from prepare-reception-e2e.ps1.
// Only the explicitly named connection-failure test intercepts one request (abort; no fake success).
const base = process.env.E2E_RECEPTION_API ?? 'http://localhost:5082';
const password = process.env.E2E_DEMO_PASSWORD;
type Patient = { id: number; patientCode: string; fullName: string };
type Appointment = { appointmentId: string; code: string; version: number; startAt: string };
let token: string;
let options: { branches: { id: number }[]; dentists: { id: number; branchId: number; name: string }[]; services: { id: number; branchId: number }[] };
let branchId: number;
const auth = () => ({ Authorization: `Bearer ${token}` });
test.beforeAll(async ({ request }) => {
  if (!password) throw new Error('Set E2E_DEMO_PASSWORD from ignored .env.');
  const response = await request.post(`${base}/api/auth/sign-in`, { data: { userName: 'assistant.demo', password } });
  expect(response.status()).toBe(200); token = (await response.json()).accessToken;
  options = await (await request.get(`${base}/api/appointments/options`, { headers: auth() })).json(); branchId = options.branches[0].id;
});
async function open(page: Page, path: string) {
  await page.addInitScript(t => sessionStorage.setItem('dcms.web.access-token', t), token);
  await page.goto(path);
}
async function patient(request: APIRequestContext, phone: string | null = null): Promise<Patient> {
  const response = await request.post(`${base}/api/patients`, { headers: auth(), data: {
    fullName: `SYNTHETIC browser ${crypto.randomUUID()}`, birthDate: null, gender: null, phone, duplicatesReviewed: true,
  } });
  expect(response.status()).toBe(201); return (await response.json()).patient;
}
async function appointment(request: APIRequestContext): Promise<Appointment> {
  const date = new Date(Date.now() + 7 * 3600_000).toISOString().slice(0, 10);
  const dentistId = options.dentists.find(d => d.branchId === branchId)!.id;
  const serviceId = options.services.find(s => s.branchId === branchId)!.id;
  const available = await request.get(`${base}/api/availability?branchId=${branchId}&dentistId=${dentistId}&serviceId=${serviceId}&date=${date}&durationMinutes=30`, { headers: auth() });
  expect(available.status()).toBe(200);
  const slot = (await available.json()).slots.find((s: { start: string }) => Date.parse(s.start) > Date.now() + 65 * 60_000);
  expect(slot).toBeTruthy();
  const response = await request.post(`${base}/api/appointments`, { headers: auth(), data: {
    branchId, booker: { name: 'SYNTHETIC different booker', phone: '0912345678' }, attendee: { name: 'SYNTHETIC expected relative' },
    attendeeRelation: 'Relative', kind: 'WalkIn', source: 'InPerson', serviceId, dentistId, chairId: slot.chairId, startAt: slot.start,
    durationMinutes: 30, requestKey: crypto.randomUUID(), scheduleVersion: slot.scheduleVersion, bookingSettingsVersion: slot.bookingSettingsVersion,
  } });
  expect(response.status()).toBe(201); return response.json();
}
async function verify(page: Page, p: Patient) {
  await page.getByLabel('Tìm Patient', { exact: true }).fill(p.patientCode);
  await page.getByRole('button', { name: `Chọn ${p.patientCode}`, exact: true }).click();
  await page.getByRole('checkbox', { name: 'Tôi đã đối chiếu trực tiếp và xác minh đây là đúng người đến khám' }).check();
  await page.getByLabel('Căn cứ đối chiếu danh tính', { exact: true }).fill('SYNTHETIC compared name, code and identity in person');
}

test('S18 manually distinguishes shared-phone Patients and S17 shows the committed Patient, arrival and dentist', async ({ page, request }) => {
  const errors: string[] = []; page.on('pageerror', e => errors.push(e.message));
  const p1 = await patient(request, '0988776655'); const p2 = await patient(request, '0988776655'); const a = await appointment(request);
  await open(page, `/reception/check-in/${a.appointmentId}`);
  await expect(page.getByText('SYNTHETIC different booker', { exact: true })).toBeVisible();
  await expect(page.getByText('SYNTHETIC expected relative', { exact: true })).toBeVisible();
  await page.getByLabel('Tìm Patient', { exact: true }).fill('0988776655');
  await expect(page.getByRole('button', { name: `Chọn ${p1.patientCode}`, exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: `Chọn ${p2.patientCode}`, exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Xác nhận check-in', exact: true })).toBeDisabled();
  await verify(page, p2);
  await page.screenshot({ path: '../.artifacts/reception-s18.png', fullPage: true });
  const saved = page.waitForResponse(r => r.url().endsWith('/api/reception/check-ins') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Xác nhận check-in', exact: true }).click();
  const response = await saved; expect(response.status()).toBe(200); const visit = await response.json(); expect(visit.patientId).toBe(p2.id);
  await expect(page.getByRole('heading', { name: `Lượt khám ${visit.visitCode}` })).toBeVisible();
  await expect(page.getByText('Chờ khám', { exact: true })).toBeVisible();
  await page.getByRole('link', { name: '← Tiếp nhận và hàng chờ', exact: true }).click();
  await page.getByLabel('Tìm tên hoặc mã', { exact: true }).fill(p2.patientCode);
  const row = page.getByRole('row').filter({ hasText: p2.patientCode }); await expect(row).toHaveCount(1);
  await expect(row).toContainText(p2.fullName); await expect(row).toContainText(visit.dentistName);
  await page.screenshot({ path: '../.artifacts/reception-s17.png', fullPage: true });
  expect(errors).toEqual([]);
});

test('double reception via real concurrent requests returns the same Visit and S18 reopens it', async ({ page, request }) => {
  const p = await patient(request); const a = await appointment(request);
  const body = { appointment: a.appointmentId, branchId, patientId: p.id, identityVerified: true, identityNote: 'SYNTHETIC verified', purpose: 'InitialExamination', expectedVersion: a.version, requestKey: crypto.randomUUID() };
  const results = await Promise.all([request.post(`${base}/api/reception/check-ins`, { headers: auth(), data: body }), request.post(`${base}/api/reception/check-ins`, { headers: auth(), data: body })]);
  expect(results.map(r => r.status())).toEqual([200, 200]); const visits = await Promise.all(results.map(r => r.json())); expect(visits[0].visitId).toBe(visits[1].visitId);
  await open(page, `/reception/check-in/${a.appointmentId}`);
  await expect(page.getByRole('link', { name: 'Mở lượt khám hiện có', exact: true })).toHaveAttribute('href', `/reception/visits/${visits[0].visitId}`);
  await expect(page.getByRole('button', { name: 'Xác nhận check-in', exact: true })).toHaveCount(0);
});

test('UC-38 creates an identity with unknown optional fields and still requires manual verification', async ({ page, request }) => {
  const a = await appointment(request); await open(page, `/reception/check-in/${a.appointmentId}`);
  await page.getByRole('button', { name: 'Chưa có hồ sơ · UC-38', exact: true }).click();
  const drawer = page.getByRole('dialog'); const name = `SYNTHETIC new intake ${crypto.randomUUID()}`;
  await drawer.getByLabel('Họ tên', { exact: true }).fill(name);
  const saved = page.waitForResponse(r => /\/api\/patients\/?$/.test(r.url()) && r.request().method() === 'POST');
  await drawer.getByRole('button', { name: 'Lưu', exact: true }).click();
  const response = await saved; expect(response.status()).toBe(201); const { patient: p } = await response.json();
  expect(p.birthDate).toBeNull(); expect(p.phone).toBeNull(); expect(p.link.verifiedAccountUserName).toBeNull();
  await expect(drawer).not.toBeVisible(); await expect(page.getByRole('button', { name: 'Xác nhận check-in', exact: true })).toBeDisabled();
  await page.getByRole('checkbox', { name: 'Tôi đã đối chiếu trực tiếp và xác minh đây là đúng người đến khám' }).check();
  await page.getByLabel('Căn cứ đối chiếu danh tính', { exact: true }).fill('SYNTHETIC checked new identity');
  await page.getByRole('button', { name: 'Xác nhận check-in', exact: true }).click();
  await expect(page.getByRole('heading', { name: /^Lượt khám VS-/ })).toBeVisible();
});

test('connection failure preserves input and requestKey then retry commits through the real API', async ({ page, request }) => {
  const p = await patient(request); const a = await appointment(request); await open(page, `/reception/check-in/${a.appointmentId}`); await verify(page, p);
  const keys: string[] = []; let abort = true;
  await page.route('**/api/reception/check-ins', async route => { keys.push(route.request().postDataJSON().requestKey); if (abort) { abort = false; await route.abort('connectionfailed'); } else await route.continue(); });
  await page.getByLabel('Vấn đề mới / mối quan tâm bổ sung', { exact: true }).fill('SYNTHETIC concern retained');
  await page.getByRole('button', { name: 'Xác nhận check-in', exact: true }).click();
  await expect(page.getByText(/Mất kết nối · MSG144/)).toBeVisible();
  await expect(page.getByLabel('Vấn đề mới / mối quan tâm bổ sung', { exact: true })).toHaveValue('SYNTHETIC concern retained');
  await page.getByRole('button', { name: 'Xác nhận check-in', exact: true }).click();
  await expect(page.getByRole('heading', { name: /^Lượt khám VS-/ })).toBeVisible(); expect(keys).toHaveLength(2); expect(keys[0]).toBe(keys[1]);
});

test('continuation without source is explicitly pending assessment and reorder records reason', async ({ page, request }) => {
  const p = await patient(request); const a = await appointment(request); await open(page, `/reception/check-in/${a.appointmentId}`); await verify(page, p);
  await page.getByText('Khám lần đầu', { exact: true }).click(); await page.getByText('Tiếp tục điều trị', { exact: true }).click();
  await expect(page.getByText(/Nguồn thiếu hoặc đang bị hold/)).toBeVisible();
  await page.getByRole('button', { name: 'Xác nhận check-in', exact: true }).click();
  await expect(page.getByRole('heading', { name: /^Lượt khám VS-/ })).toBeVisible();
  await expect(page.getByText('Vui lòng chọn mục đích lần đến và đối chiếu hồ sơ, hạng mục hoặc thủ thuật nguồn.', { exact: true })).toBeVisible();
  await page.getByLabel('Lý do điều chỉnh', { exact: true }).fill('SYNTHETIC operational reorder');
  await page.getByRole('button', { name: 'Lưu thứ tự hàng chờ', exact: true }).click();
  await expect(page.getByRole('cell', { name: 'SYNTHETIC operational reorder', exact: true })).toBeVisible();
});

test('walk-in uses S11 availability and shared booking before check-in while retaining arrival', async ({ page, request }) => {
  await open(page, '/reception');
  await page.getByRole('button', { name: 'Khách chưa có lịch · S11', exact: true }).click();
  const drawer = page.getByRole('dialog');
  await expect(drawer.getByText('Trực tiếp', { exact: true })).toBeVisible();
  await drawer.getByLabel('Họ tên người đặt', { exact: true }).fill('SYNTHETIC walk-in booker');
  await drawer.getByLabel('SĐT (10 số) hoặc email', { exact: true }).fill('0912345678');
  await drawer.getByLabel('Họ tên người khám', { exact: true }).fill('SYNTHETIC walk-in attendee');
  await drawer.getByLabel('Dịch vụ', { exact: true }).press('ArrowDown'); await drawer.getByLabel('Dịch vụ', { exact: true }).press('Enter');
  await drawer.getByLabel('Nha sĩ', { exact: true }).press('ArrowDown'); await drawer.getByLabel('Nha sĩ', { exact: true }).press('Enter');
  await drawer.locator('.booking-slots button').last().click();
  await drawer.getByRole('button', { name: 'Xem lại', exact: true }).click();
  const booked = page.waitForResponse(r => /\/api\/appointments\/?$/.test(r.url()) && r.request().method() === 'POST');
  await drawer.getByRole('button', { name: 'Xác nhận đặt lịch', exact: true }).click();
  const response = await booked; expect(response.status()).toBe(201); const result = await response.json();
  await expect(page).toHaveURL(new RegExp(`/reception/check-in/${result.appointmentId}\\?arrival=`));
  await expect(page.getByRole('checkbox', { name: 'Nhập giờ đến thực tế trước thời điểm xác nhận', exact: true })).toBeChecked();
  const actualArrival = new URL(page.url()).searchParams.get('arrival')!;
  expect(Date.parse(actualArrival)).toBeLessThan(Date.parse(result.startAt));
  await verify(page, await patient(request));
  const checkedIn = page.waitForResponse(r => r.url().endsWith('/api/reception/check-ins') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Xác nhận check-in', exact: true }).click();
  const received = await checkedIn; expect(received.status()).toBe(200); const visit = await received.json();
  expect(Date.parse(visit.actualArrivalAt)).toBe(Date.parse(actualArrival)); expect(visit.appointmentId).toBe(result.appointmentId);
  await expect(page.getByRole('heading', { name: /^Lượt khám VS-/ })).toBeVisible();
});
