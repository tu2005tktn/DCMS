import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import { randomInt, randomUUID } from 'node:crypto';
import { clinicDate, formatClock, formatTime, type AppointmentDetail, type Slot } from '../src/features/customer-appointments/types';

const api = process.env.E2E_API_BASE_URL ?? 'http://127.0.0.1:5080';
const user = { id: 999, userName: '0912345678', fullName: 'SYNTHETIC customer', accountType: 'Public',
  roles: ['Guest'], effectiveRoles: ['Guest'], permissions: [], hasVerifiedPatientLink: false };
const date = clinicDate(Date.now() + 3 * 86400_000);
const original: AppointmentDetail = {
  code: 'AP-WEB-TEST', start: `${date}T09:00:00+07:00`, end: `${date}T09:30:00+07:00`, durationMinutes: 30,
  dentistName: 'SYNTHETIC dentist', attendeeName: 'SYNTHETIC attendee', attendeeKind: 'RELATIVE',
  status: 'Confirmed', access: 'BOOKED_BY_ME', version: 1, branchId: 1, branchName: 'SYNTHETIC branch', dentistId: 2,
  assistantId: null, serviceId: 1, serviceName: 'Khám', bookerName: 'SYNTHETIC customer', bookerContactMasked: '09******78',
  eligibility: { canSelfService: true, hoursRemaining: 72, reasonCode: null, canRequestSupport: false },
  followUp: null, history: [], clinicContact: { phone: '0234 3 888 999', hours: '08:00–17:00' },
};
const slot: Slot = { start: `${date}T10:00:00+07:00`, end: `${date}T10:30:00+07:00`, chairId: 1, dentistId: 2,
  assistantId: null, scheduleVersion: 3, assistantScheduleVersion: null };
const json = (body: unknown, status = 200) => ({ status, contentType: 'application/json', body: JSON.stringify(body) });
async function mockSession(page: Page, detail = original) {
  await page.addInitScript(() => sessionStorage.setItem('dcms.customer.accessToken', 'customer-test-token'));
  await page.route('**/api/auth/me', route => route.fulfill(json(user)));
  await page.route('**/api/auth/google/config', route => route.fulfill(json({ enabled: false })));
  await page.route('**/api/me/appointments?*', route => route.fulfill(json({ items: [detail], total: 1, page: 1, pageSize: 20 })));
  await page.route('**/api/me/appointments/*', route => route.fulfill(json(detail)));
  await page.route('**/api/appointments/*/availability?*', route => route.fulfill(json({ slots: [slot] })));
}
async function reviewMove(page: Page, reason = 'SYNTHETIC change') {
  await page.getByRole('button', { name: 'Đổi lịch', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Đổi lịch hẹn' });
  await dialog.getByRole('group', { name: 'Khung giờ mới' }).getByRole('button').first().click();
  await dialog.getByLabel('Lý do', { exact: true }).fill(reason);
  await dialog.getByRole('button', { name: 'Xem lại', exact: true }).click();
  return dialog;
}

test('date validation, empty state and responsive detail preserve ownership labels', async ({ page }) => {
  await mockSession(page);
  await page.goto('/customer/appointments');
  await expect(page.getByText('Người thân · Lịch tôi đặt')).toBeVisible();
  await page.getByLabel('Từ ngày', { exact: true }).fill('2030-02-02');
  await page.getByLabel('Đến ngày', { exact: true }).fill('2030-02-01');
  await page.getByRole('button', { name: 'Áp dụng', exact: true }).click();
  await expect(page.getByText('Đến ngày phải từ ngày bắt đầu trở đi.')).toBeVisible();
  await page.getByRole('link', { name: 'Xem lịch AP-WEB-TEST', exact: true }).click();
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole('heading', { name: 'Chi tiết lịch hẹn', exact: true })).toBeVisible();
  await expect(page.getByText('SYNTHETIC attendee (người thân)')).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBeTruthy();
  await page.route('**/api/me/appointments?*', route => route.fulfill(json({ items: [], total: 0, page: 1, pageSize: 20 })));
  await page.getByRole('link', { name: '← Lịch hẹn của tôi', exact: true }).click();
  await expect(page.getByText('Không có lịch hẹn phù hợp.')).toBeVisible();
});

test('under twelve hours has contact guidance without self-service controls', async ({ page }) => {
  await mockSession(page, { ...original, eligibility: { canSelfService: false, hoursRemaining: 6, reasonCode: 'WITHIN_12H', canRequestSupport: true } });
  await page.goto('/customer/appointments/AP-WEB-TEST');
  await expect(page.getByText(/Còn dưới 12 giờ/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Đổi lịch', exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Hủy lịch', exact: true })).toHaveCount(0);
  await expect(page.getByRole('link', { name: original.clinicContact.phone })).toHaveAttribute('href', 'tel:02343888999');
});

