import { expect, test, type Page } from '@playwright/test';

const user = { id: 7, userName: 'synthetic.assistant', fullName: 'SYNTHETIC Assistant', accountType: 'Staff',
  roles: ['ClinicAssistant'], effectiveRoles: ['ClinicAssistant'],
  permissions: ['reception.read', 'appointments.read', 'appointments.no-show', 'appointments.reschedule'], hasVerifiedPatientLink: false };
const at = (minutes: number) => new Date(Date.now() + minutes * 60_000).toISOString();
const clinicDate = new Date(Date.now() + 7 * 3_600_000).toISOString().slice(0, 10);

function row(code: string, attendeeName: string, start: number, extra: Record<string, unknown> = {}) {
  return { appointmentId: `00000000-0000-0000-0000-0000000000${code}`, code: `AP-${code}`, branchId: 1, branchName: 'Chi nhánh thử',
    dentistId: 2, dentistName: 'Bác sĩ thử', chairId: 7, chairName: 'Ghế 1', startAt: at(start), endAt: at(start + 30), status: 'Confirmed',
    version: 1, patientId: null, patientCode: null, patientName: null, bookerName: 'Người đặt thử', bookerPhone: '0901234567',
    bookerEmail: null, attendeeName, attendeeBirthDate: null, attendeeRelation: 'Self', visitId: null, canCheckIn: false,
    ineligibleReason: null, canUndoNoShow: false, ...extra };
}

async function setup(page: Page) {
  const sent: { path: string; body: Record<string, unknown> }[] = [];
  let today = [row('01', 'Khách trễ', -10), row('02', 'Khách sắp tới', 120), row('03', 'Khách bấm nhầm', -90, { status: 'NoShow', version: 2, canUndoNoShow: true })];
  let overdue = [row('04', 'Khách hôm trước', -2880)];
  const wrap = (items: unknown[]) => ({ items, total: items.length, page: 1, pageSize: 20, clinicDate, serverNow: at(0) });
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    const json = (body: unknown) => route.fulfill({ json: body });
    if (path === '/api/auth/sign-in') return json({ accessToken: 'synthetic-token', user });
    if (path === '/api/auth/me') return json(user);
    if (path === '/api/reception/options') return json({ canCheckIn: true, clinicDate, serverNow: at(0), calendar: {
      branches: [{ id: 1, name: 'Chi nhánh thử' }], dentists: [], chairs: [], services: [], ownScheduleOnly: false } });
    if (path === '/api/reception/appointments') return json(wrap(today));
    if (path === '/api/reception/overdue') return json(wrap(overdue));
    if (path === '/api/appointments/options') return json({ branches: [{ id: 1, name: 'Chi nhánh thử' }],
      dentists: [{ id: 2, branchId: 1, name: 'Bác sĩ thử' }], chairs: [], services: [], ownScheduleOnly: false });
    if (path.endsWith('/availability')) return json({ slots: [{ start: at(20), end: at(50), chairId: 7, dentistId: 2, scheduleVersion: 1,
      assistantScheduleVersion: null, bookingSettingsVersion: 1 }] });
    if (path.endsWith('/reschedule')) {
      sent.push({ path, body: route.request().postDataJSON() });
      today = today.map(x => path.includes(x.code) ? { ...x, status: 'Confirmed', startAt: at(20), endAt: at(50), version: 3, canUndoNoShow: false } : x);
      return json({ notification: { message: 'Đã đổi lịch.' } });
    }
    if (path.endsWith('/no-show') || path.endsWith('/no-show/undo')) {
      sent.push({ path, body: route.request().postDataJSON() });
      if (path.includes('AP-04')) overdue = [];
      if (path.includes('AP-03')) today = today.map(x => x.code === 'AP-03' ? { ...x, status: 'Confirmed', canUndoNoShow: false } : x);
      return json({ notification: { message: 'Đã ghi nhận.' } });
    }
    return route.fulfill({ status: 404, json: { code: 'MSG49', message: 'not mocked' } });
  });
  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill('synthetic.assistant');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('synthetic');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Đăng xuất' })).toBeVisible();
  await page.evaluate(() => { history.pushState(null, '', '/reception'); window.dispatchEvent(new PopStateEvent('popstate')); });
  return sent;
}

