import { test } from '@playwright/test';
import { staffLogin } from '../helpers';

test('explore working schedules', async ({ page }) => {
  await staffLogin(page, 'owner.demo'); await page.goto('/working-schedules'); await page.waitForTimeout(2500);
  const start = page.getByRole('combobox', { name: 'Bắt đầu 1', exact: true });
  await start.focus(); await start.press('ArrowDown'); await page.waitForTimeout(600);
  console.log('START OPTIONS ' + (await page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option').allInnerTexts()).slice(0, 20).join(' '));
  await start.press('Escape');
  const kind = page.getByRole('combobox', { name: 'Loại khoảng 1', exact: true });
  await kind.focus(); await kind.press('ArrowDown'); await page.waitForTimeout(400);
  console.log('KIND OPTIONS ' + (await page.locator('.ant-select-dropdown:visible').last().innerText()).replace(/\n/g, ' '));
  await kind.press('Escape');
  const type = page.getByRole('combobox', { name: 'Loại lịch', exact: true });
  await type.focus(); await type.press('ArrowDown'); await page.waitForTimeout(400);
  console.log('TYPE OPTIONS ' + (await page.locator('.ant-select-dropdown:visible').last().innerText()).replace(/\n/g, ' '));
  await type.press('Escape');
  const who = page.getByRole('combobox', { name: 'Nhân viên / bác sĩ', exact: true });
  await who.focus(); await who.press('ArrowDown'); await page.waitForTimeout(400);
  console.log('WHO OPTIONS ' + (await page.locator('.ant-select-dropdown:visible').last().innerText()).replace(/\n/g, ' '));
});