test('slot race keeps original booking and entered reason, reloads slots for review', async ({ page }) => {
  await mockSession(page);
  await page.route('**/api/appointments/*/reschedule', route => route.fulfill(json({ code: 'MSG34', message: 'MSG34: Lịch hẹn cũ vẫn được giữ nguyên.' }, 409)));
  await page.goto('/customer/appointments/AP-WEB-TEST');
  const dialog = await reviewMove(page);
  await dialog.getByRole('button', { name: 'Xác nhận đổi lịch', exact: true }).click();
  await expect(dialog.getByText('MSG34: Lịch hẹn cũ vẫn được giữ nguyên.')).toBeVisible();
  await expect(dialog.getByLabel('Lý do', { exact: true })).toHaveValue('SYNTHETIC change');
  await expect(dialog.getByRole('button', { name: 'Xem lại', exact: true })).toBeDisabled();
  await expect(page.getByText(formatTime(original.start), { exact: true }).first()).toBeVisible();
});

test('stale version requires reload instead of allowing another confirmation', async ({ page }) => {
  await mockSession(page);
  await page.route('**/api/appointments/*/reschedule', route => route.fulfill(json({ code: 'MSG111', message: 'MSG111: Dữ liệu đã thay đổi.' }, 409)));
  await page.goto('/customer/appointments/AP-WEB-TEST');
  const dialog = await reviewMove(page);
  await dialog.getByRole('button', { name: 'Xác nhận đổi lịch', exact: true }).click();
  await expect(dialog.getByText('MSG111: Dữ liệu đã thay đổi.')).toBeVisible();
  await expect(dialog.getByRole('button', { name: 'Xác nhận đổi lịch', exact: true })).toBeDisabled();
  await dialog.getByRole('button', { name: 'Tải lại lịch hẹn', exact: true }).click();
  await expect(dialog).not.toBeVisible();
  await expect(page.getByRole('button', { name: 'Đổi lịch', exact: true })).toBeVisible();
});

test('network retry reuses requestKey and never updates the displayed appointment early', async ({ page }) => {
  await mockSession(page);
  const keys: string[] = [];
  await page.route('**/api/appointments/*/reschedule', async route => {
    keys.push(route.request().postDataJSON().requestKey);
    if (keys.length === 1) await route.abort('failed');
    else await route.fulfill(json({ appointment: { ...original, start: slot.start, end: slot.end, version: 2 },
      notification: { code: 'MSG35', message: 'Đã đổi lịch thử nghiệm.' } }));
  });
  await page.goto('/customer/appointments/AP-WEB-TEST');
  const dialog = await reviewMove(page);
  expect(keys).toHaveLength(0);
  await dialog.getByRole('button', { name: 'Xác nhận đổi lịch', exact: true }).click();
  await expect(dialog.getByText(/Không thể hoàn tất thao tác/)).toBeVisible();
  await expect(page.getByText(formatTime(original.start), { exact: true }).first()).toBeVisible();
  await dialog.getByRole('button', { name: 'Xác nhận đổi lịch', exact: true }).click();
  await expect(page.getByText('Đã đổi lịch thử nghiệm.')).toBeVisible();
  expect(keys).toHaveLength(2); expect(keys[0]).toBe(keys[1]);
});

