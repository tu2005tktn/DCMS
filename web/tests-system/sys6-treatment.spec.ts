import { test, expect, type Page } from '@playwright/test';
import { staffLogin, password, suffix } from './helpers';

test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });

async function pickFirst(page: Page, combo: ReturnType<Page['getByRole']>) {
  await combo.click();
  await page.waitForTimeout(400);
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Enter');
}

async function newPatient(page: Page) {
  await page.getByRole('link', { name: 'Bệnh nhân', exact: true }).click();
  await page.getByRole('button', { name: 'Thêm hồ sơ', exact: true }).click();
  const form = page.getByRole('dialog');
  await form.getByLabel('Họ tên', { exact: true }).fill(`Điều Trị ${suffix()}`);
  await form.getByLabel('Ngày sinh', { exact: true }).fill('1992-04-05');
  await form.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(page.getByRole('tab', { name: 'Điều trị & Thanh toán' })).toBeVisible();
}

test('SYS6-01 dentist opens a treatment dossier', async ({ page }) => {
  await staffLogin(page, 'dentist.demo');
  await page.goto('/patients/38');
  await page.getByRole('tab', { name: 'Điều trị & Thanh toán' }).click();
  await page.waitForTimeout(1500);
  const before = await page.getByText('Ngày lập phiếu').count();
  const resp = page.waitForResponse(r => r.request().method() === 'POST' && /dossiers\/quick/.test(r.url()));
  await page.getByRole('button', { name: /tạo phiếu mới/i }).click();
  expect((await resp).status()).toBe(200);
  await expect(page.getByText('Ngày lập phiếu')).toHaveCount(before + 1);
  await page.reload();
  await page.getByRole('tab', { name: 'Điều trị & Thanh toán' }).click();
  await expect(page.getByText('Ngày lập phiếu')).toHaveCount(before + 1);
});

test('SYS6-05 template is created and applied to a dossier', async ({ page }) => {
  const s = suffix(); const tpl = `Mẫu HT ${s}`;
  await staffLogin(page, 'dentist.demo');
  await page.goto('/treatment-templates');
  await page.getByRole('button', { name: 'Tạo kế hoạch', exact: true }).click();
  const modal = page.getByRole('dialog', { name: 'Tạo kế hoạch điều trị' });
  await modal.getByLabel('Tên kế hoạch', { exact: true }).fill(tpl);
  await modal.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(modal).not.toBeVisible();
  await page.getByRole('button', { name: '+ Thêm hạng mục', exact: true }).click();
  const row = page.getByRole('row', { name: 'Thêm hạng mục', exact: true });
  await row.getByLabel('Răng', { exact: true }).fill('25');
  await pickFirst(page, row.getByRole('combobox', { name: 'Điều trị/dịch vụ', exact: true }));
  await pickFirst(page, row.getByRole('combobox', { name: 'Bác sĩ', exact: true }));
  await row.getByLabel('Thời gian dự kiến (phút)').fill('30');
  await row.getByRole('button', { name: 'Đồng ý', exact: true }).click();
  await expect(page.locator('.item-plan-table tbody')).toContainText('25');
  await page.goto('/patients/38');
  await page.getByRole('tab', { name: 'Điều trị & Thanh toán' }).click();
  await page.waitForTimeout(1500);
  const n = await page.getByText('Ngày lập phiếu').count();
  await page.getByRole('button', { name: /tạo phiếu mới/i }).click();
  await expect(page.getByText('Ngày lập phiếu')).toHaveCount(n + 1);
  await page.getByRole('button', { name: 'Kế hoạch điều trị', exact: true }).first().click();
  const dlg = page.getByRole('dialog', { name: 'Chọn kế hoạch điều trị' });
  await dlg.getByRole('combobox').click();
  await dlg.getByRole('combobox').fill(tpl);
  await page.locator('.ant-select-dropdown:visible').getByText(`${tpl} · 1 hạng mục`, { exact: true }).click();
  await dlg.getByRole('button', { name: 'Áp dụng', exact: true }).click();
  await expect(dlg).not.toBeVisible();
  await expect(page.getByText('Chưa có hạng mục.')).toHaveCount(0);
});
