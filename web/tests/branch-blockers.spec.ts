import { test, expect, type Page } from '@playwright/test';

// Ngừng chi nhánh: danh sách nghĩa vụ chặn. docs/specs/S58-BRANCHES-AND-CHAIRS.md §3. API chi nhánh được mock.
const password = process.env.E2E_DEMO_PASSWORD;
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD from ignored .env.'); });

async function login(page: Page, user: string) {
  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill(user);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Tài khoản của tôi' })).toBeVisible();
}

const branch = { id: 901, code: 'BR-TEST', name: 'Chi nhánh thử nghiệm', timeZoneId: 'Asia/Ho_Chi_Minh', slotIntervalMinutes: 15, bookingSettingsVersion: 1, isActive: true, version: 1, chairCount: 1 };

test('deactivating a branch lists every blocker with a label and a link', async ({ page }) => {
  await page.route('**/api/settings/branches', route => route.request().method() === 'GET' ? route.fulfill({ json: [branch] }) : route.fallback());
  await page.route('**/api/settings/branches/901/state-preview', route => route.fulfill({ json: { allowed: false, impact: 'Không thể ngừng chi nhánh vì còn 3 nhóm nghĩa vụ cần xử lý trước.', blockers: [
    { kind: 'PayOsOrders', message: 'Còn 1 đơn chuyển khoản PayOS chưa hoàn tất.', count: 1, navigationUrl: '/payments?branchId=901' },
    { kind: 'TreatmentDossiers', message: 'Còn 2 hồ sơ điều trị đang mở tại chi nhánh.', count: 2, navigationUrl: '/patients' },
    { kind: 'LabOrders', message: 'Còn 4 phiếu labo chưa giao bệnh nhân.', count: 4, navigationUrl: '/lab-orders' }] } }));
  await login(page, 'owner.demo');
  await page.goto('/settings/branches');
  await page.getByRole('button', { name: 'Ngừng', exact: true }).first().click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByText('Chuyển khoản PayOS:')).toBeVisible();
  await expect(dialog.getByText('Hồ sơ điều trị:')).toBeVisible();
  await expect(dialog.getByText('Phiếu labo:')).toBeVisible();
  await expect(dialog.getByText('Còn 4 phiếu labo chưa giao bệnh nhân.')).toBeVisible();
  await expect(dialog.getByRole('link', { name: 'Mở để xử lý' })).toHaveCount(3);
  await expect(dialog.locator('a[href="/lab-orders"]')).toHaveCount(1);
});
