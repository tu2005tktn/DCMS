import { test } from '@playwright/test';
import { staffLogin } from '../helpers';

const dump = async (page: any, label: string, loc: any) =>
  console.log(label + '\n' + (await loc.ariaSnapshot()).split('\n').filter((l: string) => !l.includes('/url')).slice(0, 90).join('\n'));

test('explore calendar detail + booking drawer', async ({ page }) => {
  await staffLogin(page, 'assistant.demo'); await page.goto('/appointments'); await page.waitForTimeout(2000);
  await page.getByRole('button', { name: /^Lịch AP-/ }).last().click(); await page.waitForTimeout(1500);
  await dump(page, 'DETAIL', page.getByRole('dialog').last());
  await page.keyboard.press('Escape'); await page.waitForTimeout(500);
  await page.getByRole('button', { name: 'Đặt lịch', exact: true }).click(); await page.waitForTimeout(1500);
  await dump(page, 'DRAWER', page.getByRole('dialog').last());
});
