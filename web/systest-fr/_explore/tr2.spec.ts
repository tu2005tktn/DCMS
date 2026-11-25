import { test } from '@playwright/test';
import { staffLogin } from '../helpers';

const NL = String.fromCharCode(10);
const dump = async (label: string, loc: any, n = 60) => console.log(label + NL + (await loc.ariaSnapshot()).split(NL).filter((l: string) => !l.includes('/url')).slice(0, n).join(NL));
test('explore tr2 pages', async ({ page }) => {
  await staffLogin(page, 'owner.demo');
  await page.goto('/treatment-templates'); await page.waitForTimeout(2000); await dump('TEMPLATES', page.locator('main').last(), 50);
  await page.goto('/lab-orders'); await page.waitForTimeout(2000); await dump('LAB', page.locator('main').last(), 50);
  await page.goto('/materials'); await page.waitForTimeout(2000); await dump('MATERIALS', page.locator('main').last(), 50);
  await page.goto('/patients/4?tab=prescriptions'); await page.waitForTimeout(2000);
  await page.getByRole('tab', { name: 'Đơn thuốc', exact: true }).click(); await page.waitForTimeout(1200);
  await page.getByRole('button', { name: /Kê đơn thuốc/ }).first().click(); await page.waitForTimeout(1200);
  await dump('RX FORM', page.locator('.ant-modal-wrap:visible .ant-modal, .ant-drawer-open .ant-drawer-content').last(), 70);
});
