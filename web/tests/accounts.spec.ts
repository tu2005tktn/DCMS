import { test, expect, type Page, type APIRequestContext } from '@playwright/test';

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
  await editor.getByLabel('Email', { exact: true }).fill(`${name}@example.invalid`);
  await editor.getByLabel('Vai trò được cấp', { exact: true }).click();
  await page.getByTitle('Nha sĩ', { exact: true }).click();
  await editor.getByLabel('Họ tên', { exact: true }).click();
  await editor.getByRole('button', { name: 'Xác nhận tạo', exact: true }).click();
  await expect(editor).not.toBeVisible();
  const search = page.getByPlaceholder('Tìm mã, tên hoặc liên hệ');
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
  await page.getByRole('button', { name: 'Đăng xuất', exact: true }).click();
  await expect(page).toHaveURL(/sign-in/);
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

async function createManaged(request: APIRequestContext, token: string, suffix: string, roles = ['Dentist']) {
  const response = await request.post('http://localhost:5080/api/accounts', {
    headers: { Authorization: `Bearer ${token}` }, data: {
      userName: `e2e.${suffix}`, employeeCode: `E2E-${suffix}`, fullName: `S04 ${suffix}`,
      password, email: `${suffix}@example.invalid`, phone: null, roles,
    },
  });
  expect(response.status()).toBe(201);
  return (await response.json()).account as { id: number; version: number; userName: string; fullName: string; email: string; phone: string | null; roles: string[]; employeeCode: string };
}

async function searchAccounts(page: Page, term: string) {
  await page.getByPlaceholder('Tìm mã, tên hoặc liên hệ').fill(term);
  const loaded = page.waitForResponse(r => r.url().includes('/api/accounts?') && new URL(r.url()).searchParams.get('search') === term);
  await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click();
  expect((await loaded).status()).toBe(200);
}

test('S04 searches staff code, filters roles/status, sorts on server and opens real detail Drawer', async ({ page, request }) => {
  const session = await login(page);
  const account = await createManaged(request, session.accessToken, `list${Date.now().toString(36)}`, ['ClinicOwner']);
  await page.getByRole('link', { name: 'Quản lý tài khoản', exact: true }).click();
  await searchAccounts(page, account.employeeCode);
  const row = page.getByRole('row').filter({ hasText: account.userName });
  await expect(row).toHaveCount(1); await expect(row).toContainText(account.email);
  await page.getByRole('combobox', { name: 'Lọc vai trò' }).click();
  await page.getByTitle('Chủ phòng khám', { exact: true }).click();
  await expect(row).toHaveCount(1);
  const sorted = page.waitForResponse(r => r.url().includes('/api/accounts?') && new URL(r.url()).searchParams.get('sortBy') === 'fullName');
  await page.getByRole('columnheader', { name: 'Họ tên' }).click();
  expect((await sorted).status()).toBe(200);
  await page.getByRole('heading', { name: 'Quản lý tài khoản' }).click();
  await expect(page.locator('.ant-select-dropdown').filter({ visible: true })).toHaveCount(0);
  await page.screenshot({ path: 'test-results/s04-desktop.png', fullPage: true });
  await row.getByRole('button', { name: 'Chi tiết', exact: true }).click();
  const drawer = page.getByRole('dialog');
  await expect(drawer).toContainText(account.employeeCode);
  await expect(drawer).toContainText(account.email);
  await expect(drawer).toContainText('Phiên bản');
  await expect(drawer.getByRole('button', { name: 'Hỗ trợ khôi phục (S03 chưa sẵn sàng)' })).toBeDisabled();
  await expect(page.locator('.ant-drawer-content-wrapper')).toHaveCSS('transform', 'none');
  await page.screenshot({ path: 'test-results/s04-detail.png', fullPage: true });
  await page.getByRole('button', { name: 'Close', exact: true }).click();
  await page.getByRole('combobox', { name: 'Lọc trạng thái' }).click();
  await page.getByTitle('Đã khóa', { exact: true }).click();
  await expect(row).toHaveCount(0);
  await expect(page.getByText('Không tìm thấy kết quả phù hợp.', { exact: true })).toBeVisible();
  await page.getByRole('heading', { name: 'Quản lý tài khoản' }).click();
  await expect(page.locator('.ant-select-dropdown').filter({ visible: true })).toHaveCount(0);
  await page.setViewportSize({ width: 390, height: 844 });
  await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth)).toBe(390);
  await page.screenshot({ path: 'test-results/s04-small-screen.png', fullPage: true });
});

test('S04 validates contact, rejects duplicate through real API and prevents repeated creation', async ({ page, request }) => {
  const session = await login(page);
  const original = await createManaged(request, session.accessToken, `dup${Date.now().toString(36)}`);
  await page.getByRole('link', { name: 'Quản lý tài khoản', exact: true }).click();
  await page.getByRole('button', { name: 'Tạo nhân viên' }).click();
  const editor = page.getByRole('dialog');
  const suffix = `form${Date.now().toString(36)}`;
  await editor.getByLabel('Tên đăng nhập', { exact: true }).fill(`e2e.${suffix}`);
  await editor.getByLabel('Mã nhân viên', { exact: true }).fill(`E2E-${suffix}`);
  await editor.getByLabel('Mật khẩu ban đầu', { exact: true }).fill(password!);
  await editor.getByLabel('Họ tên', { exact: true }).fill('S04 duplicate validation');
  await editor.getByLabel('Vai trò được cấp', { exact: true }).click();
  await page.getByTitle('Trợ lý phòng khám', { exact: true }).click();
  await editor.getByLabel('Họ tên', { exact: true }).click();
  await editor.getByRole('button', { name: 'Xác nhận tạo', exact: true }).click();
  await expect(editor.getByText('Vui lòng nhập Email hoặc số điện thoại.', { exact: true })).toBeVisible();
  await editor.getByLabel('Email', { exact: true }).fill(original.email.toUpperCase());
  const rejected = page.waitForResponse(r => r.url().endsWith('/api/accounts') && r.request().method() === 'POST');
  await editor.getByRole('button', { name: 'Xác nhận tạo', exact: true }).click();
  expect((await rejected).status()).toBe(409);
  await expect(editor).toContainText('Thông tin tài khoản chưa thể sử dụng. Vui lòng kiểm tra hoặc dùng chức năng khôi phục tài khoản.');
  await editor.getByLabel('Email', { exact: true }).fill(`${suffix}@example.invalid`);
  let submissions = 0;
  page.on('request', req => { if (req.url().endsWith('/api/accounts') && req.method() === 'POST') submissions++; });
  await editor.getByRole('button', { name: 'Xác nhận tạo', exact: true }).evaluate(button => {
    (button as HTMLButtonElement).click(); (button as HTMLButtonElement).click();
  });
  await expect(editor).not.toBeVisible();
  expect(submissions).toBe(1);
});

