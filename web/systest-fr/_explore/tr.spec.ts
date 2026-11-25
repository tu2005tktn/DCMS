import { test } from '@playwright/test';
import { staffLogin } from '../helpers';

const NL = String.fromCharCode(10);
test('explore treatment sheet', async ({ page }) => {
  await staffLogin(page, 'owner.demo'); await page.goto('/patients/4?tab=treatment'); await page.waitForTimeout(2500);
  await page.getByRole('button', { name: 'Mở phiếu HS0B2770DE1383' }).click(); await page.waitForTimeout(2000);
  const snap = (await page.locator('main').last().ariaSnapshot()).split(NL).filter(l => !l.includes('/url'));
  const i = snap.findIndex(l => l.includes('Thu gọn phiếu HS0B2770DE1383'));
  console.log('SHEET' + NL + snap.slice(i, i + 220).join(NL));
});
