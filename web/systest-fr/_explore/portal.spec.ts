import { test, expect } from '@playwright/test';
import { customerLogin } from '../helpers';

const dump = async (page: any, label: string, loc = 'main') =>
  console.log(label + '\n' + (await page.locator(loc).first().ariaSnapshot()).split('\n').filter((l: string) => !l.includes('/url')).slice(0, 80).join('\n'));

test('explore portal', async ({ page }) => {
  await customerLogin(page, 'guest.demo');
  await page.goto('/customer/patient-link'); await expect(page.locator('main h2, main h1').first()).toBeVisible({ timeout: 15000 }); await page.waitForTimeout(800);
  await dump(page, 'LINK');
  await page.goto('/customer/family-members'); await expect(page.locator('main h2, main h1').first()).toBeVisible({ timeout: 15000 }); await page.waitForTimeout(800);
  await dump(page, 'FAMILY');
  await page.getByRole('button', { name: /Xem hồ sơ/ }).first().click(); await page.waitForTimeout(1500);
  await dump(page, 'FAMILY RECORD', 'body');
});
