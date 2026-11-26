import { test, expect } from '@playwright/test';
import { staffLogin, password, suffix } from './helpers';
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });

test('SYS7-06 owner adds a booking service and saves the settings', async ({ page }) => {
  const name = `Dịch vụ HT ${suffix()}`;
  await staffLogin(page, 'owner.demo');
  await page.getByRole('link', { name: 'Cài đặt đặt lịch', exact: true }).click();
  await page.getByRole('button', { name: 'Thêm dịch vụ' }).waitFor({ timeout: 30000 });
  await page.waitForTimeout(1500);
  await page.getByRole('button', { name: 'Thêm dịch vụ' }).click();
  const rows = page.getByRole('row');
  const last = rows.last();
  await last.locator('input[type=text], input:not([type])').first().fill(name);
  await last.getByRole('spinbutton').first().fill('20').catch(async () => { await last.locator('input').nth(1).fill('20'); });
  const save = page.getByRole('button', { name: 'Lưu cài đặt' });
  await save.scrollIntoViewIfNeeded();
  await save.click();
  await page.waitForTimeout(3000);
  await page.screenshot({ path: 'test-results/sys7-06.png', fullPage: true });
  await page.reload();
  await page.getByRole('button', { name: 'Thêm dịch vụ' }).waitFor({ timeout: 30000 });
  await page.waitForTimeout(1500);
  await expect(page.locator(`input[value="${name}"]`)).toHaveCount(1);
});
