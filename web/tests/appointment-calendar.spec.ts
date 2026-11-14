import { expect, test, type Page } from '@playwright/test';

// S10 — lịch hẹn web (docs/implementation/UC04-05-S10-S11.md + màn spec v2.0, mục S10 dòng 5–8):
// lưới 15 phút thể hiện giờ làm / nghỉ / ngoài lịch; card có mã lịch, người khám, nguồn, dịch vụ, ghế và
// trạng thái — không chứa chẩn đoán/lý do nội bộ; bấm ô trống trong giờ làm mở S11 với dữ liệu điền sẵn;
// bấm lịch hẹn mở S12. API được mock ở lớp network như các test treatment hiện có.

const appointmentId = '66666666-6666-4666-8666-666666666666';
const user = { id: 9, userName: 'synthetic.assistant', fullName: 'SYNTHETIC Assistant',
  accountType: 'Staff', roles: ['ClinicAssistant'], effectiveRoles: ['ClinicAssistant'], permissions: ['appointments.read', 'appointments.create', 'appointments.write'] };

// Cùng công thức với `OFFSET` trong web/src/features/appointments/types.ts (giờ VN, UTC+7).
const clinicToday = () => new Date(Date.now() + 7 * 3_600_000).toISOString().slice(0, 10);

const appointment = {
  id: appointmentId, code: 'AP-SYN-001', branchId: 1, branchName: 'Chi nhánh thử nghiệm',
  startAt: `${clinicToday()}T01:30:00Z`, endAt: `${clinicToday()}T02:00:00Z`, durationMinutes: 30,
  dentistId: 2, dentistName: 'Bác sĩ thử nghiệm', chairId: 7, chairName: 'Ghế 1',
  serviceName: 'Trám răng', attendeeName: 'Nguyễn Văn Thử', attendeeRelation: 'Self',
  kind: 'Standard', source: 'Phone', status: 'Confirmed', version: 1,
};

const view = {
  branchId: 1, from: clinicToday(), to: clinicToday(), ownScheduleOnly: false,
  appointments: [appointment],
  days: [{ dentistId: 2, date: clinicToday(),
    periods: [{ startMinute: 480, endMinute: 1020, kind: 'Work', reason: null }] }],
};

const detail = {
  appointment, bookerName: 'Người đặt thử', bookerPhoneMasked: '0901***234',
  bookerEmailMasked: null, bookedFromApp: false, reason: 'SYNTHETIC-REASON-KHONG-LO',
  exceptionReason: null, createdAt: `${clinicToday()}T00:00:00Z`,
};

