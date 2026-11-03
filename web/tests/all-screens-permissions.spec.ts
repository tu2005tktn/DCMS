import { expect, test, type Page } from '@playwright/test';

const id = '11111111-1111-4111-8111-111111111111';
const routes = [
  '/appointments', `/appointments/${id}`, '/working-schedules', '/booking-settings',
  '/patients', '/patients/1', '/patients/1/documents', '/reception', `/reception/appointments/${id}`,
  `/visits/${id}`, `/visits/${id}/prescription`, `/visits/${id}/payments`,
  `/patients/1/dossiers/${id}`, `/patients/1/dossiers/${id}/plan`,
  `/patients/1/dossiers/${id}/plan/revise/${id}`, '/treatment-templates', '/commercial/treatment',
  '/payments', `/payments/${id}/print`, '/clinical-services', '/accounts', '/system-settings',
  '/link-verification', '/support-inbox',
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
    ['treatment.templates.read', 'Mẫu điều trị'], ['treatment.prices.read', 'Giá và duyệt điều trị'],
    ['services.read', 'Dịch vụ cận lâm sàng'], ['finance.read', 'Thanh toán'],
    ['accounts.read', 'Quản lý tài khoản'], ['settings.read', 'Thiết lập hệ thống'],
    ['patient-links.read', 'Xác minh liên kết hồ sơ'],
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

test('treatment template reader can inspect templates without writing', async ({ page }) => {
  const state = await prepare(page, ['treatment.templates.read'], {
    '/api/appointments/options': { branches: [{ id: 1, name: 'SYNTHETIC branch' }], dentists: [], chairs: [], services: [] },
    '/api/treatment-templates': [],
  });
  await page.goto('/treatment-templates');
  await expect(page.getByRole('heading', { name: 'Mẫu điều trị S58', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Lưu bản nháp', exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Tạo mẫu', exact: true })).toBeDisabled();
  expect(state.calls).toContain('/api/treatment-templates');
});
