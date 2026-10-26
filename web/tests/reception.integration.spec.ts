import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5082';
const password = process.env.E2E_DEMO_PASSWORD;
test.skip(!password, 'Run against the isolated prepare-reception-e2e fixture with E2E_DEMO_PASSWORD.');
type Patient = { id: number; fullName: string; patientCode: string };
type Context = { headers: { Authorization: string }; branchId: number; dentistId: number; dentistName: string; serviceId: number; date: string };
async function context(request: APIRequestContext): Promise<Context> {
  const auth = await request.post(`${apiUrl}/api/auth/sign-in`, { data: { userName: 'assistant.demo', password, deviceLabel: 'DCMS111 browser fixture' } });
  expect(auth.ok()).toBeTruthy(); const login = await auth.json(); const headers = { Authorization: `Bearer ${login.accessToken}` };
  const optionsResponse = await request.get(`${apiUrl}/api/reception/options`, { headers }); expect(optionsResponse.ok()).toBeTruthy();
  const options = await optionsResponse.json(); const branchId = options.calendar.branches[0].id;
  const dentist = options.calendar.dentists.find((x: { name: string }) => x.name === 'Nha sĩ demo');
  return { headers, branchId, dentistId: dentist.id, dentistName: dentist.name, serviceId: options.calendar.services[0].id, date: options.clinicDate };
}
async function patient(request: APIRequestContext, c: Context): Promise<Patient> {
  const fullName = `SYNTHETIC tiếp nhận ${crypto.randomUUID().slice(0, 8)}`;
  const response = await request.post(`${apiUrl}/api/patients`, { headers: c.headers, data: { fullName, phone: '0912345678', birthDate: '2000-01-01', duplicatesReviewed: true } });
  expect(response.ok()).toBeTruthy(); return (await response.json()).patient;
}
async function appointment(request: APIRequestContext, c: Context, p: Patient, date = c.date) {
  const response = await request.get(`${apiUrl}/api/availability?branchId=${c.branchId}&dentistId=${c.dentistId}&serviceId=${c.serviceId}&date=${date}&durationMinutes=1`, { headers: c.headers });
  expect(response.ok()).toBeTruthy(); const data = await response.json();
  const slot = data.slots.find((x: { start: string }) => Date.parse(x.start) > Date.now() + 10000); expect(slot).toBeTruthy();
  const booked = await request.post(`${apiUrl}/api/appointments`, { headers: c.headers, data: {
    branchId: c.branchId, booker: { name: 'SYNTHETIC người đặt khác', phone: '0912345678', email: null },
    attendee: { patientId: p.id, name: p.fullName, birthDate: null }, attendeeRelation: 'Relative', kind: 'WalkIn', source: 'InPerson',
    serviceId: c.serviceId, dentistId: c.dentistId, chairId: slot.chairId, startAt: slot.start, durationMinutes: 1,
    followUpId: null, exceptionReason: 'Vãng lai đủ nguồn lực', requestKey: crypto.randomUUID(),
    scheduleVersion: slot.scheduleVersion, assistantScheduleVersion: null, bookingSettingsVersion: slot.bookingSettingsVersion,
  } });
  expect(booked.ok(), await booked.text()).toBeTruthy(); return booked.json();
}
async function login(page: Page, userName = 'assistant.demo') {
  await page.goto('/sign-in'); await page.getByLabel('Tên đăng nhập', { exact: true }).fill(userName);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password!); await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('link', { name: 'Tiếp nhận và hàng chờ' })).toBeVisible();
}

