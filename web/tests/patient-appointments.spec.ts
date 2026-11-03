import { expect, test, type Page } from '@playwright/test';

const appointmentId = '00000000-0000-0000-0000-000000000001';
const appointment = { id: appointmentId, code: 'AP-PATIENT', branchId: 1, branchName: 'Synthetic branch',
  startAt: '2026-11-01T02:00:00Z', endAt: '2026-11-01T02:30:00Z', durationMinutes: 30,
  dentistId: 1, dentistName: 'Synthetic dentist', chairId: 1, chairName: 'Chair', serviceName: 'Exam',
  attendeeName: 'Synthetic patient', attendeeRelation: 'Self', kind: 'Standard', source: 'Phone', status: 'Confirmed', version: 1 };

async function setup(page: Page) {
  const queries: URL[] = [];
  let fail = false;
  await page.addInitScript(() => sessionStorage.setItem('dcms.web.access-token', 'fixture'));
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url());
    let body: unknown = {};
    if (url.pathname === '/api/auth/me') body = { id: 2, userName: 'fixture', fullName: 'Fixture', accountType: 'Staff',
      roles: ['Dentist'], effectiveRoles: ['Dentist'], permissions: ['patients.read', 'appointments.read', 'appointments.create', 'treatment.read', 'treatment.progress.manage', 'treatment.items.update'], hasVerifiedPatientLink: false };
    else if (url.pathname === '/api/patients/7') body = { id: 7, patientCode: 'P-FIXTURE', fullName: 'Synthetic patient',
      isArchived: false, createdAt: '2026-10-19T00:00:00Z', updatedAt: '2026-10-19T00:00:00Z', version: 1, link: {}, intakeHistory: [] };
    else if (url.pathname === '/api/patients/7/appointments') {
      queries.push(url);
      if (fail) return route.fulfill({ status: 503, contentType: 'application/json', body: JSON.stringify({ code: 'MSG144', message: 'Synthetic connection failure' }) });
      const items = url.searchParams.get('scope') === 'HISTORY' ? [] : [appointment];
      body = { items, total: items.length, page: 1, pageSize: 20 };
    } else if (url.pathname === `/api/appointments/${appointmentId}`) body = { appointment, bookerName: 'Booker', bookerPhoneMasked: '***' };
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
  });
  await page.goto('/patients/7');
  await expect(page.getByRole('heading', { name: 'Synthetic patient', exact: true })).toBeVisible();
  return { queries, fail: (value: boolean) => { fail = value; } };
}

test('patient appointment tab loads lazily, filters history and opens the exact appointment detail', async ({ page }) => {
  const state = await setup(page);
  expect(state.queries).toEqual([]);
  await page.getByRole('tab', { name: 'Lịch hẹn', exact: true }).click();
  const link = page.getByRole('link', { name: 'AP-PATIENT', exact: true });
  await expect(link).toHaveAttribute('href', `/appointments/${appointmentId}`);
  await expect(page.getByRole('row').filter({ hasText: 'AP-PATIENT' })).toContainText('01/11/2026 · 09:00–09:30');
  expect(state.queries[0].pathname).toBe('/api/patients/7/appointments');
  const scopeSelect = page.locator('.ant-select').filter({ has: page.getByRole('combobox', { name: 'Nhóm lịch hẹn' }) });
  await scopeSelect.locator('.ant-select-selector').click();
  await page.locator('.ant-select-dropdown:visible').getByText('Lịch sử', { exact: true }).click();
  await expect(page.getByText('Không có lịch hẹn phù hợp của bệnh nhân trong phạm vi của bạn.', { exact: true })).toBeVisible();
  expect(state.queries.at(-1)?.searchParams.get('scope')).toBe('HISTORY');
  expect(state.queries.at(-1)?.searchParams.get('page')).toBe('1');
  await scopeSelect.locator('.ant-select-selector').click();
  await page.locator('.ant-select-dropdown:visible').getByText('Tất cả', { exact: true }).click();
  await link.click();
  await expect(page).toHaveURL(new RegExp(`/appointments/${appointmentId}$`));
  await expect(page.getByRole('heading', { name: 'Lịch hẹn AP-PATIENT', exact: true })).toBeVisible();
});

