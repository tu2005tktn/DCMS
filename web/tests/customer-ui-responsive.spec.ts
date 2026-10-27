import { expect, test, type Page } from '@playwright/test';

const json = (body: unknown) => ({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
const user = { id: 21, userName: '0912345678', fullName: 'Nguyễn Văn A', accountType: 'Public',
  roles: ['Guest'], effectiveRoles: ['Guest'], permissions: [], hasVerifiedPatientLink: false };

async function expectNoHorizontalOverflow(page: Page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBeTruthy();
}

// 320 CSS px also exercises the reflow expected around 200% browser zoom on a 640 px window.
for (const width of [320, 390, 768, 1440]) {
  test(`patient portal remains usable at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    await page.route('**/api/auth/google/config', route => route.fulfill(json({ enabled: false })));
    await page.route('**/api/auth/policy', route => route.fulfill(json({ requiredLength: 12, maximumLength: 256,
      requireDigit: true, requireLowercase: true, requireUppercase: true, requireNonAlphanumeric: true })));
    await page.route('**/api/auth/me', route => route.fulfill(json(user)));
    await page.route('**/api/availability/options', route => route.fulfill(json({
      branches: [{ id: 1, name: 'Chi nhánh Trung tâm' }],
      services: [{ id: 3, branchId: 1, name: 'Khám tổng quát', publicDurationMinutes: 30 }],
      employees: [{ id: 2, branchId: 1, name: 'Bác sĩ Nguyễn', canDentist: true }],
    })));
    await page.route('**/api/availability?*', route => route.fulfill(json({ slots: [{
      start: '2026-10-23T09:00:00+07:00', end: '2026-10-23T09:30:00+07:00', chairId: 1, dentistId: 2,
      scheduleVersion: 1, assistantScheduleVersion: null,
    }] })));
    await page.route('**/api/me/appointments?*', route => route.fulfill(json({ items: [], total: 0, page: 1, pageSize: 20 })));
    await page.route('**/api/patient-links/me', route => route.fulfill(json({ account: {
      id: 21, userName: '0912345678', fullName: 'Nguyễn Văn A', verifiedEmail: null, verifiedPhone: null,
    }, linkStatus: 'NOT_LINKED', current: null, history: [], canSubmit: true })));

    for (const path of ['/access', '/customer/sign-in', '/customer/register', '/customer/recover']) {
      await page.goto(path);
      await expectNoHorizontalOverflow(page);
      if (width === 390) await page.screenshot({ path: test.info().outputPath(`${path.replaceAll('/', '-') || 'root'}-390.png`), fullPage: true });
      if (path === '/access') {
        await page.keyboard.press('Tab');
        await expect(page.getByRole('link', { name: 'Khách hàng', exact: true })).toBeFocused();
        expect(await page.getByRole('link', { name: 'Khách hàng', exact: true }).evaluate(element =>
          getComputedStyle(element).outlineStyle)).not.toBe('none');
      }
    }

    await page.addInitScript(() => sessionStorage.setItem('dcms.customer.accessToken', 'ui-test-token'));
    await page.goto('/customer/book');
    await page.getByRole('button', { name: 'Tiếp tục' }).click();
    await page.locator('.ant-select').nth(0).click();
    await page.getByText('Chi nhánh Trung tâm').last().click();
    await page.locator('.ant-select').nth(1).click();
    await page.getByText(/Khám tổng quát/).last().click();
    await page.locator('.ant-select').nth(2).click();
    await page.getByText('Bác sĩ Nguyễn').last().click();
    await page.getByRole('heading', { name: 'Chọn nơi khám và khung giờ' }).click();
    await page.keyboard.press('Escape');
    await expect(page.locator('.ant-select-dropdown:visible')).toHaveCount(0);
    await expect(page.getByRole('group', { name: 'Khung giờ buổi sáng' }).getByRole('button')).toBeVisible();
    await expectNoHorizontalOverflow(page);
    await page.screenshot({ path: test.info().outputPath(`booking-${width}.png`), fullPage: true });

    for (const path of ['/customer/home', '/customer/appointments', '/customer/patient-link']) {
      await page.goto(path);
      if (path === '/customer/patient-link') await expect(page.getByText('Chưa liên kết', { exact: true })).toBeVisible();
      await expectNoHorizontalOverflow(page);
      if (path === '/customer/home') await page.screenshot({ path: test.info().outputPath(`home-${width}.png`), fullPage: true });
      if (width === 390 && path !== '/customer/home') await page.screenshot({ path: test.info().outputPath(`${path.replaceAll('/', '-')}-390.png`), fullPage: true });
    }
    await expect(page.getByRole('heading', { name: 'Liên kết hồ sơ của tôi' })).toBeVisible();
    if (width === 768) {
      await page.emulateMedia({ reducedMotion: 'reduce' });
      await page.goto('/customer/recover');
      expect(await page.locator('.recovery-fade-in').first().evaluate(element => getComputedStyle(element).animationDuration)).toBe('0s');
    }
  });
}

test('customer recovery completes through its own four-step UI without a real email or database', async ({ page }) => {
  const cors = { 'Access-Control-Allow-Origin': 'http://localhost:5173',
    'Access-Control-Allow-Headers': 'content-type', 'Access-Control-Allow-Methods': 'POST, OPTIONS' };
  await page.route('**/api/auth/policy', route => route.fulfill(json({ requiredLength: 12, maximumLength: 256,
    requireDigit: true, requireLowercase: true, requireUppercase: true, requireNonAlphanumeric: true })));
  await page.route('**/api/customer/recovery/*', route => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: cors });
    const path = new URL(route.request().url()).pathname;
    const response = path.endsWith('/verify') ? { resetGrant: 'synthetic-grant' }
      : path.endsWith('/reset') ? { code: 'MSG13', message: 'Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.' }
        : { code: 'MSG12', message: 'Nếu thông tin tài khoản hợp lệ, hướng dẫn đặt lại mật khẩu sẽ được gửi qua kênh liên hệ đã đăng ký.' };
    return route.fulfill({ ...json(response), headers: cors });
  });
  await page.goto('/customer/recover');
  await page.getByLabel('Email', { exact: true }).fill('synthetic@example.invalid');
  await page.getByRole('button', { name: 'Gửi yêu cầu', exact: true }).click();
  await expect(page.getByText('Bước 2/4 · Xác minh mã')).toBeVisible();
  await page.getByLabel('Mã xác minh', { exact: true }).fill('123456');
  await page.getByRole('button', { name: 'Xác minh', exact: true }).click();
  await expect(page.getByText('Bước 3/4 · Đặt mật khẩu mới')).toBeVisible();
  await page.getByLabel('Mật khẩu mới', { exact: true }).fill('SyntheticPassword123!');
  await page.getByLabel('Xác nhận mật khẩu', { exact: true }).fill('SyntheticPassword123!');
  await page.getByRole('button', { name: 'Đặt lại mật khẩu', exact: true }).click();
  await expect(page.getByText('Bước 4/4 · Hoàn tất')).toBeVisible();
  await page.getByRole('button', { name: 'Về trang đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/\/customer\/sign-in$/);
});

test('patient link shows pending request before the form and offers a clear withdrawal action', async ({ page }) => {
  await page.addInitScript(() => sessionStorage.setItem('dcms.customer.accessToken', 'ui-test-token'));
  await page.route('**/api/auth/me', route => route.fulfill(json(user)));
  await page.route('**/api/patient-links/me', route => route.fulfill(json({
    account: { id: 21, userName: '0912345678', fullName: 'Nguyễn Văn A', verifiedEmail: null, verifiedPhone: null },
    linkStatus: 'PENDING', canSubmit: false, history: [], current: {
      id: 4, requestCode: 'LK-SYNTHETIC', status: 'PENDING', requestedAt: '2026-10-19T09:00:00+07:00',
      decidedAt: null, publicNote: null, version: 1, result: null,
    },
  })));
  await page.goto('/customer/patient-link');
  await expect(page.getByText('Đang chờ xác minh', { exact: true })).toBeVisible();
  await expect(page.getByText('Phòng khám đang kiểm tra yêu cầu; bạn chưa có quyền xem hồ sơ.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Rút yêu cầu' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Gửi yêu cầu liên kết' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Xem hồ sơ của tôi' })).toHaveCount(0);
});
