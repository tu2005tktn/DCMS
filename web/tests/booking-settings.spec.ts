import { expect, test } from '@playwright/test';

test('dentist edits slot interval and distinct service durations, with separate new-visit visibility', async ({ page }) => {
  let settings = { branchId: 1, slotIntervalMinutes: 15, version: 1,
    services: [{ id: 1, name: 'Khám', durationMinutes: 30, active: true, publicBookable: true, isInitialExamination: true }] };
  await page.addInitScript(() => sessionStorage.setItem('dcms.web.access-token', 'fixture'));
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    let body: unknown = {};
    if (path === '/api/auth/me') body = { id: 2, fullName: 'Dentist fixture', userName: 'fixture', accountType: 'Staff',
      roles: ['Dentist'], effectiveRoles: ['Dentist'], permissions: ['patients.read'] };
    else if (path === '/api/booking-settings/options') body = [{ id: 1, name: 'Branch fixture' }];
    else if (path === '/api/booking-settings/1') {
      if (route.request().method() === 'PUT') {
        const command = route.request().postDataJSON();
        expect(command.expectedVersion).toBe(1); expect(command.requestKey).toBeTruthy();
        expect(command.slotIntervalMinutes).toBe(20);
        expect(command.services.map((s: { durationMinutes: number }) => s.durationMinutes)).toEqual([35, 95]);
        expect(command.services[1].publicBookable).toBe(false);
        settings = { branchId: 1, version: 2, slotIntervalMinutes: command.slotIntervalMinutes,
          services: command.services.map((s: object, i: number) => ({ ...s, id: i + 1 })) };
      }
      body = settings;
    }
    await route.fulfill({ json: body });
  });
  await page.goto('/booking-settings');
  await page.getByLabel('Bước chia slot (phút)').fill('20');
  await page.getByRole('spinbutton', { name: 'Thời lượng dịch vụ 1', exact: true }).fill('35');
  await page.getByRole('button', { name: 'Thêm dịch vụ', exact: true }).click();
  await page.getByRole('textbox', { name: 'Tên dịch vụ 2', exact: true }).fill('Planned treatment');
  await page.getByRole('spinbutton', { name: 'Thời lượng dịch vụ 2', exact: true }).fill('95');
  await expect(page.getByRole('textbox', { name: 'Tên dịch vụ 1', exact: true })).toBeDisabled();
  await expect(page.getByRole('checkbox', { name: /Cho đặt khám mới/ })).toHaveCount(0);
  await page.getByRole('button', { name: 'Lưu cài đặt', exact: true }).click();
  await expect(page.getByText('Đã lưu cấu hình. Các lịch hẹn đã đặt giữ nguyên thời lượng.', { exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByLabel('Bước chia slot (phút)')).toHaveValue('20');
  await expect(page.getByRole('spinbutton', { name: 'Thời lượng dịch vụ 2', exact: true })).toHaveValue('95');
});

test('new-visit patient selects service before duration-specific slots', async ({ page }) => {
  let requestedDuration = '';
  await page.route('**/api/availability/options*', route => route.fulfill({ json: {
    branches: [{ id: 1, name: 'Branch' }], employees: [{ id: 2, branchId: 1, name: 'Dentist', canDentist: true }],
    services: [{ id: 3, branchId: 1, name: 'Exam 35', publicDurationMinutes: 35 }],
  } }));
  await page.route('**/api/availability?*', route => {
    requestedDuration = new URL(route.request().url()).searchParams.get('durationMinutes') ?? '';
    return route.fulfill({ json: { slots: [{ start: '2030-01-07T01:20:00Z', end: '2030-01-07T01:55:00Z', chairId: 1,
      dentistId: 2, scheduleVersion: 1, assistantScheduleVersion: null, bookingSettingsVersion: 2 }] } });
  });
  await page.goto('/customer/book');
  await expect(page.getByText('Khám lần đầu hoặc đợt khám mới', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
  await page.locator('.ant-select').nth(0).click();
  await page.locator('.ant-select-dropdown:visible').getByText('Branch', { exact: true }).click();
  await expect(page.getByLabel('Dịch vụ', { exact: true })).toHaveValue('Khám · 35 phút');
  await expect(page.getByLabel('Dịch vụ', { exact: true })).toHaveAttribute('readonly', '');
  await page.locator('.ant-select').nth(1).click();
  await page.locator('.ant-select-dropdown:visible').getByText('Dentist', { exact: true }).click();
  await expect(page.getByRole('button', { name: '08:20–08:55', exact: true })).toBeVisible();
  expect(requestedDuration).toBe('35');
});
