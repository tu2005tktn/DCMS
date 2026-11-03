import { expect, test, type Page } from '@playwright/test';

const content = { symptoms: 'SYNTHETIC đau răng', medicalHistory: { detail: 'SYNTHETIC tiền sử' }, allergies: {}, medications: {},
  findings: [{ tooth: 16, condition: 'SYNTHETIC sâu mặt nhai', surfaces: ['O'], diagnoses: ['Sâu răng'] }],
  advice: 'SYNTHETIC vệ sinh răng', disposition: 'FollowUp', returnNeed: 'SYNTHETIC quay lại tuần sau', notes: 'SYNTHETIC ghi chú' };
const row = (code: string, status: string) => ({ visitId: code, appointmentCode: code, branchId: 1, branchName: 'Chi nhánh thử', dentistName: 'Bác sĩ thử',
  arrivalAt: '2026-11-02T08:00:00Z', visitStatus: status, completedAt: status === 'Completed' ? '2026-11-02T09:00:00Z' : null,
  examination: { content, confirmed: true, version: 1 } });

async function prepare(page: Page, fail = false) {
  const state = { rows: [row('AP-OPEN', 'InProgress'), row('AP-DONE', 'Completed')], requests: 0, fail };
  await page.addInitScript(() => sessionStorage.setItem('dcms.web.access-token', 'synthetic'));
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/auth/me') return route.fulfill({ json: { id: 7, accountType: 'Staff', fullName: 'SYNTHETIC bác sĩ', roles: ['Dentist'], effectiveRoles: ['Dentist'], permissions: ['patients.read', 'examinations.read'] } });
    if (path === '/api/auth/session/idle') return route.fulfill({ json: { serverNow: new Date().toISOString(), deadlineAt: new Date(Date.now() + 1200000).toISOString(), activityVersion: 1 } });
    if (path === '/api/patients/1') return route.fulfill({ json: { id: 1, fullName: 'SYNTHETIC bệnh nhân', patientCode: 'BN-SYN', birthDate: null, gender: null, link: {}, intakeHistory: [] } });
    if (path === '/api/patients/1/examinations') {
      state.requests++;
      return state.fail ? route.fulfill({ status: 503, json: { code: 'MSG144', message: 'SYNTHETIC không tải được lịch sử khám' } }) : route.fulfill({ json: state.rows });
    }
    return route.fulfill({ status: 404, json: { code: 'MSG113', message: 'Không tìm thấy.' } });
  });
  await page.goto('/patients/1?tab=examinations');
  return state;
}

test('patient history distinguishes confirmed examination from completed visit and exposes clinical content', async ({ page }) => {
  await prepare(page);
  const history = page.getByRole('region', { name: 'Phiếu khám của bệnh nhân' });
  await expect(history.getByRole('row').filter({ hasText: 'AP-OPEN' })).toContainText('Đang khám');
  await expect(history.getByRole('row').filter({ hasText: 'AP-OPEN' })).not.toContainText('Đã kết thúc lượt khám');
  await history.getByRole('combobox', { name: 'Trạng thái phiếu khám' }).press('ArrowDown');
  await page.locator('.ant-select-dropdown:visible').getByText('Đã kết thúc lượt khám', { exact: true }).click();
  await expect(history.getByRole('row').filter({ hasText: 'AP-OPEN' })).toHaveCount(0);
  await expect(history.getByRole('row').filter({ hasText: 'AP-DONE' })).toContainText('16:00');
  await history.getByRole('button', { name: 'Xem nội dung phiếu AP-DONE' }).click();
  const detail = page.getByRole('dialog');
  await expect(detail.getByText('SYNTHETIC đau răng', { exact: true })).toBeVisible();
  await expect(detail.getByText('Răng 16 (O): Sâu răng · SYNTHETIC sâu mặt nhai', { exact: true })).toBeVisible();
  await expect(detail.getByText('SYNTHETIC vệ sinh răng', { exact: true })).toBeVisible();
  await expect(detail.getByText('SYNTHETIC quay lại tuần sau', { exact: true })).toBeVisible();
  await expect(detail.getByRole('link', { name: 'Mở phiếu khám gốc' })).toHaveAttribute('href', '/visits/AP-DONE');
});

test('returning to the window and reactivating the tab refresh newly completed visits', async ({ page }) => {
  const state = await prepare(page);
  await expect(page.getByRole('row').filter({ hasText: 'AP-OPEN' })).toContainText('Đang khám');
  state.rows[0] = row('AP-OPEN', 'Completed');
  const loaded = page.waitForResponse(r => r.url().endsWith('/api/patients/1/examinations'));
  await page.evaluate(() => window.dispatchEvent(new Event('focus'))); await loaded;
  await expect(page.getByRole('row').filter({ hasText: 'AP-OPEN' })).toContainText('Đã kết thúc lượt khám');
  await page.getByRole('tab', { name: 'Thông tin cơ bản', exact: true }).click();
  const before = state.requests; await page.getByRole('tab', { name: 'Khám', exact: true }).click();
  await expect.poll(() => state.requests).toBeGreaterThan(before);
  const beforeReload = state.requests; await page.getByRole('button', { name: 'Tải lại', exact: true }).click();
  await expect.poll(() => state.requests).toBeGreaterThan(beforeReload);
});

test('failed history load shows an error and retry restores the saved history', async ({ page }) => {
  const state = await prepare(page, true);
  await expect(page.getByText('SYNTHETIC không tải được lịch sử khám', { exact: true })).toBeVisible();
  state.fail = false; await page.getByRole('button', { name: 'Thử lại', exact: true }).click();
  await expect(page.getByRole('row').filter({ hasText: 'AP-DONE' })).toContainText('Đã kết thúc lượt khám');
});
