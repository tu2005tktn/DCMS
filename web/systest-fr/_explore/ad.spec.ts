import { test } from '@playwright/test';
import { staffLogin } from '../helpers';
const NL = String.fromCharCode(10);
const dump = async (label: string, loc: any, n = 40) => console.log('=== ' + label + NL + (await loc.ariaSnapshot()).split(NL).filter((l: string) => !l.includes('/url') && !l.includes('- img')).slice(0, n).join(NL));
test('explore admin pages', async ({ page }) => {
  await staffLogin(page, 'owner.demo');
  for (const [u, n] of [['/system-settings', 45], ['/settings', 35], ['/settings/treatment-services', 30], ['/materials', 40], ['/medicines', 25], ['/finance/statistics', 45], ['/work-statistics', 40]] as const) {
    await page.goto(u); await page.waitForTimeout(2200); await dump(u, page.locator('main').last(), n);
  }
});
