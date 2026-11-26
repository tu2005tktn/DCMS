import { test, expect, type Page } from '@playwright/test';
import { staffLogin, password, suffix } from './helpers';

test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });

export async function createMedicine(page: Page, code: string, name: string) {
  await page.goto('/settings/medicines');
  await page.getByRole('button', { name: 'Thêm mới', exact: true }).click();
  const drawer = page.getByRole('dialog');
  await drawer.getByLabel('Mã', { exact: true }).fill(code);
  await drawer.getByLabel('Tên', { exact: true }).fill(name);
  await drawer.getByLabel('Đơn vị gốc', { exact: true }).fill('viên');
  await drawer.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(drawer).not.toBeVisible();
  await page.getByPlaceholder('Tìm theo tên hoặc mã').fill(code);
  await page.getByPlaceholder('Tìm theo tên hoặc mã').press('Enter');
  await expect(page.getByRole('row').filter({ hasText: name })).toBeVisible();
}

test('SYS7-05 owner adds a medicine and deactivates it', async ({ page }) => {
  const s = suffix().slice(0, 7).toUpperCase(); const name = `Thuốc HT ${s}`;
  await staffLogin(page, 'owner.demo');
  await createMedicine(page, `HT${s}`, name);
  const row = page.getByRole('row').filter({ hasText: name });
  await row.getByRole('button', { name: /Ngừng dùng/ }).click();
  const dlg = page.getByRole('dialog');
  await dlg.getByRole('textbox').fill('Ngừng nhập thuốc này');
  await dlg.getByRole('button', { name: 'Xác nhận', exact: true }).click();
  await page.waitForTimeout(1200);
  await expect(page.getByRole('row').filter({ hasText: name })).toHaveCount(0);
});