test('S04 keeps a stale draft, reloads current version and saves after review', async ({ page, request }) => {
  const session = await login(page);
  const account = await createManaged(request, session.accessToken, `stale${Date.now().toString(36)}`);
  await page.getByRole('link', { name: 'Quản lý tài khoản', exact: true }).click();
  await searchAccounts(page, account.userName);
  const row = page.getByRole('row').filter({ hasText: account.userName });
  await row.getByRole('button', { name: 'Sửa', exact: true }).click();
  const editor = page.getByRole('dialog');
  await editor.getByLabel('Họ tên', { exact: true }).fill('Bản nháp đang sửa');
  await editor.getByLabel('Lý do cập nhật', { exact: true }).fill('Rà soát xung đột');
  const changed = await request.put(`http://localhost:5080/api/accounts/${account.id}`, {
    headers: { Authorization: `Bearer ${session.accessToken}` }, data: { ...account, fullName: 'Thay đổi ở phiên khác', reason: 'Concurrent browser test' },
  });
  expect(changed.status()).toBe(200);
  await editor.getByRole('button', { name: 'Xác nhận cập nhật', exact: true }).click();
  await expect(editor).toContainText('Dữ liệu đã được cập nhật bởi thao tác khác. Vui lòng tải lại và kiểm tra trước khi xác nhận.');
  await expect(editor.getByLabel('Họ tên', { exact: true })).toHaveValue('Bản nháp đang sửa');
  await expect(editor.getByRole('button', { name: 'Xác nhận cập nhật', exact: true })).toBeDisabled();
  await editor.getByRole('button', { name: 'Tải phiên bản mới, giữ bản nháp' }).click();
  await expect(editor).toContainText('Thay đổi ở phiên khác');
  await expect(editor.getByLabel('Họ tên', { exact: true })).toHaveValue('Bản nháp đang sửa');
  await editor.getByRole('button', { name: 'Xác nhận cập nhật', exact: true }).click();
  await expect(editor).not.toBeVisible(); await expect(row).toContainText('Bản nháp đang sửa');
});

test('S04 detail retries connection errors and displays permission failure without stale data', async ({ page, request }) => {
  const session = await login(page);
  const account = await createManaged(request, session.accessToken, `detail${Date.now().toString(36)}`);
  await page.getByRole('link', { name: 'Quản lý tài khoản', exact: true }).click();
  await searchAccounts(page, account.userName);
  const target = `**/api/accounts/${account.id}`;
  await page.route(target, route => route.abort('failed'));
  await page.getByRole('row').filter({ hasText: account.userName }).getByRole('button', { name: 'Chi tiết', exact: true }).click();
  const drawer = page.getByRole('dialog');
  await expect(drawer).toContainText('Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.');
  await expect(drawer).not.toContainText(account.email);
  await page.unroute(target);
  await drawer.getByRole('button', { name: 'Thử lại' }).click();
  await expect(drawer).toContainText(account.email);
  await page.getByRole('button', { name: 'Close', exact: true }).click();
  await page.route(target, route => route.fulfill({ status: 403, contentType: 'application/json', body: JSON.stringify({ code: 'MSG06', message: 'Bạn không có quyền thực hiện thao tác này.' }) }));
  await page.getByRole('row').filter({ hasText: account.userName }).getByRole('button', { name: 'Chi tiết', exact: true }).click();
  await expect(drawer).toContainText('Bạn không có quyền thực hiện thao tác này.');
  await expect(drawer).not.toContainText(account.email);
});

test('S04 confirms discarding unsaved changes', async ({ page, request }) => {
  const session = await login(page);
  const account = await createManaged(request, session.accessToken, `draft${Date.now().toString(36)}`);
  await page.getByRole('link', { name: 'Quản lý tài khoản', exact: true }).click();
  await searchAccounts(page, account.userName);
  await page.getByRole('row').filter({ hasText: account.userName }).getByRole('button', { name: 'Sửa', exact: true }).click();
  const editor = page.getByRole('dialog', { name: 'Cập nhật tài khoản nhân viên', exact: true });
  await editor.getByLabel('Họ tên', { exact: true }).fill('Nội dung chưa lưu');
  await page.getByRole('button', { name: 'Hủy', exact: true }).click();
  await expect(page.getByText('Bỏ thay đổi chưa lưu?', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Tiếp tục sửa', exact: true }).click();
  await expect(editor.getByLabel('Họ tên', { exact: true })).toHaveValue('Nội dung chưa lưu');
  await page.getByRole('button', { name: 'Hủy', exact: true }).click();
  await page.getByRole('button', { name: 'Bỏ thay đổi', exact: true }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
});
