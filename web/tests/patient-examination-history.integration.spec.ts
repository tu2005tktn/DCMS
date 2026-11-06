import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiUrl = process.env.E2E_API_URL, password = process.env.E2E_DEMO_PASSWORD;
test.skip(!apiUrl || !password, 'Requires the isolated clinical PostgreSQL runtime.');
async function ok(response: Awaited<ReturnType<APIRequestContext['post']>>) {
  expect(response.ok(), await response.text()).toBeTruthy(); return response.json();
}
async function auth(request: APIRequestContext, userName: string) {
  return { Authorization: `Bearer ${(await ok(await request.post(`${apiUrl}/api/auth/sign-in`, { data: { userName, password } }))).accessToken}` };
}
async function fixture(request: APIRequestContext) {
  const assistant = await auth(request, 'assistant.demo'), dentist = await auth(request, 'dentist.demo');
  const options = await ok(await request.get(`${apiUrl}/api/reception/options`, { headers: assistant }));
  const branchId = options.calendar.branches[0].id, dentistId = options.calendar.dentists.find((x: { name: string }) => x.name === 'Nha sĩ demo').id, serviceId = options.calendar.services[0].id;
  const patient = (await ok(await request.post(`${apiUrl}/api/patients`, { headers: assistant, data: { fullName: 'SYNTHETIC lịch sử khám ' + crypto.randomUUID().slice(0, 8), duplicatesReviewed: true } }))).patient;
  const availability = await ok(await request.get(`${apiUrl}/api/availability?branchId=${branchId}&dentistId=${dentistId}&serviceId=${serviceId}&date=${options.clinicDate}&durationMinutes=1`, { headers: assistant }));
  const slot = availability.slots.find((x: { start: string }) => Date.parse(x.start) > Date.now() + 10000); expect(slot).toBeTruthy();
  const appointment = await ok(await request.post(`${apiUrl}/api/appointments`, { headers: assistant, data: { branchId, booker: { name: 'SYNTHETIC người đặt', phone: '0912345678' }, attendee: { patientId: patient.id, name: patient.fullName }, attendeeRelation: 'Relative', kind: 'WalkIn', source: 'InPerson', serviceId, dentistId, chairId: slot.chairId, startAt: slot.start, durationMinutes: 1, exceptionReason: 'SYNTHETIC history fixture', requestKey: crypto.randomUUID(), scheduleVersion: slot.scheduleVersion, bookingSettingsVersion: slot.bookingSettingsVersion } }));
  const visit = await ok(await request.post(`${apiUrl}/api/reception/appointments/${appointment.appointmentId}/check-in`, { headers: assistant, data: { patientId: patient.id, expectedVersion: appointment.version, requestKey: crypto.randomUUID(), identityConfirmed: true, purpose: 'InitialExamination' } }));
  const started = await ok(await request.post(`${apiUrl}/api/visits/${visit.visitId}/start`, { headers: dentist, data: { patientId: patient.id, expectedVersion: visit.visitVersion, requestKey: crypto.randomUUID() } }));
  const content = { ...started.initialContent, symptoms: 'SYNTHETIC đau răng khi nhai', medicalHistory: { detail: 'SYNTHETIC tiền sử đã hỏi' }, allergies: {}, medications: {}, findings: [{ tooth: 16, condition: 'SYNTHETIC tổn thương mặt nhai', surfaces: ['O'] }], diagnoses: ['SYNTHETIC chẩn đoán lịch sử'], advice: 'SYNTHETIC dặn dò trong hồ sơ', disposition: 'FollowUp', returnNeed: 'SYNTHETIC quay lại theo dõi', notes: 'SYNTHETIC ghi chú sau khám' };
  const workspace = await ok(await request.put(`${apiUrl}/api/visits/${visit.visitId}/examination`, { headers: dentist, data: { patientId: patient.id, expectedVersion: started.visit.visitVersion, requestKey: crypto.randomUUID(), content, confirm: false } }));
  return { patient, visit: workspace.visit, dentist, content };
}
async function signIn(page: Page) {
  await page.goto('/sign-in'); await page.getByLabel('Tên đăng nhập', { exact: true }).fill('dentist.demo');
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password!); await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Tài khoản của tôi', exact: true })).toBeVisible();
}

