import { test, expect } from '@playwright/test';
import { staffLogin, password } from './helpers';

test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });
const denied = 'Bạn không có quyền thực hiện thao tác này.';

test('SYS8-01 owner views the financial statistics', async ({ page }) => {
  await staffLogin(page, 'owner.demo');
  await page.getByRole('link', { name: 'Thống kê tài chính', exact: true }).click();
  await expect(page).toHaveURL(/finance\/statistics/);
  await expect(page.getByRole('heading', { name: /Thống kê tài chính/ }).first()).toBeVisible();
  await page.getByRole('button', { name: 'Tải lại', exact: true }).click();
  await page.waitForTimeout(1500);
  await expect(page.getByRole('alert')).toHaveCount(0);
  await page.screenshot({ path: 'test-results/sys8-01.png', fullPage: true });
});

for (const [id, user] of [['SYS8-02a', 'dentist.demo'], ['SYS8-02b', 'assistant.demo']]) {
  test(`${id} ${user} cannot open financial statistics`, async ({ page }) => {
    await staffLogin(page, user);
    await expect(page.getByRole('link', { name: 'Thống kê tài chính', exact: true })).toHaveCount(0);
    await page.goto('/finance/statistics');
    await expect(page.getByText(denied)).toBeVisible({ timeout: 20_000 });
    await expect(page.getByText(/\d+\.\d{3} ₫/)).toHaveCount(0);
  });
}

test('SYS8-03 work statistics filter by period', async ({ page }) => {
  await staffLogin(page, 'owner.demo');
  await page.goto('/work-statistics');
  await expect(page.getByRole('heading', { name: 'Thống kê công việc/dịch vụ' })).toBeVisible();
  await expect(page.getByText(/Tìm thấy \d+ công việc/)).toBeVisible();
  const box = page.getByPlaceholder('Tìm MSKH, khách hàng, dịch vụ');
  await box.fill('Khám'); await box.press('Enter');
  await expect(page.getByRole('row').filter({ hasText: 'Khám' }).first()).toBeVisible();
  await box.fill('khong-co-' + Date.now()); await box.press('Enter');
  await expect(page.getByText(/Tìm thấy 0 công việc/)).toBeVisible();
  await page.screenshot({ path: 'test-results/sys8-03.png', fullPage: true });
});

test('SYS8-07 dentist cannot open customer care', async ({ page }) => {
  await staffLogin(page, 'dentist.demo');
  await expect(page.getByRole('link', { name: 'Chăm sóc khách hàng', exact: true })).toHaveCount(0);
  await page.goto('/customer-care');
  await expect(page.getByText('Chỉ Trợ lý và Chủ phòng khám dùng màn Chăm sóc khách hàng.')).toBeVisible();
});

test('SYS3-05 assistant filters the appointment list', async ({ page }) => {
  await staffLogin(page, 'assistant.demo');
  await page.getByRole('link', { name: 'Lịch hẹn', exact: true }).click();
  await expect(page).toHaveURL(/appointments/);
  await page.getByRole('button', { name: 'Hôm nay', exact: true }).click();
  await page.getByRole('button', { name: /Sau/ }).click();
  await page.waitForTimeout(1200);
  await expect(page.getByText(/(T\d|CN), \d{2}\/\d{2}\/\d{4}/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Tải lại', exact: true })).toBeVisible();
  await page.screenshot({ path: 'test-results/sys3-05.png', fullPage: true });
});
