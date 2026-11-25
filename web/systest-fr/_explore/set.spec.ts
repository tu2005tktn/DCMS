import { test } from '@playwright/test';
import { staffLogin } from '../helpers';
const NL = String.fromCharCode(10);
const dump = async (label: string, loc: any, n = 45) => console.log('=== ' + label + NL + (await loc.ariaSnapshot()).split(NL).filter((l: string) => !l.includes('/url') && !l.includes('- img') && !/^\s+- (cell|row) "/.test(l) || /columnheader/.test(l)).slice(0, n).join(NL));
test('explore settings sections', async ({ page }) => {
  test.setTimeout(300000);
  await staffLogin(page, 'owner.demo');
  for (const s of ['general', 'diagnoses', 'treatment-services', 'material-suppliers', 'materials', 'lab-providers', 'lab-work-types', 'customer-groups', 'medicines', 'branches', 'audit']) {
    await page.goto('/settings/' + s); await page.waitForTimeout(2500); await dump(s, page.locator('main').last(), s === 'general' ? 90 : 40);
  }
  await page.goto('/customer-care'); await page.waitForTimeout(2500); await dump('customer-care', page.locator('main').last(), 60);
});