test('ending an examination opens durable clinical history in the correct patient profile', async ({ page, request }, testInfo) => {
  const f = await fixture(request); await signIn(page); await page.goto(`/visits/${f.visit.visitId}`);
  await page.getByRole('tab', { name: 'Tổng kết khám', exact: true }).click();
  await page.getByRole('button', { name: 'Hoàn tất khám', exact: true }).click();
  await page.getByRole('button', { name: 'Kết thúc lượt khám', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Kết thúc lượt khám', exact: true }); await dialog.getByRole('checkbox').check();
  await dialog.getByRole('button', { name: 'Xác nhận kết thúc lượt', exact: true }).click();
  await expect(page.getByText('Đã lưu lịch sử khám vào hồ sơ bệnh nhân', { exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'Xem lịch sử khám của bệnh nhân', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/patients/${f.patient.id}\\?tab=examinations$`));
  await expect(page.getByRole('heading', { name: f.patient.fullName, exact: true })).toBeVisible();
  await expect(page.getByText(f.patient.patientCode, { exact: true })).toBeVisible();
  const history = page.getByRole('region', { name: 'Phiếu khám của bệnh nhân' });
  const row = history.getByRole('row').filter({ hasText: f.visit.appointmentCode });
  await expect(row).toHaveCount(1); await expect(row).toContainText('Đã kết thúc lượt khám');
  await expect(row).toContainText('SYNTHETIC chẩn đoán lịch sử');
  await history.getByRole('button', { name: `Xem nội dung phiếu ${f.visit.appointmentCode}` }).click();
  const detail = page.getByRole('dialog');
  for (const value of [f.content.symptoms, f.content.advice, f.content.returnNeed, f.content.notes]) await expect(detail.getByText(value, { exact: true })).toBeVisible();
  const records = await ok(await request.get(`${apiUrl}/api/patients/${f.patient.id}/examinations`, { headers: f.dentist }));
  expect(records).toHaveLength(1); expect(records[0]).toMatchObject({ visitId: f.visit.visitId, visitStatus: 'Completed', examination: { confirmed: true, content: { advice: f.content.advice } } });
  expect(Number.isFinite(Date.parse(records[0].completedAt))).toBe(true);
  await page.screenshot({ path: testInfo.outputPath('patient-history-desktop.png'), fullPage: true });
  await page.setViewportSize({ width: 390, height: 900 }); await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(391);
  await expect.poll(async () => Math.round((await detail.boundingBox())!.x)).toBeGreaterThanOrEqual(0);
  await expect.poll(async () => Math.round((await detail.boundingBox())!.width)).toBeLessThanOrEqual(390);
  await page.screenshot({ path: testInfo.outputPath('patient-history-mobile.png'), fullPage: true });
  await page.reload(); await expect(row).toHaveCount(1); await expect(row).toContainText('Đã kết thúc lượt khám');
  await row.getByRole('link', { name: 'Xem phiếu khám', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/visits/${f.visit.visitId}$`));
  await expect(page.getByText('Đã lưu lịch sử khám vào hồ sơ bệnh nhân', { exact: true })).toBeVisible();
});

test('an already open profile refreshes when returning to the examination tab after completion elsewhere', async ({ page, request }) => {
  const f = await fixture(request);
  const confirmed = await ok(await request.put(`${apiUrl}/api/visits/${f.visit.visitId}/examination`, { headers: f.dentist, data: { patientId: f.patient.id, expectedVersion: f.visit.visitVersion, requestKey: crypto.randomUUID(), content: f.content, confirm: true } }));
  await signIn(page); await page.goto(`/patients/${f.patient.id}?tab=examinations`);
  const row = page.getByRole('row').filter({ hasText: f.visit.appointmentCode }); await expect(row).toContainText('Đang khám');
  await page.getByRole('tab', { name: 'Thông tin cơ bản', exact: true }).click();
  await ok(await request.post(`${apiUrl}/api/visits/${f.visit.visitId}/complete`, { headers: f.dentist, data: { patientId: f.patient.id, expectedVersion: confirmed.visit.visitVersion, requestKey: crypto.randomUUID(), workReviewed: true } }));
  await page.getByRole('tab', { name: 'Khám', exact: true }).click();
  await expect(row).toContainText('Đã kết thúc lượt khám'); await expect(row).toHaveCount(1);
});
