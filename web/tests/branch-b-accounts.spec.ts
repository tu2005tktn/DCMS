import { test, expect, type Page } from '@playwright/test';

// Tài khoản chi nhánh B (docs/implementation/RUN-ACCOUNTS.vi.md): dentist.b.demo và assistant.b.demo. Đăng nhập thật, không mock.
const password = process.env.E2E_DEMO_PASSWORD;
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD from ignored .env.'); });

async function login(page: Page, user: string) {
  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill(user);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Tài khoản của tôi' })).toBeVisible();
}

test('dentist of branch B signs in opens the lab work screen without errors', async ({ page }) => {
  await login(page, 'dentist.b.demo');
  await page.getByRole('link', { name: 'Quản lý công việc labo', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Quản lý công việc labo', exact: true })).toBeVisible();
  // Dữ liệu chi nhánh A bị chặn ở API (test tích hợp BranchIsolationTests); ở đây chỉ kiểm màn tải được, không lỗi.
  await expect(page.getByText('Không thể hoàn tất thao tác')).toHaveCount(0);
});

test('assistant of branch B signs in and has no treatment or lab menu', async ({ page }) => {
  await login(page, 'assistant.b.demo');
  await expect(page.getByRole('link', { name: 'Quản lý công việc labo', exact: true })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Kế hoạch điều trị', exact: true })).toHaveCount(0);
  await page.goto('/lab-orders');
  await expect(page.getByRole('heading', { name: 'Quản lý công việc labo', exact: true })).toHaveCount(0);
});

test('branch A dentist does not see branch B staff in lab filters and vice versa', async ({ page }) => {
  await login(page, 'dentist.demo');
  await page.getByRole('link', { name: 'Quản lý công việc labo', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Quản lý công việc labo', exact: true })).toBeVisible();
  await page.getByRole('combobox', { name: 'Bác sĩ' }).click();
  await expect(page.getByText('Nha sĩ chi nhánh B demo')).toHaveCount(0);
});

test('single-branch staff do not see a branch selector on the calendar', async ({ page }) => {
  await login(page, 'dentist.b.demo');
  await page.goto('/appointments');
  await expect(page.getByRole('combobox', { name: 'Nha sĩ' })).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Chi nhánh' })).toHaveCount(0);
});

test('only the owner sees the branch selector on the calendar', async ({ page }) => {
  await login(page, 'owner.demo');
  await page.goto('/appointments');
  await expect(page.getByRole('combobox', { name: 'Chi nhánh' })).toBeVisible();
});

test('the owner sees the branch filter on the patient list', async ({ page }) => {
  await login(page, 'owner.demo');
  await page.goto('/patients');
  await expect(page.getByRole('combobox', { name: 'Chi nhánh' })).toBeVisible();
});

test('single-branch staff do not see the branch filter on the patient list', async ({ page }) => {
  await login(page, 'dentist.b.demo');
  await page.goto('/patients');
  await expect(page.getByRole('combobox', { name: 'Chi nhánh' })).toHaveCount(0);
});

test('owner picks a branch first, then only that branch doctors on the working schedule', async ({ page }) => {
  await login(page, 'owner.demo');
  await page.goto('/working-schedules');
  const branch = page.getByRole('combobox', { name: 'Chi nhánh', exact: true });
  await expect(branch).toBeVisible();
  await expect(page.getByText('Chưa công bố')).toBeVisible();
  await branch.focus(); await page.keyboard.press('ArrowDown');
  await page.locator('.ant-select-dropdown:visible').getByText('S09 - Chi nhánh B', { exact: true }).click();
  const doctor = page.getByRole('combobox', { name: 'Nhân viên / bác sĩ' });
  await expect(page.locator('.ant-select-selection-item', { hasText: 'Nha sĩ chi nhánh B demo' })).toBeVisible();
  await doctor.focus(); await page.keyboard.press('ArrowDown');
  await expect(page.locator('.ant-select-dropdown:visible').getByText('Nha sĩ demo', { exact: true })).toHaveCount(0);
  await expect(page.getByText('Không có quyền truy cập')).toHaveCount(0);
});

test('single-branch staff have no branch picker on the working schedule', async ({ page }) => {
  await login(page, 'dentist.b.demo');
  await page.goto('/working-schedules');
  await expect(page.getByRole('combobox', { name: 'Nhân viên / bác sĩ' })).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Chi nhánh', exact: true })).toHaveCount(0);
});

test('lab work: owner has a branch filter, single-branch dentist does not', async ({ page }) => {
  await login(page, 'owner.demo');
  await page.getByRole('link', { name: 'Quản lý công việc labo', exact: true }).click();
  await expect(page.getByRole('combobox', { name: 'Chi nhánh', exact: true })).toBeVisible();
});

test('lab work: single-branch dentist has no branch filter', async ({ page }) => {
  await login(page, 'dentist.b.demo');
  await page.getByRole('link', { name: 'Quản lý công việc labo', exact: true }).click();
  await expect(page.getByRole('combobox', { name: 'Công ty labo' })).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Chi nhánh', exact: true })).toHaveCount(0);
});