// Next plan item to book (docs/specs/TREATMENT-NEXT-VISIT-BOOKING.md). Dates are far in the future so the test does not age.
async function setupNextVisit(page: Page, candidatesStatus = 200, extra: { candidate?: object; slotDates?: string[]; preview?: object } = {}) {
  const sent: any[] = []; let booked = false;
  const candidate = { dossierId: 'd1', dossierCode: 'DT-001', itemId: 'i1', serviceId: 3, serviceName: 'Trám răng', region: '26',
    estimatedStart: '2099-01-10', durationMinutes: 30, dentistId: 5, dentistName: 'Bác sĩ A', branchId: 1, status: 'Ready',
    followUpId: null, appointment: null, earliestOn: null, latestOn: null,
    targetOn: '2099-01-10', windowFrom: '2099-01-06', windowTo: '2099-01-14', ...extra.candidate };
  await page.addInitScript(() => sessionStorage.setItem('dcms.web.access-token', 'fixture'));
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url()), method = route.request().method();
    const json = (body: unknown, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
    if (url.pathname === '/api/auth/me') return json({ id: 2, userName: 'fixture', fullName: 'Fixture', accountType: 'Staff',
      roles: ['Dentist'], effectiveRoles: ['Dentist'], permissions: ['patients.read', 'appointments.read', 'appointments.create', 'treatment.read', 'treatment.progress.manage', 'treatment.items.update'], hasVerifiedPatientLink: false });
    if (url.pathname === '/api/patients/7') return json({ id: 7, patientCode: 'P-FIXTURE', fullName: 'Synthetic patient',
      isArchived: false, createdAt: '2026-10-19T00:00:00Z', updatedAt: '2026-10-19T00:00:00Z', version: 1, link: {}, intakeHistory: [] });
    if (url.pathname === '/api/patients/7/appointments') return json({ items: [], total: 0, page: 1, pageSize: 20 });
    if (url.pathname === '/api/patients/7/next-visit-candidates') {
      if (candidatesStatus !== 200) return json({ code: 'MSG06', message: 'Không có quyền.' }, candidatesStatus);
      return json({ items: [booked ? { ...candidate, status: 'Booked', followUpId: 'fu-1',
        appointment: { id: 'ap-1', code: 'AP-NEXT', startAt: '2099-01-12T02:00:00Z' } } : candidate] });
    }
    if (url.pathname === '/api/availability') {
      sent.push({ command: 'availability', date: url.searchParams.get('date'), dentistId: url.searchParams.get('dentistId') });
      const day = url.searchParams.get('date') ?? '';
      const slots = (extra.slotDates ?? ['2099-01-12']).includes(day) ? [{ start: `${day}T02:00:00Z`, end: `${day}T02:30:00Z`, chairId: 1,
        dentistId: 5, assistantId: null, scheduleVersion: 1, assistantScheduleVersion: null, bookingSettingsVersion: 1 }] : [];
      return json({ slots });
    }
    if (url.pathname === '/api/dossiers/d1/plan/items/i1/planned-date-preview') return json(extra.preview ?? {});
    if (url.pathname === '/api/dossiers/d1/plan/planned-dates' && method === 'POST') { sent.push({ command: 'planned-dates', ...route.request().postDataJSON() }); return json({}); }
    if (url.pathname === '/api/follow-up-instructions' && method === 'POST') { sent.push({ command: 'follow-up', ...route.request().postDataJSON() }); return json({ instruction: { id: 'fu-1' } }, 201); }
    if (url.pathname === '/api/appointments' && method === 'POST') {
      sent.push({ command: 'appointment', ...route.request().postDataJSON() }); booked = true;
      return json({ appointmentId: 'ap-1', code: 'AP-NEXT', version: 1, status: 'Confirmed', startAt: '2099-01-12T02:00:00Z', endAt: '2099-01-12T02:30:00Z',
        notification: { code: 'MSG29', message: 'Đã đặt lịch.' } });
    }
    return json({});
  });
  await page.goto('/patients/7');
  await expect(page.getByRole('heading', { name: 'Synthetic patient', exact: true })).toBeVisible();
  return sent;
}

