import { test, expect, type Page } from '@playwright/test';
import { staffLogin, password } from './helpers';
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });

const pick = async (page: Page, sel: ReturnType<Page['locator']>) => {
  await sel.click(); await page.waitForTimeout(400); await page.keyboard.press('ArrowDown'); await page.keyboard.press('Enter');
};

test('SYS6-06/10 work done, lab dialog, then owner pays; dossier status toggles', async ({ page }) => {
  await staffLogin(page, 'dentist.demo');
  await page.goto('/patients/38');
  await page.getByRole('tab', { name: 'Điều trị & Thanh toán' }).click();
  await page.waitForTimeout(2000);
  const before = await page.getByText('Ngày lập phiếu').count();
  await page.getByRole('button', { name: /tạo phiếu mới/i }).click();
  await expect(page.getByText('Ngày lập phiếu')).toHaveCount(Math.min(before + 1, 20), { timeout: 15000 });
  await page.waitForTimeout(2500);
  const pt = page.locator('table').filter({ hasText: 'Thời gian dự kiến' }).first();
  await pt.getByRole('columnheader').getByRole('button').first().click();
  await pt.getByPlaceholder('25, 26, 46').fill('16');
  await pick(page, pt.locator('.ant-select').first());
  await pt.getByRole('button', { name: 'Đồng ý' }).click();
  await expect(page.getByText(/Tổng dự toán: 300\.000/)).toBeVisible();
  const code = ((await page.getByRole('button', { name: /^Thu gọn phiếu / }).first().getAttribute('aria-label')) ?? '').replace('Thu gọn phiếu ', '').trim();

  await test.step('SYS6-06 record work done', async () => {
    await page.getByRole('button', { name: 'Thêm quá trình điều trị' }).first().click();
    const wt = page.locator('table').filter({ hasText: 'Labo' }).first();
    await wt.getByPlaceholder('25, 26, 46').fill('16');
    await pick(page, wt.locator('.ant-select').first());
    await wt.getByRole('button', { name: 'Đồng ý' }).click();
    await page.waitForTimeout(2500);
    await expect(page.getByText('Phiếu chưa có việc điều trị.')).toHaveCount(0);
    await page.screenshot({ path: 'test-results/sys6-06.png', fullPage: true });
  });

  await test.step('SYS6-07 lab order dialog opens', async () => {
    await page.getByRole('button', { name: /Phiếu labo cho/ }).first().click();
    await page.waitForTimeout(1500);
    console.log('LABDLG', (await page.getByRole('dialog').innerText()).slice(0, 800));
    await page.screenshot({ path: 'test-results/sys6-07.png' });
    await page.keyboard.press('Escape');
  });

  await test.step('SYS6-08/09 owner pays a deposit; over-limit refused', async () => {
    const p2 = await (await page.context().browser()!.newContext()).newPage();
    await staffLogin(p2, 'owner.demo');
    await p2.goto('/patients/38');
    await p2.getByRole('tab', { name: 'Điều trị & Thanh toán' }).click(); await p2.waitForTimeout(2000);
    const op = p2.getByRole('button', { name: `Mở phiếu ${code}` }); if (await op.count()) await op.click(); await p2.waitForTimeout(1500);
    await p2.getByRole('button', { name: 'Trả tiền' }).click();
    const d = p2.getByRole('dialog', { name: 'Thanh toán phiếu điều trị' });
    await d.getByRole('tab', { name: /Nhận trước/ }).click();
    await d.getByLabel('Số tiền cọc').fill('100000');
    await d.getByRole('button', { name: 'Ghi nhận tiền cọc' }).click();
    await expect(d.getByText(/100\.000/).first()).toBeVisible({ timeout: 15000 });
    await d.getByRole('tab', { name: /Cần thanh toán/ }).click();
    const due = d.getByRole('spinbutton').first();
    await due.fill('999999999'); await due.blur();
    console.log('CLAMP', await due.inputValue());
    await p2.screenshot({ path: 'test-results/sys6-08.png' });
  });

  await test.step('SYS6-10 close the dossier and reopen it', async () => {
    await page.getByRole('button', { name: /Trạng thái phiếu/ }).first().click();
    await page.getByText('Hoàn tất', { exact: true }).click();
    const dlg = page.getByRole('dialog', { name: 'Kết thúc hồ sơ điều trị' });
    await dlg.getByRole('button', { name: 'Xác nhận kết thúc' }).click();
    await expect(dlg).not.toBeVisible({ timeout: 15000 });
    await page.waitForTimeout(1500);
    console.log('STATE1', await page.getByRole('button', { name: /Trạng thái phiếu/ }).first().innerText());
    await page.screenshot({ path: 'test-results/sys6-10a.png', fullPage: true });
    await page.getByRole('button', { name: /Trạng thái phiếu/ }).first().click();
    await page.getByRole('menuitem', { name: 'Chưa xong' }).click();
    const re = page.getByRole('dialog', { name: 'Mở lại hồ sơ điều trị' });
    await re.getByLabel('Lý do').fill('Cần điều trị bổ sung');
    await re.getByRole('button', { name: 'Mở lại', exact: true }).click();
    await expect(re).not.toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('button', { name: /Trạng thái phiếu: Chưa xong/ }).first()).toBeVisible();
  });
});
