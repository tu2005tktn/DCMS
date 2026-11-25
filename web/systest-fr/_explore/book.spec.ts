import { test } from '@playwright/test';
import { customerLogin, addDays, todayIso } from '../helpers';

test('explore booking 2', async ({ page }) => {
  await customerLogin(page, 'guest.demo');
  await page.goto('/customer/book');
  await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
  await page.getByRole('combobox', { name: 'Chi nhánh' }).click();
  await page.locator('.ant-select-dropdown:visible').getByText('S09 - Chi nhánh thử nghiệm', { exact: true }).click();
  await page.waitForTimeout(500);
  await page.getByRole('combobox', { name: 'Bác sĩ' }).click(); await page.waitForTimeout(500);
  console.log('DENTISTS ' + await page.locator('.ant-select-dropdown:visible').last().innerText());
  await page.locator('.ant-select-dropdown:visible').last().getByText('Nha sĩ demo', { exact: true }).click();
  await page.getByRole('textbox', { name: 'Ngày khám' }).fill(addDays(todayIso(), 2)); await page.keyboard.press('Tab');
  await page.waitForTimeout(1500);
  const slots = page.getByRole('region', { name: 'Chọn khung giờ khám' }).getByRole('button');
  console.log('SLOTS ' + (await slots.allInnerTexts()).join(' | '));
  await slots.nth(4).click();
  await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click(); await page.waitForTimeout(800);
  console.log('CONTACT\n' + await page.locator('main').ariaSnapshot());
  await page.getByRole('button', { name: 'Xem lại', exact: true }).click(); await page.waitForTimeout(800);
  console.log('REVIEW\n' + await page.locator('main').ariaSnapshot());
  await page.getByRole('button', { name: /Xác nhận/ }).first().click(); await page.waitForTimeout(2500);
  console.log('DONE ' + await page.locator('main').ariaSnapshot());
});
