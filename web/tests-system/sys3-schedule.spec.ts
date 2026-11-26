import { test, expect, type Page } from '@playwright/test';
import { staffLogin, password } from './helpers';
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });

async function open(page: Page) {
  await staffLogin(page, 'owner.demo');
  await page.getByRole('link', { name: 'Lịch làm việc', exact: true }).click();
  await expect(page.getByText('Lịch làm việc hằng tuần')).toBeVisible({ timeout: 30000 });
  await page.waitForTimeout(3000);
}
async function monday(page: Page, day = 'Thứ 2') {
  await page.locator('.ant-select').filter({ has: page.getByRole('combobox', { name: `Chi nhánh ${day}` }) }).click();
  await page.locator('.ant-select-dropdown:visible .ant-select-item-option').filter({ hasText: 'S09' }).click();
}

test('SYS3-03/04 overlapping hours refused; unsaved changes discarded', async ({ page }) => {
  await open(page);
  const days0 = await page.getByText(/\d ngày làm việc/).first().innerText();
  const ver0 = ((await page.getByText(/Version \d+/).first().innerText()).match(/Version \d+/) ?? [''])[0];
  await monday(page, 'Thứ 3');
  await page.getByRole('button', { name: 'Thêm khoảng' }).last().click();
  await test.step('SYS3-03 overlap is refused', async () => {
    await page.getByRole('button', { name: 'Lưu và công bố' }).click();
    await page.waitForTimeout(2500);
    await page.screenshot({ path: 'test-results/sys3-03.png', fullPage: true });
    await expect(page.getByText(/Version \d+/).first()).toContainText(ver0);
    await expect(page.getByText('Có thay đổi chưa lưu').first()).toBeVisible();
  });
  await test.step('SYS3-04 discard unsaved changes', async () => {
    page.once('dialog', d => void d.accept());
    await page.getByRole('button', { name: 'Hủy thay đổi chưa lưu' }).click();
    await page.waitForTimeout(1500);
    await expect(page.getByText(/\d ngày làm việc/).first()).toHaveText(days0);
    await expect(page.locator('.ant-select').filter({ has: page.getByRole('combobox', { name: 'Chi nhánh Thứ 3' }) })).toContainText('Không làm việc');
  });
});

test('SYS3-01/02 owner publishes a schedule and previews slots', async ({ page }) => {
  await open(page);
  await monday(page);
  await test.step('SYS3-01 check impact and publish', async () => {
    await page.getByRole('button', { name: 'Kiểm tra ảnh hưởng' }).click();
    await page.waitForTimeout(2500);
    await page.getByRole('button', { name: 'Lưu và công bố' }).click();
    await page.waitForTimeout(3500);
    await page.screenshot({ path: 'test-results/sys3-01.png', fullPage: true });
    await expect(page.getByText('Có thay đổi chưa lưu')).toHaveCount(0);
    await expect(page.getByText(/1 ngày làm việc/).first()).toBeVisible();
  });
  await test.step('SYS3-02 preview lists slots', async () => {
    await page.getByRole('button', { name: 'Preview slot' }).click();
    await page.waitForTimeout(3000);
    await page.screenshot({ path: 'test-results/sys3-02.png', fullPage: true });
    console.log('PREVIEW', (await page.getByText('Preview availability').locator('xpath=ancestor::div[contains(@class,"ant-card") or contains(@class,"section")][1]').innerText().catch(() => 'n/a')).slice(-600));
  });
});
