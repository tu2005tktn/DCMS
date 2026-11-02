import { expect, test, type Page } from '@playwright/test';

const user = { id: 21, userName: 'synthetic', fullName: 'Bệnh nhân mẫu', accountType: 'Public',
  roles: ['Public'], effectiveRoles: ['Public'], permissions: [], hasVerifiedPatientLink: false };
const first = { code: 'LH-MAU-01', start: '2030-11-05T02:00:00Z', end: '2030-11-05T02:30:00Z',
  durationMinutes: 30, dentistName: 'Bác sĩ mẫu', attendeeName: 'Bệnh nhân mẫu', attendeeKind: 'SELF',
  status: 'Confirmed', access: 'BOOKED_BY_ME', version: 1 };
async function setup(page: Page, mode: 'ok' | 'empty' | 'error' | 'expired' | 'forbidden' = 'ok') {
  const requests: string[] = [], writes: string[] = [];
  let failing = mode === 'error';
  await page.addInitScript(() => sessionStorage.setItem('dcms.customer.accessToken', 'synthetic'));
  await page.route('**/api/**', async route => {
    const request = route.request(), url = new URL(request.url());
    if (request.method() !== 'GET') writes.push(url.pathname);
    if (url.pathname === '/api/auth/me') return route.fulfill({ json: user });
    if (url.pathname === '/api/auth/google/config') return route.fulfill({ json: { enabled: false } });
    if (url.pathname === '/api/me/appointments') {
      requests.push(url.search);
      if (mode === 'expired') return route.fulfill({ status: 401, json: { code: 'MSG001', message: 'Hết phiên.' } });
      if (mode === 'forbidden') return route.fulfill({ status: 403, json: { code: 'MSG144', message: 'Không có quyền xem lịch.' } });
      if (failing) return route.fulfill({ status: 503, json: { code: 'MSG144', message: 'Không tải được lịch hẹn.' } });
      return route.fulfill({ json: { items: mode === 'empty' ? [] : [first, { ...first, code: 'LH-MAU-02' }], total: mode === 'empty' ? 0 : 2, page: 1, pageSize: 20 } });
    }
    return route.fulfill({ json: {} });
  });
  return { requests, writes, recover: () => { failing = false; } };
}

test('dashboard dùng danh sách hiện có, đúng lịch đầu tiên và route; không tự tạo tính năng', async ({ page }) => {
  const calls = await setup(page); await page.goto('/customer/home');
  await expect(page.getByRole('heading', { name: 'Xin chào, Bệnh nhân mẫu' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'LH-MAU-01', exact: true })).toBeVisible();
  await expect(page.getByText('LH-MAU-02', { exact: true })).toHaveCount(0);
  const time = page.locator('.customer-dashboard-appointment-info p').first();
  await expect(time).toContainText('05/11/2030');
  await expect(time).toContainText('09:00');
  await expect(time).toContainText('30 phút');
  await expect(page.getByRole('link', { name: 'Xem lịch hẹn', exact: true })).toHaveAttribute('href', '/customer/appointments/LH-MAU-01');
  await expect(page.getByRole('link', { name: 'Bắt đầu đặt lịch' })).toHaveAttribute('href', '/customer/book');
  await expect(page.getByRole('link', { name: 'Liên kết hồ sơ của tôi', exact: true })).toHaveAttribute('href', '/customer/patient-link');
  expect(new URLSearchParams(calls.requests[0]).get('scope')).toBe('UPCOMING');
  expect(new URLSearchParams(calls.requests[0]).get('pageSize')).toBe('20');
  await expect(page.getByRole('heading', { name: /Thông báo|AI|Sơ đồ răng|Đơn thuốc/ })).toHaveCount(0);
  expect(calls.writes).toEqual([]);
  for (const width of [390, 768, 1440]) {
    await page.setViewportSize({ width, height: 950 });
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width + 1);
    await page.screenshot({ path: `test-results/customer-dashboard-${width}.png`, fullPage: true });
  }
});

test('không có lịch không tạo lịch hoặc trạng thái giả, vẫn có đặt lịch', async ({ page }) => {
  await setup(page, 'empty'); await page.goto('/customer/home');
  await expect(page.getByText('Bạn chưa có lịch hẹn sắp tới.')).toBeVisible();
  await expect(page.getByRole('link', { name: 'Bắt đầu đặt lịch' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Xem lịch hẹn', exact: true })).toHaveCount(0);
});

test('lỗi tải giữ truy cập nhanh, thử lại bằng GET và không giả thành empty', async ({ page }) => {
  const calls = await setup(page, 'error'); await page.goto('/customer/home');
  await expect(page.getByRole('alert')).toContainText('Không tải được lịch hẹn.');
  await expect(page.getByText('Bạn chưa có lịch hẹn sắp tới.')).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Liên kết hồ sơ của tôi', exact: true })).toBeVisible();
  calls.recover();
  await page.getByRole('button', { name: 'Thử lại' }).click();
  await expect(page.getByRole('heading', { name: 'LH-MAU-01', exact: true })).toBeVisible();
  expect(calls.requests.length).toBeGreaterThanOrEqual(2); expect(calls.writes).toEqual([]);
});

test('401 hết phiên dùng guard hiện có; 403 không hiển thị dữ liệu', async ({ page }) => {
  await setup(page, 'expired'); await page.goto('/customer/home');
  await expect(page).toHaveURL(/\/customer\/sign-in$/);
  await expect(page.getByRole('heading', { name: 'Lịch hẹn gần nhất' })).toHaveCount(0);
});

test('thiếu quyền giữ lỗi đọc riêng', async ({ page }) => {
  await setup(page, 'forbidden'); await page.goto('/customer/home');
  await expect(page.getByRole('alert')).toContainText('Không có quyền xem lịch.');
  await expect(page.getByRole('link', { name: 'Xem lịch hẹn', exact: true })).toHaveCount(0);
});
