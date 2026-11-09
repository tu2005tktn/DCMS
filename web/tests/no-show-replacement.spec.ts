import { expect, test, type Page } from '@playwright/test';

const id = '00000000-0000-0000-0000-000000000061';
async function setup(page: Page, settings: { status?: string; permissions?: string[]; expired?: boolean; network?: boolean } = {}) {
  await page.clock.install({ time: new Date('2099-01-10T02:05:00Z') });
  const commands: { action: string; [key: string]: any }[] = [];
  let failed = false;
  let appointment = { id, code: 'AP-NOSHOW', branchId: 1, branchName: 'Chi nhánh mẫu', dentistId: 2, dentistName: 'Bác sĩ mẫu', chairId: 1, chairName: 'Ghế 1',
    startAt: '2099-01-10T02:00:00Z', endAt: settings.expired ? '2099-01-10T02:04:00Z' : '2099-01-10T03:00:00Z', durationMinutes: 60,
    status: settings.status ?? 'Confirmed', attendeeName: 'Khách cũ', attendeeRelation: 'Self', serviceName: 'Khám', kind: 'Standard', source: 'App', version: 1 };
  await page.addInitScript(() => sessionStorage.setItem('dcms.web.access-token', 'synthetic'));
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url()), path = url.pathname;
    const json = (data: unknown) => route.fulfill({ json: data });
    if (path === '/api/auth/me') return json({ id: 7, fullName: 'Nhân viên mẫu', accountType: 'Staff', userName: 'synthetic', roles: ['Dentist'], effectiveRoles: ['Dentist'],
      permissions: ['appointments.read', ...(settings.permissions ?? ['appointments.no-show', 'appointments.create'])] });
    if (path === '/api/auth/session/idle') return json({ serverNow: '2099-01-10T02:05:00Z', deadlineAt: '2099-01-10T04:00:00Z', activityVersion: 1 });
    if (path === `/api/appointments/${id}`) return json({ appointment, bookerName: 'Người đặt cũ', bookerPhoneMasked: '***' });
    if (path === '/api/appointments/options') return json({ branches: [{ id: 1, name: appointment.branchName }], dentists: [{ id: 2, branchId: 1, name: appointment.dentistName }],
      chairs: [{ id: 1, branchId: 1, name: 'Ghế 1' }], services: [{ id: 3, branchId: 1, name: 'Khám', publicDurationMinutes: 30 }], ownScheduleOnly: true });
    if (path === '/api/availability') {
      const duration = Number(url.searchParams.get('durationMinutes'));
      return json({ slots: ['2099-01-10T02:00:00Z', '2099-01-10T02:15:00Z', '2099-01-10T03:15:00Z'].map(start => ({ start,
        end: new Date(Date.parse(start) + duration * 60000).toISOString(), chairId: 1, dentistId: 2, scheduleVersion: 3, assistantScheduleVersion: null, bookingSettingsVersion: 2 })) });
    }
    if (path.endsWith('/no-show')) {
      commands.push({ action: 'no-show', ...route.request().postDataJSON() });
      appointment = { ...appointment, status: 'NoShow', version: 2 };
      return json({ notification: { message: 'Đã đánh dấu khách không tới.' } });
    }
    if (path === '/api/appointments' && route.request().method() === 'POST') {
      commands.push({ action: 'book', ...route.request().postDataJSON() });
      if (settings.network && !failed) { failed = true; return route.abort('failed'); }
      return json({ appointmentId: '00000000-0000-0000-0000-000000000062', code: 'AP-REPLACEMENT', notification: { message: 'Đã đặt lịch mới.' } });
    }
    return json({});
  });
  return { commands };
}

