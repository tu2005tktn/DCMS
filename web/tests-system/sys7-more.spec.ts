import { test, expect } from '@playwright/test';
import { staffLogin, password, suffix } from './helpers';

test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });

test('SYS7-04 owner creates a lab work type; invalid data is refused', async ({ page }) => {
  const s = suffix().slice(0, 7).toUpperCase(); const code = `LWT${s}`; const name = `Loại labo ${s}`;
  await staffLogin(page, 'owner.demo');
  await page.goto('/settings/lab-work-types');
  await page.getByRole('button', { name: 'Thêm mới', exact: true }).click();
  const dlg = page.getByRole('dialog');
  await test.step('empty or invalid data is refused', async () => {
    await dlg.getByLabel('Mã', { exact: true }).fill('MA CO DAU CACH');
    await dlg.getByLabel('Tên', { exact: true }).fill(name);
    await dlg.locator('.ant-form-item').filter({ hasText: 'Đơn vị' }).locator('.ant-select').first().click();
    await page.waitForTimeout(500);
    await page.locator('.ant-select-dropdown:visible .ant-select-item-option').first().click({ force: true });
    await dlg.getByLabel('Giá tham khảo (VND)').fill('-5');
    await dlg.getByRole('button', { name: 'Lưu', exact: true }).click();
    await page.waitForTimeout(1200);
    await expect(dlg).toBeVisible();
    await page.screenshot({ path: 'test-results/sys7-04-invalid.png' });
  });
  await test.step('valid data is saved and listed', async () => {
    await dlg.getByLabel('Mã', { exact: true }).fill(code);
    await dlg.getByLabel('Giá tham khảo (VND)').fill('500000');
    await dlg.getByRole('button', { name: 'Lưu', exact: true }).click();
    await expect(dlg).not.toBeVisible();
    await expect(page.getByText(name).first()).toBeVisible();
  });
});

test('SYS7-01 owner adds a branch', async ({ page }) => {
  const s = suffix().slice(0, 6).toUpperCase(); const name = `Chi nhánh HT ${s}`;
  await staffLogin(page, 'owner.demo');
  await page.goto('/settings/branches');
  await page.getByRole('button', { name: 'Thêm chi nhánh', exact: true }).click();
  const dlg = page.getByRole('dialog');
  await page.screenshot({ path: 'test-results/sys7-01-form.png' });
  console.log('BRANCH LABELS', JSON.stringify(await dlg.locator('label').allInnerTexts()));
  await dlg.getByLabel('Tên chi nhánh').fill(name);
  const code = dlg.getByLabel(/Mã/).first();
  if (await code.count()) await code.fill(`CN${s}`);
  await dlg.getByRole('button', { name: /Lưu chi nhánh|Lưu/ }).click();
  await page.waitForTimeout(1500);
  await expect(page.getByText(name).first()).toBeVisible();
});

test('SYS7-03 owner records a material purchase and sees it listed', async ({ page }) => {
  const lot = `HT-${suffix().slice(0, 6).toUpperCase()}`;
  await staffLogin(page, 'owner.demo');
  await page.getByRole('link', { name: 'Dụng cụ - vật liệu', exact: true }).click();
  await page.getByRole('tab', { name: 'Tồn kho' }).click();
  await page.waitForTimeout(2500);
  await page.screenshot({ path: 'test-results/sys7-03-stock-before.png', fullPage: true });
  await page.getByRole('tab', { name: 'Nhập hàng' }).click();
  await page.getByRole('button', { name: 'Thêm mới', exact: true }).click();
  const dlg = page.getByRole('dialog');
  await dlg.getByLabel('Số lô', { exact: true }).fill(lot);
  await dlg.getByLabel('Ngày nhận', { exact: true }).fill(new Date(Date.now() + 7 * 3600_000).toISOString().slice(0, 10));
  await dlg.locator('.ant-form-item').filter({ hasText: 'Dụng cụ/Vật liệu' }).locator('.ant-select').first().click();
  await page.waitForTimeout(600);
  await page.locator('.ant-select-dropdown:visible .ant-select-item-option').first().click({ force: true });
  await dlg.getByLabel('Số lượng', { exact: true }).fill('5');
  const unit = dlg.getByRole('combobox', { name: 'Đơn vị', exact: true });
  await unit.fill('cái');
  await page.keyboard.press('Enter');
  await dlg.getByLabel(/Đơn giá/).fill('20000');
  await dlg.getByLabel(/Đã thanh toán/).fill('100000');
  await dlg.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(dlg).not.toBeVisible();
  await expect(page.getByRole('cell', { name: lot, exact: true })).toBeVisible();
  await page.getByRole('tab', { name: 'Tồn kho' }).click();
  await page.waitForTimeout(2500);
  await expect(page.getByRole('row').filter({ hasText: lot })).toBeVisible();
  await page.screenshot({ path: 'test-results/sys7-03-stock-after.png', fullPage: true });
});
