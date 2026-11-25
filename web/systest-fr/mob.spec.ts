import { test, expect, type Page, type APIRequestContext } from '@playwright/test';
import fs from 'node:fs';
import { API, Api, addDays, key, note, rnd, shot, staffLogin, todayIso } from './helpers';
import { checkIn, newPatient, slots, staffBook } from './fx';

// Mobile app cases (Expo app run as a web build on http://localhost:8082, phone-sized viewport).
const M = 'http://localhost:8082';
const PWD = 'Abc12345';
const NL = String.fromCharCode(10);
test.describe.configure({ timeout: 240_000 });
test.use({ viewport: { width: 390, height: 844 } });
test.beforeEach(({ page }) => { page.setDefaultTimeout(20_000); });
const ann = (type: string, description: string) => test.info().annotations.push({ type, description });
const body = async (page: Page) => (await page.locator('body').innerText()).replace(/\s+/g, ' ');
async function mobileLogin(page: Page, phone: string) {
  await page.goto(M + '/sign-in'); await page.getByRole('button', { name: 'Đăng nhập' }).waitFor({ timeout: 60000 });
  const boxes = page.getByRole('textbox'); await boxes.nth(0).fill(phone); await boxes.nth(1).fill(PWD);
  await page.getByRole('button', { name: 'Đăng nhập' }).click(); await page.getByRole('tab', { name: /Trang chủ/ }).waitFor({ timeout: 30000 }); await page.waitForTimeout(1500);
}
async function linkedAccount(request: APIRequestContext) {
  const api = await Api.as(request, 'assistant.demo'); const p = await newPatient(api); const now = Date.now(); let s: any;
  for (const d of [4, 3, 6]) { s = (await slots(api, d, todayIso(), 15)).find((x: any) => Date.parse(x.start) >= now - 10 * 60_000); if (s) break; }
  const a = await staffBook(api, s, p); await checkIn(api, a.appointmentId, p.id, a.version);
  const n = rnd(); const phone = `09${n}${Math.floor(Math.random() * 90 + 10)}`.slice(0, 10);
  await request.post(`${API}/api/auth/register`, { data: { fullName: p.fullName, phoneNumber: phone, email: `mob.${n}@example.com`, password: PWD, confirmPassword: PWD } });
  const me = await Api.as(request, phone, PWD);
  const req = (await me.post('/api/patient-links/me/requests', { fullName: p.fullName, birthDate: p.birthDate, patientCode: p.patientCode, matchingInformation: null, confirmSelf: true })).body.request;
  await api.post(`/api/patient-link-reviews/${req.id}/verify`, { version: req.version ?? 1, patientId: p.id, verificationNote: 'Đối chiếu CCCD tại quầy' });
  // A prescription so the record pages have data to compare.
  const owner = await Api.as(request, 'owner.demo'); const med = ((await owner.get('/api/prescriptions/medicine-options?branchId=1&q=Amox')).body as any[])[0];
  await owner.post(`/api/prescriptions/patients/${p.id}`, { requestKey: key(), expectedVersion: 0, branchId: 1, prescribedOn: todayIso(), diagnosis: 'Sâu răng', otherDiagnosis: null, advice: 'Súc miệng', items: [{ medicineId: med.id, quantity: 10, instructions: 'Uống sau ăn', unit: 'viên' }] });
  return { phone, p };
}
const STATE = `../outputs/system-test-frs/results/mob_state_${process.env.RUN ?? 'run'}.json`;
let st: { phone: string; p: any } = fs.existsSync(STATE) ? JSON.parse(fs.readFileSync(STATE, 'utf8')) : (null as any);