for (const status of [401, 403, 404]) test(`HTTP ${status} after opening detail removes cached appointment`, async ({ page }) => {
  await mockSession(page);
  await page.route('**/api/appointments/*/cancel', route => route.fulfill(json({ code: status === 401 ? 'MSG07' : 'MSG102', message: 'Quyền truy cập đã thay đổi.' }, status)));
  await page.goto('/customer/appointments/AP-WEB-TEST');
  await page.getByRole('button', { name: 'Hủy lịch', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Hủy lịch hẹn' });
  await dialog.getByRole('button', { name: 'Xem lại', exact: true }).click();
  await expect(dialog.getByText('Vui lòng nhập Lý do.')).toBeVisible();
  await dialog.getByLabel('Lý do', { exact: true }).fill('SYNTHETIC cancellation');
  await dialog.getByRole('button', { name: 'Xem lại', exact: true }).click();
  await dialog.getByRole('button', { name: 'Xác nhận hủy lịch', exact: true }).click();
  await expect(page.getByText(original.bookerContactMasked)).toHaveCount(0);
  if (status === 401) {
    await expect(page).toHaveURL(/\/customer\/sign-in$/);
    expect(await page.evaluate(() => sessionStorage.getItem('dcms.customer.accessToken'))).toBeNull();
  } else {
    await expect(page.getByText('Quyền truy cập đã thay đổi.')).toBeVisible();
    expect(await page.evaluate(() => sessionStorage.getItem('dcms.customer.accessToken'))).toBe('customer-test-token');
  }
});

async function availableSetup(request: APIRequestContext) {
  const response = await request.get(`${api}/api/availability/options`);
  expect(response.ok()).toBeTruthy();
  const options = await response.json();
  for (const branch of options.branches) {
    const dentist = options.employees.find((item: { branchId: number; canDentist: boolean }) => item.branchId === branch.id && item.canDentist);
    const service = options.services.find((item: { branchId: number }) => item.branchId === branch.id);
    if (!dentist || !service) continue;
    for (let offset = 2; offset < 23; offset++) {
      const day = clinicDate(Date.now() + offset * 86400_000);
      const query = new URLSearchParams({ branchId: branch.id, dentistId: dentist.id, serviceId: service.id, date: day,
        durationMinutes: service.publicDurationMinutes });
      const result = await request.get(`${api}/api/availability?${query}`);
      if (!result.ok()) continue;
      const { slots } = await result.json();
      if (slots.length >= 4) return { branch, dentist, service, slots: slots as Slot[], day };
    }
  }
  throw new Error('Publish a future demo schedule in S09 before running the real customer appointment flow.');
}

test('actual API and PostgreSQL: customer views, reschedules and cancels on web', async ({ page, request }) => {
  test.slow();
  const setup = await availableSetup(request);
  const password = `Aa1!${randomUUID()}${randomUUID()}`;
  const phone = `09${randomInt(10000000, 99999999)}`;
  const registered = await request.post(`${api}/api/auth/register`, { data: { fullName: 'SYNTHETIC Web UC06', phoneNumber: phone,
    email: `${randomUUID()}@example.invalid`, password, confirmPassword: password } });
  expect(registered.status()).toBe(201);
  const session = await (await request.post(`${api}/api/auth/sign-in`, { data: { userName: phone, password } })).json();
  const headers = { Authorization: `Bearer ${session.accessToken}` };
  const booked = await request.post(`${api}/api/appointments`, { headers, data: { branchId: setup.branch.id,
    booker: { name: 'SYNTHETIC Web UC06', phone }, attendee: { name: 'SYNTHETIC Web attendee' }, attendeeRelation: 'Self',
    kind: 'Standard', serviceId: setup.service.id, dentistId: setup.dentist.id, startAt: setup.slots[0].start,
    durationMinutes: setup.service.publicDurationMinutes, scheduleVersion: setup.slots[0].scheduleVersion, requestKey: randomUUID() } });
  expect(booked.status()).toBe(201);
  const appointment = await booked.json();
  try {
    await page.goto('/customer/sign-in');
    await page.getByLabel('Số điện thoại', { exact: true }).fill(phone);
    await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
    await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
    await page.getByRole('link', { name: 'Lịch hẹn của tôi', exact: true }).click();
    await page.getByRole('link', { name: `Xem lịch ${appointment.code}`, exact: true }).click();
    await expect(page.getByText('SYNTHETIC Web attendee', { exact: true })).toBeVisible();
    const allowed = await (await request.get(`${api}/api/appointments/${appointment.code}/availability?date=${setup.day}`, { headers })).json();
    const target: Slot = allowed.slots.find((item: Slot) => Date.parse(item.start) >= Date.parse(appointment.endAt));
    expect(target).toBeTruthy();
    await page.getByRole('button', { name: 'Đổi lịch', exact: true }).click();
    const moveDialog = page.getByRole('dialog', { name: 'Đổi lịch hẹn' });
    await moveDialog.getByRole('button', { name: `${formatClock(target.start)}–${formatClock(target.end)}`, exact: true }).click();
    await moveDialog.getByLabel('Lý do', { exact: true }).fill('SYNTHETIC web reschedule');
    await moveDialog.getByRole('button', { name: 'Xem lại', exact: true }).click();
    const movedResponse = page.waitForResponse(response => response.url().endsWith(`/${appointment.code}/reschedule`) && response.request().method() === 'POST');
    await moveDialog.getByRole('button', { name: 'Xác nhận đổi lịch', exact: true }).click();
    expect((await movedResponse).status()).toBe(200);
    await expect(moveDialog).not.toBeVisible();
    const afterMove = await (await request.get(`${api}/api/me/appointments/${appointment.code}`, { headers })).json();
    expect(Date.parse(afterMove.start)).toBe(Date.parse(target.start)); expect(afterMove.version).toBe(2);
    await page.getByRole('button', { name: 'Hủy lịch', exact: true }).click();
    const cancelDialog = page.getByRole('dialog', { name: 'Hủy lịch hẹn' });
    await cancelDialog.getByLabel('Lý do', { exact: true }).fill('SYNTHETIC web cancellation');
    await cancelDialog.getByRole('button', { name: 'Xem lại', exact: true }).click();
    const cancelledResponse = page.waitForResponse(response => response.url().endsWith(`/${appointment.code}/cancel`) && response.request().method() === 'POST');
    await cancelDialog.getByRole('button', { name: 'Xác nhận hủy lịch', exact: true }).click();
    expect((await cancelledResponse).status()).toBe(200);
    await expect(cancelDialog).not.toBeVisible();
    const final = await (await request.get(`${api}/api/me/appointments/${appointment.code}`, { headers })).json();
    expect(final.status).toBe('Cancelled'); expect(final.version).toBe(3); expect(final.history).toHaveLength(2);
    const releasedQuery = new URLSearchParams({ branchId: setup.branch.id, dentistId: setup.dentist.id,
      serviceId: setup.service.id, date: setup.day, durationMinutes: setup.service.publicDurationMinutes });
    const released = await (await request.get(`${api}/api/availability?${releasedQuery}`)).json();
    expect(released.slots.some((item: Slot) => Date.parse(item.start) === Date.parse(target.start))).toBeTruthy();
    await page.screenshot({ path: '../.artifacts/test-results/customer-appointments/cancelled-web.png', fullPage: true });
    await page.getByRole('link', { name: '← Lịch hẹn của tôi', exact: true }).click();
    await expect(page.getByText('Không có lịch hẹn phù hợp.')).toBeVisible();
    await page.getByRole('tab', { name: 'Lịch sử', exact: true }).click();
    await expect(page.getByRole('row').filter({ hasText: appointment.code })).toContainText('Đã hủy');
    await test.info().attach('actual-api-evidence', { contentType: 'application/json', body: JSON.stringify({
      code: appointment.code, status: final.status, version: final.version, historyCount: final.history.length,
    }) });
  } finally {
    const current = await request.get(`${api}/api/me/appointments/${appointment.code}`, { headers });
    if (current.ok()) {
      const detail = await current.json();
      if (detail.status === 'Confirmed') await request.post(`${api}/api/appointments/${appointment.code}/cancel`, { headers,
        data: { expectedVersion: detail.version, requestKey: randomUUID(), reason: 'SYNTHETIC test cleanup' } });
    }
  }
});
