import { test, expect, type Page, type Browser } from '@playwright/test';
import { Api, addDays, customerLogin, note, shot, todayIso, API, rnd } from './helpers';

const BRANCH = 'S09 - Chi nhánh thử nghiệm';
const dd = (iso: string) => iso.split('-').reverse().join('/');
const nextWeekday = (offset: number) => { let d = addDays(todayIso(), offset); while (new Date(d + 'T00:00:00Z').getUTCDay() === 0) d = addDays(d, 1); return d; };

async function pick(page: Page, combo: string, text: string) {
  await page.getByRole('combobox', { name: combo }).click(); await page.waitForTimeout(300);
  await page.locator('.ant-select-dropdown:visible').last().getByText(text, { exact: true }).click();
}
/** Walks the 5-step web booking flow up to the review step and returns the slot buttons' labels. */
async function toSchedule(page: Page, opts: { relative?: boolean; dentist?: string; date: string }) {
  await page.goto('/customer/book');
  if (opts.relative) await page.getByRole('button', { name: /Người thân/ }).click();
  await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
  await pick(page, 'Chi nhánh', BRANCH);
  await pick(page, 'Bác sĩ', opts.dentist ?? 'Nha sĩ demo');
  await page.getByRole('textbox', { name: 'Ngày khám' }).fill(opts.date); await page.keyboard.press('Tab');
  await page.waitForTimeout(1200);
  return page.getByRole('region', { name: 'Chọn khung giờ khám' }).getByRole('button');
}
async function book(page: Page, opts: { relative?: boolean; dentist?: string; date: string; slot?: string; attendee?: string }) {
  const slots = await toSchedule(page, opts);
  const button = opts.slot ? slots.filter({ hasText: opts.slot }).first() : slots.first();
  const label = await button.innerText(); await button.click();
  await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
  if (opts.attendee) await page.getByLabel('Họ tên người khám', { exact: true }).fill(opts.attendee);
  await page.getByRole('button', { name: 'Xem lại', exact: true }).click();
  await page.getByRole('button', { name: 'Xác nhận đặt lịch', exact: true }).click();
  const status = page.getByRole('status').filter({ hasText: /Đặt lịch thành công|không|Không/ });
  await expect(status.first()).toBeVisible({ timeout: 20_000 });
  const text = await page.locator('main').innerText();
  return { code: text.match(/AP-[0-9A-F]{32}/)?.[0], text, slot: label };
}

