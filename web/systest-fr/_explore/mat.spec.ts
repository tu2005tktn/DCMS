import { test } from '@playwright/test';
import { staffLogin } from '../helpers';
test('explore purchase combo', async ({ page }) => {
  await staffLogin(page, 'owner.demo'); await page.goto('/materials'); await page.waitForTimeout(1500);
  await page.getByRole('button', { name: 'Thêm mới' }).first().click(); await page.waitForTimeout(800);
  const m = page.locator('.ant-modal-wrap:visible .ant-modal').last();
  for (const n of ['Dụng cụ/Vật liệu', 'Đơn vị', 'Công ty cung cấp']) {
    const c = m.getByRole('combobox', { name: n }); console.log(n, await c.count());
    await c.click({ force: true }); await page.waitForTimeout(1200);
    console.log(' opts:', (await page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option').allInnerTexts()).slice(0, 8).join(' | '));
    console.log(' dropdowns visible', await page.locator('.ant-select-dropdown:visible').count());
    await page.keyboard.press('Escape'); await page.waitForTimeout(300);
  }
  console.log('modal open', await m.isVisible());
});
