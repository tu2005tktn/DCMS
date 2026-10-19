import { expect, test, type Page } from '@playwright/test';

const publicUser = { id: 21, userName: '0912345678', fullName: 'Nguyễn Văn A', accountType: 'Public',
  roles: ['Guest'], effectiveRoles: ['Guest'], permissions: [], hasVerifiedPatientLink: false };
const staffUser = { ...publicUser, id: 2, userName: 'staff.demo', fullName: 'Nhân viên Demo', accountType: 'Staff',
  roles: ['ClinicAssistant'], effectiveRoles: ['ClinicAssistant'] };
const json = (body: unknown, status = 200) => ({ status, contentType: 'application/json', body: JSON.stringify(body) });

async function mockCommon(page: Page) {
  await page.route('**/api/auth/google/config', route => route.fulfill(json({ enabled: false, clientId: null })));
  await page.route('**/api/auth/me', route => route.fulfill(json(publicUser)));
  await page.route('**/api/auth/sign-out', route => route.fulfill({ status: 204 }));
}

test('entry offers separate customer and unchanged staff login', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveURL(/\/access$/);
  await expect(page.getByRole('link', { name: 'Khách hàng', exact: true })).toHaveAttribute('href', '/customer/sign-in');
  await page.getByRole('link', { name: 'Nhân viên', exact: true }).click();
  await expect(page).toHaveURL(/\/sign-in$/);
  await expect(page.getByLabel('Tên đăng nhập', { exact: true })).toBeVisible();
  await expect(page.getByLabel('Mật khẩu', { exact: true })).toBeVisible();
});

test('staff sign-in and sign-out still use existing routes', async ({ page }) => {
  await mockCommon(page);
  await page.route('**/api/auth/sign-in', route => route.fulfill(json({ accessToken: 'staff-token', user: staffUser })));
  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill('staff.demo');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Password123!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL('http://localhost:5173/');
  await expect(page.getByRole('heading', { name: 'Tài khoản của tôi' })).toBeVisible();
  await page.getByRole('button', { name: 'Đăng xuất' }).click();
  await expect(page).toHaveURL(/\/sign-in$/);
});

