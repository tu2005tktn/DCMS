import { expect, test, type Page } from '@playwright/test';

const id = '00000000-0000-0000-0000-000000000071';
async function setup(page: Page, settings: { due?: boolean; permissions?: string[]; detailError?: boolean; currentDentistId?: number | null } = {}) {
  await page.clock.install({ time: new Date('2099-01-10T02:05:00Z') });
  const reads: URL[] = [], commands: { action: string; [key: string]: any }[] = [];
  let detailError = settings.detailError;
  let current = { id, code: 'AP-CALENDAR', branchId: 1, branchName: 'Chi nhánh của tôi', dentistId: 3, dentistName: 'Bác sĩ đang đăng nhập', chairId: 1, chairName: 'Ghế 1',
    startAt: settings.due ? '2099-01-10T02:00:00Z' : '2099-01-10T04:00:00Z', endAt: settings.due ? '2099-01-10T03:00:00Z' : '2099-01-10T04:30:00Z',
    durationMinutes: settings.due ? 60 : 30, status: 'Confirmed', attendeeName: 'Khách cũ', attendeeRelation: 'Self', serviceName: 'Khám', kind: 'Standard', source: 'App', version: 1 };
  await page.addInitScript(() => sessionStorage.setItem('dcms.web.access-token', 'synthetic'));
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url()), path = url.pathname;
    const json = (value: unknown, status = 200) => route.fulfill({ status, json: value });
    if (path === '/api/auth/me') return json({ id: 9, userName: 'synthetic', fullName: 'Tên tài khoản khác tên bác sĩ', accountType: 'Staff', roles: ['ClinicOwner'], effectiveRoles: ['ClinicOwner'],
      permissions: ['appointments.read', ...(settings.permissions ?? ['appointments.reschedule', 'appointments.cancel', 'appointments.no-show', 'appointments.create'])] });
    if (path === '/api/auth/session/idle') return json({ serverNow: '2099-01-10T02:05:00Z', deadlineAt: '2099-01-10T04:00:00Z', activityVersion: 1 });
    if (path === '/api/appointments/options') return json({ branches: [{ id: 2, name: 'Chi nhánh khác' }, { id: 1, name: current.branchName }], dentists: [
      { id: 2, branchId: 1, name: 'Bác sĩ đầu danh sách' }, { id: 3, branchId: 1, name: current.dentistName }, { id: 4, branchId: 2, name: 'Bác sĩ chi nhánh khác' }],
      chairs: [{ id: 1, branchId: 1, name: 'Ghế 1' }], services: [{ id: 5, branchId: 1, name: 'Khám', publicDurationMinutes: 30 }], ownScheduleOnly: false,
      currentDentistId: settings.currentDentistId === undefined ? 3 : settings.currentDentistId });
    if (path === '/api/appointments/calendar') {
      reads.push(url); const date = url.searchParams.get('from')!;
      return json({ branchId: Number(url.searchParams.get('branchId')), from: date, to: url.searchParams.get('to'), ownScheduleOnly: false,
        appointments: url.searchParams.get('status') && url.searchParams.get('status') !== current.status ? [] : [current],
        days: [{ dentistId: 3, date, periods: [{ startMinute: 480, endMinute: 1020, kind: 'Work', reason: null }] }] });
    }
    if (path === `/api/appointments/${id}`) {
      if (detailError) return json({ code: 'MSG144', message: 'Không tải được chi tiết thử nghiệm.' }, 503);
      return json({ appointment: current, bookerName: 'Người đặt cũ', bookerPhoneMasked: '***', reason: 'Lý do chỉ xuất hiện trong chi tiết' });
    }
    if (path.endsWith('/availability')) {
      const date = url.searchParams.get('date'), start = settings.due ? '2099-01-10T02:15:00Z' : `${date}T05:00:00Z`;
      return json({ slots: [{ start, end: new Date(Date.parse(start) + Number(url.searchParams.get('durationMinutes') ?? 30) * 60000).toISOString(),
        chairId: 1, dentistId: 3, scheduleVersion: 3, assistantScheduleVersion: null, bookingSettingsVersion: 2 }] });
    }
    if (/\/(cancel|reschedule|no-show)$/.test(path)) {
      const action = path.split('/').at(-1)!; const body = route.request().postDataJSON(); commands.push({ action, ...body });
      current = { ...current, status: action === 'cancel' ? 'Cancelled' : action === 'no-show' ? 'NoShow' : 'Confirmed', version: current.version + 1,
        ...(action === 'reschedule' ? { startAt: body.newStart, endAt: new Date(Date.parse(body.newStart) + 30 * 60000).toISOString() } : {}) };
      return json({ notification: { message: 'Thao tác đã lưu.' } });
    }
    if (path === '/api/appointments' && route.request().method() === 'POST') {
      commands.push({ action: 'book', ...route.request().postDataJSON() });
      return json({ code: 'AP-NEW', notification: { message: 'Đã đặt lịch thay thế.' } });
    }
    return json({});
  });
  await page.goto('/appointments');
  return { reads, commands, recoverDetail: () => { detailError = false; } };
}
const openDetail = async (page: Page) => {
  await page.getByRole('button', { name: 'Lịch AP-CALENDAR', exact: true }).click();
  const drawer = page.getByRole('dialog', { name: 'Thông tin lịch hẹn', exact: true });
  await expect(drawer.getByText('Khách cũ (Bản thân)', { exact: true })).toBeVisible(); return drawer;
};

