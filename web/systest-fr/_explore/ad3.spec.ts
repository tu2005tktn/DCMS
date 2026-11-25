import { test } from '@playwright/test';
import { staffLogin } from '../helpers';
test('explore ad3', async ({ page }) => {
  test.setTimeout(200000); page.setDefaultTimeout(8000);
  await staffLogin(page, 'owner.demo');
  await page.goto('/settings/diagnoses'); await page.waitForTimeout(2000);
  await page.locator('.ant-segmented-item').filter({ hasText: /^Ngừng dùng$/ }).first().click(); await page.waitForTimeout(1500);
  console.log('INACTIVE', JSON.stringify((await page.locator('tbody tr.ant-table-row').allInnerTexts()).slice(0, 3)));
  console.log('BTNS', JSON.stringify(await page.locator('tbody tr.ant-table-row').first().getByRole('button').allInnerTexts()));
  await page.goto('/settings/general'); await page.waitForTimeout(2500);
  const c = page.getByRole('combobox', { name: 'Tự đăng xuất khi không thao tác sau' });
  await c.click({ force: true }); await page.waitForTimeout(800);
  await page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option').filter({ hasText: /^10 phút/ }).first().click(); await page.waitForTimeout(800);
  console.log('AFTER', await page.locator('.ant-select').filter({ has: c }).innerText(), 'disabled', await page.getByRole('button', { name: 'Xem trước và lưu' }).isDisabled());
});