test.describe('UC-08 Book Appointment Online', () => {
  test('TC-PB-001 book an examination for myself (5 steps)', async ({ page, request }) => {
    await customerLogin(page, 'guest.demo');
    const r = await book(page, { date: nextWeekday(3) });
    expect(r.text).toContain('Đặt lịch thành công'); expect(r.text).toContain('Đã xác nhận'); expect(r.code).toBeTruthy();
    await shot(page, 'TC-PB-001');
    await page.getByRole('link', { name: 'Xem và quản lý lịch hẹn' }).click();
    await expect(page.getByText(r.code!).first()).toBeVisible();
    const asst = await Api.as(request, 'assistant.demo');
    const cal = await asst.get(`/api/appointments/calendar?branchId=1&from=${nextWeekday(3)}&to=${nextWeekday(3)}`);
    expect(JSON.stringify(cal.body)).toContain(r.code!);
    note(`Booked ${r.code} at ${r.slot} on ${nextWeekday(3)}: "Đặt lịch thành công", status Đã xác nhận; shown in Lịch hẹn của tôi and in the staff calendar`);
  });
  test('TC-PB-002 book for a family member', async ({ page }) => {
    await customerLogin(page, 'guest.demo');
    await page.goto('/customer/family-members'); await page.waitForTimeout(1000);
    const row = page.getByRole('row').filter({ hasText: 'Nguyễn Thị Mẹ' }).first();
    await row.getByRole('button', { name: /Đặt lịch/ }).or(row.getByRole('link', { name: /Đặt lịch/ })).first().click();
    await expect(page).toHaveURL(/\/customer\/book/);
    await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
    await pick(page, 'Chi nhánh', BRANCH); await pick(page, 'Bác sĩ', 'Nha sĩ demo');
    await page.getByRole('textbox', { name: 'Ngày khám' }).fill(nextWeekday(4)); await page.keyboard.press('Tab'); await page.waitForTimeout(1200);
    await page.getByRole('region', { name: 'Chọn khung giờ khám' }).getByRole('button').nth(2).click();
    await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
    const attendee = await page.getByLabel('Họ tên người khám', { exact: true }).inputValue();
    await page.getByRole('button', { name: 'Xem lại', exact: true }).click();
    await page.getByRole('button', { name: 'Xác nhận đặt lịch', exact: true }).click();
    await expect(page.getByText('Đặt lịch thành công')).toBeVisible({ timeout: 20_000 });
    await shot(page, 'TC-PB-002'); note(`Booked from the family member row "Đặt lịch": examinee prefilled "${attendee}"; booking confirmed`);
    expect(attendee).toContain('Nguyễn Thị Mẹ');
  });
  test('TC-PB-003 any dentist with a free chair', async ({ page }) => {
    await customerLogin(page, 'guest.demo');
    const r = await book(page, { dentist: 'Bất kỳ bác sĩ (ghế trống)', date: nextWeekday(5) });
    expect(r.text).toContain('Đặt lịch thành công'); await shot(page, 'TC-PB-003');
    await page.getByRole('link', { name: 'Xem và quản lý lịch hẹn' }).click(); await page.waitForTimeout(1500);
    const detail = await page.locator('main').innerText();
    const dentist = detail.match(/Nha sĩ : ([^\n]+)/)?.[1] ?? '?';
    note(`"Bất kỳ bác sĩ (ghế trống)" booking ${r.code} assigned to ${dentist}`);
  });
  test('TC-PB-004 slots less than 60 minutes ahead are not offered', async ({ page }) => {
    await customerLogin(page, 'guest.demo');
    const slots = await toSchedule(page, { dentist: 'Nhân viên đa vai trò demo', date: todayIso() });
    const labels = await slots.allInnerTexts();
    const now = new Date(Date.now() + 7 * 3600_000); const minutes = now.getUTCHours() * 60 + now.getUTCMinutes();
    const tooSoon = labels.filter(l => { const [h, m] = l.slice(0, 5).split(':').map(Number); return h * 60 + m < minutes + 60; });
    await shot(page, 'TC-PB-004'); note(`Today's slots offered to the patient: ${labels.join(', ') || 'none'}; within 60 minutes: ${tooSoon.join(', ') || 'none'}`);
    expect(tooSoon).toEqual([]);
  });
  test('TC-PB-005 slot taken by another patient during booking', async ({ page, browser }) => {
    const n = rnd(); const phone = `097${n}4`.slice(0, 10);
    await page.request.post(`${API}/api/auth/register`, { data: { fullName: 'Bệnh nhân B', phoneNumber: phone, email: `b.${n}@example.com`, password: 'Abc12345', confirmPassword: 'Abc12345' } });
    const other = await (await browser.newContext()).newPage();
    await customerLogin(page, 'guest.demo'); await customerLogin(other, phone, 'Abc12345');
    const date = nextWeekday(6);
    for (const p of [page, other]) {
      const slots = await toSchedule(p, { date }); await slots.nth(6).click();
      await p.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
      await p.getByRole('button', { name: 'Xem lại', exact: true }).click();
    }
    await page.getByRole('button', { name: 'Xác nhận đặt lịch', exact: true }).click();
    await expect(page.getByText('Đặt lịch thành công')).toBeVisible({ timeout: 20_000 });
    await other.getByRole('button', { name: 'Xác nhận đặt lịch', exact: true }).click(); await other.waitForTimeout(2500);
    const msg = await other.locator('main').innerText(); await shot(other, 'TC-PB-005');
    note(`Patient A confirmed first; patient B on the same slot sees: ${msg.match(/(Thời gian này[^\n]*|Khung giờ[^\n]*)/)?.[0] ?? msg.slice(0, 200)}`);
    expect(msg).toMatch(/Thời gian này trùng với lịch hẹn đã có|Khung giờ bạn chọn không còn khả dụng|Khung giờ này đã hết chỗ/);
    expect(msg).not.toContain('Đặt lịch thành công');
  });
  test('TC-PB-006 the same patient cannot hold two overlapping appointments', async ({ page }) => {
    await customerLogin(page, 'guest.demo'); const date = nextWeekday(8);
    const first = await book(page, { date, dentist: 'Nha sĩ demo', slot: '13:30' });
    const second = await book(page, { date, dentist: 'Chủ phòng khám demo', slot: '13:30' });
    await shot(page, 'TC-PB-006');
    note(`Self booking 13:30 ${date} with Nha sĩ demo -> ${first.code}; second self booking 13:30 with Chủ phòng khám demo -> ${second.text.includes('Đặt lịch thành công') ? 'ALSO CONFIRMED ' + second.code : 'refused'}`);
    expect(second.text, 'Second overlapping booking for the same account was confirmed (no patient overlap check for an account without a linked record).').not.toContain('Đặt lịch thành công');
  });
  test('TC-PB-007 double-click on confirm creates one appointment', async ({ page }) => {
    await customerLogin(page, 'guest.demo');
    const day = nextWeekday(15);
    const countRows = async () => { await page.goto('/customer/appointments'); await page.waitForTimeout(800);
      await page.getByRole('textbox', { name: 'Từ ngày' }).fill(day); await page.getByRole('textbox', { name: 'Đến ngày' }).fill(day);
      await page.getByRole('button', { name: 'Áp dụng', exact: true }).click(); await page.waitForTimeout(1500);
      return page.getByRole('row').filter({ hasText: dd(day) }).count(); };
    const before = await countRows();
    const slots = await toSchedule(page, { date: day, dentist: 'Chủ phòng khám demo' }); await slots.nth(3).click();
    await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
    await page.getByRole('button', { name: 'Xem lại', exact: true }).click();
    let posts = 0; page.on('request', r => { if (r.url().endsWith('/api/appointments') && r.method() === 'POST') posts++; });
    await page.getByRole('button', { name: 'Xác nhận đặt lịch', exact: true }).dblclick();
    await expect(page.getByText('Đặt lịch thành công')).toBeVisible({ timeout: 20_000 });
    const rows = await countRows();
    await shot(page, 'TC-PB-007'); note(`Double-click sent ${posts} POST request(s); appointments on ${dd(day)} before ${before}, after ${rows}`);
    expect(rows - before).toBe(1);
  });
});

