import { expect, test, type Page } from '@playwright/test';

const id = '00000000-0000-0000-0000-000000000041';
async function setup(page: Page, settings: { permissions?: string[]; status?: string; network?: boolean; stale?: boolean; conflict?: boolean } = {}) {
  const commands: any[] = [], reads: string[] = [];
  let failed = false;
  let current = { id, code: 'AP-PROACTIVE', branchId: 1, branchName: 'Chi nhánh mẫu', startAt: '2099-01-10T02:00:00Z', endAt: '2099-01-10T02:30:00Z',
    durationMinutes: 30, dentistId: 2, dentistName: 'Bác sĩ mẫu', chairId: 1, chairName: 'Ghế 1', serviceName: 'Khám', attendeeName: 'Bệnh nhân mẫu',
    attendeeRelation: 'Self', kind: 'Standard', source: 'App', status: settings.status ?? 'Confirmed', version: 1 };
  await page.addInitScript(() => sessionStorage.setItem('dcms.web.access-token', 'synthetic'));
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url()), path = url.pathname;
    const json = (value: unknown, status = 200) => route.fulfill({ status, json: value });
    if (path === '/api/auth/me') return json({ id: 7, fullName: 'Nhân viên mẫu', userName: 'synthetic', accountType: 'Staff', roles: ['Dentist'], effectiveRoles: ['Dentist'],
      permissions: ['patients.read', 'appointments.read', ...(settings.permissions ?? ['appointments.reschedule', 'appointments.cancel'])] });
    if (path === '/api/auth/session/idle') return json({ deadlineAt: new Date(Date.now() + 600000).toISOString(), serverNow: new Date().toISOString(), activityVersion: 1 });
    if (path === '/api/patients/7') return json({ id: 7, patientCode: 'BN-SYNTHETIC', fullName: 'Bệnh nhân mẫu', isArchived: false, version: 1, link: {}, intakeHistory: [] });
    if (path === '/api/patients/7/next-visit-candidates') return json({ items: [] });
    if (path === '/api/patients/7/appointments') { reads.push('list'); return json({ items: [current], total: 1, page: 1, pageSize: 20 }); }
    if (path === `/api/appointments/${id}`) { reads.push('detail'); return json({ appointment: current, bookerName: 'Người đặt mẫu', bookerPhoneMasked: '***' }); }
    if (path === '/api/appointments/options') return json({ branches: [{ id: 1, name: current.branchName }], dentists: [{ id: 2, branchId: 1, name: 'Bác sĩ mẫu' }], chairs: [], services: [], ownScheduleOnly: true });
    if (path.endsWith('/availability')) {
      reads.push('slots'); const day = url.searchParams.get('date'); const hour = settings.conflict && failed ? '04' : '03';
      return json({ slots: [{ start: `${day}T${hour}:00:00Z`, end: `${day}T${hour}:30:00Z`, chairId: 1, dentistId: 2, scheduleVersion: 3, assistantScheduleVersion: null, bookingSettingsVersion: 2 }] });
    }
    if (route.request().method() === 'POST' && /\/(reschedule|cancel)$/.test(path)) {
      const body = route.request().postDataJSON(); commands.push({ action: path.endsWith('/cancel') ? 'cancel' : 'reschedule', ...body });
      if (!failed && (settings.network || settings.stale || settings.conflict)) {
        failed = true;
        if (settings.network) return route.abort('failed');
        if (settings.stale) current = { ...current, version: 2 };
        return json({ code: settings.stale ? 'MSG111' : 'MSG34', message: settings.stale ? 'Phiên bản đã thay đổi.' : 'Khung giờ không còn trống.' }, 409);
      }
      if (path.endsWith('/cancel')) current = { ...current, status: 'Cancelled', version: current.version + 1 };
      else current = { ...current, startAt: body.newStart, endAt: new Date(Date.parse(body.newStart) + 30 * 60000).toISOString(), version: current.version + 1 };
      return json({ notification: { message: path.endsWith('/cancel') ? 'Đã hủy lịch hẹn.' : 'Đã cập nhật lịch hẹn.' } });
    }
    return json({});
  });
  return { commands, reads };
}
async function openPatient(page: Page) {
  await page.goto('/patients/7'); await page.getByRole('tab', { name: 'Lịch hẹn', exact: true }).click();
  await expect(page.getByRole('link', { name: 'AP-PROACTIVE', exact: true })).toBeVisible();
}

test('chủ động chỉnh ngày giờ từ hồ sơ, lưu command và tải lại lịch', async ({ page }) => {
  const state = await setup(page); await openPatient(page);
  await page.getByRole('button', { name: 'Chỉnh sửa lịch', exact: true }).click();
  const form = page.getByRole('dialog', { name: 'Chỉnh sửa lịch hẹn', exact: true });
  await form.getByLabel('Ngày hẹn mới (giờ Việt Nam)').fill('2099-01-11');
  await form.getByRole('button', { name: '10:00–10:30', exact: true }).click();
  await expect(form.getByRole('button', { name: 'Lưu thay đổi', exact: true })).toBeDisabled();
  await form.getByLabel('Lý do chỉnh sửa (bắt buộc)').fill('Phòng khám đổi lịch bác sĩ');
  await form.getByRole('button', { name: 'Lưu thay đổi', exact: true }).click();
  await expect(form).not.toBeVisible();
  await expect(page.getByRole('row').filter({ hasText: 'AP-PROACTIVE' })).toContainText('11/01/2099 · 10:00–10:30');
  expect(state.commands[0]).toMatchObject({ action: 'reschedule', clinicInitiated: true, expectedVersion: 1, newStart: '2099-01-11T03:00:00Z', scheduleVersion: 3, bookingSettingsVersion: 2, reason: 'Phòng khám đổi lịch bác sĩ' });
  expect(state.commands[0].requesterVerification).toBeUndefined();
  expect(state.reads.filter(value => value === 'list').length).toBeGreaterThan(1);
});

