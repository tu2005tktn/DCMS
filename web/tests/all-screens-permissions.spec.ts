import { expect, test, type Page } from '@playwright/test';

const id = '11111111-1111-4111-8111-111111111111';
const routes = [
  '/appointments', `/appointments/${id}`, '/working-schedules', '/booking-settings',
  '/patients', '/patients/1', '/patients/1/documents', '/reception', `/reception/appointments/${id}`,
  `/visits/${id}`, `/visits/${id}/prescription`, `/visits/${id}/payments`,
  `/patients/1/dossiers/${id}`, `/patients/1/dossiers/${id}/plan`,
  `/patients/1/dossiers/${id}/plan/revise/${id}`, '/treatment-templates', '/commercial/treatment',
  '/payments', `/payments/${id}/print`, '/clinical-services', '/accounts', '/system-settings',
  '/link-verification', '/support-inbox', '/materials', '/revenue', '/work-statistics',
];
async function prepare(page: Page, permissions: string[] = [], data: Record<string, unknown> = {}) {
  const state = { permissions, calls: [] as string[] };
  await page.addInitScript(() => sessionStorage.setItem('dcms.web.access-token', 'synthetic'));
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    state.calls.push(path);
    if (path === '/api/auth/me') return route.fulfill({ json: { id: 7, accountType: 'Staff', userName: 'synthetic', fullName: 'SYNTHETIC staff', roles: ['ClinicOwner', 'Admin', 'Dentist'], effectiveRoles: ['ClinicOwner', 'Admin', 'Dentist'], permissions: state.permissions } });
    if (path === '/api/auth/session/idle') return route.fulfill({ json: { deadlineAt: new Date(Date.now() + 1200000).toISOString(), serverNow: new Date().toISOString(), activityVersion: 1 } });
    return route.fulfill({ json: data[path] ?? [] });
  });
  return state;
}
for (const path of routes) test(`direct staff route requires its grant: ${path}`, async ({ page }) => {
  const state = await prepare(page);
  await page.goto(path);
  await expect(page.getByText('Bạn không có quyền thực hiện thao tác này.', { exact: true })).toBeVisible();
  expect(state.calls.filter(path => !path.startsWith('/api/auth/'))).toEqual([]);
});

test('every sidebar entry follows grants, even for an owner role', async ({ page }) => {
  const menu = [
    ['appointments.read', 'Lịch hẹn'], ['working-schedules.read', 'Lịch làm việc'],
    ['booking-settings.read', 'Cài đặt đặt lịch'], ['patients.read', 'Bệnh nhân'],
    ['reception.read', 'Tiếp nhận và hàng chờ'], ['appointments.support.handle', 'Hộp thư hỗ trợ'],
    ['treatment.templates.read', 'Kế hoạch điều trị'], ['treatment.prices.read', 'Giá và duyệt điều trị'],
    ['services.read', 'Dịch vụ cận lâm sàng'], ['finance.read', 'Thanh toán'],
    ['accounts.read', 'Quản lý tài khoản'], ['settings.read', 'Thiết lập hệ thống'],
    ['patient-links.read', 'Xác minh liên kết hồ sơ'],
    ['materials.stock.read', 'Dụng cụ - vật liệu'], ['finance.revenue.read', 'Doanh thu'],
    ['finance.work-statistics.read', 'Thống kê công việc/dịch vụ'],
  ];
  const state = await prepare(page);
  await page.goto('/');
  const sidebar = page.locator('.staff-sidebar');
  for (const [, label] of menu) await expect(sidebar.getByRole('link', { name: label, exact: true })).toHaveCount(0);
  state.permissions = menu.map(([grant]) => grant);
  await page.getByRole('button', { name: 'Cập nhật quyền', exact: true }).click();
  for (const [, label] of menu) await expect(sidebar.getByRole('link', { name: label, exact: true })).toBeVisible();
  state.permissions = ['appointments.read'];
  await page.getByRole('button', { name: 'Cập nhật quyền', exact: true }).click();
  for (const [grant, label] of menu) await expect(sidebar.getByRole('link', { name: label, exact: true })).toHaveCount(grant === 'appointments.read' ? 1 : 0);
});

test('booking settings reader can inspect but cannot change or save configuration', async ({ page }) => {
  await prepare(page, ['booking-settings.read'], {
    '/api/booking-settings/options': [{ id: 1, name: 'SYNTHETIC branch' }],
    '/api/booking-settings/1': { branchId: 1, slotIntervalMinutes: 15, version: 1, services: [] },
  });
  await page.goto('/booking-settings');
  await expect(page.getByRole('spinbutton')).toBeDisabled();
  await expect(page.getByRole('button', { name: /Lưu/ })).toBeDisabled();
});

for (const [permission, tab, endpoint] of [
  ['materials.purchases.read', 'Nhập hàng', 'material-purchases'],
  ['materials.stock.read', 'Tồn kho', 'material-stock'],
  ['materials.usage.read', 'Lịch sử xuất dùng', 'material-usage'],
]) test(`material tab only loads its own authorized data: ${tab}`, async ({ page }) => {
  const data = { items: [], total: 0, page: 1, pageSize: 20, totals: { totalVnd: 0, paidVnd: 0, dueVnd: 0 } };
  const state = await prepare(page, ['settings.read', 'catalog.materials.read', permission], {
    [`/api/system-settings/${endpoint}`]: data,
    '/api/system-settings/materials': { items: [], total: 0 },
  });
  await page.goto('/materials');
  await expect(page.getByRole('tab')).toHaveCount(1);
  await expect(page.getByRole('tab', { name: tab, exact: true })).toBeVisible();
  await expect.poll(() => state.calls).toContain(`/api/system-settings/${endpoint}`);
  for (const other of ['material-purchases', 'material-stock', 'material-usage'].filter(value => value !== endpoint))
    expect(state.calls).not.toContain(`/api/system-settings/${other}`);
  if (tab === 'Nhập hàng') await expect(page.getByRole('button', { name: 'Thêm mới', exact: true })).toBeDisabled();
});

test('old parent grants keep their screens and do not expose new reports or warehouse', async ({ page }) => {
  await prepare(page, ['finance.read', 'settings.read', 'catalog.materials.read']);
  await page.goto('/');
  const sidebar = page.locator('.staff-sidebar');
  await expect(sidebar.getByRole('link', { name: 'Thanh toán', exact: true })).toBeVisible();
  for (const label of ['Doanh thu', 'Thống kê công việc/dịch vụ', 'Dụng cụ - vật liệu'])
    await expect(sidebar.getByRole('link', { name: label, exact: true })).toHaveCount(0);
});

test('treatment template reader can inspect templates without writing', async ({ page }) => {
  const state = await prepare(page, ['treatment.templates.read'], {
    '/api/appointments/options': { branches: [{ id: 1, name: 'SYNTHETIC branch' }], dentists: [], chairs: [], services: [] },
    '/api/treatment-templates/item-plans': { items: [], total: 0, page: 1, pageSize: 20 },
    '/api/treatment-templates/item-plans/options': { services: [], members: [] },
  });
  await page.goto('/treatment-templates');
  await expect(page.getByRole('heading', { name: 'Kế hoạch điều trị', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Tạo kế hoạch', exact: true })).toBeDisabled();
  await expect.poll(() => state.calls).toContain('/api/treatment-templates/item-plans');
});
