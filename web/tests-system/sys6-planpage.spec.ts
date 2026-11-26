import { test, expect, type Page } from '@playwright/test';
import { staffLogin, password } from './helpers';
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });
const choose = async (page: Page, d: ReturnType<Page['getByRole']>, label: string) => {
  const f = d.locator('.ant-form-item, .ant-row, div').filter({ hasText: new RegExp('^' + label) }).locator('.ant-select').first();
  await f.click(); await page.waitForTimeout(600);
  await page.locator('.ant-select-dropdown:visible .ant-select-item-option').first().click({ force: true }); await page.waitForTimeout(400);
};

test('SYS6-02/03/04 plan page: add item, save draft, finalize', async ({ page }) => {
  await staffLogin(page, 'dentist.demo');
  await page.goto('/patients/38');
  await page.getByRole('tab', { name: 'Điều trị & Thanh toán' }).click(); await page.waitForTimeout(2000);
  const resp = page.waitForResponse(r => r.request().method() === 'POST' && /dossiers\/quick/.test(r.url()));
  await page.getByRole('button', { name: /tạo phiếu mới/i }).click();
  const id = (await (await resp).json()).dossier.id;
  await page.goto(`/patients/38/dossiers/${id}/plan`); await page.waitForTimeout(4000);
  await page.getByRole('button', { name: 'Thêm hạng mục' }).click();
  const d = page.getByRole('dialog');
  await choose(page, d as any, 'Dịch vụ');
  await choose(page, d as any, 'Răng hoặc vùng điều trị');
  const unit = d.locator('.ant-select').nth(2);
  if (!(await unit.innerText()).trim() || /Chọn đơn vị/.test(await unit.innerText())) { await unit.click(); await page.waitForTimeout(500); await page.locator('.ant-select-dropdown:visible .ant-select-item-option').first().click({ force: true }); }
  await d.getByRole('spinbutton').last().fill('250000');
  await page.screenshot({ path: 'test-results/sys6-planpage-a.png' });
  await d.getByRole('button', { name: 'Lưu hạng mục' }).click(); await page.waitForTimeout(2000);
  console.log('AFTERITEM', await page.getByRole('dialog').count(), (await page.locator('#staff-content').innerText()).slice(0, 600));
  await page.locator('textarea').first().fill('Điều trị sâu răng và theo dõi');
  const sv = page.waitForResponse(r => ['PUT', 'POST', 'PATCH'].includes(r.request().method()) && /plan/.test(r.url()));
  await page.getByRole('button', { name: 'Lưu nháp kế hoạch' }).click(); const sr = await sv; console.log('SAVERES', sr.request().method(), sr.url(), sr.status(), (await sr.text()).slice(0, 500), (sr.request().postData() ?? '').slice(0, 500)); await page.waitForTimeout(2500);
  console.log('SAVEMSG', JSON.stringify(await page.locator('.ant-message, .ant-alert, [role=alert], .ant-form-item-explain-error').allInnerTexts()), await page.getByText('Lưu nháp trước khi chốt.').count());
  await page.screenshot({ path: 'test-results/sys6-draft.png', fullPage: true });
  await page.getByRole('button', { name: 'Chốt kế hoạch' }).click(); await page.waitForTimeout(1500);
  const c = page.getByRole('dialog');
  console.log('CONFIRM', await c.count(), (await c.innerText().catch(() => '')).slice(0, 400), JSON.stringify(await c.getByRole('button').allInnerTexts().catch(() => [])));
  await page.screenshot({ path: 'test-results/sys6-planpage-b.png', fullPage: true });
});