test('tự chọn nha sĩ đăng nhập theo EmployeeId, giữ lựa chọn thủ công khi tải lại', async ({ page }) => {
  const state = await setup(page);
  await expect.poll(() => state.reads.at(-1)?.searchParams.get('dentistId')).toBe('3');
  expect(state.reads[0].searchParams.get('branchId')).toBe('1');
  await expect(page.locator('.ant-select').filter({ has: page.getByRole('combobox', { name: 'Nha sĩ', exact: true }) })).toContainText('Bác sĩ đang đăng nhập');
  await page.getByRole('combobox', { name: 'Nha sĩ', exact: true }).press('ArrowDown');
  await page.locator('.ant-select-dropdown:visible').getByText('Bác sĩ đầu danh sách', { exact: true }).click();
  await expect.poll(() => state.reads.at(-1)?.searchParams.get('dentistId')).toBe('2');
  await page.getByRole('button', { name: 'Tải lại', exact: true }).click();
  await expect.poll(() => state.reads.at(-1)?.searchParams.get('dentistId')).toBe('2');
  await page.getByRole('combobox', { name: 'Chi nhánh', exact: true }).press('ArrowDown');
  await page.locator('.ant-select-dropdown:visible').getByText('Chi nhánh khác', { exact: true }).click();
  await expect.poll(() => state.reads.at(-1)?.searchParams.get('branchId')).toBe('2');
  expect(state.reads.at(-1)?.searchParams.has('dentistId')).toBe(false);
});

test('trợ lý không phải nha sĩ giữ lọc tất cả', async ({ page }) => {
  const state = await setup(page, { currentDentistId: null });
  await expect.poll(() => state.reads.length).toBeGreaterThan(0); expect(state.reads[0].searchParams.has('dentistId')).toBe(false);
});