test('customer registers, signs in, and reaches the separate home', async ({ page }) => {
  await mockCommon(page);
  await page.route('**/api/auth/policy', route => route.fulfill(json({ requiredLength: 12, maximumLength: 256,
    requireDigit: true, requireLowercase: true, requireUppercase: true, requireNonAlphanumeric: true })));
  let registration: Record<string, unknown> | null = null;
  await page.route('**/api/auth/register', async route => {
    registration = route.request().postDataJSON() as Record<string, unknown>;
    await route.fulfill(json({ accountId: 21, phoneNumber: '0912345678', notification: { code: 'MSG11', message: 'Đăng ký thành công.' } }, 201));
  });
  await page.route('**/api/auth/sign-in', route => route.fulfill(json({ accessToken: 'public-token', user: publicUser })));
  await page.goto('/customer/register');
  await page.getByLabel('Họ tên').fill('Nguyễn Văn A');
  await page.getByLabel('Số điện thoại').fill('0912345678');
  await page.getByLabel('Email').fill('a@example.com');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Password123!');
  await page.getByLabel('Xác nhận mật khẩu').fill('Password123!');
  await page.getByRole('button', { name: 'Đăng ký' }).click();
  await expect(page).toHaveURL(/\/customer\/sign-in$/);
  expect(registration?.phoneNumber).toBe('0912345678');
  await page.getByLabel('Số điện thoại').fill('0912345678');
  await page.getByLabel('Mật khẩu').fill('Password123!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/\/customer\/home$/);
  await expect(page.getByRole('link', { name: 'Bắt đầu đặt lịch' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Liên kết hồ sơ của tôi' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Quản lý tài khoản' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Đăng xuất' }).click();
  await expect(page).toHaveURL(/\/customer\/sign-in$/);
});

test('customer portal rejects a staff account without storing its token', async ({ page }) => {
  await mockCommon(page);
  await page.route('**/api/auth/sign-in', route => route.fulfill(json({ accessToken: 'staff-token', user: staffUser })));
  await page.goto('/customer/sign-in');
  await page.getByLabel('Số điện thoại').fill('0912345678');
  await page.getByLabel('Mật khẩu').fill('Password123!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByText('Đây là tài khoản nhân viên. Vui lòng chọn đăng nhập Nhân viên.')).toBeVisible();
  expect(await page.evaluate(() => sessionStorage.getItem('dcms.customer.accessToken'))).toBeNull();
});

test('stored customer session returns to customer home after reload', async ({ page }) => {
  await page.addInitScript(() => sessionStorage.setItem('dcms.customer.accessToken', 'public-token'));
  await page.route('**/api/auth/me', route => route.fulfill(json(publicUser)));
  await page.goto('/');
  await expect(page).toHaveURL(/\/customer\/home$/);
  await expect(page.getByRole('heading', { name: 'Xin chào, Nguyễn Văn A' })).toBeVisible();
  await page.reload();
  await expect(page).toHaveURL(/\/customer\/home$/);
});

test('Google credential signs a Public customer in through the customer session', async ({ page }) => {
  await page.route('**/api/auth/google/config', route => route.fulfill(json({ enabled: true, clientId: 'web-client-id' })));
  await page.route('https://accounts.google.com/gsi/client', route => route.fulfill({ contentType: 'application/javascript', body: `
    window.google = { accounts: { id: {
      initialize: function (options) { window.googleCallback = options.callback; },
      renderButton: function (element) {
        var button = document.createElement('button'); button.textContent = 'Mock Google';
        button.onclick = function () { window.googleCallback({ credential: 'google-id-token' }); };
        element.appendChild(button);
      }
    } } };
  ` }));
  await page.route('**/api/auth/google/sign-in', route => route.fulfill(json({ accessToken: 'google-token', user: publicUser })));
  await page.route('**/api/auth/me', route => route.fulfill(json(publicUser)));
  await page.goto('/customer/sign-in');
  await page.getByRole('button', { name: 'Mock Google' }).click();
  await expect(page).toHaveURL(/\/customer\/home$/);
  expect(await page.evaluate(() => sessionStorage.getItem('dcms.customer.accessToken'))).toBe('google-token');
});

test('guest booking retains form after a slot conflict and confirms another slot', async ({ page }) => {
  await mockCommon(page);
  const start = new Date(Date.now() + 3 * 86_400_000).toISOString();
  const second = new Date(Date.now() + 3 * 86_400_000 + 3_600_000).toISOString();
  await page.route('**/api/availability/options', route => route.fulfill(json({
    branches: [{ id: 1, name: 'Chi nhánh A' }],
    employees: [{ id: 2, branchId: 1, name: 'Bác sĩ B', canDentist: true }],
    services: [{ id: 3, branchId: 1, name: 'Khám tổng quát', publicDurationMinutes: 30 }],
  })));
  await page.route('**/api/availability?*', route => route.fulfill(json({ timeZone: 'Asia/Ho_Chi_Minh', date: '2026-10-20',
    slots: [{ start, end: start, chairId: 4, dentistId: 2, scheduleVersion: 1, assistantScheduleVersion: null },
      { start: second, end: second, chairId: 4, dentistId: 2, scheduleVersion: 1, assistantScheduleVersion: null }] })));
  let calls = 0;
  const requestKeys: string[] = [];
  await page.route('**/api/appointments', route => {
    calls += 1;
    requestKeys.push((route.request().postDataJSON() as { requestKey: string }).requestKey);
    if (calls === 1) return route.fulfill(json({ code: 'MSG25', message: 'Khung giờ không còn khả dụng.' }, 409));
    return route.fulfill(json({ appointmentId: 'id-1', code: 'AP-123', version: 1, status: 'Confirmed',
      startAt: second, endAt: second, notification: { code: 'MSG29', message: 'Đặt lịch thành công.' } }, 201));
  });
  await page.goto('/customer/book');
  await page.getByRole('button', { name: 'Tiếp tục' }).click();
  await expect(page.locator('.customer-booking-progress')).toContainText('Bước 2/5');
  await page.locator('.ant-select').nth(0).click();
  await page.getByText('Chi nhánh A').last().click();
  await expect(page.getByLabel('Dịch vụ', { exact: true })).toHaveValue('Khám · 30 phút');
  await page.locator('.ant-select').nth(1).click();
  await page.getByText('Bác sĩ B').last().click();
  await page.locator('.customer-slots button').first().click();
  await expect(page.getByRole('complementary', { name: 'Lịch đã chọn' })).toContainText('Giờ khám');
  await expect(page.getByRole('complementary', { name: 'Lịch đã chọn' })).not.toContainText('Chưa chọn giờ');
  await page.getByRole('button', { name: 'Tiếp tục' }).click();
  await expect(page.getByRole('group', { name: 'Người đặt lịch' })).toBeVisible();
  await expect(page.getByRole('group', { name: 'Người đến khám' })).toBeVisible();
  await page.getByLabel('Họ tên người đặt').fill('Khách A');
  await page.getByLabel('Số điện thoại Việt Nam hoặc email').fill('0912345678');
  await page.getByLabel('Họ tên người khám').fill('Người thân B');
  await page.getByRole('button', { name: 'Xem lại' }).click();
  await page.getByRole('button', { name: 'Xác nhận đặt lịch' }).click();
  await expect(page.getByText('MSG25: Khung giờ không còn khả dụng.')).toBeVisible();
  await page.locator('.customer-slots button').last().click();
  await page.getByRole('button', { name: 'Tiếp tục' }).click();
  await expect(page.getByLabel('Họ tên người đặt')).toHaveValue('Khách A');
  await page.getByRole('button', { name: 'Xem lại' }).click();
  await page.getByRole('button', { name: 'Xác nhận đặt lịch' }).click();
  await expect(page.getByText('AP-123')).toBeVisible();
  await expect(page.getByText('Trạng thái: Đã xác nhận')).toBeVisible();
  expect(calls).toBe(2);
  expect(requestKeys[0]).not.toBe(requestKeys[1]);
});

test('verified customer sees only own linked record', async ({ page }) => {
  await mockCommon(page);
  await page.route('**/api/auth/sign-in', route => route.fulfill(json({ accessToken: 'public-token', user: publicUser })));
  await page.route('**/api/patient-links/me', route => route.fulfill(json({
    account: { id: 21, userName: '0912345678', fullName: 'Nguyễn Văn A', verifiedEmail: null, verifiedPhone: null },
    linkStatus: 'VERIFIED', current: null, history: [], canSubmit: false,
  })));
  await page.route('**/api/patient-links/me/record', route => route.fulfill(json({
    patientCode: 'PAT-1', fullName: 'Nguyễn Văn A', birthDate: '1990-01-01', linkedAt: '2026-10-16T00:00:00Z',
  })));
  await page.goto('/customer/sign-in');
  await page.getByLabel('Số điện thoại').fill('0912345678');
  await page.getByLabel('Mật khẩu').fill('Password123!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await page.getByRole('link', { name: 'Liên kết hồ sơ của tôi' }).click();
  await page.getByRole('button', { name: 'Xem hồ sơ của tôi' }).click();
  await expect(page.getByText('PAT-1')).toBeVisible();
});
