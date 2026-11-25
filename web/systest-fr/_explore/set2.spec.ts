import { test } from '@playwright/test';
import { staffLogin } from '../helpers';
const NL = String.fromCharCode(10);
const dump = async (label: string, loc: any, n = 45) => console.log('=== ' + label + NL + (await loc.ariaSnapshot()).split(NL).filter((l: string) => !l.includes('/url') && !l.includes('- img') && !/Increase Value|Decrease Value/.test(l)).slice(0, n).join(NL));
const modal = (page: any) => page.locator('.ant-modal-wrap:visible .ant-modal, .ant-drawer-open .ant-drawer-content').last();
test('explore settings forms', async ({ page }) => {
  test.setTimeout(400000);
  await staffLogin(page, 'owner.demo');
  for (const [s, btn] of [['diagnoses', 'Thêm mới'], ['material-suppliers', 'Thêm mới'], ['materials', '+ Thêm mới'], ['lab-work-types', 'Thêm mới'], ['customer-groups', 'Thêm mới'], ['medicines', 'Thêm mới'], ['branches', 'Thêm chi nhánh']]) {
    await page.goto('/settings/' + s); await page.waitForTimeout(2200);
    await page.getByRole('button', { name: btn, exact: true }).first().click(); await page.waitForTimeout(1200); await dump(s + ' ADD', modal(page), 50);
    await page.keyboard.press('Escape'); await page.waitForTimeout(500);
  }
  await page.goto('/settings/materials'); await page.waitForTimeout(2000); await page.getByRole('button', { name: 'Nhóm dụng cụ / vật liệu' }).click(); await page.waitForTimeout(1200);
  await dump('MATERIAL GROUPS', page.locator('main').last(), 40);
  await page.goto('/settings/branches'); await page.waitForTimeout(2000); await page.getByRole('row').filter({ hasText: 'BR-01' }).getByRole('button', { name: 'Sửa' }).click(); await page.waitForTimeout(1500);
  await dump('BRANCH EDIT', modal(page), 70); await page.keyboard.press('Escape');
  await page.goto('/settings/general'); await page.waitForTimeout(2000);
  const t = (await page.locator('main').last().ariaSnapshot()).split(NL); console.log('=== GENERAL TAIL' + NL + t.slice(-30).join(NL));
  await page.goto('/commercial/treatment'); await page.waitForTimeout(2200); await dump('COMMERCIAL', page.locator('main').last(), 40);
  await page.goto('/customer-care'); await page.waitForTimeout(2000); await page.getByRole('button', { name: /TẠO MỚI/ }).click(); await page.waitForTimeout(1200); await dump('CARE NEW', modal(page), 50);
});
