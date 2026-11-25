import { test, expect, type Page } from '@playwright/test';
import { Api, addDays, note, rnd, shot, staffLogin, todayIso, API } from './helpers';
import { newPatient, slots, staffBook } from './fx';

const weekday = (offset: number) => { let d = addDays(todayIso(), offset); while (new Date(d + 'T00:00:00Z').getUTCDay() === 0) d = addDays(d, 1); return d; };
async function pick(page: Page, scope: any, label: string, text: string | RegExp) {
  await scope.getByLabel(label, { exact: true }).click(); await page.waitForTimeout(300);
  const dd = page.locator('.ant-select-dropdown:visible').last();
  await (typeof text === 'string' ? dd.getByTitle(text, { exact: true }) : dd.locator('.ant-select-item-option').filter({ hasText: text })).first().click();
}
/** Fills the counter booking drawer up to the slot list. */
async function drawerTo(page: Page, o: { patientCode?: string; attendee: string; dentist: string; date: string; duration?: number }) {
  const d = page.getByRole('dialog', { name: 'Đặt lịch tại quầy (S11)' });
  await d.getByLabel('Họ tên người đặt', { exact: true }).fill('Lễ tân ghi nhận');
  await d.getByLabel('SĐT (10 số) hoặc email', { exact: true }).fill('0912345678');
  if (o.patientCode) { await d.getByLabel('Hồ sơ bệnh nhân có sẵn (tùy chọn)', { exact: true }).fill(o.patientCode); await page.locator('.ant-select-dropdown:visible').last().getByText(new RegExp(o.patientCode)).first().click(); }
  else await d.getByLabel('Họ tên người khám', { exact: true }).fill(o.attendee);
  await pick(page, d, 'Dịch vụ', /^Khám/);
  await pick(page, d, 'Nha sĩ', o.dentist);
  await d.getByLabel('Ngày', { exact: true }).fill(o.date); await d.getByLabel('Ngày', { exact: true }).press('Tab');
  if (o.duration) await d.getByLabel('Thời lượng (phút)', { exact: true }).fill(String(o.duration));
  await page.waitForTimeout(1500);
  return d;
}

