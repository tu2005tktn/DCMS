import { expect, test, type Page } from '@playwright/test';

async function prepare(page: Page, canReadAppointments = true) {
  const state = { fail: false, cancelled: false, calls: [] as URL[], writes: 0 };
  await page.addInitScript(() => sessionStorage.setItem('dcms.web.access-token', 'synthetic'));
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url()), path = url.pathname;
    const ok = (json: unknown) => route.fulfill({ json });
    if (route.request().method() !== 'GET') state.writes++;
    if (path === '/api/auth/me') return ok({ id: 7, userName: 'synthetic', fullName: 'SYNTHETIC dentist', accountType: 'Staff', roles: ['Dentist'], effectiveRoles: ['Dentist'],
      permissions: ['working-schedules.read', ...canReadAppointments ? ['appointments.read'] : []] });
    if (path === '/api/auth/session/idle') return ok({ deadlineAt: new Date(Date.now() + 1200000).toISOString(), serverNow: new Date().toISOString(), activityVersion: 1 });
    if (path === '/api/working-schedules/options') return ok({ branches: [{ id: 1, name: 'SYNTHETIC branch' }], employees: [{ id: 7, branchId: 1, name: 'SYNTHETIC dentist', self: true, canView: true, canEdit: false, canDentist: true }], services: [], chairs: [] });
    if (path === '/api/working-schedules/recurring') return ok({ dentistId: 7, version: 0, template: null, exception: null, futureChanges: [], legacyWeek: [] });
    if (path === '/api/working-schedules/appointments') {
      state.calls.push(url);
      if (state.fail) return route.fulfill({ status: 503, json: { code: 'MSG144', message: 'SYNTHETIC cannot load appointments' } });
      return ok({ items: [
        { id: 'plan-1', code: 'AP-PLAN-1', patientName: 'SYNTHETIC An', serviceName: 'Trám răng', branchId: 1, branchName: 'SYNTHETIC branch', startAt: '2032-01-05T17:30:00Z', endAt: '2032-01-05T18:00:00Z', status: state.cancelled ? 'Cancelled' : 'Confirmed', isTreatmentPlan: true, patientConsentConfirmedAt: '2032-01-01T01:00:00Z' },
        { id: 'plan-2', code: 'AP-PLAN-2', patientName: 'SYNTHETIC Bình', serviceName: 'Bọc sứ', branchId: 1, branchName: 'SYNTHETIC branch', startAt: '2032-01-06T02:00:00Z', endAt: '2032-01-06T02:30:00Z', status: 'Confirmed', isTreatmentPlan: true, patientConsentConfirmedAt: null },
      ] });
    }
    return ok({});
  });
  await page.goto('/working-schedules');
  await page.getByLabel('Xem lịch hẹn theo ngày', { exact: true }).fill('2032-01-05');
  await expect(page.getByText('AP-PLAN-1', { exact: true })).toBeVisible();
  return state;
}

test('working schedule shows appointments, agreement, clinic-local dates and safe weekly navigation', async ({ page }) => {
  const state = await prepare(page);
  const panel = page.locator('[aria-label="Lịch hẹn của bác sĩ trong tuần"]');
  await expect(panel.getByText('Đã đồng ý', { exact: true })).toBeVisible();
  await expect(panel.getByText('Chưa ghi nhận', { exact: true })).toBeVisible();
  await expect(panel.getByRole('row').filter({ hasText: 'AP-PLAN-1' })).toContainText('06/01/2032');
  await expect(panel.getByRole('row').filter({ hasText: 'AP-PLAN-1' })).toContainText('00:30–01:00');
  await expect(panel.getByRole('link', { name: 'AP-PLAN-1', exact: true })).toHaveAttribute('href', '/appointments/plan-1');
  await panel.getByRole('button', { name: 'Tuần sau của lịch hẹn' }).click();
  await expect.poll(() => state.calls.at(-1)?.searchParams.get('from')).toBe('2032-01-12');
  await expect.poll(() => state.calls.at(-1)?.searchParams.get('to')).toBe('2032-01-18');
  expect(state.writes).toBe(0);
  state.cancelled = true; await page.evaluate(() => window.dispatchEvent(new Event('focus')));
  await expect(panel.getByRole('row').filter({ hasText: 'AP-PLAN-1' })).toContainText('Đã hủy');
});

test('schedule reader sees bookings without gaining appointment links and can retry a load failure', async ({ page }) => {
  const state = await prepare(page, false);
  const panel = page.locator('[aria-label="Lịch hẹn của bác sĩ trong tuần"]');
  await expect(panel.getByRole('link')).toHaveCount(0);
  state.fail = true; await panel.getByRole('button', { name: 'Tải lại lịch hẹn' }).click();
  await expect(panel.getByRole('alert')).toContainText('SYNTHETIC cannot load appointments');
  state.fail = false; await panel.getByRole('button', { name: 'Thử lại lịch hẹn' }).click();
  await expect(panel.getByText('AP-PLAN-1', { exact: true })).toBeVisible();
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(panel.getByRole('button', { name: 'Tải lại lịch hẹn' })).toBeVisible();
  await panel.screenshot({ path: '../.artifacts/consent-schedule-20261107/doctor-appointments-mobile.png' });
});