async function signIn(page: Page) {
  await page.goto('http://127.0.0.1:5173/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill('synthetic.assistant');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('synthetic');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
}

test('S10 lưới 15 phút: card có mã/nguồn/trạng thái, không lộ lý do; ô trống mở S11 và lịch mở S12', async ({ page }) => {
  await page.route('**/api/**', route => route.fulfill({ status: 404, json: { code: 'MSG144', message: 'not mocked' } }));
  await page.route('**/api/auth/me', route => route.fulfill({ json: user }));
  await page.route('**/api/auth/sign-in', route => route.fulfill({ json: { accessToken: 'synthetic-token', user } }));
  await page.route('**/api/appointments/options', route => route.fulfill({ json: {
    branches: [{ id: 1, name: 'Chi nhánh thử nghiệm' }],
    dentists: [{ id: 2, branchId: 1, name: 'Bác sĩ thử nghiệm' }],
    chairs: [{ id: 7, branchId: 1, name: 'Ghế 1' }],
    services: [{ id: 3, branchId: 1, name: 'Trám răng', publicDurationMinutes: 30 }],
    ownScheduleOnly: false,
  } }));
  await page.route('**/api/appointments/calendar*', route => route.fulfill({ json: view }));
  await page.route(`**/api/appointments/${appointmentId}`, route => route.fulfill({ json: detail }));

  await signIn(page);
  await page.evaluate(path => { history.pushState(null, '', path); window.dispatchEvent(new PopStateEvent('popstate')); }, '/appointments');

  // Lưới thể hiện giờ làm / nghỉ block / ngoài lịch (màn spec S10 dòng 5).
  await expect(page.getByText('Giờ làm', { exact: true })).toBeVisible();
  await expect(page.getByText('Ngoài lịch')).toBeVisible();
  await expect(page.getByText('Nghỉ / block')).toBeVisible();

  // Card: mã lịch · nguồn · dịch vụ · ghế · trạng thái (dòng 6) và không chứa chẩn đoán/lý do nội bộ.
  const card = page.getByRole('button', { name: `Lịch ${appointment.code}` });
  await expect(card).toContainText(appointment.code);
  await expect(card).toContainText('Điện thoại');
  await expect(card).toContainText('Trám răng');
  await expect(card).toContainText('Ghế 1');
  await expect(card).toContainText('Đã xác nhận');
  await expect(page.getByText('SYNTHETIC-REASON-KHONG-LO')).toHaveCount(0);

  // Bấm ô trống trong giờ làm → mở S11 đã điền sẵn (dòng 7).
  await page.getByTitle('Đặt lịch 09:15').click();
  await expect(page.getByText('Đặt lịch tại quầy (S11)')).toBeVisible();
  await page.keyboard.press('Escape');

  // Bấm lịch hẹn → mở drawer tại lưới, vẫn có đường tới trang chi tiết đầy đủ.
  await card.click();
  const drawer = page.getByRole('dialog', { name: 'Thông tin lịch hẹn', exact: true });
  await expect(drawer).toBeVisible(); await expect(page).toHaveURL(/\/appointments$/);
  await expect(page.getByText(`Lịch hẹn ${appointment.code}`)).toBeVisible();
  await expect(drawer.getByText(/0901\*\*\*234/)).toBeVisible();
  await drawer.getByRole('link', { name: 'Mở chi tiết đầy đủ', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/appointments/${appointmentId}$`));
});

// docs/specs/CHAIR-CAPACITY-ANY-DENTIST-BOOKING.vi.md: S11 "Bất kỳ nha sĩ" lấy slot theo ghế trống của mọi nha sĩ,
// hiển thị nha sĩ được gán trên từng khung giờ và gửi dentistId/chairId của slot đã chọn.
test('S11 đặt lịch Bất kỳ nha sĩ: slot gộp theo ghế trống, gửi nha sĩ và ghế của slot', async ({ page }) => {
  const day = clinicToday();
  const at = (hour: number) => new Date(Date.parse(`${day}T00:00:00+07:00`) + hour * 3_600_000 + 2 * 86_400_000).toISOString();
  const date = new Date(Date.parse(at(0)) + 7 * 3_600_000).toISOString().slice(0, 10);
  const queries: URLSearchParams[] = [];
  let posted: Record<string, unknown> | null = null;
  await page.route('**/api/**', route => route.fulfill({ status: 404, json: { code: 'MSG144', message: 'not mocked' } }));
  await page.route('**/api/auth/me', route => route.fulfill({ json: user }));
  await page.route('**/api/auth/sign-in', route => route.fulfill({ json: { accessToken: 'synthetic-token', user } }));
  await page.route('**/api/appointments/options', route => route.fulfill({ json: {
    branches: [{ id: 1, name: 'Chi nhánh thử nghiệm' }],
    dentists: [{ id: 2, branchId: 1, name: 'Bác sĩ thử nghiệm' }, { id: 5, branchId: 1, name: 'Bác sĩ thứ hai' }],
    chairs: [{ id: 7, branchId: 1, name: 'Ghế 1' }, { id: 8, branchId: 1, name: 'Ghế 2' }],
    services: [{ id: 3, branchId: 1, name: 'Trám răng', publicDurationMinutes: 30 }],
    ownScheduleOnly: false,
  } }));
  await page.route('**/api/appointments/calendar*', route => route.fulfill({ json: { ...view, appointments: [] } }));
  await page.route('**/api/availability?*', route => {
    queries.push(new URL(route.request().url()).searchParams);
    return route.fulfill({ json: { timeZone: 'Asia/Ho_Chi_Minh', date, slots: [
      { start: at(9), end: at(9.5), chairId: 7, dentistId: 2, assistantId: null, scheduleVersion: 1, assistantScheduleVersion: null, bookingSettingsVersion: 1 },
      { start: at(10), end: at(10.5), chairId: 8, dentistId: 5, assistantId: null, scheduleVersion: 4, assistantScheduleVersion: null, bookingSettingsVersion: 1 },
    ] } });
  });
  await page.route('**/api/appointments', route => {
    posted = route.request().postDataJSON() as Record<string, unknown>;
    return route.fulfill({ status: 201, json: { appointmentId: 'id-9', code: 'AP-ANY-1', version: 1, status: 'Confirmed',
      startAt: at(10), endAt: at(10.5), notification: { code: 'MSG29', message: 'Đặt lịch thành công.' } } });
  });

  await signIn(page);
  await page.evaluate(path => { history.pushState(null, '', path); window.dispatchEvent(new PopStateEvent('popstate')); }, '/appointments');
  await page.getByRole('button', { name: 'Đặt lịch', exact: true }).click();
  const drawer = page.getByRole('dialog', { name: 'Đặt lịch tại quầy (S11)' });
  await expect(drawer).toBeVisible();
  await drawer.getByLabel('Họ tên người đặt').fill('Người đặt thử');
  await drawer.getByLabel('SĐT (10 số) hoặc email').fill('0901234567');
  await drawer.getByLabel('Họ tên người khám').fill('Người khám thử');
  await drawer.getByLabel('Dịch vụ', { exact: true }).click();
  await page.locator('.ant-select-dropdown:visible').getByText('Trám răng · 30 phút').click();
  await drawer.getByLabel('Nha sĩ', { exact: true }).click();
  await page.locator('.ant-select-dropdown:visible').getByText('Bất kỳ nha sĩ (ghế trống)').click();
  await drawer.getByLabel('Ngày', { exact: true }).fill(date);
  const slots = drawer.locator('.booking-slots button');
  await expect(slots).toHaveCount(2);
  expect(queries.at(-1)?.get('dentistId')).toBe('0');
  await expect(slots.nth(0)).toContainText('Bác sĩ thử nghiệm');
  await expect(slots.nth(1)).toContainText('Bác sĩ thứ hai');
  await expect(slots.nth(1)).toContainText('Ghế 2');
  await slots.nth(1).click();
  await drawer.getByRole('button', { name: 'Xem lại' }).click();
  await expect(drawer).toContainText('Bác sĩ thứ hai · Ghế 2');
  await drawer.getByRole('button', { name: 'Xác nhận đặt lịch' }).click();
  await expect.poll(() => posted?.dentistId).toBe(5);
  expect(posted?.chairId).toBe(8);
  expect(posted?.scheduleVersion).toBe(4);
});