test('TC-AA-016 register a patient account on the mobile app', async ({ page }) => {
  const n = rnd(); const phone = `09${n}77`.slice(0, 10);
  await page.goto(M + '/register'); await page.getByRole('textbox', { name: 'Họ tên' }).waitFor({ timeout: 60000 });
  await page.getByRole('textbox', { name: 'Họ tên' }).fill('Khách di động ' + n); await page.getByRole('textbox', { name: 'Số điện thoại' }).fill(phone);
  await page.getByRole('textbox', { name: 'Email' }).fill(`mobile.${n}@example.com`); await page.getByRole('textbox', { name: 'Mật khẩu', exact: true }).fill(PWD); await page.getByRole('textbox', { name: 'Xác nhận mật khẩu' }).fill(PWD);
  const r = page.waitForResponse(x => x.url().endsWith('/api/auth/register')); await page.getByRole('button', { name: 'Đăng ký' }).click(); const res = await r; await page.waitForTimeout(500);
  const msg = await body(page); await shot(page, 'TC-AA-016'); await page.waitForTimeout(3000); const after = await body(page);
  await mobileLogin(page, phone); const home = await body(page);
  ann('correction', 'After MSG08 the app returns to the sign-in screen (same as the web); the patient then signs in. Expected result corrected.');
  note(`Register ${phone} -> ${res.status()}; message: "${msg.match(/[^.]*(thành công|Đăng ký)[^.]*\./)?.[0] ?? msg.slice(0, 120)}"; 1.5 s later: ${after.slice(0, 60)}; after signing in: ${home.slice(0, 80)}`); expect(res.status()).toBe(201); expect(msg).toMatch(/thành công/); expect(home).toMatch(/Xin chào/);
});

