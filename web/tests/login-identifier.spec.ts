import { expect, test, type Page } from '@playwright/test';

const customer = { id: 21, userName: 'synthetic.patient', fullName: 'Khách thử nghiệm', accountType: 'Public', roles: ['Guest'], effectiveRoles: ['Guest'], permissions: [], hasVerifiedPatientLink: false };
async function mock(page: Page, fail = false) {
  const commands: { userName: string; password: string }[] = [];
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/auth/sign-in') {
      commands.push(route.request().postDataJSON());
      if (fail) { fail = false; return route.fulfill({ status: 401, json: { code: 'MSG09', message: 'Thông tin đăng nhập không đúng.' } }); }
      return route.fulfill({ json: { accessToken: 'synthetic-customer-token', user: customer } });
    }
    if (path === '/api/auth/me') return route.fulfill({ json: customer });
    if (path === '/api/auth/google/config') return route.fulfill({ json: { enabled: false } });
    return route.fulfill({ json: {} });
  });
  return commands;
}

for (const identifier of ['patient@example.invalid', `${'e'.repeat(70)}@example.invalid`, 'patient.' + 'x'.repeat(100), '0912345678', 'a'])
  test(`cổng bệnh nhân nhận định danh ${identifier.includes('@') ? 'email' : 'username'} dài ${identifier.length}`, async ({ page }) => {
    const commands = await mock(page); await page.goto('/customer/sign-in');
    const input = page.getByLabel('Email hoặc tên đăng nhập', { exact: true });
    expect(await input.getAttribute('maxlength')).toBeNull(); expect(await input.getAttribute('inputmode')).not.toBe('numeric');
    await input.fill('  ' + identifier + '  '); await page.getByLabel('Mật khẩu', { exact: true }).fill('Synthetic-password-123!');
    await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click(); await expect(page).toHaveURL(/\/customer\/home$/);
    expect(commands).toHaveLength(1); expect(commands[0].userName).toBe(identifier);
  });

test('bắt buộc định danh không trắng và giữ định danh sau lỗi API để thử lại', async ({ page }) => {
  const commands = await mock(page, true); await page.goto('/customer/sign-in');
  const input = page.getByLabel('Email hoặc tên đăng nhập', { exact: true });
  await input.fill('   '); await page.getByLabel('Mật khẩu', { exact: true }).fill('Synthetic-password-123!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.locator('#customer-sign-in-errors')).toBeFocused(); expect(commands).toHaveLength(0);
  await input.fill('patient@example.invalid'); await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByText('Thông tin đăng nhập không đúng.', { exact: true })).toBeVisible(); await expect(input).toHaveValue('patient@example.invalid');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click(); await expect(page).toHaveURL(/\/customer\/home$/);
  expect(commands).toHaveLength(2);
});

test('form nhân viên cũng không cắt tên đăng nhập quá 64 ký tự', async ({ page }) => {
  const commands = await mock(page, true); await page.goto('/sign-in');
  const input = page.getByLabel('Tên đăng nhập', { exact: true }), name = 'staff.' + 'x'.repeat(100);
  expect(await input.getAttribute('maxlength')).toBeNull(); await input.fill(name);
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Synthetic-password-123!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByText('Thông tin đăng nhập không đúng.', { exact: true })).toBeVisible(); expect(commands[0].userName).toBe(name);
});
