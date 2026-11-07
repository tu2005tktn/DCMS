import { expect, test, type Page } from '@playwright/test';

// S10 — lịch hẹn web (docs/implementation/UC04-05-S10-S11.md + màn spec v2.0, mục S10 dòng 5–8):
// lưới 15 phút thể hiện giờ làm / nghỉ / ngoài lịch; card có mã lịch, người khám, nguồn, dịch vụ, ghế và
// trạng thái — không chứa chẩn đoán/lý do nội bộ; bấm ô trống trong giờ làm mở S11 với dữ liệu điền sẵn;
// bấm lịch hẹn mở S12. API được mock ở lớp network như các test treatment hiện có.

const appointmentId = '66666666-6666-4666-8666-666666666666';
const user = { id: 9, userName: 'synthetic.assistant', fullName: 'SYNTHETIC Assistant',
  accountType: 'Staff', roles: ['ClinicAssistant'], effectiveRoles: ['ClinicAssistant'], permissions: ['appointments.write'] };

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

  // Bấm lịch hẹn → mở S12, đọc chi tiết từ server (dòng 8).
  await card.click();
  await expect(page.getByText(`Lịch hẹn ${appointment.code}`)).toBeVisible();
  await expect(page.getByText('0901***234')).toBeVisible();
});
