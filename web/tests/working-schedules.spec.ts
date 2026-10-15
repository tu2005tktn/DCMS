import { expect, test, type Page } from '@playwright/test';
import type { Options, Schedule, ScheduleCommand } from '../src/features/working-schedules/contracts';

const options: Options = { branches: [{ id: 1, name: 'Chi nhánh kiểm thử' }], employees: [{ id: 10, branchId: 1, name: 'Bác sĩ kiểm thử', canDentist: true, canEdit: true, self: true }], chairs: [{ id: 1, branchId: 1, name: 'Ghế 1' }], services: [{ id: 1, branchId: 1, name: 'Khám 30 phút', publicDurationMinutes: 30 }] };
const schedule = (): Schedule => ({ id: null, branchId: 1, dentistId: 10, weekStart: '2030-01-07', version: 0, status: 'Unpublished', intervals: [], publishedBy: null, publishedAt: null });
async function mockedWorkspace(page: Page) {
  const user = { id: 10, userName: 'dentist.test', fullName: 'Bác sĩ kiểm thử', accountType: 'Staff', roles: ['Dentist'], effectiveRoles: ['Dentist'], permissions: [] };
  await page.route('**/api/auth/sign-in', route => route.fulfill({ json: { accessToken: 'test-only', user } }));
  await page.route('**/api/auth/me', route => route.fulfill({ json: user }));
  await page.route('**/api/working-schedules/options', route => route.fulfill({ json: options }));
  await page.route('**/api/working-schedules?*', route => route.fulfill({ json: schedule() }));
  await page.goto('/sign-in'); await page.getByLabel('Tên đăng nhập', { exact: true }).fill('dentist.test'); await page.getByLabel('Mật khẩu', { exact: true }).fill('test-only');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await page.getByRole('link', { name: 'Lịch làm việc', exact: true }).click();
  await expect(page.getByText('Chưa công bố', { exact: true })).toBeVisible();
}
test('S09 retains draft on stale/network failure and discards only after confirmation', async ({ page }) => {
  await mockedWorkspace(page);
  await page.getByRole('button', { name: 'Thêm khoảng', exact: true }).first().click();
  await page.getByLabel('Bắt đầu 1', { exact: true }).fill('09:00');
  await page.route('**/api/working-schedules/publish', route => route.fulfill({ status: 409, json: { code: 'MSG111', message: 'Dữ liệu đã thay đổi.' } }));
  await page.getByRole('button', { name: 'Lưu và công bố' }).click();
  await expect(page.getByText('Xung đột · Cần kiểm tra lại · MSG111')).toBeVisible();
  await expect(page.getByLabel('Bắt đầu 1', { exact: true })).toHaveValue('09:00');
  await page.route('**/api/working-schedules/publish', route => route.abort());
  await page.getByRole('button', { name: 'Lưu và công bố' }).click();
  await expect(page.getByText('Lỗi kết nối hoặc dịch vụ · MSG144')).toBeVisible();
  await expect(page.getByLabel('Bắt đầu 1', { exact: true })).toHaveValue('09:00');
  page.once('dialog', dialog => dialog.accept());
  await page.getByRole('button', { name: 'Hủy thay đổi chưa lưu' }).click();
  await expect(page.getByLabel('Bắt đầu 1', { exact: true })).toHaveCount(0);
});
test('S09 shows affected confirmed appointments and draft slot preview then publishes readback', async ({ page }) => {
  await mockedWorkspace(page);
  await page.getByRole('button', { name: 'Thêm khoảng', exact: true }).first().click();
  await page.route('**/api/working-schedules/publish', route => route.fulfill({ status: 409, json: { code: 'MSG38', message: 'Lịch Confirmed bị ảnh hưởng.' } }));
  await page.route('**/api/working-schedules/preview', route => route.fulfill({ json: { affectedAppointments: [{ id: 'r1', code: 'AP-001', start: '2030-01-07T09:00:00+07:00', end: '2030-01-07T10:00:00+07:00', attendee: 'Người đến kiểm thử', status: 'Confirmed' }] } }));
  await page.getByRole('button', { name: 'Lưu và công bố' }).click();
  await expect(page.getByRole('link', { name: 'AP-001 · S12' })).toHaveAttribute('href', '/appointments/r1');
  await page.route('**/api/working-schedules/preview-availability', route => route.fulfill({ json: { timeZone: 'Asia/Ho_Chi_Minh', date: '2030-01-07', slots: [] } }));
  await page.getByRole('button', { name: 'Preview slot', exact: true }).click();
  await expect(page.getByText('Không có khung giờ đáp ứng tài nguyên')).toBeVisible();
  await page.route('**/api/working-schedules/publish', route => {
    const command = route.request().postDataJSON() as ScheduleCommand;
    expect(command.requestKey).toMatch(/^[0-9a-f-]{36}$/); expect(command.expectedVersion).toBe(0);
    return route.fulfill({ json: { schedule: { ...schedule(), id: 'week-id', status: 'Published', version: 1, intervals: command.intervals, publishedBy: 10, publishedAt: '2030-01-07T00:00:00Z' }, notification: { code: 'MSG39', message: 'Đã cập nhật lịch làm việc.' } } });
  });
  await page.getByRole('button', { name: 'Lưu và công bố' }).click();
  await expect(page.getByText('Đã công bố', { exact: true })).toBeVisible();
  await expect(page.getByText('Version 1', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Hủy thay đổi chưa lưu' })).toBeDisabled();
});
test('S09 distinguishes access denied from an empty week', async ({ page }) => {
  await mockedWorkspace(page);
  await page.route('**/api/working-schedules?*', route => route.fulfill({ status: 403, json: { code: 'MSG06', message: 'Bạn không có quyền.' } }));
  await page.getByLabel('Tuần bắt đầu', { exact: true }).fill('2030-01-14');
  await expect(page.getByText('Không có quyền truy cập · MSG06')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Lưu và công bố' })).toHaveCount(0);
});
test('S09 publishes and reads real availability through local API', async ({ page, request }) => {
  const password = process.env.E2E_DEMO_PASSWORD;
  test.skip(!password, 'Set E2E_DEMO_PASSWORD from the ignored .env for the real API smoke test.');
  await page.goto('/sign-in'); await page.getByLabel('Tên đăng nhập', { exact: true }).fill('dentist.demo'); await page.getByLabel('Mật khẩu', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click(); await page.getByRole('link', { name: 'Lịch làm việc', exact: true }).click();
  await expect(page.getByLabel('Tuần bắt đầu', { exact: true })).toBeVisible();
  await page.getByLabel('Tuần bắt đầu', { exact: true }).fill('2031-01-06');
  await expect(page.getByRole('button', { name: 'Lưu và công bố' })).toBeEnabled();
  if (await page.getByLabel('Bắt đầu 1', { exact: true }).count() === 0) await page.getByRole('button', { name: 'Thêm khoảng', exact: true }).first().click();
  await page.getByLabel('Bắt đầu 1', { exact: true }).fill('08:00');
  await page.getByLabel('Kết thúc 1', { exact: true }).fill('12:00');
  const publishing = page.waitForResponse(r => r.url().endsWith('/api/working-schedules/publish'));
  await page.getByRole('button', { name: 'Lưu và công bố' }).click(); const response = await publishing; expect(response.status()).toBe(200);
  const result = await response.json(); await expect(page.getByText('Đã công bố', { exact: true })).toBeVisible();
  const publicOptions = await (await request.get(`http://localhost:5080/api/availability/options?branchId=${result.schedule.branchId}`)).json();
  const service = publicOptions.services[0];
  const available = await request.get(`http://localhost:5080/api/availability?branchId=${result.schedule.branchId}&dentistId=${result.schedule.dentistId}&serviceId=${service.id}&date=2031-01-06&durationMinutes=${service.publicDurationMinutes}`);
  expect(available.status()).toBe(200); expect((await available.json()).slots.length).toBeGreaterThan(0);
  await page.screenshot({ path: '../.tools/qa/s09-working-schedules.png', fullPage: true });
});
