import { test, expect } from '@playwright/test';
import { staffLogin, customerLogin, password, suffix, phone } from './helpers';

test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });

test('SYS1-01 staff signs in and out', async ({ page }) => {
  await staffLogin(page, 'dentist.demo');
  await expect(page.getByRole('link', { name: 'Bệnh nhân', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Đăng xuất' }).click();
  await expect(page).toHaveURL(/sign-in/);
  await page.goBack();
  await expect(page.getByRole('heading', { name: 'Tài khoản của tôi' })).toHaveCount(0);
});

test('SYS1-02 wrong password is rejected', async ({ page }) => {
  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill('dentist.demo');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('wrong-password-1');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByText('Thông tin đăng nhập không đúng')).toBeVisible();
  await expect(page).toHaveURL(/sign-in/);
});

test('SYS1-03 protected page redirects to sign-in', async ({ page }) => {
  await page.goto('/patients');
  await expect(page).toHaveURL(/sign-in/);
});

test('SYS1-04 menu follows the role', async ({ page }) => {
  await staffLogin(page, 'assistant.demo');
  await expect(page.getByRole('link', { name: 'Quản lý tài khoản', exact: true })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Thống kê tài chính', exact: true })).toHaveCount(0);
  await page.goto('/accounts');
  await expect(page.getByRole('button', { name: 'Tạo nhân viên' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Đăng xuất' }).click();
  await expect(page).toHaveURL(/sign-in/);
  await staffLogin(page, 'owner.demo');
  await expect(page.getByRole('link', { name: 'Thống kê tài chính', exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Quản lý tài khoản', exact: true })).toHaveCount(0);
});

test('SYS1-05 customer registers, signs in', async ({ page }) => {
  const p = phone(); const s = suffix();
  await page.goto('/customer/register');
  await page.getByLabel('Họ tên').fill('Khách Hệ Thống ' + s);
  await page.getByLabel('Số điện thoại').fill(p);
  await page.getByLabel('Email').fill(`sys.${s}@example.com`);
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Password123!');
  await page.getByLabel('Xác nhận mật khẩu').fill('Password123!');
  await page.getByRole('button', { name: 'Đăng ký' }).click();
  await page.waitForTimeout(1500);
  await customerLogin(page, p, 'Password123!');
  await expect(page.getByRole('link', { name: 'Bắt đầu đặt lịch' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Quản lý tài khoản' })).toHaveCount(0);
});

test('SYS1-06 registration with invalid data is blocked', async ({ page }) => {
  await page.goto('/customer/register');
  await page.getByLabel('Số điện thoại').fill('12345');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Password123!');
  await page.getByLabel('Xác nhận mật khẩu').fill('Different123!');
  await page.getByRole('button', { name: 'Đăng ký' }).click();
  await expect(page).toHaveURL(/register/);
  await expect(page.getByRole('link', { name: 'Bắt đầu đặt lịch' })).toHaveCount(0);
  await page.screenshot({ path: 'test-results/sys1-06.png' });
});

test('SYS1-07 staff account is refused on customer portal', async ({ page }) => {
  await customerLogin(page, 'dentist.demo');
  await expect(page.getByText('Đây là tài khoản nhân viên. Vui lòng chọn đăng nhập Nhân viên.')).toBeVisible();
});

test('SYS1-08 admin creates a staff account that can sign in', async ({ page }) => {
  const s = suffix(); const user = `sys.${s}`;
  await staffLogin(page, 'admin.demo');
  await page.getByRole('link', { name: 'Quản lý tài khoản', exact: true }).click();
  await page.getByRole('button', { name: 'Tạo nhân viên' }).click();
  const dlg = page.getByRole('dialog');
  await dlg.getByLabel('Tên đăng nhập').fill(user);
  await dlg.getByLabel('Họ tên').fill('Nhân Viên Hệ Thống');
  await dlg.getByLabel('Mã nhân viên').fill('SYS' + s.slice(0, 6).toUpperCase());
  await dlg.getByLabel('Email').fill(`${user}@example.com`);
  await dlg.getByLabel('Mật khẩu ban đầu').fill('Password123!');
  await dlg.getByLabel('Vai trò được cấp').click();
  await page.locator('.ant-select-item-option').filter({ hasText: 'Trợ lý' }).first().click();
  await dlg.getByRole('button', { name: 'Xác nhận tạo' }).click();
  await page.screenshot({ path: 'test-results/sys1-08-after.png' });
  await expect(dlg).toBeHidden();
  await page.getByRole('button', { name: 'Đăng xuất' }).click();
  await expect(page).toHaveURL(/sign-in/);
  await staffLogin(page, user, 'Password123!');
  await expect(page.getByRole('link', { name: 'Bệnh nhân', exact: true })).toBeVisible();
});

