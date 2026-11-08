import { test, expect, type Page } from '@playwright/test';

// Quản lý công việc labo: docs/specs/LAB-ORDER-MANAGEMENT.md. API được mock; chỉ đăng nhập thật.
const password = process.env.E2E_DEMO_PASSWORD;
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD from ignored .env.'); });

async function login(page: Page, user: string) {
  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill(user);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Tài khoản của tôi' })).toBeVisible();
}

const item = (over: Record<string, unknown> = {}) => ({ id: 'a1', code: 'LB-001', dossierId: 'd1', dossierCode: 'HS-1', activityId: 'ac1', patientId: 7, patientCode: 'BN-007',
  patientName: 'Nguyễn Văn An', workName: 'Mão sứ Zirconia', labProviderId: 'l1', labName: 'Xưởng Hoa Mai', dentistId: 3, dentistName: 'BS Minh', orderedOn: '2026-11-01',
  dueOn: '2026-11-03', sentOn: null, receivedOn: null, deliveredOn: null, quantity: 2, material: 'Zirconia', teeth: ['36', '37'], note: 'Gấp', totalFeeVnd: 3000000,
  paidVnd: 1000000, status: 'NotSent', overdue: true, version: 1, ...over });
const counts = { notSent: 1, sent: 0, received: 0, delivered: 0, rework: 0, overdue: 1 };

test('lab work screen filters, opens the drawer and changes status', async ({ page }) => {
  const queries: string[] = []; let current = item(); const log = [{ fromStatus: null, toStatus: 'NotSent', changedAt: '2026-11-01T03:00:00Z', changedByName: 'BS Minh', note: null }];
  await page.route('**/api/lab-orders?**', route => {
    queries.push(new URL(route.request().url()).search);
    return route.fulfill({ json: { items: [current], total: 1, page: 1, pageSize: 20, counts, labs: [{ id: 'l1', name: 'Xưởng Hoa Mai' }], dentists: [{ id: 3, name: 'BS Minh' }] } });
  });
  await page.route('**/api/lab-orders/a1/history', route => route.fulfill({ json: log }));
  await page.route('**/api/lab-orders/a1/status', async route => {
    const body = route.request().postDataJSON() as { status: string; note: string | null };
    current = item({ status: body.status, sentOn: '2026-11-08', overdue: true, version: 2 });
    log.unshift({ fromStatus: 'NotSent', toStatus: body.status, changedAt: '2026-11-08T03:00:00Z', changedByName: 'BS Minh', note: body.note });
    await route.fulfill({ json: current });
  });
  await login(page, 'dentist.demo');
  await page.getByRole('link', { name: 'Quản lý công việc labo', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Quản lý công việc labo', exact: true })).toBeVisible();
  await expect(page.getByText('Mão sứ Zirconia')).toBeVisible();
  await expect(page.getByText('(quá hạn)')).toBeVisible();

  await page.getByRole('button', { name: /^Quá hạn hẹn/ }).click();
  await expect.poll(() => queries.at(-1)).toContain('status=Overdue');
  await page.getByRole('button', { name: /^Quá hạn hẹn/ }).click();
  await page.getByLabel('Tìm phiếu labo').fill('Zirconia'); await page.getByLabel('Tìm phiếu labo').press('Enter');
  await expect.poll(() => queries.at(-1)).toContain('q=Zirconia');
  await page.getByRole('button', { name: 'Đặt lại' }).click();

  await page.getByRole('button', { name: /Mở phiếu labo Mão sứ Zirconia/ }).click();
  const drawer = page.getByRole('dialog');
  await expect(drawer.getByText('Nhật ký trạng thái')).toBeVisible();
  await drawer.getByLabel('Ghi chú đổi trạng thái').fill('Gửi xưởng Hoa Mai');
  await drawer.getByRole('button', { name: 'Gửi xưởng', exact: true }).click();
  await expect(drawer.getByText('Gửi xưởng Hoa Mai')).toBeVisible();
  await expect(drawer.getByRole('button', { name: 'Đã nhận về', exact: true })).toBeVisible();
  await expect(drawer.getByRole('button', { name: 'Yêu cầu làm lại', exact: true })).toBeVisible();
});

test('lab work cards show every field when narrow', async ({ page }) => {
  await page.route('**/api/lab-orders?**', route => route.fulfill({ json: { items: [item()], total: 1, page: 1, pageSize: 20, counts, labs: [], dentists: [] } }));
  await page.setViewportSize({ width: 600, height: 900 });
  await login(page, 'dentist.demo');
  await page.goto('/lab-orders');
  const cell = page.locator('.lab-work-table td.ant-table-cell').first();
  await expect(cell).toBeVisible();
  const labels = await page.locator('.lab-work-table tbody tr.ant-table-row').first().locator('td').evaluateAll(tds => tds.map(td => td.getAttribute('data-label')));
  expect(labels).toEqual(['Mã phiếu', 'Bệnh nhân', 'Công việc', 'Công ty labo', 'Hẹn', 'Phí / Đã trả', 'Trạng thái']);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});
