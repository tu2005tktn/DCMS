import { test, expect } from '@playwright/test';
import { staffLogin, password } from './helpers';
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });

test('SYS8-06 assistant marks a care task done', async ({ page }) => {
  await staffLogin(page, 'assistant.demo');
  await page.getByRole('link', { name: 'Chăm sóc khách hàng', exact: true }).click();
  const open = page.getByText('Gọi nhắc tái khám').first();
  await expect(open).toBeVisible({ timeout: 20000 });
  const total = await page.getByText(/Tìm thấy \d+ phiếu/).innerText();
  await open.click();
  const d = page.getByRole('dialog');
  await d.getByRole('button', { name: 'Đánh dấu đã xong' }).click();
  await page.waitForTimeout(1500);
  const confirm = page.getByRole('button', { name: /Xác nhận|Đồng ý|Hoàn tất/ });
  if (await confirm.count()) await confirm.last().click();
  await page.waitForTimeout(2500);
  await page.screenshot({ path: 'test-results/sys8-06.png' });
  console.log('STATUS', await page.locator('#staff-content, body').first().innerText().then(t => t.match(/Trạng thái\s+\S+/)?.[0]));
  console.log('TOTAL', total);
});

test('SYS7-06 owner edits booking settings and saves', async ({ page }) => {
  await staffLogin(page, 'owner.demo');
  await page.getByRole('link', { name: 'Cài đặt đặt lịch', exact: true }).click();
  await page.getByRole('button', { name: 'Thêm dịch vụ' }).waitFor({ timeout: 30000 });
  await page.waitForTimeout(2000);
  await page.getByRole('button', { name: 'Lưu cài đặt' }).click();
  await page.waitForTimeout(3000);
  await page.screenshot({ path: 'test-results/sys7-06.png', fullPage: true });
  console.log('MSG', JSON.stringify(await page.locator('.ant-message, .ant-alert, [role=alert]').allInnerTexts()));
});
