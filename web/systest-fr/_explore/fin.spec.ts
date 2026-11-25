import { test } from '@playwright/test';
import { Api, staffLogin } from '../helpers';
import { sheetWithWork } from '../fx';
const NL = String.fromCharCode(10);
const dump = async (label: string, loc: any, n = 40) => console.log('=== ' + label + NL + (await loc.ariaSnapshot()).split(NL).filter((l: string) => !l.includes('/url') && !l.includes('- img')).slice(0, n).join(NL));
test('explore finance & materials', async ({ page, request }) => {
  const f = await sheetWithWork(request); await staffLogin(page, 'owner.demo');
  await page.goto('/materials'); await page.waitForTimeout(2000);
  await page.getByRole('button', { name: 'Thêm mới' }).first().click(); await page.waitForTimeout(1200);
  await dump('PURCHASE FORM', page.locator('.ant-modal-wrap:visible .ant-modal, .ant-drawer-open .ant-drawer-content, main').last(), 60);
  await page.keyboard.press('Escape');
  await page.getByRole('tab', { name: 'Tồn kho' }).click(); await page.waitForTimeout(1500); await dump('STOCK', page.locator('main').last(), 30);
  await page.goto(`/patients/${f.patient.id}?tab=treatment`); await page.waitForTimeout(2500);
  const mb = page.getByRole('button', { name: /^Chọn vật liệu dùng cho/ }).first(); console.log('MAT BTN', await mb.count());
  if (await mb.count()) { await mb.click(); await page.waitForTimeout(1000); await dump('MAT DIALOG', page.getByRole('dialog').last(), 30); await page.keyboard.press('Escape'); await page.waitForTimeout(500); }
  await page.getByRole('button', { name: 'Trả tiền' }).first().click(); await page.waitForTimeout(2000);
  await dump('PAY', page.getByRole('dialog').last(), 50);
});