test('tab shows the nearest unperformed plan item, jumps to the first day the dentist works and books it as a follow-up', async ({ page }) => {
  const sent = await setupNextVisit(page);
  await page.getByRole('tab', { name: 'Lịch hẹn', exact: true }).click();
  await expect(page.getByText('Trám răng · 26', { exact: true })).toBeVisible();
  await expect(page.getByText(/Ngày dự kiến: 10\/01\/2099/)).toBeVisible();
  await page.getByRole('button', { name: 'Đặt lịch', exact: true }).click();
  const drawer = page.getByRole('dialog').filter({ hasText: 'Đặt lịch theo kế hoạch điều trị' });
  await expect(drawer.getByLabel('Ngày hẹn (giờ Việt Nam)')).toHaveValue('2099-01-12');
  await expect(drawer.getByText(/Gợi ý ngày gần nhất/)).toHaveCount(0);
  expect(sent.filter(x => x.command === 'availability').every(x => x.dentistId === '5')).toBe(true);
  await drawer.getByRole('button', { name: '09:00', exact: true }).click();
  await drawer.getByLabel('SĐT (10 số) hoặc email').fill('0912345678');
  await drawer.getByRole('button', { name: 'Xác nhận đặt lịch' }).click();
  await expect(drawer).toBeHidden();
  const followUp = sent.find(x => x.command === 'follow-up'), appointment = sent.find(x => x.command === 'appointment');
  expect(followUp.source).toMatchObject({ dossierId: 'd1', itemId: 'i1' }); expect(followUp.dentistId).toBe(5);
  expect(appointment).toMatchObject({ kind: 'FollowUp', followUpId: 'fu-1', serviceId: 3, dentistId: 5, durationMinutes: 30 });
  await expect(page.getByText('Mọi hạng mục chưa thực hiện đã có lịch hẹn.')).toBeVisible();
  await expect(page.getByText('AP-NEXT')).toHaveCount(0);
});

test('only days within 4 days of the planned date are offered, nearest first with their deviation, and the planned dates can be updated after booking', async ({ page }) => {
  const sent = await setupNextVisit(page, 200, {
    slotDates: ['2099-01-08', '2099-01-12', '2099-01-17'],
    preview: { dossierVersion: 4, items: [
      { itemId: 'i1', serviceName: 'Trám răng', region: '26', currentPlannedOn: '2099-01-10', proposedPlannedOn: '2099-01-12', isBookedItem: true },
      { itemId: 'i2', serviceName: 'Bọc sứ', region: '26', currentPlannedOn: '2099-01-20', proposedPlannedOn: '2099-01-22', isBookedItem: false }] } });
  await page.getByRole('tab', { name: 'Lịch hẹn', exact: true }).click();
  await page.getByRole('button', { name: 'Đặt lịch', exact: true }).click();
  const drawer = page.getByRole('dialog').filter({ hasText: 'Đặt lịch theo kế hoạch điều trị' });
  await expect(drawer.getByText('06/01/2099 – 14/01/2099')).toBeVisible();
  // Equally near days: the earlier one is selected first.
  await expect(drawer.getByLabel('Ngày hẹn (giờ Việt Nam)')).toHaveValue('2099-01-08');
  const days = drawer.getByRole('group', { name: 'Ngày gần ngày dự kiến có lịch của bác sĩ' });
  await expect(days.getByRole('button', { name: '08/01/2099 · Sớm 2 ngày' })).toBeVisible();
  await expect(days.getByRole('button', { name: '12/01/2099 · Trễ 2 ngày' })).toBeVisible();
  await expect(days.getByRole('button')).toHaveCount(2);
  expect(sent.filter(x => x.command === 'availability').every(x => x.date >= '2099-01-06' && x.date <= '2099-01-14')).toBe(true);
  await days.getByRole('button', { name: /12\/01\/2099/ }).click();
  await drawer.getByRole('button', { name: '09:00', exact: true }).click();
  await drawer.getByLabel('SĐT (10 số) hoặc email').fill('0912345678');
  await drawer.getByRole('button', { name: 'Xác nhận đặt lịch' }).click();
  const sync = page.getByRole('dialog').filter({ hasText: 'Cập nhật ngày dự kiến theo lịch hẹn' });
  await expect(sync.getByText('Bọc sứ', { exact: false })).toBeVisible();
  await sync.getByRole('button', { name: 'Cập nhật ngày dự kiến' }).click();
  await expect(sync).toBeHidden();
  const followUp = sent.find(x => x.command === 'follow-up');
  expect(followUp).toMatchObject({ earliestOn: '2099-01-06', targetOn: '2099-01-10', latestOn: '2099-01-14' });
  expect(sent.find(x => x.command === 'appointment').exceptionReason).toBeNull();
  expect(sent.find(x => x.command === 'planned-dates')).toMatchObject({ expectedVersion: 4,
    changes: [{ itemId: 'i1', plannedOn: '2099-01-12' }, { itemId: 'i2', plannedOn: '2099-01-22' }] });
});

