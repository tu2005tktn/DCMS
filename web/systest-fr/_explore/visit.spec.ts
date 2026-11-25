import { test } from '@playwright/test';
import { staffLogin } from '../helpers';

test('explore documents + AI', async ({ page }) => {
  await staffLogin(page, 'owner.demo'); await page.goto(`/visits/1fa0693c-24e1-4aee-bf59-f4074f623921`); await page.waitForTimeout(2000);
  await page.getByRole('tab', { name: 'Ảnh & tài liệu', exact: true }).click(); await page.waitForTimeout(1500);
  console.log('DOCS\n' + (await page.locator('main').last().locator('.ant-tabs-tabpane-active').last().ariaSnapshot()).split('\n').filter(l => !l.includes('/url')).slice(0, 60).join('\n'));
  await page.getByText('panorama-R018.jpg', { exact: true }).first().click(); await page.waitForTimeout(2000);
  console.log('DETAIL\n' + (await page.locator('.ant-drawer-open .ant-drawer-content, .ant-modal-wrap:visible .ant-modal').last().ariaSnapshot()).split('\n').slice(0, 80).join('\n'));
});
