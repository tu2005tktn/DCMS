import { test, expect, type Page } from '@playwright/test';

// Ngừng chi nhánh: danh sách nghĩa vụ chặn. docs/specs/S58-BRANCHES-AND-CHAIRS.md §3. API chi nhánh được mock.
const password = process.env.E2E_DEMO_PASSWORD;
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD from ignored .env.'); });

async function login(page: Page, user: string) {
  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill(user);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).not.toHaveURL(/\/sign-in/);
}

const branch = { id: 901, code: 'BR-TEST', name: 'Chi nhánh thử nghiệm', slotIntervalMinutes: 15, isActive: true, version: 1, chairCount: 1 };

test('deactivating a branch lists every blocker with a label and a link', async ({ page }) => {
  await page.route('**/api/settings/branches', route => route.request().method() === 'GET' ? route.fulfill({ json: [branch] }) : route.fallback());
  await page.route('**/api/settings/branches/901', route => route.request().method() === 'GET'
    ? route.fulfill({ json: { ...branch, address: '123 Cầu Giấy', phone: '0901234567', email: 'cs1@dcms.test', chairs: [] } })
    : route.fallback());
  await page.route('**/api/settings/branches/901/state-preview', route => route.fulfill({ json: { allowed: false, impact: 'Không thể ngừng chi nhánh vì còn 3 nhóm nghĩa vụ cần xử lý trước.', blockers: [
    { kind: 'Appointments', message: 'Còn 1 lịch hẹn tương lai chưa hủy tại chi nhánh.', count: 1, navigationUrl: '/appointments?branchId=901' },
    { kind: 'TreatmentDossiers', message: 'Còn 2 hồ sơ điều trị đang mở tại chi nhánh.', count: 2, navigationUrl: '/patients' },
    { kind: 'LabOrders', message: 'Còn 4 phiếu labo chưa giao bệnh nhân.', count: 4, navigationUrl: '/lab-orders' }] } }));
  await login(page, 'owner.demo');
  await page.goto('/settings/branches');
  await page.getByRole('button', { name: 'Ngừng', exact: true }).first().click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByText('Lịch hẹn:')).toBeVisible();
  await expect(dialog.getByText('Hồ sơ điều trị:')).toBeVisible();
  await expect(dialog.getByText('Phiếu labo:')).toBeVisible();
  await expect(dialog.getByText('Còn 4 phiếu labo chưa giao bệnh nhân.')).toBeVisible();
  await expect(dialog.getByRole('link', { name: 'Mở để xử lý' })).toHaveCount(3);
  await expect(dialog.locator('a[href="/lab-orders"]')).toHaveCount(1);

  await dialog.getByRole('button', { name: 'Close' }).click();
  await page.getByRole('button', { name: 'Sửa', exact: true }).first().click();
  const drawer = page.getByRole('dialog');
  await expect(drawer.getByLabel('Mã chi nhánh')).toHaveValue('BR-TEST');
  await expect(drawer.getByLabel('Tên chi nhánh')).toHaveValue('Chi nhánh thử nghiệm');
  await expect(drawer.getByLabel('Địa chỉ')).toHaveValue('123 Cầu Giấy');
  await expect(drawer.getByLabel('Điện thoại')).toHaveValue('0901234567');
  await expect(drawer.getByLabel('Email')).toHaveValue('cs1@dcms.test');
  await expect(drawer.getByText('Website')).toHaveCount(0);
  await expect(drawer.getByText('Múi giờ')).toHaveCount(0);
  await expect(drawer.getByText('Logo chi nhánh')).toHaveCount(0);
  await expect(drawer.getByText('Tài khoản nhận chuyển khoản')).toHaveCount(0);
});