test('no slot inside the 4-day window shows how to proceed', async ({ page }) => {
  await setupNextVisit(page, 200, { slotDates: ['2099-01-20'] });
  await page.getByRole('tab', { name: 'Lịch hẹn', exact: true }).click();
  await page.getByRole('button', { name: 'Đặt lịch', exact: true }).click();
  const drawer = page.getByRole('dialog').filter({ hasText: 'Đặt lịch theo kế hoạch điều trị' });
  await expect(drawer.getByText(/chưa có lịch làm còn trống trong khoảng 06\/01\/2099 – 14\/01\/2099/)).toBeVisible();
  await expect(drawer.getByText(/đổi ngày dự kiến của hạng mục trong kế hoạch/)).toBeVisible();
  await expect(drawer.getByRole('button', { name: 'Xác nhận đặt lịch' })).toBeDisabled();
});

test('keeping the planned dates after booking sends no update', async ({ page }) => {
  const sent = await setupNextVisit(page, 200, { preview: { dossierVersion: 2, items: [
    { itemId: 'i1', serviceName: 'Trám răng', region: '26', currentPlannedOn: '2099-01-10', proposedPlannedOn: '2099-01-12', isBookedItem: true }] } });
  await page.getByRole('tab', { name: 'Lịch hẹn', exact: true }).click();
  await page.getByRole('button', { name: 'Đặt lịch', exact: true }).click();
  const drawer = page.getByRole('dialog').filter({ hasText: 'Đặt lịch theo kế hoạch điều trị' });
  await drawer.getByRole('button', { name: '09:00', exact: true }).click();
  await drawer.getByLabel('SĐT (10 số) hoặc email').fill('0912345678');
  await drawer.getByRole('button', { name: 'Xác nhận đặt lịch' }).click();
  const sync = page.getByRole('dialog').filter({ hasText: 'Cập nhật ngày dự kiến theo lịch hẹn' });
  await sync.getByRole('button', { name: 'Giữ nguyên' }).click();
  await expect(sync).toBeHidden();
  expect(sent.some(x => x.command === 'planned-dates')).toBe(false);
  await expect(page.getByText('Mọi hạng mục chưa thực hiện đã có lịch hẹn.')).toBeVisible();
});

test('next plan item block is hidden for staff who are not dentists of the branch', async ({ page }) => {
  await setupNextVisit(page, 403);
  await page.getByRole('tab', { name: 'Lịch hẹn', exact: true }).click();
  await expect(page.getByText('Không có lịch hẹn phù hợp của bệnh nhân trong phạm vi của bạn.', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Hạng mục kế tiếp cần hẹn' })).toHaveCount(0);
});

test('read error is distinct from empty data and can be retried', async ({ page }) => {
  const state = await setup(page); state.fail(true);
  await page.getByRole('tab', { name: 'Lịch hẹn', exact: true }).click();
  await expect(page.getByText('Synthetic connection failure', { exact: true })).toBeVisible();
  await expect(page.getByText('Không có lịch hẹn phù hợp của bệnh nhân trong phạm vi của bạn.', { exact: true })).toHaveCount(0);
  state.fail(false);
  await page.getByRole('button', { name: 'Thử lại', exact: true }).click();
  await expect(page.getByRole('link', { name: 'AP-PATIENT', exact: true })).toBeVisible();
});