test('xác nhận khách không tới rồi đặt khách khác vào thời gian còn lại', async ({ page }) => {
  const state = await setup(page); await page.goto(`/appointments/${id}`);
  await expect(page.getByRole('button', { name: 'Đặt lịch thay thế', exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Khách không tới', exact: true }).click();
  const modal = page.getByRole('dialog', { name: 'Khách không tới', exact: true });
  await modal.getByRole('button', { name: 'Giữ nguyên', exact: true }).click(); expect(state.commands).toHaveLength(0);
  await page.getByRole('button', { name: 'Khách không tới', exact: true }).click();
  await modal.getByLabel('Lý do khách không tới (bắt buộc)').fill('');
  await expect(modal.getByRole('button', { name: 'Xác nhận khách không tới', exact: true })).toBeDisabled();
  await modal.getByLabel('Lý do khách không tới (bắt buộc)').fill('Gọi xác nhận nhưng khách không đến');
  await modal.getByRole('button', { name: 'Xác nhận khách không tới', exact: true }).click();
  await expect(modal).not.toBeVisible(); await page.getByRole('button', { name: 'Đặt lịch thay thế', exact: true }).click();
  const form = page.getByRole('dialog', { name: 'Đặt lịch thay thế', exact: true });
  await expect(form.getByLabel('Họ tên người đặt', { exact: true })).toHaveValue('');
  await expect(form.getByLabel('Họ tên người khám', { exact: true })).toHaveValue('');
  await expect(form.getByLabel('Nha sĩ', { exact: true })).toBeDisabled(); await expect(form.getByLabel('Ngày', { exact: true })).toBeDisabled();
  await expect(form.getByLabel('Thời lượng (phút)', { exact: true })).toHaveValue('30');
  await expect(form.getByRole('button', { name: /09:00 Ghế/ })).toHaveCount(0);
  await expect(form.getByRole('button', { name: /10:15 Ghế/ })).toHaveCount(0);
  await form.getByLabel('Họ tên người đặt', { exact: true }).fill('Người đặt mới');
  await form.getByLabel('SĐT (10 số) hoặc email', { exact: true }).fill('0987654321');
  await form.getByLabel('Họ tên người khám', { exact: true }).fill('Khách mới');
  await form.getByLabel('Thời lượng (phút)', { exact: true }).fill('20');
  await form.getByRole('button', { name: /09:15 Ghế/ }).click();
  await expect(form.getByText('Đã chọn 09:15–09:35', { exact: false })).toBeVisible();
  await form.getByRole('button', { name: 'Xem lại', exact: true }).click();
  expect(state.commands).toHaveLength(1);
  await form.getByRole('button', { name: 'Xác nhận đặt lịch', exact: true }).click();
  await expect(form).not.toBeVisible();
  expect(state.commands[1]).toMatchObject({ action: 'book', replacesAppointmentId: id, attendee: { patientId: null, name: 'Khách mới' },
    booker: { name: 'Người đặt mới', phone: '0987654321' }, kind: 'WalkIn', source: 'InPerson', branchId: 1, dentistId: 2,
    durationMinutes: 20, startAt: '2099-01-10T02:15:00Z' });
  expect(state.commands[1].exceptionReason).toContain('AP-NOSHOW');
  await expect(page.getByText('Khách cũ (Bản thân)', { exact: true })).toBeVisible();
  await expect(page.getByText('Khách không tới', { exact: true })).toBeVisible();
});

for (const variant of ['read-only', 'checked-in', 'expired', 'no-create'] as const) test(`kiểm quyền và trạng thái ${variant}`, async ({ page }) => {
  await setup(page, { permissions: variant === 'read-only' ? [] : variant === 'no-create' ? ['appointments.no-show'] : undefined,
    status: variant === 'checked-in' ? 'CheckedIn' : variant === 'expired' || variant === 'no-create' ? 'NoShow' : 'Confirmed', expired: variant === 'expired' });
  await page.goto(`/appointments/${id}`);
  await expect(page.getByRole('button', { name: 'Khách không tới', exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Đặt lịch thay thế', exact: true })).toHaveCount(0);
});

test('thử lại đặt thay sau mất mạng giữ request key và thông tin khách mới', async ({ page }) => {
  const state = await setup(page, { status: 'NoShow', network: true }); await page.goto(`/appointments/${id}`);
  await page.getByRole('button', { name: 'Đặt lịch thay thế', exact: true }).click();
  const form = page.getByRole('dialog', { name: 'Đặt lịch thay thế', exact: true });
  await form.getByLabel('Họ tên người đặt', { exact: true }).fill('Người mới');
  await form.getByLabel('SĐT (10 số) hoặc email', { exact: true }).fill('0987654321');
  await form.getByLabel('Họ tên người khám', { exact: true }).fill('Khách mới');
  await form.getByRole('button', { name: 'Xem lại', exact: true }).click();
  await form.getByRole('button', { name: 'Xác nhận đặt lịch', exact: true }).click();
  await expect(form.locator('.ant-alert-error')).toBeVisible();
  await form.getByRole('button', { name: 'Xác nhận đặt lịch', exact: true }).click(); await expect(form).not.toBeVisible();
  expect(state.commands).toHaveLength(2); expect(state.commands[0].requestKey).toBe(state.commands[1].requestKey);
});