test('chỉnh sửa rồi hủy ngay trên lưới, giữ drawer và bộ lọc', async ({ page }, testInfo) => {
  const state = await setup(page); const drawer = await openDetail(page);
  await expect(page).toHaveURL(/\/appointments$/);
  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: 1000 });
    await expect.poll(async () => (await drawer.boundingBox())?.x ?? -1000).toBeGreaterThanOrEqual(-1);
    await expect.poll(async () => (await drawer.boundingBox())?.width ?? 10000).toBeLessThanOrEqual(width + 1);
    await expect.poll(async () => { const box = await drawer.boundingBox(); return box ? box.x + box.width : 10000; }).toBeLessThanOrEqual(width + 1);
    await page.screenshot({ path: testInfo.outputPath(`calendar-actions-${width}.png`), fullPage: true });
  }
  await page.setViewportSize({ width: 1440, height: 1000 });
  await drawer.getByRole('button', { name: 'Chỉnh sửa lịch', exact: true }).click();
  const editor = page.getByRole('dialog', { name: 'Chỉnh sửa lịch hẹn', exact: true });
  await editor.getByRole('button', { name: '12:00–12:30', exact: true }).click();
  await editor.getByLabel('Lý do chỉnh sửa (bắt buộc)').fill('Điều chỉnh tại lưới');
  await editor.getByRole('button', { name: 'Lưu thay đổi', exact: true }).click(); await expect(editor).not.toBeVisible();
  await expect(drawer.getByText(/12:00–12:30/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Lịch AP-CALENDAR', exact: true })).toContainText('12:00–12:30');
  await drawer.getByRole('button', { name: 'Hủy lịch', exact: true }).click();
  const modal = page.getByRole('dialog', { name: 'Hủy lịch hẹn', exact: true });
  await modal.getByLabel('Lý do hủy lịch (bắt buộc)').fill('Hủy tại lưới');
  await modal.getByRole('button', { name: 'Xác nhận hủy lịch', exact: true }).click(); await expect(modal).not.toBeVisible();
  await expect(drawer.getByText('Đã hủy', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Lịch AP-CALENDAR', exact: true })).toContainText('Đã hủy');
  expect(state.commands.map(command => command.action)).toEqual(['reschedule', 'cancel']);
  expect(state.reads.at(-1)?.searchParams.get('dentistId')).toBe('3'); await expect(page).toHaveURL(/\/appointments$/);
});

test('đánh dấu không tới rồi đặt thay tại lưới kể cả khi bộ lọc ẩn thẻ cũ', async ({ page }) => {
  const state = await setup(page, { due: true });
  await page.getByText('Tuần', { exact: true }).click();
  await page.getByRole('combobox', { name: 'Trạng thái', exact: true }).press('ArrowDown');
  await page.locator('.ant-select-dropdown:visible').getByText('Đã xác nhận', { exact: true }).click();
  const drawer = await openDetail(page); await drawer.getByRole('button', { name: 'Khách không tới', exact: true }).click();
  const modal = page.getByRole('dialog', { name: 'Khách không tới', exact: true });
  await modal.getByRole('button', { name: 'Xác nhận khách không tới', exact: true }).click(); await expect(modal).not.toBeVisible();
  await expect(page.getByRole('button', { name: 'Lịch AP-CALENDAR', exact: true })).toHaveCount(0);
  await expect(drawer.getByText('Khách không tới', { exact: true })).toBeVisible();
  await drawer.getByRole('button', { name: 'Đặt lịch thay thế', exact: true }).click();
  const form = page.getByRole('dialog', { name: 'Đặt lịch thay thế', exact: true });
  await form.getByLabel('Họ tên người đặt', { exact: true }).fill('Người đặt mới');
  await form.getByLabel('SĐT (10 số) hoặc email', { exact: true }).fill('0987654321');
  await form.getByLabel('Họ tên người khám', { exact: true }).fill('Khách mới');
  await form.getByRole('button', { name: 'Xem lại', exact: true }).click();
  await form.getByRole('button', { name: 'Xác nhận đặt lịch', exact: true }).click(); await expect(form).not.toBeVisible();
  await expect(drawer.getByText('Khách cũ (Bản thân)', { exact: true })).toBeVisible();
  expect(state.commands[1]).toMatchObject({ action: 'book', replacesAppointmentId: id, attendee: { name: 'Khách mới' } });
  await expect(page).toHaveURL(/\/appointments$/);
});

test('chỉ quyền đọc không hiện thao tác, lỗi chi tiết có thể thử lại', async ({ page }) => {
  const state = await setup(page, { permissions: [], detailError: true });
  await page.getByRole('button', { name: 'Lịch AP-CALENDAR', exact: true }).click();
  const drawer = page.getByRole('dialog', { name: 'Thông tin lịch hẹn', exact: true });
  await expect(drawer.getByText('Không tải được chi tiết thử nghiệm.')).toBeVisible();
  state.recoverDetail();
  await drawer.getByRole('button', { name: 'Thử lại', exact: true }).click();
  await expect(drawer.getByText('Khách cũ (Bản thân)', { exact: true })).toBeVisible();
  for (const action of ['Chỉnh sửa lịch', 'Hủy lịch', 'Khách không tới', 'Đặt lịch thay thế']) await expect(drawer.getByRole('button', { name: action, exact: true })).toHaveCount(0);
  await page.setViewportSize({ width: 390, height: 844 });
  await expect.poll(async () => (await drawer.boundingBox())?.x ?? -1000).toBeGreaterThanOrEqual(-1);
  await expect.poll(async () => (await drawer.boundingBox())?.width ?? 10000).toBeLessThanOrEqual(391);
});
