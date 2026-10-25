import { expect, test, type Page } from '@playwright/test';

const id = '00000000-0000-0000-0000-000000000001';
const date = new Date(Date.now() + 7 * 3600000).toISOString().slice(0, 10);
const appointment = { id, code: 'AP-DIRECT', branchId: 1, branchName: 'Synthetic branch',
  startAt: `${date}T02:00:00Z`, endAt: `${date}T02:30:00Z`, durationMinutes: 30,
  dentistId: 1, dentistName: 'Synthetic dentist', chairId: 1, chairName: 'Chair', serviceName: 'Service',
  attendeeName: 'Synthetic attendee', attendeeRelation: 'Self', kind: 'Standard', source: 'Phone', status: 'Confirmed', version: 1 };

async function setup(page: Page, permitted = true) {
  const user = { id: 2, userName: 'fixture', fullName: 'Synthetic staff', accountType: 'Staff',
    roles: ['ClinicAssistant'], effectiveRoles: ['ClinicAssistant'],
    permissions: permitted ? ['appointments.support.handle'] : [], hasVerifiedPatientLink: false };
  const request = { id: 'SR-DIRECT', appointmentId: id, appointmentCode: appointment.code, kind: 'Cancel', status: 'Pending',
    expectedVersion: 1, requestKey: id, reason: 'Synthetic cancellation', requester: { name: 'Requester', phone: null, isAnonymous: false },
    permission: { canResolve: true, canEdit: true, branchMatch: true, ownershipMatch: true }, appointmentActionCommitted: false,
    createdAt: appointment.startAt, updatedAt: appointment.startAt, history: [],
    appointment: { ...appointment, attendee: { name: appointment.attendeeName, relation: 'Self' },
      booker: { name: 'Booker', isAnonymous: false }, confirmedToAttend: 'Pending' } };
  const queries: URL[] = [];
  let fail = false, empty = false;
  const writes: string[] = [];
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url());
    let body: unknown = {};
    if (url.pathname === '/api/auth/sign-in') body = { accessToken: 'fixture-token', user };
    else if (url.pathname === '/api/auth/me') { await new Promise(resolve => setTimeout(resolve, 150)); body = user; }
    else if (url.pathname === '/api/appointments/options') body = { branches: [{ id: 1, name: 'Branch' }], dentists: [{ id: 1, branchId: 1, name: 'Dentist' }], chairs: [], services: [], ownScheduleOnly: false };
    else if (url.pathname === '/api/appointments/calendar') body = { appointments: [appointment], days: [{ dentistId: 1, date, periods: [] }], ownScheduleOnly: false };
    else if (url.pathname === `/api/appointments/${id}`) body = { appointment, bookerName: 'Booker', bookerPhoneMasked: '***' };
    else if (url.pathname === '/api/support-requests') {
      queries.push(url);
      if (fail) return route.fulfill({ status: 503, contentType: 'application/json', body: JSON.stringify({ code: 'MSG144', message: 'Synthetic read error' }) });
      body = { items: empty ? [] : [request], total: empty ? 0 : 1, page: 1, pageSize: 10 };
    } else if (url.pathname === '/api/support-requests/SR-DIRECT/verify-requester') {
      writes.push(url.pathname);
      expect(route.request().postDataJSON().expectedVersion).toBe(1);
      body = { ...request, expectedVersion: 2, requesterVerifiedAt: appointment.startAt };
    } else if (url.pathname === '/api/support-requests/SR-DIRECT') body = request;
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
  });
  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill('fixture');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('fixture-password');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await page.getByRole('link', { name: 'Lịch hẹn', exact: true }).click();
  return { queries, writes, fail: (value: boolean) => { fail = value; }, empty: () => { empty = true; } };
}

test('calendar opens exact appointment, refresh preserves it and scoped request opens in place', async ({ page }) => {
  const state = await setup(page);
  await page.getByRole('button', { name: 'Lịch AP-DIRECT', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/appointments/${id}$`));
  await expect(page.getByRole('heading', { name: 'Lịch hẹn AP-DIRECT', exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByRole('button', { name: 'SR-DIRECT', exact: true })).toBeVisible();
  expect(state.queries.length).toBeGreaterThan(0);
  expect(state.queries.every(url => url.searchParams.get('appointmentId') === id)).toBe(true);
  await page.getByRole('button', { name: 'SR-DIRECT', exact: true }).click();
  const drawer = page.getByRole('dialog');
  await expect(drawer).toContainText('AP-DIRECT');
  await drawer.getByRole('tab', { name: 'Xử lý', exact: true }).click();
  await drawer.getByRole('button', { name: 'Xác minh người yêu cầu', exact: true }).click();
  await expect(drawer.getByText('Đã xác minh người yêu cầu.', { exact: true })).toBeVisible();
  await drawer.getByRole('tab', { name: 'Tổng quan', exact: true }).click();
  await expect(drawer.getByText('v2', { exact: true })).toBeVisible();
  expect(state.writes).toEqual(['/api/support-requests/SR-DIRECT/verify-requester']);
  await expect(page).toHaveURL(new RegExp(`/appointments/${id}$`));
});

test('detail readers without support permission do not load or display request controls', async ({ page }) => {
  const state = await setup(page, false);
  await page.getByRole('button', { name: 'Lịch AP-DIRECT', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Lịch hẹn AP-DIRECT', exact: true })).toBeVisible();
  await expect(page.getByText('Yêu cầu đổi / hủy của lịch hẹn', { exact: true })).toHaveCount(0);
  expect(state.queries).toEqual([]);
});

test('support read failure retries into a distinct empty state', async ({ page }) => {
  const state = await setup(page);
  state.fail(true);
  await page.getByRole('button', { name: 'Lịch AP-DIRECT', exact: true }).click();
  await expect(page.getByText('Synthetic read error', { exact: true })).toBeVisible();
  await expect(page.getByText('Lịch hẹn này chưa có yêu cầu hỗ trợ.', { exact: true })).toHaveCount(0);
  state.fail(false); state.empty();
  await page.getByRole('button', { name: 'Thử lại', exact: true }).click();
  await expect(page.getByText('Lịch hẹn này chưa có yêu cầu hỗ trợ.', { exact: true })).toBeVisible();
});