test.describe('UC-09 Reschedule or Cancel My Appointment', () => {
  test('TC-PB-009 list upcoming and past appointments with history', async ({ page }) => {
    await customerLogin(page, 'guest.demo'); await page.goto('/customer/appointments');
    await expect(page.getByRole('tab', { name: 'Sắp tới' })).toBeVisible();
    const upcoming = await page.getByRole('row').count(); await shot(page, 'TC-PB-009-upcoming');
    await page.getByRole('tab', { name: 'Lịch sử' }).click(); await page.waitForTimeout(1000);
    await shot(page, 'TC-PB-009-history');
    await page.getByRole('tab', { name: 'Sắp tới' }).click();
    await page.getByRole('link', { name: /^Xem lịch/ }).first().click();
    await expect(page.getByText('Lịch sử thay đổi')).toBeVisible();
    note(`Upcoming table rows: ${upcoming - 1}; Lịch sử tab opens; detail shows "Lịch sử thay đổi"`);
  });
  test('TC-PB-010 reschedule more than 12 hours ahead', async ({ page }) => {
    await customerLogin(page, 'guest.demo');
    const r = await book(page, { date: nextWeekday(10), slot: '08:30' });
    await page.goto(`/customer/appointments/${r.code}`);
    await page.getByRole('button', { name: 'Đổi lịch', exact: true }).click();
    const dlg = page.getByRole('dialog', { name: 'Đổi lịch hẹn' });
    await dlg.getByRole('group', { name: 'Khung giờ mới' }).getByRole('button', { name: /^14:00/ }).click();
    await dlg.getByRole('textbox', { name: '* Lý do' }).fill('Bận việc buổi sáng');
    await dlg.getByRole('button', { name: 'Xem lại', exact: true }).click();
    await dlg.getByRole('button', { name: /Xác nhận/ }).click();
    await expect(page.getByText(/Đã đổi lịch hẹn sang/)).toBeVisible({ timeout: 20_000 });
    await shot(page, 'TC-PB-010');
    await expect(page.getByText('Bận việc buổi sáng').first()).toBeVisible();
    note(`${r.code} moved 08:30 -> 14:00: MSG30 shown; history lists the change with the reason`);
  });
  test('TC-PB-011 cancel more than 12 hours ahead', async ({ page }) => {
    await customerLogin(page, 'guest.demo');
    const r = await book(page, { date: nextWeekday(10), slot: '09:00' });
    await page.goto(`/customer/appointments/${r.code}`);
    await page.getByRole('button', { name: 'Hủy lịch', exact: true }).click();
    const dlg = page.getByRole('dialog');
    await dlg.getByRole('textbox').first().fill('Không sắp xếp được thời gian');
    await dlg.getByRole('button', { name: /Xem lại|Tiếp tục/ }).first().click().catch(() => {});
    await dlg.getByRole('button', { name: /Xác nhận/ }).click();
    await expect(page.getByText(`Đã hủy lịch hẹn ${r.code}.`)).toBeVisible({ timeout: 20_000 });
    await shot(page, 'TC-PB-011');
    const slots = await toSchedule(page, { date: nextWeekday(10) });
    const free = (await slots.allInnerTexts()).some(l => l.startsWith('09:00'));
    note(`MSG31 "Đã hủy lịch hẹn ${r.code}."; 09:00 offered again=${free}`); expect(free).toBeTruthy();
  });
  test('TC-PB-012/013 inside 12 hours: support request, one open request, withdraw', async ({ page, request }) => {
    await customerLogin(page, 'guest.demo');
    const tomorrow = addDays(todayIso(), 1);
    const slots = await toSchedule(page, { date: tomorrow, dentist: 'Chủ phòng khám demo' });
    const now = new Date(Date.now() + 7 * 3600_000); const left = (24 * 60 - (now.getUTCHours() * 60 + now.getUTCMinutes()));
    const labels = await slots.allInnerTexts();
    const within = labels.filter(l => { const [h, m] = l.slice(0, 5).split(':').map(Number); return left + h * 60 + m < 12 * 60; });
    test.skip(within.length === 0, 'No slot within 12 hours at execution time');
    const r = await book(page, { date: tomorrow, dentist: 'Chủ phòng khám demo', slot: within[0].slice(0, 5) });
    await page.goto(`/customer/appointments/${r.code}`); await page.waitForTimeout(1000);
    const selfChange = await page.getByRole('button', { name: 'Đổi lịch', exact: true }).count();
    expect(selfChange).toBe(0);
    const info = await page.locator('main').innerText();
    await page.getByRole('button', { name: 'Yêu cầu hủy lịch', exact: true }).click();
    const dlg = page.getByRole('dialog');
    console.log('SUPPORT DIALOG ' + await dlg.ariaSnapshot());
    await dlg.getByRole('textbox').last().fill('Có việc đột xuất');
    await dlg.getByRole('button', { name: /Gửi|Xác nhận|Xem lại/ }).last().click();
    if (await dlg.getByRole('button', { name: /Gửi|Xác nhận/ }).count()) await dlg.getByRole('button', { name: /Gửi|Xác nhận/ }).last().click();
    await expect(page.getByText(/Đã gửi yêu cầu hỗ trợ/)).toBeVisible({ timeout: 20_000 });
    await shot(page, 'TC-PB-012');
    const asst = await Api.as(request, 'assistant.demo');
    const inbox = await asst.get('/api/support-requests?statuses=Pending');
    expect(JSON.stringify(inbox.body)).toContain(r.code!);
    note(`Booked ${r.code} at ${within[0]} tomorrow (<12 h): Đổi lịch buttons ${selfChange}; notice "${info.match(/[^\n]*12 giờ[^\n]*/)?.[0] ?? ''}"; support request sent (MSG28) and in the staff inbox`);
    // TC-PB-013
    await page.reload(); await expect(page.getByText(r.code!).first()).toBeVisible({ timeout: 20_000 }); await page.waitForTimeout(800);
    console.log('AFTER REQUEST ' + await page.locator('main').ariaSnapshot());
    const again = await page.getByRole('button', { name: /^Yêu cầu (đổi|hủy) lịch$/ }).count();
    await page.getByRole('button', { name: 'Rút yêu cầu', exact: true }).first().click(); await page.waitForTimeout(600);
    const confirm = page.locator('.ant-popover:visible, .ant-modal-wrap:visible').getByRole('button', { name: /Rút|Xác nhận|OK|Đồng ý/ });
    if (await confirm.count()) await confirm.last().click();
    await page.waitForTimeout(2000); await shot(page, 'TC-PB-013');
    const after = await page.locator('main').innerText();
    test.info().annotations.push({ type: 'TC-PB-013', description: `While the request is pending the "Yêu cầu đổi/hủy lịch" buttons shown: ${again} (only "Xem và rút yêu cầu"); after "Rút yêu cầu": ${after.includes('Đã rút yêu cầu hỗ trợ.') ? '"Đã rút yêu cầu hỗ trợ." and status Đã rút' : after.slice(0, 160)}` });
    expect(again).toBe(0); expect(after).toMatch(/Đã rút/i);
  });
  test('TC-PB-014 rescheduling to a slot taken meanwhile keeps the original', async ({ page, request }) => {
    await customerLogin(page, 'guest.demo'); const date = nextWeekday(11);
    const r = await book(page, { date, slot: '08:00' });
    await page.goto(`/customer/appointments/${r.code}`);
    await page.getByRole('button', { name: 'Đổi lịch', exact: true }).click();
    const dlg = page.getByRole('dialog', { name: 'Đổi lịch hẹn' });
    await dlg.getByRole('group', { name: 'Khung giờ mới' }).getByRole('button', { name: /^15:00/ }).click();
    await dlg.getByRole('textbox', { name: '* Lý do' }).fill('Đổi giờ');
    await dlg.getByRole('button', { name: 'Xem lại', exact: true }).click();
    // another patient takes 15:00 before confirmation
    const n = rnd(); const phone = `098${n}5`.slice(0, 10);
    await page.request.post(`${API}/api/auth/register`, { data: { fullName: 'Bệnh nhân C', phoneNumber: phone, email: `c.${n}@example.com`, password: 'Abc12345', confirmPassword: 'Abc12345' } });
    const c = await Api.as(request, phone, 'Abc12345');
    const av = (await c.get(`/api/availability?branchId=1&dentistId=3&serviceId=1&date=${date}&durationMinutes=30`)).body.slots.find((s: any) => s.start.slice(11, 16) === '15:00');
    const taken = await c.post('/api/appointments', { branchId: 1, booker: { name: null, phone, email: null }, attendee: { patientId: null, name: 'Bệnh nhân C', birthDate: null }, attendeeRelation: 'Self', kind: 'Standard', source: 'App', serviceId: 1, reason: null, dentistId: 3, chairId: null, startAt: av.start, durationMinutes: 30, dossierId: null, exceptionReason: null, requestKey: crypto.randomUUID(), scheduleVersion: av.scheduleVersion, assistantScheduleVersion: null, bookingSettingsVersion: av.bookingSettingsVersion, patientConsentConfirmed: false, familyMemberId: null, replacesAppointmentId: null });
    expect(taken.status).toBe(201);
    await dlg.getByRole('button', { name: /Xác nhận/ }).click();
    await expect(page.getByText('Không thể đổi sang khung giờ đã chọn. Lịch hẹn cũ của bạn vẫn được giữ nguyên.').first()).toBeVisible({ timeout: 20_000 });
    await shot(page, 'TC-PB-014'); note('Slot taken by another patient before confirming: MSG29 shown; original 08:00 kept');
  });
});