test('hủy cần lý do và xác nhận, có thể giữ lịch rồi hủy tại chi tiết', async ({ page }) => {
  const state = await setup(page); await page.goto(`/appointments/${id}`);
  await page.getByRole('button', { name: 'Hủy lịch', exact: true }).click();
  const modal = page.getByRole('dialog', { name: 'Hủy lịch hẹn', exact: true });
  await expect(modal.getByRole('button', { name: 'Xác nhận hủy lịch', exact: true })).toBeDisabled();
  await modal.getByRole('button', { name: 'Giữ lịch hẹn', exact: true }).click();
  expect(state.commands).toHaveLength(0);
  await page.getByRole('button', { name: 'Hủy lịch', exact: true }).click();
  await modal.getByLabel('Lý do hủy lịch (bắt buộc)').fill('Phòng khám ngừng hoạt động ngày này');
  await modal.getByRole('button', { name: 'Xác nhận hủy lịch', exact: true }).click();
  await expect(modal).not.toBeVisible(); await expect(page.getByText('Đã hủy', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Hủy lịch', exact: true })).toHaveCount(0);
  expect(state.commands[0]).toMatchObject({ action: 'cancel', expectedVersion: 1, clinicInitiated: true, reason: 'Phòng khám ngừng hoạt động ngày này' });
});

test('quyền đọc và lịch đã tiếp nhận không hiện thao tác; quyền hủy không mở quyền sửa', async ({ page }) => {
  await setup(page, { permissions: [] }); await openPatient(page);
  await expect(page.getByRole('button', { name: 'Chỉnh sửa lịch', exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Hủy lịch', exact: true })).toHaveCount(0);
  await page.unroute('**/api/**'); await setup(page, { permissions: ['appointments.cancel'] }); await openPatient(page);
  await expect(page.getByRole('button', { name: 'Hủy lịch', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Chỉnh sửa lịch', exact: true })).toHaveCount(0);
  await page.unroute('**/api/**'); await setup(page, { status: 'CheckedIn' }); await openPatient(page);
  await expect(page.getByRole('button', { name: 'Hủy lịch', exact: true })).toHaveCount(0);
});

test('lỗi mạng giữ bản nhập và request key khi thử lại hủy', async ({ page }) => {
  const state = await setup(page, { network: true }); await openPatient(page);
  await page.getByRole('button', { name: 'Hủy lịch', exact: true }).click();
  const form = page.getByRole('dialog', { name: 'Hủy lịch hẹn', exact: true });
  await form.getByLabel('Lý do hủy lịch (bắt buộc)').fill('Thay đổi vận hành');
  await form.getByRole('button', { name: 'Xác nhận hủy lịch', exact: true }).click();
  await expect(form.locator('.ant-alert-error')).toBeVisible();
  await expect(form.getByLabel('Lý do hủy lịch (bắt buộc)')).toHaveValue('Thay đổi vận hành');
  await form.getByRole('button', { name: 'Xác nhận hủy lịch', exact: true }).click();
  await expect(form).not.toBeVisible(); expect(state.commands).toHaveLength(2);
  expect(state.commands[0].requestKey).toBe(state.commands[1].requestKey);
});

for (const failure of ['stale', 'conflict'] as const) test(`đổi lịch xử lý ${failure}, giữ lý do và chọn lại giờ`, async ({ page }) => {
  page.setDefaultTimeout(15000);
  const state = await setup(page, { [failure]: true }); await openPatient(page);
  await page.getByRole('button', { name: 'Chỉnh sửa lịch', exact: true }).click();
  const form = page.getByRole('dialog', { name: 'Chỉnh sửa lịch hẹn', exact: true });
  await form.getByRole('button', { name: '10:00–10:30', exact: true }).click();
  await form.getByLabel('Lý do chỉnh sửa (bắt buộc)').fill('Điều chỉnh chủ động');
  await form.getByRole('button', { name: 'Lưu thay đổi', exact: true }).click();
  await expect(form.getByRole('alert')).toBeVisible();
  if (failure === 'stale') await form.getByRole('button', { name: 'Tải lại phiên bản', exact: true }).click();
  await expect(form.getByLabel('Lý do chỉnh sửa (bắt buộc)')).toHaveValue('Điều chỉnh chủ động');
  await form.getByRole('button', { name: failure === 'conflict' ? '11:00–11:30' : '10:00–10:30', exact: true }).click();
  await expect(form.getByRole('button', { name: 'Lưu thay đổi', exact: true })).toBeEnabled();
  await form.getByRole('button', { name: 'Lưu thay đổi', exact: true }).click();
  await expect(form).not.toBeVisible(); expect(state.commands).toHaveLength(2);
  expect(state.commands[1].expectedVersion).toBe(failure === 'stale' ? 2 : 1);
  expect(state.commands[0].requestKey).not.toBe(state.commands[1].requestKey);
});