test('bảng tiếp nhận gắn nhãn trễ hẹn, ghi nhận không tới và hoàn tác ngay tại bảng', async ({ page }, info) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  const sent = await setup(page);
  const late = page.getByRole('row').filter({ hasText: 'Khách trễ' });
  await expect(late).toContainText('Trễ hẹn');
  await expect(late.getByRole('button', { name: 'Khách không tới' })).toBeVisible();
  const upcoming = page.getByRole('row').filter({ hasText: 'Khách sắp tới' });
  await expect(upcoming).toContainText('Đã xác nhận');
  await expect(upcoming.getByRole('button', { name: 'Khách không tới' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Hủy lịch' })).toHaveCount(0);
  await page.screenshot({ path: info.outputPath('reception-today.png') });
  await page.getByRole('row').filter({ hasText: 'Khách bấm nhầm' }).getByRole('button', { name: 'Hoàn tác không tới' }).click();
  await page.getByRole('dialog', { name: 'Hoàn tác khách không tới' }).getByRole('button', { name: 'Xác nhận hoàn tác' }).click();
  await expect.poll(() => sent.length).toBe(1);
  expect(sent[0]).toMatchObject({ path: '/api/appointments/AP-03/no-show/undo', body: { expectedVersion: 2, reason: 'Ghi nhận nhầm khách không tới.' } });
  expect(sent[0].body).not.toHaveProperty('clinicInitiated');
});

test('lịch khách không tới được đổi sang giờ trống ngay trong ngày', async ({ page }, info) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  const sent = await setup(page);
  await expect(page.getByRole('row').filter({ hasText: 'Khách sắp tới' }).getByRole('button', { name: 'Đổi lịch' })).toHaveCount(0);
  await expect(page.getByRole('row').filter({ hasText: 'Khách trễ' }).getByRole('button', { name: 'Đổi lịch' })).toBeVisible();
  await page.screenshot({ path: info.outputPath('reception-rebook-row.png') });
  await page.getByRole('row').filter({ hasText: 'Khách bấm nhầm' }).getByRole('button', { name: 'Đổi lịch' }).click();
  const drawer = page.getByRole('dialog', { name: 'Đổi lịch' });
  await expect(drawer.getByLabel('Ngày hẹn mới (giờ Việt Nam)')).toHaveValue(clinicDate);
  await drawer.getByRole('group', { name: 'Khung giờ chỉnh sửa lịch hẹn' }).getByRole('button').first().click();
  await page.screenshot({ path: info.outputPath('reception-rebook-drawer.png'), animations: 'disabled' });
  await drawer.getByRole('button', { name: 'Lưu thay đổi' }).click();
  await expect.poll(() => sent.length).toBe(1);
  expect(sent[0]).toMatchObject({ path: '/api/appointments/AP-03/reschedule',
    body: { expectedVersion: 2, clinicInitiated: true, dentistId: 2, chairId: 7, reason: 'Khách đến muộn, đổi sang giờ khác.' } });
  await expect(page.getByRole('row').filter({ hasText: 'Khách bấm nhầm' })).toContainText('Đã xác nhận');
});

test('tab quá giờ chưa xử lý gom lịch cũ và vơi đi sau khi xác nhận', async ({ page }, info) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  const sent = await setup(page);
  await page.getByRole('tab', { name: 'Quá giờ chưa xử lý (1)' }).click();
  const old = page.getByRole('row').filter({ hasText: 'Khách hôm trước' });
  await expect(old).toContainText('Chờ xử lý');
  await expect(old).toContainText('0901234567');
  // Only today's missed bookings can be moved; older ones are recorded as absent first.
  await expect(old.getByRole('button', { name: 'Đổi lịch' })).toHaveCount(0);
  await expect(page.locator('.ant-spin-spinning')).toHaveCount(0);
  await page.screenshot({ path: info.outputPath('reception-overdue.png'), animations: 'disabled' });
  await old.getByRole('button', { name: 'Khách không tới' }).click();
  await page.getByRole('button', { name: 'Xác nhận khách không tới' }).click();
  await expect(page.getByText('Không còn lịch quá giờ cần xử lý.')).toBeVisible();
  await expect(page.getByRole('tab', { name: 'Quá giờ chưa xử lý (0)' })).toBeVisible();
  expect(sent[0].path).toBe('/api/appointments/AP-04/no-show');
});