test('real check-in appears once in queue and assigned dentist opens the identical Visit', async ({ page, request, browser }, testInfo) => {
  const c = await context(request); const p = await patient(request, c); const booked = await appointment(request, c, p);
  await login(page); await page.goto(`/reception/appointments/${booked.appointmentId}`);
  await expect(page.getByText(`${p.patientCode} · ${p.fullName}`, { exact: true }).last()).toBeVisible();
  await page.getByLabel('Lý do đến', { exact: true }).focus(); await page.getByLabel('Lý do đến', { exact: true }).press('ArrowDown');
  await page.locator('.ant-select-dropdown:visible').getByText('Điều trị tiếp', { exact: true }).click();
  await expect(page.getByText('Bác sĩ xác nhận nguồn và hạng mục chuyên môn sau.', { exact: false })).toBeVisible();
  await page.getByRole('checkbox', { name: 'Đã đối chiếu đúng người đến khám với lịch và hồ sơ đã chọn' }).check();
  const savedResponse = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith(`/appointments/${booked.appointmentId}/check-in`));
  await page.getByRole('button', { name: 'Xác nhận Check-in', exact: true }).dblclick(); const saved = await (await savedResponse).json();
  expect(saved.status).toBe('Queued'); expect(saved.patientId).toBe(p.id); expect(saved.appointmentId).toBe(booked.appointmentId);
  await page.getByRole('link', { name: 'Xem hàng chờ', exact: true }).click(); await page.getByRole('tab', { name: 'Hàng chờ và lượt khám' }).click();
  const row = page.getByRole('row').filter({ hasText: p.patientCode }); await expect(row).toHaveCount(1); await expect(row).toContainText('Chờ khám');
  await row.getByRole('link', { name: 'Mở lượt khám' }).click(); await expect(page).toHaveURL(new RegExp(`/visits/${saved.visitId}$`));
  await expect(page.getByRole('heading', { name: `${p.patientCode} · ${p.fullName}` })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('visit-handoff.png'), fullPage: true });
  const doctorContext = await browser.newContext(); const doctor = await doctorContext.newPage(); await login(doctor, 'dentist.demo');
  await doctor.goto(`/visits/${saved.visitId}`); await expect(doctor.getByRole('heading', { name: `${p.patientCode} · ${p.fullName}` })).toBeVisible();
  const repeated = await request.post(`${apiUrl}/api/reception/appointments/${booked.appointmentId}/check-in`, { headers: c.headers, data: {
    patientId: p.id, expectedVersion: booked.version, requestKey: crypto.randomUUID(), identityConfirmed: true, purpose: 'TreatmentContinuation',
  } }); expect(repeated.ok()).toBeTruthy(); expect((await repeated.json()).visitId).toBe(saved.visitId);
  const queue = await request.get(`${apiUrl}/api/reception/queue?branchId=${c.branchId}&date=${c.date}&keyword=${encodeURIComponent(p.patientCode)}`, { headers: c.headers });
  const queueBody = await queue.json(); expect(queueBody.total).toBe(1); expect(queueBody.items[0].visitId).toBe(saved.visitId);
  await doctorContext.close();
});

test('identity must be confirmed before the real check-in request is sent', async ({ page, request }) => {
  const c = await context(request); const p = await patient(request, c); const booked = await appointment(request, c, p);
  await login(page); await page.goto(`/reception/appointments/${booked.appointmentId}`);
  let writes = 0; page.on('request', r => { if (r.method() === 'POST' && r.url().endsWith('/check-in')) writes++; });
  await expect(page.getByRole('button', { name: 'Xác nhận Check-in', exact: true })).toBeEnabled();
  await page.getByRole('button', { name: 'Xác nhận Check-in', exact: true }).click();
  await expect(page.getByText('Vui lòng đối chiếu và xác nhận đúng người đến khám.', { exact: true })).toBeVisible(); expect(writes).toBe(0);
  const cancelled = await request.post(`${apiUrl}/api/appointments/${booked.code}/cancel`, { headers: c.headers,
    data: { expectedVersion: booked.version, requestKey: crypto.randomUUID(), reason: 'Dọn lịch fixture chưa check-in', requesterVerification: 'SYNTHETIC fixture' } }); expect(cancelled.ok()).toBeTruthy();
});

test('same phone never auto-selects a different person and a rejected check-in retains the draft', async ({ page, request }) => {
  const c = await context(request); const p = await patient(request, c); const other = await patient(request, c); const booked = await appointment(request, c, p);
  await login(page); await page.goto(`/reception/appointments/${booked.appointmentId}`);
  await page.getByLabel('Hồ sơ bệnh nhân', { exact: true }).fill(other.patientCode);
  await page.locator('.ant-select-dropdown:visible').getByText(new RegExp(other.patientCode)).click();
  await expect(page.getByText(`${other.patientCode} · ${other.fullName}`, { exact: true }).last()).toBeVisible();
  await page.getByLabel('Ghi chú thêm', { exact: true }).fill('SYNTHETIC nháp cần giữ sau lỗi');
  await page.getByRole('checkbox', { name: 'Đã đối chiếu đúng người đến khám với lịch và hồ sơ đã chọn' }).check();
  const rejected = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/check-in'));
  await page.getByRole('button', { name: 'Xác nhận Check-in', exact: true }).click(); expect((await rejected).status()).toBe(409);
  await expect(page.getByLabel('Ghi chú thêm', { exact: true })).toHaveValue('SYNTHETIC nháp cần giữ sau lỗi');
  await expect(page.getByRole('button', { name: 'Xác nhận Check-in', exact: true })).toBeEnabled();
  const detail = await request.get(`${apiUrl}/api/reception/appointments/${booked.appointmentId}`, { headers: c.headers }); expect((await detail.json()).visit).toBeNull();
  const cancelled = await request.post(`${apiUrl}/api/appointments/${booked.code}/cancel`, { headers: c.headers,
    data: { expectedVersion: booked.version, requestKey: crypto.randomUUID(), reason: 'Dọn lịch fixture chưa check-in', requesterVerification: 'SYNTHETIC fixture' } }); expect(cancelled.ok()).toBeTruthy();
});

