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

test('assistant creates, searches, edits, archives, restores and reads audit using actual APIs', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', e => errors.push(e.message));
  await login(page);
  await page.getByRole('link', { name: 'Bệnh nhân', exact: true }).click();
  await page.getByRole('button', { name: 'Thêm hồ sơ', exact: true }).click();
  const form = page.getByRole('dialog');
  await form.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(form.getByText('Vui lòng nhập Họ tên.', { exact: true })).toBeVisible();
  const suffix = Date.now().toString(36);
  const name = `SYNTHETIC Đặng Ánh ${suffix}`;
  await form.getByLabel('Họ tên', { exact: true }).fill(name);
  await form.getByLabel('Ngày sinh', { exact: true }).fill('1990-05-06');
  const createResponse = page.waitForResponse(r => /\/api\/patients\/?$/.test(r.url()) && r.request().method() === 'POST');
  await form.getByRole('button', { name: 'Lưu', exact: true }).click();
  const created = await createResponse;
  expect(created.status()).toBe(201);
  const { patient } = await created.json();
  await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();
  await expect(page.locator('.ant-menu-item-selected')).toContainText('Bệnh nhân');
  await page.getByRole('button', { name: 'Sửa thông tin', exact: true }).click();
  await form.getByLabel('Địa chỉ', { exact: true }).fill('SYNTHETIC updated address');
  await form.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(form).not.toBeVisible();
  await expect(page.getByText('SYNTHETIC updated address', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Ngừng sử dụng', exact: true }).click();
  await form.getByLabel('Lý do', { exact: true }).fill('SYNTHETIC archive');
  await form.getByRole('button', { name: 'Ngừng sử dụng', exact: true }).click();
  await expect(page.getByText('Hồ sơ đã ngừng sử dụng', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Sửa thông tin', exact: true })).toBeDisabled();
  await page.getByRole('link', { name: '← Danh sách bệnh nhân', exact: true }).click();
  await page.getByPlaceholder('Mã bệnh nhân, họ tên (có hoặc không dấu), SĐT').fill(`dang anh ${suffix}`);
  await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click();
  await expect(page.getByText('Không tìm thấy hồ sơ bệnh nhân phù hợp.', { exact: true })).toBeVisible();
  await page.getByRole('checkbox', { name: 'Gồm hồ sơ ngừng sử dụng' }).check();
  await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click();
  const row = page.getByRole('row').filter({ hasText: name });
  await expect(row).toHaveCount(1);
  await row.getByRole('button', { name: 'Mở hồ sơ', exact: true }).click();
  await page.getByRole('button', { name: 'Khôi phục hồ sơ', exact: true }).click();
  await form.getByLabel('Lý do', { exact: true }).fill('SYNTHETIC restore');
  await form.getByRole('button', { name: 'Khôi phục', exact: true }).click();
  await expect(form).not.toBeVisible();
  await expect(page.getByRole('button', { name: 'Sửa thông tin', exact: true })).toBeEnabled();
  await page.getByRole('tab', { name: 'Lịch sử thay đổi', exact: true }).click();
  for (const action of ['Tạo hồ sơ', 'Cập nhật hành chính', 'Ngừng sử dụng', 'Khôi phục'])
    await expect(page.locator('.audit-entry strong').filter({ hasText: new RegExp(`^${action}$`) })).toBeVisible();
  await page.screenshot({ path: '../.artifacts/test-results/trinm/patient-audit.png', fullPage: true });
  await page.getByRole('tab', { name: 'Điều trị & Thanh toán', exact: true }).click();
  await expect(page.getByText('Chức năng chưa triển khai', { exact: true })).toBeVisible();
  expect(patient.id).toBeGreaterThan(0);
  expect(errors).toEqual([]);
});

test('duplicate warning requires explicit review and creates a separate patient', async ({ page, request }) => {
  const signedIn = await request.post('http://localhost:5080/api/auth/sign-in', { data: { userName: 'assistant.demo', password } });
  const session = await signedIn.json();
  const name = `SYNTHETIC Duplicate ${Date.now()}`;
  const seed = await request.post('http://localhost:5080/api/patients', {
    headers: { Authorization: `Bearer ${session.accessToken}` }, data: { fullName: name, duplicatesReviewed: true },
  });
  expect(seed.status()).toBe(201);
  const original = (await seed.json()).patient;
  await login(page);
  await page.getByRole('link', { name: 'Bệnh nhân', exact: true }).click();
  await page.getByRole('button', { name: 'Thêm hồ sơ', exact: true }).click();
  const form = page.getByRole('dialog');
  await form.getByLabel('Họ tên', { exact: true }).fill(name);
  await form.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(form.getByText('Có hồ sơ nghi trùng', { exact: true })).toBeVisible();
  await expect(form.getByRole('button', { name: /Lưu$/ })).toBeDisabled();
  await form.getByRole('checkbox', { name: 'Tôi đã đối chiếu, đây là người khác với các hồ sơ trên' }).check();
  const createdResponse = page.waitForResponse(r => /\/api\/patients\/?$/.test(r.url()) && r.request().method() === 'POST');
  await form.getByRole('button', { name: /Lưu$/ }).click();
  const response = await createdResponse;
  expect(response.status()).toBe(201);
  expect((await response.json()).patient.id).not.toBe(original.id);
  await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();
});

test('dentist can read patients but has no administrative edit actions', async ({ page }) => {
  await login(page, 'dentist.demo');
  await page.getByRole('link', { name: 'Bệnh nhân', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Thêm hồ sơ', exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Mở hồ sơ', exact: true }).first().click();
  await expect(page.getByRole('tab', { name: 'Thông tin cơ bản', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Sửa thông tin', exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Ngừng sử dụng', exact: true })).toHaveCount(0);
});

test('admin cannot enter patient routes', async ({ page }) => {
  await login(page, 'admin.demo');
  await expect(page.getByRole('link', { name: 'Bệnh nhân', exact: true })).toHaveCount(0);
  await page.evaluate(() => { history.pushState(null, '', '/patients'); window.dispatchEvent(new PopStateEvent('popstate')); });
  await expect(page.getByText('Bạn không có quyền thực hiện thao tác này.', { exact: true })).toBeVisible();
});

test('load failure is distinct from empty results and retry recovers', async ({ page }) => {
  await login(page);
  await page.route('**/api/patients?*', route => route.fulfill({ status: 503, contentType: 'application/json', body: JSON.stringify({ code: 'MSG144', message: 'SYNTHETIC unavailable' }) }));
  await page.getByRole('link', { name: 'Bệnh nhân', exact: true }).click();
  await expect(page.getByText('SYNTHETIC unavailable', { exact: true })).toBeVisible();
  await expect(page.getByText('Không tìm thấy hồ sơ bệnh nhân phù hợp.', { exact: true })).toHaveCount(0);
  await page.unroute('**/api/patients?*');
  await page.getByRole('button', { name: 'Thử lại', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Mở hồ sơ', exact: true }).first()).toBeVisible();
});
