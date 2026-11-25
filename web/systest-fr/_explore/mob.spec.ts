import { test } from '@playwright/test';
const NL = String.fromCharCode(10);
test('explore mobile web', async ({ page }) => {
  test.setTimeout(240000); page.setDefaultTimeout(20000);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('http://localhost:8082/'); await page.waitForTimeout(15000);
  console.log('START' + NL + (await page.locator('body').ariaSnapshot()).split(NL).slice(0, 50).join(NL));
  await page.goto('http://localhost:8082/register'); await page.waitForTimeout(5000);
  console.log('REGISTER' + NL + (await page.locator('body').ariaSnapshot()).split(NL).slice(0, 50).join(NL));
});
