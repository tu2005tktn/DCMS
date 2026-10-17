import { test, expect, type Page } from '@playwright/test';

const password = process.env.E2E_DEMO_PASSWORD;
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD from ignored .env.'); });

async function login(page: Page, user = 'assistant.demo') {
  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill(user);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Tài khoản của tôi' })).toBeVisible();
}

test('staff sees inbox menu, can filter and open a request drawer', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', e => errors.push(e.message));
  await login(page, 'assistant.demo');
  await expect(page.getByRole('link', { name: 'Hộp thư hỗ trợ', exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'Hộp thư hỗ trợ', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Hộp thư hỗ trợ', exact: true })).toBeVisible();
  await expect(page.getByText('Xử lý yêu cầu đổi/huỷ/xác nhận từ booker & attendee')).toBeVisible();

  // Bộ lọc mặc định "Cần xử lý" — phải có ít nhất một dòng.
  const firstRow = page.locator('table tbody tr.ant-table-row').first();
  await expect(firstRow).toBeVisible();
  await expect(firstRow).toContainText('AP-');

  // Bấm chuyển sang "Đã đóng" và kiểm tra dòng Withdrawn xuất hiện.
  await page.getByRole('button', { name: 'Đã đóng', exact: true }).click();
  await expect(page.locator('table tbody tr.ant-table-row').filter({ hasText: 'Đã rút' }).first()).toBeVisible();

  // Quay lại "Cần xử lý", mở drawer yêu cầu đầu tiên.
  await page.getByRole('button', { name: 'Cần xử lý', exact: true }).click();
  await firstRow.click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(page.getByText('Tổng quan', { exact: true })).toBeVisible();
  await expect(page.getByText('Lịch hẹn', { exact: true })).toBeVisible();

  // Tabs lịch sử.
  await page.getByRole('tab', { name: /^Lịch sử/ }).click();
  await expect(page.getByText('Tạo lịch', { exact: true })).toBeVisible();

  expect(errors).toEqual([]);
});

test('resolve without prior change is blocked for change/cancel requests', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', e => errors.push(e.message));
  await login(page, 'assistant.demo');
  await page.getByRole('link', { name: 'Hộp thư hỗ trợ', exact: true }).click();
  await page.getByRole('button', { name: 'Chờ staff', exact: true }).click();
  // Mở yêu cầu Change (chưa đổi lịch).
  const changeRow = page.locator('table tbody tr.ant-table-row').filter({ hasText: 'Yêu cầu đổi lịch' }).first();
  await changeRow.click();
  await page.getByRole('tab', { name: 'Xử lý', exact: true }).click();
  // Nút Xử lý yêu cầu phải bị disabled khi chưa đổi lịch.
  const resolveButton = page.getByRole('button', { name: 'Xử lý yêu cầu', exact: true });
  await expect(resolveButton).toBeDisabled();
  // Bấm Đổi lịch → command chạy trực tiếp.
  await page.getByRole('button', { name: 'Đổi lịch', exact: true }).click();
  // Sau khi commit, nút "Đổi lịch" chuyển thành "Đã thay đổi lịch" + resolve bật.
  await expect(page.getByRole('button', { name: 'Đã thay đổi lịch', exact: true })).toBeVisible({ timeout: 10000 });
  // Sau khi commit, nút Xử lý yêu cầu phải bật.
  await expect(resolveButton).toBeEnabled();
  expect(errors).toEqual([]);
});

test('withdrawn request hides resolve and reject actions', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', e => errors.push(e.message));
  await login(page, 'assistant.demo');
  await page.getByRole('link', { name: 'Hộp thư hỗ trợ', exact: true }).click();
  await page.getByRole('button', { name: 'Đã đóng', exact: true }).click();
  // Mở dòng Withdrawn.
  const withdrawnRow = page.locator('table tbody tr.ant-table-row').filter({ hasText: 'Đã rút' }).first();
  await withdrawnRow.click();
  // Banner thông báo xuất hiện ngay ở tab Tổng quan.
  await expect(page.getByText('Yêu cầu đã được rút bởi người gửi. Không thể resolve/reject.')).toBeVisible();
  // Mở tab Xử lý — nút vẫn disabled.
  await page.getByRole('tab', { name: 'Xử lý', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Xử lý yêu cầu', exact: true })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Từ chối', exact: true })).toBeDisabled();
  expect(errors).toEqual([]);
});

test('public account does not see support inbox menu or page', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', e => errors.push(e.message));
  await login(page, 'guest.demo');
  await expect(page.getByRole('link', { name: 'Hộp thư hỗ trợ', exact: true })).toHaveCount(0);
  await page.evaluate(() => { history.pushState(null, '', '/support-inbox'); window.dispatchEvent(new PopStateEvent('popstate')); });
  await expect(page.getByText('Bạn không có quyền thực hiện thao tác này.')).toBeVisible();
  expect(errors).toEqual([]);
});
