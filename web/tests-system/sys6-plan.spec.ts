import { test, expect, type Page } from '@playwright/test';
import { staffLogin, password } from './helpers';
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });

async function newDossier(page: Page) {
  await staffLogin(page, 'dentist.demo');
  await page.goto('/patients/38');
  await page.getByRole('tab', { name: 'Điều trị & Thanh toán' }).click();
  await page.waitForTimeout(2000);
  const n = await page.getByText('Ngày lập phiếu').count();
  await page.getByRole('button', { name: /tạo phiếu mới/i }).click();
  await expect(page.getByText('Ngày lập phiếu')).toHaveCount(n + 1);
}
const plan = (page: Page) => page.locator('table').filter({ hasText: 'Thời gian dự kiến' }).first();

test('SYS6-02/03 plan items: invalid refused, valid saved with total', async ({ page }) => {
  await newDossier(page);
  const t = plan(page);
  await t.getByRole('columnheader').getByRole('button').first().click();
  await test.step('SYS6-03 empty service is refused', async () => {
    await t.getByRole('button', { name: 'Đồng ý' }).click();
    await page.waitForTimeout(1500);
    await expect(t.getByText('Vui lòng nhập trường này.').first()).toBeVisible();
    await page.screenshot({ path: 'test-results/sys6-03.png' });
  });
  await test.step('SYS6-02 valid item is added and total updates', async () => {
    await t.getByPlaceholder('25, 26, 46').fill('16');
    await t.locator('.ant-select').first().click();
    await page.keyboard.press('ArrowDown'); await page.keyboard.press('Enter');
    await page.waitForTimeout(500);
    await t.getByRole('button', { name: 'Đồng ý' }).click();
    await page.waitForTimeout(2000);
    await expect(t.getByText('Chưa có hạng mục.')).toHaveCount(0);
    await expect(page.getByText(/Tổng dự toán: /).first()).toBeVisible();
    await page.screenshot({ path: 'test-results/sys6-02.png' });
  });
});
