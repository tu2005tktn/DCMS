import { test, expect, type Page } from '@playwright/test';

const password = process.env.E2E_DEMO_PASSWORD;
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD from ignored .env before running browser tests.'); });
async function login(page: Page, name = 'admin.demo') {
  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill(name);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password!);
  const response = page.waitForResponse(r => r.url().endsWith('/api/auth/sign-in') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  const session = await (await response).json() as { accessToken: string };
  await expect(page.getByRole('heading', { name: 'Tài khoản của tôi' })).toBeVisible();
  return session;
}

test('protected route, invalid login and sign-out invalidate the session', async ({ page, request }) => {
  await page.goto('/accounts');
  await expect(page).toHaveURL(/sign-in/);
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill('admin.demo');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('incorrect');
  const invalidResponse = page.waitForResponse(r => r.url().endsWith('/api/auth/sign-in') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  expect((await invalidResponse).status()).toBe(401);
  await expect(page.getByText('Thông tin đăng nhập không đúng hoặc tài khoản không thể sử dụng. Vui lòng kiểm tra và thử lại.')).toBeVisible();
  const session = await login(page);
  await page.getByRole('button', { name: 'Đăng xuất' }).click();
  await expect(page).toHaveURL(/sign-in/);
  const oldSession = await request.get('http://localhost:5080/api/auth/me', { headers: { Authorization: `Bearer ${session.accessToken}` } });
  expect(oldSession.status()).toBe(401);
  expect((await oldSession.json()).code).toBe('MSG07');
});

test('admin creates, edits roles, locks, unlocks, searches and reads audit through real APIs', async ({ page }) => {
  await login(page);
  await page.getByRole('link', { name: 'Quản lý tài khoản', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Quản lý tài khoản' })).toBeVisible();
  const suffix = Date.now().toString(36);
  const name = `e2e.${suffix}`;
  await page.getByRole('button', { name: 'Tạo nhân viên' }).click();
  const editor = page.getByRole('dialog');
  await editor.getByLabel('Tên đăng nhập', { exact: true }).fill(name);
  await editor.getByLabel('Mã nhân viên', { exact: true }).fill(`E2E-${suffix}`);
  await editor.getByLabel('Mật khẩu ban đầu', { exact: true }).fill(password!);
  await editor.getByLabel('Họ tên', { exact: true }).fill('Nhân viên E2E');
  await editor.getByLabel('Vai trò được cấp', { exact: true }).click();
  await page.getByTitle('Nha sĩ', { exact: true }).click();
  await editor.getByLabel('Họ tên', { exact: true }).click();
  await editor.getByRole('button', { name: 'Xác nhận tạo', exact: true }).click();
  await expect(editor).not.toBeVisible();
  const search = page.getByPlaceholder('Tìm tên đăng nhập, họ tên hoặc email');
  await search.fill(name); await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click();
  const row = page.getByRole('row').filter({ hasText: name });
  await expect(row).toHaveCount(1);
  await row.getByRole('button', { name: 'Sửa', exact: true }).click();
  await editor.getByLabel('Họ tên', { exact: true }).fill('Nhân viên E2E đã cập nhật');
  await editor.getByLabel('Vai trò được cấp', { exact: true }).click();
  await page.getByTitle('Trợ lý phòng khám', { exact: true }).click();
  await editor.getByLabel('Lý do cập nhật', { exact: true }).fill('Kiểm thử UI nhiều vai trò');
  await editor.getByRole('button', { name: 'Xác nhận cập nhật' }).click();
  await expect(editor).not.toBeVisible();
  await expect(row).toContainText('Nhân viên E2E đã cập nhật');
  await expect(row).toContainText('Trợ lý phòng khám');
  await row.getByRole('button', { name: 'Khóa', exact: true }).click();
  await editor.getByLabel('Lý do', { exact: true }).fill('Kiểm thử UI khóa');
  await editor.getByRole('button', { name: 'Xác nhận', exact: true }).click();
  await expect(editor).not.toBeVisible(); await expect(row).toContainText('Đã khóa');
  await row.getByRole('button', { name: 'Mở khóa', exact: true }).click();
  await editor.getByLabel('Lý do', { exact: true }).fill('Kiểm thử UI mở khóa');
  await editor.getByRole('button', { name: 'Xác nhận', exact: true }).click();
  await expect(editor).not.toBeVisible(); await expect(row).toContainText('Hoạt động');
  await row.getByRole('button', { name: 'Audit', exact: true }).click();
  await expect(page.getByRole('dialog')).toContainText('Kiểm thử UI nhiều vai trò');
  await page.getByRole('button', { name: 'Close', exact: true }).click();
  await search.fill(`does-not-exist-${suffix}`); await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click();
  await expect(page.getByText('Không tìm thấy kết quả phù hợp.', { exact: true })).toBeVisible();
});

test('Clinic Owner receives Dentist capabilities without management rights', async ({ page }) => {
  await login(page, 'owner.demo');
  await expect(page.getByRole('link', { name: 'Quản lý tài khoản', exact: true })).toHaveCount(0);
  await expect(page.getByText('Nha sĩ', { exact: true })).toBeVisible();
  await page.evaluate(() => { history.pushState({}, '', '/accounts'); window.dispatchEvent(new PopStateEvent('popstate')); });
  await expect(page.getByText('Bạn không có quyền thực hiện thao tác này.', { exact: true })).toBeVisible();
});

test('account list shows loading and service error, then retries the real API', async ({ page }) => {
  await login(page);
  let release!: () => void;
  const pending = new Promise<void>(resolve => { release = resolve; });
  await page.route('**/api/accounts?*', async route => {
    await pending;
    await route.fulfill({ status: 503, contentType: 'application/json', body: JSON.stringify({
      code: 'MSG144', message: 'Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.',
    }) });
  });
  await page.getByRole('link', { name: 'Quản lý tài khoản', exact: true }).click();
  await expect(page.locator('.ant-spin-spinning')).toBeVisible();
  release();
  await expect(page.getByText('Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.', { exact: true })).toBeVisible();
  await page.unroute('**/api/accounts?*');
  await page.getByRole('button', { name: 'Tải lại', exact: true }).click();
  await expect(page.getByRole('row').filter({ hasText: 'admin.demo' })).toHaveCount(1);
});