test('future appointment shows clinic-day reason and never offers a check-in command', async ({ page, request }) => {
  const c = await context(request); const p = await patient(request, c);
  const tomorrow = new Date(`${c.date}T00:00:00+07:00`); tomorrow.setUTCDate(tomorrow.getUTCDate() + 1);
  const date = new Date(tomorrow.getTime() + 7 * 3600000).toISOString().slice(0, 10);
  const booked = await appointment(request, c, p, date); await login(page); await page.goto(`/reception/appointments/${booked.appointmentId}`);
  await expect(page.getByText('Lịch chưa đủ điều kiện tiếp nhận', { exact: true })).toBeVisible();
  await expect(page.getByText('Chỉ tiếp nhận lịch trong ngày hiện tại của phòng khám.', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Xác nhận Check-in', exact: true })).toHaveCount(0);
});

test('walk-in uses the real S11 booking form then returns to reception', async ({ page, request }) => {
  const c = await context(request); const p = await patient(request, c); await login(page); await page.goto('/reception');
  await page.getByRole('button', { name: 'Xếp lịch khách vãng lai', exact: true }).click(); const drawer = page.getByRole('dialog');
  await expect(drawer.getByText('Đặt lịch tại quầy (S11)', { exact: true })).toBeVisible();
  await drawer.getByLabel('Họ tên người đặt', { exact: true }).fill('SYNTHETIC lễ tân'); await drawer.getByLabel('SĐT (10 số) hoặc email', { exact: true }).fill('0912345678');
  const patientSelect = drawer.getByLabel('Hồ sơ bệnh nhân có sẵn (tùy chọn)', { exact: true }); await patientSelect.fill(p.patientCode);
  await page.locator('.ant-select-dropdown:visible').getByText(new RegExp(p.patientCode)).click();
  await drawer.getByLabel('Dịch vụ', { exact: true }).click(); await page.locator('.ant-select-dropdown:visible').getByText('Khám · 1 phút', { exact: true }).click();
  await drawer.getByLabel('Nha sĩ', { exact: true }).click(); await page.locator('.ant-select-dropdown:visible').getByText(c.dentistName, { exact: true }).click();
  await drawer.getByLabel('Thời lượng (phút)', { exact: true }).fill('1');
  const slotsResponse = await request.get(`${apiUrl}/api/availability?branchId=${c.branchId}&dentistId=${c.dentistId}&serviceId=${c.serviceId}&date=${c.date}&durationMinutes=1`, { headers: c.headers });
  const slots = (await slotsResponse.json()).slots; const slot = slots.find((x: { start: string }) => Date.parse(x.start) > Date.now() + 60000);
  expect(slot).toBeTruthy(); const label = new Date(slot.start).toLocaleTimeString('en-GB', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit' });
  await drawer.getByRole('button', { name: new RegExp(`^${label}`) }).first().click();
  const exception = drawer.getByLabel('Lý do ngoại lệ (bắt đầu trong vòng 60 phút)', { exact: true });
  if (await exception.isVisible()) await exception.fill('Vãng lai còn đủ nhân sự và ghế');
  await drawer.getByRole('button', { name: 'Xem lại', exact: true }).click();
  const bookedResponse = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/api/appointments'));
  await drawer.getByRole('button', { name: 'Xác nhận đặt lịch', exact: true }).click(); const booked = await (await bookedResponse).json();
  await expect(page).toHaveURL(new RegExp(`/reception/appointments/${booked.appointmentId}$`));
  await page.getByRole('checkbox', { name: 'Đã đối chiếu đúng người đến khám với lịch và hồ sơ đã chọn' }).check();
  await page.getByRole('button', { name: 'Xác nhận Check-in', exact: true }).click(); await expect(page.getByRole('link', { name: 'Mở lượt khám', exact: true })).toBeVisible();
});
