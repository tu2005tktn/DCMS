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

test('70 minutes explains the duration error and 75 immediately recovers without losing the week', async ({ page }) => {
  await mockedWorkspace(page);
  await page.getByRole('button', { name: 'Thêm khoảng', exact: true }).first().click();
  await page.getByLabel('Kết thúc 1', { exact: true }).fill('17:00');
  let previewRequests = 0;
  await page.route('**/api/working-schedules/preview-availability', route => {
    previewRequests++;
    expect(route.request().postDataJSON().query.durationMinutes).toBe(75);
    return route.fulfill({ json: { timeZone: 'Asia/Ho_Chi_Minh', date: '2030-01-07', slots: [{ start: '2030-01-07T08:00:00+07:00', end: '2030-01-07T09:15:00+07:00', chairId: 1, dentistId: 10, assistantId: null, scheduleVersion: 0, assistantScheduleVersion: null }] } });
  });
  const duration = page.getByRole('spinbutton', { name: 'Thời lượng phút', exact: true });
  const preview = page.getByRole('button', { name: 'Preview slot', exact: true });
  await duration.fill('70');
  await preview.click();
  await expect(page.getByText('Dữ liệu chưa hợp lệ · MSG110', { exact: true })).toBeVisible();
  await expect(page.locator('#duration-help')).toContainText('60 hoặc 75');
  await expect(duration).toHaveValue('70'); await expect(preview).toBeEnabled();
  await expect(page.getByLabel('Kết thúc 1', { exact: true })).toHaveValue('17:00');
  expect(previewRequests).toBe(0);
  await duration.fill(''); await preview.click();
  await expect(duration).toHaveValue(''); await expect(page.locator('#duration-help')).toContainText('Nhập thời lượng');
  expect(previewRequests).toBe(0);
  await duration.fill('75'); await preview.click();
  await expect(page.getByRole('columnheader', { name: 'Bắt đầu', exact: true })).toBeVisible();
  await expect(page.getByText('Dữ liệu chưa hợp lệ · MSG110', { exact: true })).toHaveCount(0);
  await expect(preview).toBeEnabled(); expect(previewRequests).toBe(1);
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

test('reported Work Break Block sample previews through the real editor and API', async ({ page }) => {
  const password = process.env.E2E_DEMO_PASSWORD;
  test.skip(!password, 'Set E2E_DEMO_PASSWORD from the ignored .env.');
  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill('dentist.demo');
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await page.getByRole('link', { name: 'Lịch làm việc', exact: true }).click();
  const loading = page.waitForResponse(r => r.url().includes('/api/working-schedules?') && r.url().includes('2026-10-19'));
  await page.getByLabel('Tuần bắt đầu', { exact: true }).fill('2026-10-19');
  expect((await loading).status()).toBe(200);
  await expect(page.getByRole('button', { name: 'Lưu và công bố' })).toBeEnabled();
  // Read-only preview: discard the loaded periods locally, never publish over the user's week.
  while (await page.getByRole('button', { name: /^Xóa khoảng/ }).count()) await page.getByRole('button', { name: /^Xóa khoảng/ }).first().click();
  for (let index = 1; index <= 3; index++) await page.getByRole('button', { name: 'Thêm khoảng', exact: true }).first().click();
  await page.getByLabel('Bắt đầu 1', { exact: true }).fill('08:00');
  await page.getByLabel('Kết thúc 1', { exact: true }).fill('17:00');
  await page.locator('.ant-select[aria-label="Loại khoảng 2"] .ant-select-selection-item').click();
  await page.getByTitle('Nghỉ', { exact: true }).filter({ visible: true }).click();
  await page.getByLabel('Bắt đầu 2', { exact: true }).fill('12:00');
  await page.getByLabel('Kết thúc 2', { exact: true }).fill('13:00');
  await page.getByLabel('Lý do 2', { exact: true }).fill('Nghỉ trưa');
  await page.locator('.ant-select[aria-label="Loại khoảng 3"] .ant-select-selection-item').click();
  const blockList = await page.getByRole('combobox', { name: 'Loại khoảng 3', exact: true }).getAttribute('aria-controls');
  await page.locator(`.ant-select-dropdown:has([id="${blockList}"])`).getByTitle('Block', { exact: true }).click();
  await page.getByLabel('Bắt đầu 3', { exact: true }).fill('15:00');
  await page.getByLabel('Kết thúc 3', { exact: true }).fill('15:30');
  await page.getByLabel('Lý do 3', { exact: true }).fill('Họp nội bộ');
  const response = page.waitForResponse(r => r.url().endsWith('/api/working-schedules/preview'));
  await page.getByRole('button', { name: 'Kiểm tra ảnh hưởng', exact: true }).click();
  expect((await response).status()).toBe(200);
  await expect(page.getByText('Không có lịch Confirmed bị ảnh hưởng tại thời điểm kiểm tra.')).toBeVisible();
  await page.getByRole('button', { name: 'Preview slot', exact: true }).click();
  await expect(page.getByRole('columnheader', { name: 'Bắt đầu', exact: true })).toBeVisible();
  await expect(page.getByText('Dữ liệu chưa hợp lệ · MSG110', { exact: true })).toHaveCount(0);
});

test('S09 identifies the actual overlapping rows without submitting or losing the draft', async ({ page }) => {
  await mockedWorkspace(page);
  await page.getByRole('button', { name: 'Thêm khoảng', exact: true }).first().click();
  await page.getByRole('button', { name: 'Thêm khoảng', exact: true }).first().click();
  let requests = 0;
  await page.route('**/api/working-schedules/preview', route => { requests++; return route.fulfill({ json: { affectedAppointments: [] } }); });
  await page.getByRole('button', { name: 'Kiểm tra ảnh hưởng', exact: true }).click();
  await expect(page.getByText(/Khoảng 1 \(Thứ 2\) và Khoảng 2 \(Thứ 2\) trùng giờ Làm việc/)).toBeVisible();
  expect(requests).toBe(0);
  await expect(page.getByLabel('Bắt đầu 2', { exact: true })).toHaveValue('08:00');
  await page.locator('.ant-select[aria-label="Loại khoảng 2"] .ant-select-selection-item').click();
  await page.getByTitle('Nghỉ', { exact: true }).filter({ visible: true }).click();
  await page.getByLabel('Bắt đầu 2', { exact: true }).fill('09:00');
  await page.getByLabel('Kết thúc 2', { exact: true }).fill('09:30');
  await page.getByLabel('Lý do 2', { exact: true }).fill('Nghỉ');
  await page.getByRole('button', { name: 'Kiểm tra ảnh hưởng', exact: true }).click();
  await expect(page.getByText('Không có lịch Confirmed bị ảnh hưởng tại thời điểm kiểm tra.')).toBeVisible();
  expect(requests).toBe(1);
  await expect(page.getByText('Dữ liệu chưa hợp lệ · MSG110', { exact: true })).toHaveCount(0);
});