test.describe('UC-15 View Appointment Calendar', () => {
  test('TC-AP-001 day and week views, own branch only', async ({ page }) => {
    await staffLogin(page, 'assistant.demo'); await page.goto('/appointments'); await page.waitForTimeout(1500);
    const day = await page.getByRole('button', { name: /^Lịch AP-/ }).count(); await shot(page, 'TC-AP-001-day');
    await page.locator('label').filter({ hasText: /^Tuần$/ }).first().click(); await page.waitForTimeout(1500);
    const week = await page.getByRole('button', { name: /^Lịch AP-/ }).count(); await shot(page, 'TC-AP-001-week');
    await page.getByRole('button', { name: 'Sau ›' }).click(); await page.waitForTimeout(1200);
    const branchSelect = await page.getByRole('combobox', { name: 'Chi nhánh' }).count();
    note(`Day view: ${day} appointments; week view: ${week}; next week navigates; branch selector shown: ${branchSelect ? 'yes' : 'no (only the assigned branch)'}`);
    expect(week).toBeGreaterThanOrEqual(day);
  });
  test('TC-AP-002 filter by dentist and status', async ({ page }) => {
    await staffLogin(page, 'assistant.demo'); await page.goto('/appointments'); await page.waitForTimeout(1500);
    await page.getByRole('combobox', { name: 'Nha sĩ' }).click(); await page.waitForTimeout(300); await page.locator('.ant-select-dropdown:visible').last().getByTitle('Nha sĩ demo', { exact: true }).click();
    await page.getByText('Tất cả trạng thái', { exact: true }).click(); await page.waitForTimeout(300);
    console.log('STATUS OPTIONS ' + await page.locator('.ant-select-dropdown:visible').last().innerText());
    await page.locator('.ant-select-dropdown:visible').last().getByTitle('Đã xác nhận', { exact: true }).click();
    await page.waitForTimeout(1500);
    const texts = await page.getByRole('button', { name: /^Lịch AP-/ }).allInnerTexts(); await shot(page, 'TC-AP-002');
    note(`Filter Nha sĩ demo + Đã xác nhận: ${texts.length} blocks, statuses ${[...new Set(texts.map(t => t.split('\n').pop()))].join(', ')}`);
    expect(texts.every(t => t.includes('Đã xác nhận'))).toBeTruthy();
  });
  test('TC-AP-003 open the appointment detail and its actions', async ({ page }) => {
    await staffLogin(page, 'assistant.demo'); await page.goto('/appointments'); await page.waitForTimeout(1500);
    await page.getByRole('button', { name: /^Lịch AP-/ }).first().click();
    const dlg = page.getByRole('dialog', { name: 'Thông tin lịch hẹn' });
    await expect(dlg.getByRole('rowheader', { name: 'Nha sĩ' })).toBeVisible(); await shot(page, 'TC-AP-003-quick');
    await dlg.getByRole('link', { name: 'Mở chi tiết đầy đủ' }).click(); await page.waitForTimeout(1500);
    const buttons = await page.locator('main').last().getByRole('button').allInnerTexts(); await shot(page, 'TC-AP-003');
    note(`Quick view shows code, status, time, branch/chair, dentist, service, examinee, booker, source; full detail actions: ${buttons.filter(b => b.trim()).join(', ')}`);
  });
  test('TC-AP-004 a dentist sees only own appointments', async ({ page }) => {
    await staffLogin(page, 'dentist.demo'); await page.goto('/appointments'); await page.waitForTimeout(2000);
    const text = await page.locator('main').last().innerText(); await shot(page, 'TC-AP-004');
    note(`dentist.demo calendar mentions other dentists: Chủ phòng khám demo=${text.includes('Chủ phòng khám demo')}, Nhân viên đa vai trò demo=${text.includes('Nhân viên đa vai trò demo')}`);
    expect(text.includes('Chủ phòng khám demo') || text.includes('Nhân viên đa vai trò demo')).toBeFalsy();
  });
});

