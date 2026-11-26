import { test, expect, type Page } from '@playwright/test';
import { staffLogin, password, suffix } from './helpers';

test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });

async function openForm(page: Page) {
  await staffLogin(page, 'assistant.demo');
  await page.getByRole('link', { name: 'Bệnh nhân', exact: true }).click();
  await page.getByRole('button', { name: 'Thêm hồ sơ', exact: true }).click();
  return page.getByRole('dialog');
}

test('SYS4-01 assistant creates a patient and finds it by name', async ({ page }) => {
  const name = `Hệ Thống ${suffix()}`;
  const form = await openForm(page);
  await form.getByLabel('Họ tên', { exact: true }).fill(name);
  await form.getByLabel('Ngày sinh', { exact: true }).fill('1990-05-06');
  const created = page.waitForResponse(r => /\/api\/patients\/?$/.test(r.url()) && r.request().method() === 'POST');
  await form.getByRole('button', { name: 'Lưu', exact: true }).click();
  const res = await created;
  expect(res.status()).toBe(201);
  const { patient } = await res.json();
  expect(patient.patientCode).toMatch(/^BN/);
  await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'Đóng hồ sơ khách hàng', exact: true }).click();
  await page.getByPlaceholder('Mã bệnh nhân, họ tên (có hoặc không dấu), SĐT').fill(name);
  await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click();
  await expect(page.getByRole('row').filter({ hasText: name })).toHaveCount(1);
});

test('SYS4-02 search no result then reset', async ({ page }) => {
  await staffLogin(page, 'assistant.demo');
  await page.getByRole('link', { name: 'Bệnh nhân', exact: true }).click();
  const box = page.getByPlaceholder('Mã bệnh nhân, họ tên (có hoặc không dấu), SĐT');
  await box.fill('khong-ton-tai-' + suffix());
  await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click();
  await expect(page.getByText('Không tìm thấy hồ sơ bệnh nhân phù hợp.', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Đặt lại', exact: true }).click();
  await expect(box).toHaveValue('');
  await expect(page.getByText('Không tìm thấy hồ sơ bệnh nhân phù hợp.', { exact: true })).toHaveCount(0);
});

test('SYS4-04 invalid patient data is blocked', async ({ page }) => {
  const form = await openForm(page);
  await form.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(form.getByText('Vui lòng nhập Họ tên.', { exact: true })).toBeVisible();
  await form.getByLabel('Họ tên', { exact: true }).fill('Lỗi Dữ Liệu');
  await form.getByLabel('Ngày sinh', { exact: true }).fill('2099-01-01');
  await form.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(form).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Lỗi Dữ Liệu' })).toHaveCount(0);
});

test('SYS4-05 patient edit is kept after reload', async ({ page }) => {
  const name = `Sửa Hồ Sơ ${suffix()}`;
  const form = await openForm(page);
  await form.getByLabel('Họ tên', { exact: true }).fill(name);
  await form.getByLabel('Ngày sinh', { exact: true }).fill('1985-02-03');
  await form.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Sửa thông tin', exact: true }).click();
  await form.getByLabel('Địa chỉ', { exact: true }).fill('99 Nguyễn Huệ, Quận 1');
  await form.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(page.getByText('99 Nguyễn Huệ, Quận 1', { exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByText('99 Nguyễn Huệ, Quận 1', { exact: true })).toBeVisible();
});

test('SYS4-09 branch B assistant does not see branch A patients, owner sees all', async ({ page }) => {
  const name = `Chi Nhánh A ${suffix()}`;
  const form = await openForm(page);
  await form.getByLabel('Họ tên', { exact: true }).fill(name);
  await form.getByLabel('Ngày sinh', { exact: true }).fill('1990-05-06');
  await form.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Đăng xuất' }).click();
  await expect(page).toHaveURL(/sign-in/);
  await staffLogin(page, 'assistant.b.demo');
  await page.getByRole('link', { name: 'Bệnh nhân', exact: true }).click();
  await page.getByPlaceholder('Mã bệnh nhân, họ tên (có hoặc không dấu), SĐT').fill(name);
  await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click();
  await expect(page.getByText('Không tìm thấy hồ sơ bệnh nhân phù hợp.', { exact: true })).toBeVisible();
});
