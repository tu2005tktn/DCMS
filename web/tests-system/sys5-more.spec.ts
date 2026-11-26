import { test, expect, type Browser, type Page } from '@playwright/test';
import { staffLogin, password } from './helpers';
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });

async function visitHref(browser: Browser, code: string) {
  const ctx = await browser.newContext(); const p = await ctx.newPage();
  await staffLogin(p, 'assistant.demo');
  await p.getByRole('link', { name: 'Tiếp nhận và hàng chờ' }).click(); await p.waitForTimeout(4000);
  const href = await p.locator('tr').filter({ hasText: code }).getByRole('link', { name: 'Mở lượt khám' }).getAttribute('href');
  await ctx.close(); return href!;
}
async function startExam(page: Page) {
  const st = page.getByRole('button', { name: 'Bắt đầu khám', exact: true }); if (await st.count()) await st.click();
  await expect(page.getByLabel('Lý do khám / triệu chứng', { exact: true })).toBeEnabled();
  await page.getByLabel('Lý do khám / triệu chứng', { exact: true }).fill('Đau răng');
  await page.getByRole('button', { name: 'Lưu nháp', exact: true }).click(); await page.waitForTimeout(1500);
}

test('SYS5-04 investigation order and result', async ({ page, browser }) => {
  const href = await visitHref(browser, 'e472a7');
  await staffLogin(page, 'dentist.demo');
  await page.goto(href); await page.waitForTimeout(4000);
  await startExam(page);
  await page.getByRole('tab', { name: 'Cận lâm sàng' }).click();
  await page.getByRole('button', { name: 'Thêm chỉ định' }).click();
  const d = page.getByRole('dialog');
  await d.locator('.ant-select').nth(1).click(); await page.keyboard.type('Chụp phim panorama'); await page.waitForTimeout(800);
  await page.keyboard.press('Enter'); await page.waitForTimeout(500);
  await d.locator('textarea').fill('Đánh giá tổn thương vùng răng 16');
  await d.getByRole('button', { name: 'Tạo chỉ định' }).click();
  await page.waitForTimeout(2500);
  await page.screenshot({ path: 'test-results/sys5-04a.png', fullPage: true });
  console.log('ORDBTN', JSON.stringify(await page.getByRole('button').allInnerTexts()));
  console.log('ORDTXT', (await page.getByRole('tabpanel').innerText()).slice(0, 700));
});