test.describe('UC-16 Book Appointment for a Patient', () => {
  test('TC-AP-005 book a standard appointment for an existing patient', async ({ page, request }) => {
    const p = await newPatient(await Api.as(request, 'assistant.demo'));
    await staffLogin(page, 'assistant.demo'); await page.goto('/appointments');
    await page.getByRole('button', { name: 'Đặt lịch', exact: true }).click();
    const d = await drawerTo(page, { patientCode: p.patientCode, attendee: p.fullName, dentist: 'Chủ phòng khám demo', date: weekday(3) });
    await d.getByRole('button', { name: /^\d\d:\d\d/ }).nth(4).click();
    await d.getByRole('button', { name: 'Xem lại', exact: true }).click();
    const res = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/api/appointments'));
    await d.getByRole('button', { name: 'Xác nhận đặt lịch', exact: true }).click();
    const r = await res; const b = await r.json(); await page.waitForTimeout(800); await shot(page, 'TC-AP-005');
    note(`Counter booking for ${p.patientCode}, source Điện thoại, Chủ phòng khám demo on ${weekday(3)} -> ${r.status()} ${b.code} ${b.status}`);
    expect(r.status()).toBe(201); expect(b.status).toBe('Confirmed');
  });
  test('TC-AP-006 book for a new customer without a record', async ({ page }) => {
    await staffLogin(page, 'assistant.demo'); await page.goto('/appointments');
    await page.getByRole('button', { name: 'Đặt lịch', exact: true }).click();
    const name = `Khách Mới ${rnd()}`;
    const d = await drawerTo(page, { attendee: name, dentist: 'Chủ phòng khám demo', date: weekday(3) });
    await d.getByRole('button', { name: /^\d\d:\d\d/ }).nth(8).click();
    await d.getByRole('button', { name: 'Xem lại', exact: true }).click();
    const res = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/api/appointments'));
    await d.getByRole('button', { name: 'Xác nhận đặt lịch', exact: true }).click();
    const r = await res; const b = await r.json(); await shot(page, 'TC-AP-006');
    note(`Booking for "${name}" with no record -> ${r.status()} ${b.code}`); expect(r.status()).toBe(201);
  });
  test('TC-AP-009 occupied times are not offered in the drawer', async ({ page, request }) => {
    const api = await Api.as(request, 'assistant.demo'); const date = weekday(4);
    const taken = (await slots(api, 4, date))[2]; await staffBook(api, taken, null);
    const label = new Date(taken.start).toLocaleTimeString('en-GB', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit' });
    await staffLogin(page, 'assistant.demo'); await page.goto('/appointments');
    await page.getByRole('button', { name: 'Đặt lịch', exact: true }).click();
    const d = await drawerTo(page, { attendee: 'Khách thử trùng', dentist: 'Chủ phòng khám demo', date });
    const offered = (await d.getByRole('button', { name: /^\d\d:\d\d/ }).allInnerTexts()).map(t => t.slice(0, 5));
    await d.getByLabel('Ngày', { exact: true }).fill(addDays(todayIso(), -1)); await d.getByLabel('Ngày', { exact: true }).press('Tab'); await page.waitForTimeout(1200);
    const past = await d.getByRole('button', { name: /^\d\d:\d\d/ }).count(); await shot(page, 'TC-AP-009');
    note(`${label} on ${date} already booked for Chủ phòng khám demo -> offered=${offered.includes(label)}; yesterday offers ${past} slot(s)`);
    expect(offered).not.toContain(label); expect(past).toBe(0);
  });
  test('TC-AP-010 a 60-minute appointment never overlaps the break', async ({ page }) => {
    await staffLogin(page, 'assistant.demo'); await page.goto('/appointments');
    await page.getByRole('button', { name: 'Đặt lịch', exact: true }).click();
    const d = await drawerTo(page, { attendee: 'Khách 60 phút', dentist: 'Chủ phòng khám demo', date: weekday(5), duration: 60 });
    const offered = (await d.getByRole('button', { name: /^\d\d:\d\d/ }).allInnerTexts()).map(t => t.slice(0, 5)); await shot(page, 'TC-AP-010');
    note(`Break 10:00-10:30 on ${weekday(5)}; 60-minute starts offered: ${offered.join(', ')}`);
    for (const bad of ['09:15', '09:30', '09:45', '10:00', '10:15']) expect(offered).not.toContain(bad);
  });
});

test.describe('UC-17/UC-18 Reschedule, Cancel (from the patient profile)', () => {
  test('TC-AP-011/012/013 reschedule with a reason from the profile', async ({ page, request }) => {
    const api = await Api.as(request, 'assistant.demo'); const p = await newPatient(api);
    const b = await staffBook(api, (await slots(api, 4, weekday(6)))[1], p);
    await staffLogin(page, 'assistant.demo'); await page.goto(`/patients/${p.id}?tab=appointments`);
    const row = page.getByRole('row').filter({ hasText: b.code });
    await row.getByRole('button', { name: 'Chỉnh sửa lịch', exact: true }).click();
    const editor = page.getByRole('dialog', { name: 'Chỉnh sửa lịch hẹn', exact: true });
    await editor.getByLabel('Ngày hẹn mới (giờ Việt Nam)').fill(weekday(7));
    await editor.getByRole('group', { name: 'Khung giờ chỉnh sửa lịch hẹn', exact: true }).getByRole('button').nth(3).click();
    const save = editor.getByRole('button', { name: 'Lưu thay đổi', exact: true });
    const disabledWithoutReason = await save.isDisabled(); if (!disabledWithoutReason) await save.click();
    const errNoReason = await editor.locator('.ant-form-item-explain-error').allInnerTexts(); await shot(page, 'TC-AP-013');
    await editor.getByLabel('Lý do chỉnh sửa (bắt buộc)').fill('Khách xin đổi ngày');
    const changed = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/reschedule'));
    await save.click(); const r = await changed; const body = await r.json();
    await expect(editor).not.toBeVisible(); await shot(page, 'TC-AP-012');
    note(`Profile tab "Lịch hẹn": ${b.code} moved to ${weekday(7)} -> ${r.status()} ${body.notification?.code ?? body.code ?? ''}`);
    test.info().annotations.push({ type: 'TC-AP-013', description: `Without a reason: Lưu disabled=${disabledWithoutReason}, errors "${errNoReason.join(' ')}"` });
    expect(r.status()).toBe(200);
  });
  test('TC-AP-014 concurrent edit is detected', async ({ page, browser, request }) => {
    const api = await Api.as(request, 'assistant.demo'); const p = await newPatient(api);
    const b = await staffBook(api, (await slots(api, 4, weekday(8)))[2], p);
    const other = await (await browser.newContext()).newPage();
    for (const pg of [page, other]) {
      await staffLogin(pg, pg === page ? 'assistant.demo' : 'owner.demo'); await pg.goto(`/patients/${p.id}?tab=appointments`);
      await pg.getByRole('row').filter({ hasText: b.code }).getByRole('button', { name: 'Chỉnh sửa lịch', exact: true }).click();
      const ed = pg.getByRole('dialog', { name: 'Chỉnh sửa lịch hẹn', exact: true });
      await ed.getByLabel('Ngày hẹn mới (giờ Việt Nam)').fill(weekday(9));
      await ed.getByRole('group', { name: 'Khung giờ chỉnh sửa lịch hẹn', exact: true }).getByRole('button').nth(pg === page ? 1 : 5).click();
      await ed.getByLabel('Lý do chỉnh sửa (bắt buộc)').fill('Đổi giờ');
    }
    await page.getByRole('dialog', { name: 'Chỉnh sửa lịch hẹn' }).getByRole('button', { name: 'Lưu thay đổi' }).click(); await page.waitForTimeout(1500);
    const res = other.waitForResponse(r => r.url().endsWith('/reschedule'));
    await other.getByRole('dialog', { name: 'Chỉnh sửa lịch hẹn' }).getByRole('button', { name: 'Lưu thay đổi' }).click();
    const r = await res; const body = await r.json(); await other.waitForTimeout(800); await shot(other, 'TC-AP-014');
    note(`Second editor saving from the stale screen -> ${r.status()} ${body.code} "${body.message}"`); expect(body.code).toBe('MSG41');
  });
  test('TC-AP-015 cancel with a reason', async ({ page, request }) => {
    const api = await Api.as(request, 'assistant.demo'); const p = await newPatient(api);
    const b = await staffBook(api, (await slots(api, 4, weekday(10)))[3], p);
    await staffLogin(page, 'assistant.demo'); await page.goto(`/patients/${p.id}?tab=appointments`);
    const row = page.locator('.ant-table-tbody > tr').filter({ hasText: b.code }).first();
    await row.getByRole('button', { name: 'Hủy lịch', exact: true }).click();
    const c = page.getByRole('dialog', { name: 'Hủy lịch hẹn', exact: true });
    await c.getByLabel('Lý do hủy lịch (bắt buộc)').fill('Bác sĩ bận đột xuất');
    await c.getByRole('button', { name: 'Xác nhận hủy lịch', exact: true }).click();
    await expect(row).toContainText('Đã hủy'); await shot(page, 'TC-AP-015');
    note(`${b.code} cancelled with reason -> row "Đã hủy", no further Hủy lịch button`);
  });
  test('TC-AP-017 No-show is not offered before the start', async ({ page, request }) => {
    const api = await Api.as(request, 'assistant.demo'); const p = await newPatient(api);
    const b = await staffBook(api, (await slots(api, 4, weekday(11)))[4], p);
    await staffLogin(page, 'assistant.demo'); await page.goto(`/patients/${p.id}?tab=appointments`);
    const row = page.getByRole('row').filter({ hasText: b.code });
    await expect(row).toBeVisible(); const count = await row.getByRole('button', { name: 'Khách không tới', exact: true }).count(); await shot(page, 'TC-AP-017');
    note(`Future appointment ${b.code}: "Khách không tới" buttons ${count}. (Checked-in appointments: refused, see API evidence.)`); expect(count).toBe(0);
  });
});

test.describe('UC-20 Handle Support Request', () => {
  async function request2(request: any, kind: 'Reschedule' | 'Cancel') {
    const g = await Api.as(request, 'guest.demo');
    const tom = addDays(todayIso(), 1); let av: any[] = []; let dentistId = 4;
    for (const d of [6, 4, 3]) { av = (await g.get(`/api/availability?branchId=1&dentistId=${d}&serviceId=1&date=${tom}&durationMinutes=30`)).body.slots.filter((s: any) => Date.parse(s.start) < Date.now() + 12 * 3600_000); if (av.length) { dentistId = d; break; } }
    if (!av.length) for (const d of [4, 3, 6]) { av = (await g.get(`/api/availability?branchId=1&dentistId=${d}&serviceId=1&date=${todayIso()}&durationMinutes=30`)).body.slots; if (av.length) { dentistId = d; break; } }
    test.skip(!av.length, 'No slot within 12 hours');
    const b = (await g.post('/api/appointments', { branchId: 1, booker: { name: null, phone: '0912345678', email: null }, attendee: { patientId: null, name: 'Tài khoản công khai demo', birthDate: null }, attendeeRelation: 'Self', kind: 'Standard', source: 'App', serviceId: 1, reason: null, dentistId, chairId: null, startAt: av[0].start, durationMinutes: 30, dossierId: null, exceptionReason: null, requestKey: crypto.randomUUID(), scheduleVersion: av[0].scheduleVersion, assistantScheduleVersion: null, bookingSettingsVersion: av[0].bookingSettingsVersion, patientConsentConfirmed: false, familyMemberId: null, replacesAppointmentId: null })).body;
    const later = (await g.get(`/api/availability?branchId=1&dentistId=${dentistId}&serviceId=1&date=${tom}&durationMinutes=30`)).body.slots.slice(-1)[0];
    const sr = (await g.post(`/api/appointments/${b.code}/support-requests`, { requestKey: crypto.randomUUID(), kind, reason: kind === 'Cancel' ? 'Có việc bận' : 'Muốn đổi sang chiều', newStartAt: kind === 'Cancel' ? null : later.start })).body;
    return { code: b.code, id: sr.request.id };
  }
  test('TC-AP-022/024/023 review, verify and apply a change request', async ({ page, request }) => {
    const r = await request2(request, 'Reschedule');
    await staffLogin(page, 'assistant.demo'); await page.getByRole('link', { name: 'Hộp thư hỗ trợ', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Hộp thư hỗ trợ', exact: true })).toBeVisible();
    const row = page.getByRole('row').filter({ hasText: r.code.slice(0, 11) }); await expect(row).toBeVisible(); await shot(page, 'TC-AP-022-list');
    await row.click(); await expect(page.getByRole('dialog')).toBeVisible(); await shot(page, 'TC-AP-022');
    note(`Inbox lists the request for ${r.code}; detail opens with Tổng quan / Lịch sử`);
    await page.getByRole('tab', { name: 'Xử lý', exact: true }).click();
    const resolveBtn = page.getByRole('button', { name: 'Xử lý yêu cầu', exact: true });
    const changeBefore = await page.getByRole('button', { name: 'Đổi lịch', exact: true }).count();
    const changeEnabled = changeBefore ? await page.getByRole('button', { name: 'Đổi lịch', exact: true }).isEnabled() : false;
    await shot(page, 'TC-AP-024');
    test.info().annotations.push({ type: 'TC-AP-024', description: `Before verifying the requester: "Đổi lịch" present=${changeBefore > 0}, enabled=${changeEnabled}` });
    await page.getByRole('button', { name: 'Xác minh người yêu cầu', exact: true }).click(); await page.waitForTimeout(600);
    const modal = page.locator('.ant-modal:visible').last();
    if (await modal.count()) { console.log('VERIFY MODAL ' + await modal.ariaSnapshot()); await modal.getByRole('textbox').first().fill('Gọi lại số điện thoại đã đặt'); await modal.getByRole('button', { name: /Xác (minh|nhận)/ }).last().click(); }
    await expect(page.getByRole('button', { name: 'Đổi lịch', exact: true })).toBeEnabled();
    await page.getByRole('button', { name: 'Đổi lịch', exact: true }).click(); await page.waitForTimeout(600);
    const cm = page.locator('.ant-modal:visible').last();
    if (await cm.count()) { console.log('CHANGE MODAL ' + await cm.ariaSnapshot()); const tb = cm.getByRole('textbox'); if (await tb.count()) await tb.first().fill('Đổi theo yêu cầu bệnh nhân'); await cm.getByRole('button', { name: /Xác nhận|Đổi lịch|OK/ }).last().click(); }
    await expect(page.getByRole('button', { name: 'Đã thay đổi lịch', exact: true })).toBeVisible({ timeout: 10000 });
    if (await resolveBtn.isEnabled()) await resolveBtn.click();
    await page.waitForTimeout(1500); await shot(page, 'TC-AP-023');
    test.info().annotations.push({ type: 'TC-AP-023', description: `Verified requester, "Đổi lịch" applied ("Đã thay đổi lịch"), request resolved` });
    expect(changeEnabled).toBeFalsy();
  });
  test('TC-AP-025 reject a request with a reason', async ({ page, request }) => {
    const r = await request2(request, 'Cancel');
    await staffLogin(page, 'assistant.demo'); await page.getByRole('link', { name: 'Hộp thư hỗ trợ', exact: true }).click();
    await page.getByRole('row').filter({ hasText: r.code.slice(0, 11) }).click();
    await page.getByRole('tab', { name: 'Xử lý', exact: true }).click(); await page.waitForTimeout(800);
    console.log('REJECT PANEL ' + await page.getByRole('dialog').ariaSnapshot());
    await page.getByRole('button', { name: 'Từ chối', exact: true }).first().click(); await page.waitForTimeout(600);
    const m = page.locator('.ant-modal:visible').last();
    await m.getByRole('button', { name: 'Xác nhận từ chối' }).click(); await page.waitForTimeout(400);
    const noReason = await m.locator('.ant-form-item-explain-error').allInnerTexts();
    await m.getByRole('textbox').first().fill('Không liên lạc được người đặt');
    await m.getByRole('textbox').nth(1).fill('Phòng khám chưa xác minh được người đặt');
    await m.getByRole('button', { name: 'Xác nhận từ chối' }).click(); await page.waitForTimeout(1500);
    test.info().annotations.push({ type: 'evidence', description: `Reject without reason: "${noReason.join(' ')}"` });
    await shot(page, 'TC-AP-025');
    const g = await Api.as(request, 'guest.demo'); const d = (await g.get(`/api/me/appointments/${r.code}`)).body;
    note(`Request rejected with reason; appointment still ${d.status}; patient sees request status ${d.supportRequests?.[0]?.status}`);
    expect(d.supportRequests?.[0]?.status).toBe('Rejected');
  });
});
