import { test, expect } from '@playwright/test';
import { staffLogin, password } from './helpers';

test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });

test('SYS8-05 assistant records a customer care contact', async ({ page }) => {
  await staffLogin(page, 'assistant.demo');
  await page.goto('/customer-care');
  await page.getByRole('button', { name: /TẠO MỚI/i }).first().click();
  const dlg = page.getByRole('dialog');
  const customer = dlg.locator('.ant-select').filter({ has: page.locator('#customerId, [id*=customer]') }).first();
  await dlg.getByRole('combobox').nth(2).fill('Khám Hệ');
  await page.waitForTimeout(1500);
  await page.locator('.ant-select-dropdown:visible .ant-select-item-option').first().click({ force: true });
  await dlg.locator('.ant-form-item').filter({ hasText: 'Loại chăm sóc' }).locator('.ant-select').click();
  await page.waitForTimeout(500);
  await page.locator('.ant-select-dropdown:visible .ant-select-item-option').first().click({ force: true });
  const note = `Gọi nhắc tái khám ${Date.now()}`;
  await dlg.getByRole('textbox').last().fill(note);
  void customer;
  const resp = page.waitForResponse(r => r.request().method() === 'POST' && /care/.test(r.url()));
  await dlg.getByRole('button', { name: 'Lưu', exact: true }).click();
  expect((await resp).status()).toBeLessThan(300);
  await expect(dlg).not.toBeVisible();
  await page.screenshot({ path: 'test-results/sys8-05.png', fullPage: true });
});
