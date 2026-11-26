import { test, expect, type Page, type Locator } from '@playwright/test';
import { staffLogin, password } from './helpers';
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });
const pickFirst = async (page: Page, d: Locator, id: string, txt = '') => {
  await d.locator('.ant-select').filter({ has: page.locator('#' + id) }).click();
  if (txt) await page.keyboard.type(txt);
  await page.waitForTimeout(800);
  await page.locator('.ant-select-dropdown:visible .ant-select-item-option').first().click({ force: true });
  await page.waitForTimeout(500);
};

test('SYS4-08 assistant registers a walk-in appointment', async ({ page }) => {
  await staffLogin(page, 'assistant.demo');
  await page.getByRole('link', { name: 'Tiếp nhận và hàng chờ' }).click(); await page.waitForTimeout(3000);
  await page.getByRole('button', { name: 'Xếp lịch khách vãng lai' }).click(); await page.waitForTimeout(2500);
  const d = page.getByRole('dialog');
  await d.getByText('Vãng lai', { exact: true }).click();
  await d.locator('#bookerName').fill('Người đặt Hệ Thống'); await d.locator('#bookerContact').fill('0912345678');
  await pickFirst(page, d, 'patientId', 'Khám Hệ');
  await pickFirst(page, d, 'serviceId');
  await pickFirst(page, d, 'dentistId');
  await d.locator('#date').fill('2026-11-17'); await page.waitForTimeout(2500);
  const slots = d.getByRole('button', { name: /^\d{2}:\d{2}/ });
  await slots.nth(15).click();
  await d.getByRole('button', { name: 'Xem lại' }).click();
  await expect(d.getByText('Xem lại trước khi xác nhận')).toBeVisible();
  console.log('FINALBTN', JSON.stringify(await d.getByRole('button').allInnerTexts()));
  await d.getByRole('button', { name: /^Xác nhận/ }).click();
  await page.waitForTimeout(3000);
  await page.screenshot({ path: 'test-results/sys4-08.png', fullPage: true });
  console.log('DLGLEFT', await page.getByRole('dialog').count());
});
