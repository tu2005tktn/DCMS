import { test, expect } from '@playwright/test';
import { staffLogin, password, suffix } from './helpers';
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });

test('SYS7-07 owner assigns a permission group and it is recorded in history', async ({ page }) => {
  const reason = `Kiểm thử hệ thống ${suffix()}`;
  await staffLogin(page, 'owner.demo');
  await page.goto('/settings/access-groups'); await page.waitForTimeout(6000);
  await page.getByRole('tab', { name: 'Gán cho nhân viên' }).click();
  await page.getByRole('button', { name: 'Gán quyền Trợ lý demo' }).click();
  const dlg = page.getByRole('dialog');
  await dlg.locator('.ant-select').first().click();
  await page.locator('.ant-select-dropdown:visible .ant-select-item-option').filter({ hasText: 'Assistants' }).first().click({ force: true });
  await dlg.getByRole('textbox').last().fill(reason);
  await dlg.getByRole('button', { name: 'Lưu nhóm cho nhân viên' }).click();
  await expect(dlg).not.toBeVisible({ timeout: 15000 });
  await page.getByRole('button', { name: 'Lịch sử phân quyền' }).click();
  await expect(page.getByText(reason).locator('visible=true').first()).toBeVisible({ timeout: 15000 });
  const p2 = await (await page.context().browser()!.newContext()).newPage();
  await staffLogin(p2, 'assistant.demo');
  await expect(p2.getByRole('link', { name: 'Tiếp nhận và hàng chờ' })).toBeVisible();
  await expect(p2.getByRole('link', { name: 'Chăm sóc khách hàng' })).toHaveCount(0);
});
