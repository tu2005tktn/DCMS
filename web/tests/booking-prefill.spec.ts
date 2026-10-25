import { expect, test, type Page } from '@playwright/test';

async function openBooking(page: Page, phoneNumber: string | null) {
  const user = { id: 7, userName: 'not-a-contact', fullName: 'Synthetic Customer', phoneNumber, email: 'fixture@example.invalid',
    accountType: 'Public', roles: ['Guest'], effectiveRoles: ['Guest'], permissions: [], hasVerifiedPatientLink: false };
  await page.addInitScript(() => sessionStorage.setItem('dcms.customer.accessToken', 'fixture-token'));
  await page.route('**/api/auth/me', async route => {
    await new Promise(resolve => setTimeout(resolve, 150));
    await route.fulfill({ json: user });
  });
  await page.route('**/api/availability/options*', route => route.fulfill({ json: {
    branches: [{ id: 1, name: 'Fixture branch' }], employees: [{ id: 2, branchId: 1, name: 'Fixture dentist', canDentist: true }],
    services: [{ id: 3, branchId: 1, name: 'Khám', publicDurationMinutes: 30 }],
  } }));
  await page.route('**/api/availability?*', route => route.fulfill({ json: { slots: [{ start: '2030-01-07T02:00:00Z', end: '2030-01-07T02:30:00Z',
    chairId: 1, dentistId: 2, scheduleVersion: 1, assistantScheduleVersion: null, bookingSettingsVersion: 1 }] } }));
  await page.goto('/customer/book');
  await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
  await page.locator('.ant-select').nth(0).click();
  await page.locator('.ant-select-dropdown:visible').getByText('Fixture branch', { exact: true }).click();
  await page.locator('.ant-select').nth(1).click();
  await page.locator('.ant-select-dropdown:visible').getByText('Fixture dentist', { exact: true }).click();
  await page.getByRole('button', { name: '09:00–09:30', exact: true }).click();
  await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
  return user;
}

for (const phone of ['0912345678', null]) test(`restored self booking prefills names and ${phone ? 'phone' : 'email fallback'} without overwriting edits`, async ({ page }) => {
  const user = await openBooking(page, phone);
  await expect(page.getByLabel('Họ tên người đặt', { exact: true })).toHaveValue(user.fullName);
  await expect(page.getByLabel('Họ tên người khám', { exact: true })).toHaveValue(user.fullName);
  await expect(page.getByLabel('Số điện thoại Việt Nam hoặc email', { exact: true })).toHaveValue(phone ?? user.email);
  await page.getByLabel('Họ tên người đặt', { exact: true }).fill('Edited booker');
  await page.getByLabel('Số điện thoại Việt Nam hoặc email', { exact: true }).fill('0987654321');
  const refreshed = page.waitForResponse(response => response.url().endsWith('/api/auth/me'));
  await page.evaluate(() => window.dispatchEvent(new Event('focus')));
  await refreshed;
  await expect(page.getByLabel('Họ tên người đặt', { exact: true })).toHaveValue('Edited booker');
  await page.getByRole('button', { name: 'Quay lại chọn lịch', exact: true }).click();
  await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
  await expect(page.getByLabel('Số điện thoại Việt Nam hoặc email', { exact: true })).toHaveValue('0987654321');
});

test('relative keeps account booker/contact, requires another attendee and switching back fills self', async ({ page }) => {
  const user = await openBooking(page, '0912345678');
  await page.getByRole('button', { name: 'Quay lại chọn lịch', exact: true }).click();
  await page.getByRole('button', { name: 'Quay lại', exact: true }).click();
  await page.getByRole('button', { name: /Người thân/ }).click();
  await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
  await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
  await expect(page.getByLabel('Họ tên người đặt', { exact: true })).toHaveValue(user.fullName);
  await expect(page.getByLabel('Họ tên người khám', { exact: true })).toHaveValue('');
  await page.getByRole('button', { name: 'Quay lại chọn lịch', exact: true }).click();
  await page.getByRole('button', { name: 'Quay lại', exact: true }).click();
  await page.getByRole('button', { name: /Bản thân/ }).click();
  await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
  await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
  await expect(page.getByLabel('Họ tên người khám', { exact: true })).toHaveValue(user.fullName);
});