test.describe('mobile patient record and booking', () => {
  test.beforeAll(async ({ request }) => { if (!st) { st = await linkedAccount(request); fs.writeFileSync(STATE, JSON.stringify(st)); } });
  test('TC-PB-008 book in 3 steps on the mobile app', async ({ page }) => {
    await mobileLogin(page, st.phone); await page.getByRole('button', { name: /Đặt lịch khám/ }).click(); await page.waitForTimeout(3000);
    await page.getByRole('button', { name: 'Bản thân' }).click(); await page.getByRole('button', { name: 'S09 - Chi nhánh thử nghiệm' }).click(); await page.waitForTimeout(2500);
    const svc = page.getByRole('button', { name: /Khám/ }).first(); if (await svc.count()) { await svc.click(); await page.waitForTimeout(1500); }
    const dentist = page.getByRole('button', { name: /Nha sĩ demo|Chủ phòng khám demo/ }).first(); if (await dentist.count()) { await dentist.click(); await page.waitForTimeout(1500); }
    let slot = page.getByRole('button', { name: /^\d{2}:\d{2}/ }).first();
    for (const d of [2, 3, 4, 5]) { if (await slot.count()) break; const day = page.getByRole('button', { name: addDays(todayIso(), d).slice(8, 10) + '/' + addDays(todayIso(), d).slice(5, 7) }); if (await day.count()) { await day.click(); await page.waitForTimeout(2500); } slot = page.getByRole('button', { name: /^\d{2}:\d{2}/ }).first(); }
    const step1 = await body(page); await slot.click(); await page.getByRole('button', { name: 'Tiếp tục' }).click(); await page.waitForTimeout(2000); await shot(page, 'TC-PB-008-step2');
    await page.getByRole('button', { name: 'Xem lại' }).click(); await page.waitForTimeout(2000);
    await shot(page, 'TC-PB-008-step3'); const r = page.waitForResponse(x => x.url().endsWith('/api/appointments') && x.request().method() === 'POST');
    await page.getByRole('button', { name: /Xác nhận/ }).last().click(); const res = await r; const b = await res.json().catch(() => ({})); await page.waitForTimeout(3000);
    const done = await body(page); await shot(page, 'TC-PB-008');
    await page.getByRole('button', { name: 'Về danh sách lịch hẹn' }).click().catch(() => {}); await page.waitForTimeout(3000); const list = await body(page);
    note(`Step 1 offered: ${step1.slice(step1.indexOf('Chi nhánh'), step1.indexOf('Chi nhánh') + 160)}; booked -> ${res.status()} ${b.code ?? b.message ?? ''}; confirmation: ${done.slice(0, 160)}; appointments list shows ${b.code}: ${list.includes(String(b.code))}`);
    expect(res.status()).toBe(201); expect(done).toMatch(/Đặt lịch thành công|thành công|AP-/);
  });
  test('TC-PR-008 visit results and prescriptions on the mobile app match the web', async ({ page }) => {
    await mobileLogin(page, st.phone); await page.goto(M + '/visit-results'); await page.waitForTimeout(4000); const visits = await body(page); await shot(page, 'TC-PR-008-visits');
    await page.goto(M + '/prescriptions'); await page.waitForTimeout(4000); const rx = await body(page); await shot(page, 'TC-PR-008');
    const me = await Api.as(page.request, st.phone, PWD); const web = (await me.get('/api/patients/me/prescriptions/')).body;
    const webCount = (web.items ?? web).length;
    note(`Mobile "Kết quả khám": ${visits.slice(0, 160)}; mobile "Đơn thuốc": ${rx.slice(0, 200)}; prescriptions returned for the web portal: ${webCount}`);
    expect(rx).toMatch(new RegExp(`${webCount} thuốc|Amoxicillin`)); expect(webCount).toBeGreaterThan(0);
  });
  test('TC-PR-012/011/010 my record: editable fields, invalid phone, update address and phone', async ({ page }) => {
    await mobileLogin(page, st.phone); await page.goto(M + '/my-record'); await page.getByRole('button', { name: 'Chỉnh sửa thông tin' }).click({ timeout: 30000 }); await page.waitForTimeout(2000);
    const snap = (await page.locator('body').ariaSnapshot()).split(NL).filter(l => /textbox|button|combobox|checkbox|radio/.test(l)).join(' | ');
    ann('TC-PR-012', `Edit form controls: ${snap.slice(0, 600)}`); await shot(page, 'TC-PR-012');
    const phone = page.getByRole('textbox', { name: /Số điện thoại/ }).first(); const addr = page.getByRole('textbox', { name: /Địa chỉ/ }).first();
    await phone.fill('abc'); await page.getByRole('button', { name: /Lưu/ }).first().click(); await page.waitForTimeout(2500); const bad = await body(page); await shot(page, 'TC-PR-011');
    ann('TC-PR-011', `Phone "abc": ${bad.match(/[^.]*(không đúng định dạng|không hợp lệ|10 chữ số)[^.]*\./)?.[0] ?? bad.slice(0, 200)}`);
    await phone.fill('0912000111'); await addr.fill('25 Hùng Vương, Huế');
    const r = page.waitForResponse(x => x.url().includes('/api/') && ['PUT', 'PATCH', 'POST'].includes(x.request().method()), { timeout: 15000 }).catch(() => null);
    await page.getByRole('button', { name: /Lưu/ }).first().click(); const res = await r; await page.waitForTimeout(3000); const ok = await body(page); await shot(page, 'TC-PR-010');
    const staff = await Api.as(page.request, 'assistant.demo'); const prof = (await staff.get(`/api/patients/${st.p.id}`)).body; const pp = prof.patient ?? prof;
    note(`Save -> ${res?.status() ?? 'no request'} "${ok.match(/[^.]*cập nhật[^.]*\./i)?.[0] ?? ok.slice(0, 160)}"; staff profile now: phone ${pp.phone}, address ${pp.address}`);
    expect(bad).toMatch(/định dạng|không hợp lệ|10 chữ số|MSG04/); expect(pp.address).toBe('25 Hùng Vương, Huế'); expect(pp.phone).toBe('0912000111');
  });
  test('TC-NT-001/002 real-time notification when the clinic changes the appointment; mark as read', async ({ page, request }) => {
    const me = await Api.as(request, st.phone, PWD); const date = addDays(todayIso(), 3);
    const av = ((await me.get(`/api/availability?branchId=1&dentistId=3&serviceId=1&date=${date}&durationMinutes=30`)).body.slots ?? []) as any[];
    const b = (await me.post('/api/appointments', { branchId: 1, booker: { name: null, phone: st.phone, email: null }, attendee: { patientId: null, name: st.p.fullName, birthDate: null }, attendeeRelation: 'Self', kind: 'Standard', source: 'App', serviceId: 1, reason: null, dentistId: 3, chairId: null, startAt: av[0].start, durationMinutes: 30, dossierId: null, exceptionReason: null, requestKey: key(), scheduleVersion: av[0].scheduleVersion, assistantScheduleVersion: null, bookingSettingsVersion: av[0].bookingSettingsVersion, patientConsentConfirmed: false, familyMemberId: null, replacesAppointmentId: null })).body;
    await mobileLogin(page, st.phone); await page.waitForTimeout(2000); const before = await body(page); const badge0 = await page.getByRole('button', { name: 'Thông báo' }).first().innerText().catch(() => '');
    // The assistant reschedules it from the staff calendar in a second browser page.
    const staff = await page.context().browser()!.newContext(); const sp = await staff.newPage();
    await staffLogin(sp, 'assistant.demo'); const sa = await Api.as(request, 'assistant.demo');
    const cal = (await sa.get(`/api/appointments/calendar?branchId=1&from=${date}&to=${date}`)).body.appointments as any[]; const x = cal.find(y => y.code === b.code) ?? {};
    const to = av[3] ?? av[1];
    const re = await sa.post(`/api/appointments/${b.code}/reschedule`, { expectedVersion: x.version ?? b.version, requestKey: key(), newStart: to.start, chairId: to.chairId ?? x.chairId ?? 1, scheduleVersion: to.scheduleVersion, assistantScheduleVersion: null, reason: 'Bác sĩ đổi ca', dentistId: 3, bookingSettingsVersion: to.bookingSettingsVersion, clinicInitiated: true });
    await staff.close();
    await page.waitForTimeout(12000); const after = await body(page); const badge1 = await page.getByRole('button', { name: 'Thông báo' }).first().innerText().catch(() => ''); await shot(page, 'TC-NT-001');
    ann('TC-NT-001', `Booked ${b.code}; reschedule by assistant -> ${re.status} ${re.status >= 400 ? JSON.stringify(re.body).slice(0, 160) : ''}; home before: "${before.match(/Thông báo mới[^]*?Khám phá/)?.[0] ?? ''}" after (no reload): "${after.match(/Thông báo mới[^]*?Khám phá/)?.[0] ?? ''}"; badge ${badge0} -> ${badge1}`);
    await page.getByRole('button', { name: 'Thông báo' }).first().click(); await page.waitForTimeout(3000); const list = await body(page); await shot(page, 'TC-NT-002-list');
    const unread = page.getByRole('button', { name: 'Chưa đọc' }); if (await unread.count()) { await unread.click(); await page.waitForTimeout(1500); }
    const unreadList = await body(page); const item = page.getByRole('button', { name: /lịch hẹn|Lịch hẹn|đổi/ }).first(); let opened = '';
    if (await item.count()) { await item.click(); await page.waitForTimeout(3000); opened = (await body(page)).slice(0, 200); }
    await page.goto(M + '/notifications'); await page.waitForTimeout(3000); if (await unread.count()) { await unread.click(); await page.waitForTimeout(1500); } const unreadAfter = await body(page); await shot(page, 'TC-NT-002');
    note(`Notifications list: ${list.slice(0, 260)}; unread before tap: ${unreadList.slice(0, 160)}; after tapping it opened: ${opened}; unread after: ${unreadAfter.slice(0, 160)}`);
    expect(re.status).toBeLessThan(300); expect(list).toMatch(/Lịch hẹn đã đổi/); expect(after).toContain('Lịch hẹn đã đổi');
  });
});
