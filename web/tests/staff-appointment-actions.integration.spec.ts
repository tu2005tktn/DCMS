import { expect, test } from '@playwright/test';

const apiUrl = process.env.E2E_STAFF_API_URL, webUrl = process.env.E2E_STAFF_WEB_URL, password = process.env.E2E_DEMO_PASSWORD;
test.skip(process.env.E2E_STAFF_ISOLATED !== '1' || !apiUrl || !webUrl || !password, 'Requires an explicitly isolated staff fixture.');
test('staff proactively edits and cancels a patient appointment through UI and real APIs', async ({ page, request }, testInfo) => {
  test.setTimeout(90000); page.setDefaultTimeout(15000);
  const checked = async (response: Awaited<ReturnType<typeof request.post>>) => {
    expect(response.ok(), await response.text()).toBeTruthy(); return response.json();
  };
  const session = await checked(await request.post(`${apiUrl}/api/auth/sign-in`, { data: { userName: 'assistant.demo', password } }));
  const headers = { Authorization: `Bearer ${session.accessToken}` };
  const suffix = crypto.randomUUID().slice(0, 8), fullName = `SYNTHETIC proactive ${suffix}`;
  const patient = (await checked(await request.post(`${apiUrl}/api/patients`, { headers, data: { fullName, birthDate: '1990-05-12', gender: 'MALE', phone: '0912345678', duplicatesReviewed: true, requestKey: crypto.randomUUID() } }))).patient;
  const options = await checked(await request.get(`${apiUrl}/api/appointments/options`, { headers }));
  const branch = options.branches.find((value: { name: string }) => value.name === 'S09 - Chi nhánh thử nghiệm');
  const dentist = options.dentists.find((value: { branchId: number; name: string }) => value.branchId === branch.id && value.name === 'Nha sĩ demo');
  const service = options.services.find((value: { branchId: number }) => value.branchId === branch.id);
  const date = new Date(Date.now() + 7 * 3600000 + 86400000).toISOString().slice(0, 10);
  const newDate = new Date(Date.parse(`${date}T00:00:00Z`) + 86400000).toISOString().slice(0, 10);
  const available = await checked(await request.get(`${apiUrl}/api/availability?branchId=${branch.id}&dentistId=${dentist.id}&serviceId=${service.id}&date=${date}&durationMinutes=30`, { headers }));
  const slot = available.slots[0]; expect(slot).toBeTruthy();
  const booking = await checked(await request.post(`${apiUrl}/api/appointments`, { headers, data: {
    branchId: branch.id, booker: { name: 'SYNTHETIC scheduler', phone: '0912345678' }, attendee: { patientId: patient.id, name: fullName }, attendeeRelation: 'Self', kind: 'Standard', source: 'InPerson',
    serviceId: service.id, dentistId: dentist.id, chairId: slot.chairId, startAt: slot.start, durationMinutes: 30, requestKey: crypto.randomUUID(), scheduleVersion: slot.scheduleVersion, bookingSettingsVersion: slot.bookingSettingsVersion,
  } }));
  const errors: string[] = []; page.on('pageerror', error => errors.push(error.message));
  await page.goto(`${webUrl}/sign-in`); await page.getByLabel('Tên đăng nhập', { exact: true }).fill('assistant.demo');
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password!); await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Tài khoản của tôi', exact: true })).toBeVisible();
  await page.goto(`${webUrl}/patients/${patient.id}?tab=appointments`);
  const row = page.getByRole('row').filter({ hasText: booking.code });
  await row.getByRole('button', { name: 'Chỉnh sửa lịch', exact: true }).click();
  const editor = page.getByRole('dialog', { name: 'Chỉnh sửa lịch hẹn', exact: true });
  await editor.getByLabel('Ngày hẹn mới (giờ Việt Nam)').fill(newDate);
  await editor.getByRole('group', { name: 'Khung giờ chỉnh sửa lịch hẹn', exact: true }).getByRole('button').first().click();
  await editor.getByLabel('Lý do chỉnh sửa (bắt buộc)').fill('SYNTHETIC clinic proactively moves appointment');
  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: 1000 });
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width + 1);
    await expect.poll(async () => (await editor.boundingBox())?.x ?? -1000).toBeGreaterThanOrEqual(-1);
    await expect.poll(async () => (await editor.boundingBox())?.width ?? 10000).toBeLessThanOrEqual(width + 1);
    await page.screenshot({ path: testInfo.outputPath(`proactive-editor-${width}.png`), fullPage: true });
  }
  await page.setViewportSize({ width: 1440, height: 1000 });
  const changedResponse = page.waitForResponse(response => response.request().method() === 'POST' && response.url().endsWith('/reschedule'));
  await editor.getByRole('button', { name: 'Lưu thay đổi', exact: true }).click();
  const moved = (await checked(await changedResponse)).appointment;
  await expect(editor).not.toBeVisible();
  expect(moved.start).not.toBe(booking.startAt); expect(moved.durationMinutes).toBe(30);
  const detail = await checked(await request.get(`${apiUrl}/api/appointments/${booking.appointmentId}`, { headers }));
  expect(detail.appointment.startAt).toBe(moved.start); expect(detail.appointment.version).toBe(2);
  await row.getByRole('button', { name: 'Hủy lịch', exact: true }).click();
  const cancellation = page.getByRole('dialog', { name: 'Hủy lịch hẹn', exact: true });
  await cancellation.getByLabel('Lý do hủy lịch (bắt buộc)').fill('SYNTHETIC clinic proactively cancels appointment');
  const cancelledResponse = page.waitForResponse(response => response.request().method() === 'POST' && response.url().endsWith('/cancel'));
  await cancellation.getByRole('button', { name: 'Xác nhận hủy lịch', exact: true }).click();
  const cancelled = (await checked(await cancelledResponse)).appointment; expect(cancelled.status).toBe('Cancelled');
  await expect(row).toContainText('Đã hủy'); await expect(row.getByRole('button', { name: 'Hủy lịch', exact: true })).toHaveCount(0);
  const freed = await checked(await request.get(`${apiUrl}/api/availability?branchId=${branch.id}&dentistId=${dentist.id}&serviceId=${service.id}&date=${newDate}&durationMinutes=30`, { headers }));
  expect(freed.slots.some((value: { start: string }) => Date.parse(value.start) === Date.parse(moved.start))).toBe(true); expect(errors).toEqual([]);
});
