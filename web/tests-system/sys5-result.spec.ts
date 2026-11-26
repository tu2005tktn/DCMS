import { test, expect, type Browser } from '@playwright/test';
import { staffLogin, password } from './helpers';
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });
async function visitHref(browser: Browser, code: string) {
  const ctx = await browser.newContext(); const p = await ctx.newPage();
  await staffLogin(p, 'assistant.demo');
  await p.getByRole('link', { name: 'Tiếp nhận và hàng chờ' }).click(); await p.waitForTimeout(4000);
  const href = await p.locator('tr').filter({ hasText: code }).getByRole('link', { name: 'Mở lượt khám' }).getAttribute('href');
  await ctx.close(); return href!;
}
test('SYS5-04b result received and read', async ({ page, browser }) => {
  const href = await visitHref(browser, 'e472a7');
  await staffLogin(page, 'dentist.demo');
  await page.goto(href); await page.waitForTimeout(4000);
  await page.getByRole('tab', { name: 'Cận lâm sàng' }).click(); await page.waitForTimeout(1500);
  if (await page.getByRole('button', { name: 'Nhận kết quả' }).count()) {
  await page.getByRole('button', { name: 'Nhận kết quả' }).first().click();
  const d = page.getByRole('dialog');
  await d.locator('textarea').first().fill('Phim panorama: tổn thương sâu mặt nhai răng 16.');
  await d.getByRole('button', { name: 'Lưu kết quả' }).click();
  await expect(d).not.toBeVisible({ timeout: 15000 });
  }
  await page.waitForTimeout(2000);
  console.log('BTN', JSON.stringify(await page.getByRole('button').allInnerTexts()));
  const read = page.getByRole('button', { name: 'Đọc kết quả' });
  if (await read.count()) { await read.first().click(); await page.waitForTimeout(1500); const rd = page.getByRole('dialog'); await rd.locator('textarea').last().fill('Sâu răng 16, cần điều trị tủy.'); await rd.getByRole('button', { name: 'Xác nhận đã đọc kết quả' }).click(); await expect(rd).not.toBeVisible({ timeout: 15000 }); await page.waitForTimeout(2000); }
  console.log('BTN2', JSON.stringify(await page.getByRole('button').allInnerTexts()));
  console.log('TXT', (await page.getByRole('tabpanel').innerText()).slice(0, 700));
  await page.screenshot({ path: 'test-results/sys5-04b.png', fullPage: true });
});
