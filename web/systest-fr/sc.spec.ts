import { test, expect, type Page } from '@playwright/test';
import { Api, addDays, key, note, shot, staffLogin, todayIso } from './helpers';
import { slots, staffBook } from './fx';

async function choose(page: Page, name: string, value: string) {
  const box = page.getByRole('combobox', { name, exact: true });
  await box.focus(); await box.press('ArrowDown'); await page.waitForTimeout(300);
  const dd = page.locator('.ant-select-dropdown:visible').last();
  const opts = dd.locator('.ant-select-item-option');
  for (let tries = 0; tries < 60; tries++) {
    const texts = await opts.allInnerTexts();
    const i = texts.findIndex(t => t.trim() === value);
    if (i >= 0) { await opts.nth(i).click(); return; }
    await dd.locator('.rc-virtual-list-holder').first().evaluate(e => e.scrollBy(0, 120)).catch(() => {});
    await page.waitForTimeout(80);
  }
  throw new Error(`option "${value}" not found in ${name}`);
}
async function options(page: Page, name: string) {
  const box = page.getByRole('combobox', { name, exact: true });
  await box.focus(); await box.press('ArrowDown'); await page.waitForTimeout(400);
  const list = page.locator('.ant-select-dropdown:visible').last();
  const seen = new Set<string>();
  for (let i = 0; i < 12; i++) { (await list.locator('.ant-select-item-option').allInnerTexts()).forEach(t => seen.add(t)); await list.locator('.rc-virtual-list-holder').first().evaluate(e => e.scrollBy(0, 200)).catch(() => {}); await page.waitForTimeout(100); }
  await box.press('Escape'); return [...seen];
}
async function openSchedule(page: Page, who = 'Chủ phòng khám demo') {
  await staffLogin(page, 'owner.demo'); await page.goto('/working-schedules'); await page.waitForTimeout(2000);
  await choose(page, 'Nhân viên / bác sĩ', who); await page.waitForTimeout(1500);
}
const lastGroupOf = (page: Page, day: string) => page.locator('.working-day-row').filter({ has: page.getByText(day, { exact: true }) });

test.describe('UC-21 Set and Publish Working Schedule', () => {
  test('TC-SC-001 weekly template with work and break periods', async ({ page }) => {
    await openSchedule(page);
    const startOptions = await options(page, 'Bắt đầu 1');
    const row = lastGroupOf(page, 'Thứ 2');
    const before = await row.getByRole('group').count();
    await row.getByRole('button', { name: 'Thêm khoảng' }).click();
    const n = await page.getByRole('group', { name: /^Khoảng \d+/ }).count();
    const idx = (await row.getByRole('group').nth(before).getAttribute('aria-label'))?.match(/Khoảng (\d+)/)?.[1] ?? String(before + 1);
    await choose(page, `Loại khoảng ${idx}`, 'Nghỉ');
    await choose(page, `Bắt đầu ${idx}`, '15:00'); await choose(page, `Kết thúc ${idx}`, '15:30');
    await page.getByRole('textbox', { name: `Lý do ${idx}`, exact: true }).fill('Giải lao chiều');
    await page.getByRole('button', { name: 'Kiểm tra ảnh hưởng' }).click(); await page.waitForTimeout(1200);
    await page.getByRole('button', { name: 'Lưu và công bố' }).click();
    await expect(page.getByText('Đã công bố', { exact: true }).first()).toBeVisible({ timeout: 15_000 });
    await shot(page, 'TC-SC-001');
    note(`Break 15:00-15:30 "Giải lao chiều" added on Thứ 2 and published ("Đã công bố"). Time picker offers ${startOptions.length} start times in 30-minute steps (${startOptions.slice(0, 6).join(', ')}...); 10:15 offered=${startOptions.includes('10:15')}`);
    expect(startOptions, 'Break 10:00-10:15 from the test case cannot be entered: the time picker only offers 30-minute steps while GBR-034 states 15-minute steps (server also rejects 15-minute boundaries with MSG40).').toContain('10:15');
  });
  test('TC-SC-002 invalid periods are rejected', async ({ page }) => {
    await openSchedule(page);
    await choose(page, 'Bắt đầu 1', '12:00'); await choose(page, 'Kết thúc 1', '08:00').catch(() => {});
    const endOptions = await options(page, 'Kết thúc 1');
    await page.getByRole('button', { name: 'Lưu và công bố' }).click(); await page.waitForTimeout(1500);
    const text = await page.locator('main').last().innerText(); await shot(page, 'TC-SC-002');
    const msg = text.match(/[^\n]*(không hợp lệ|chưa hợp lệ|MSG40|trùng|phải)[^\n]*/i)?.[0] ?? '';
    note(`Start 12:00 on Thứ 2: end-time picker offers ${endOptions.slice(0, 4).join(', ')}... (no time before the start); publish -> "${msg}"`);
    expect(msg.length > 0 || !endOptions.includes('08:00')).toBeTruthy();
  });
  test('TC-SC-003 one-day exception needs a reason', async ({ page, request }) => {
    await openSchedule(page);
    let fri = addDays(todayIso(), 1); while (new Date(fri + 'T00:00:00Z').getUTCDay() !== 5) fri = addDays(fri, 1); fri = addDays(fri, 42 + 7 * Math.floor(Math.random() * 20));
    await choose(page, 'Loại lịch', 'Ngoại lệ ngày cụ thể');
    await page.getByLabel('Ngày áp dụng', { exact: true }).fill(fri); await page.getByLabel('Ngày áp dụng', { exact: true }).press('Tab'); await page.waitForTimeout(1500);
    await page.getByRole('button', { name: 'Lưu và công bố' }).click();
    await expect(page.getByText('Nhập lý do ngoại lệ, kể cả ngày không làm việc.')).toBeVisible(); await shot(page, 'TC-SC-003-noreason');
    await page.getByLabel('Lý do ngoại lệ', { exact: true }).fill('Họp chuyên môn');
    await page.getByRole('button', { name: 'Lưu và công bố' }).click();
    await expect(page.getByText('Đã công bố', { exact: true }).first()).toBeVisible({ timeout: 15_000 }); await shot(page, 'TC-SC-003');
    const g = await Api.as(request, 'guest.demo');
    const am = (d: string) => g.get(`/api/availability?branchId=1&dentistId=4&serviceId=1&date=${d}&durationMinutes=30`).then(r => (r.body?.slots ?? []).length);
    const [a, b] = [await am(fri), await am(addDays(fri, 7))];
    note(`Exception on ${fri} (day off, "Không làm việc"): no reason -> "Nhập lý do ngoại lệ..."; with "Họp chuyên môn" -> published; slots that Friday ${a}, the following Friday (template) ${b}`);
    expect(a).toBe(0); expect(b).toBeGreaterThan(0);
  });
  test('TC-SC-004 publishing is blocked by confirmed appointments outside the new hours', async ({ page, request }) => {
    const api = await Api.as(request, 'assistant.demo');
    let d = addDays(todayIso(), 15); while (new Date(d + 'T00:00:00Z').getUTCDay() !== 1) d = addDays(d, 1);
    const afternoon = (await slots(api, 4, d)).filter((s: any) => s.start.slice(11, 16) >= '14:00');
    const b = await staffBook(api, afternoon[0], null);
    await openSchedule(page);
    await page.getByLabel('Ngày áp dụng', { exact: true }).fill(d); await page.getByLabel('Ngày áp dụng', { exact: true }).press('Tab'); await page.waitForTimeout(1500);
    const pm = page.getByRole('group', { name: /^Khoảng \d+ · 13:30 → 17:30/ }).first();
    const i = (await pm.getAttribute('aria-label'))?.match(/Khoảng (\d+)/)?.[1];
    await page.getByRole('button', { name: `Xóa khoảng ${i}` }).click();
    await page.getByRole('button', { name: 'Kiểm tra ảnh hưởng' }).click(); await page.waitForTimeout(1500);
    const listed = await page.getByRole('link', { name: new RegExp(b.code.slice(0, 10), 'i') }).count();
    await page.getByRole('button', { name: 'Lưu và công bố' }).click(); await page.waitForTimeout(1500);
    const text = await page.locator('main').last().innerText(); await shot(page, 'TC-SC-004');
    note(`Removing Monday afternoon from ${d}: impact check lists ${b.code}=${listed > 0}; publish -> "${text.match(/[^\n]*ảnh hưởng đến các lịch hẹn[^\n]*/)?.[0] ?? text.match(/[^\n]*MSG32[^\n]*/)?.[0] ?? 'see screenshot'}"`);
    expect(text).toMatch(/ảnh hưởng đến các lịch hẹn đã xác nhận|MSG32/);
    await api.post(`/api/appointments/${b.code}/cancel`, { expectedVersion: b.version, requestKey: key(), reason: 'Dọn dữ liệu kiểm thử', requesterVerification: null, clinicInitiated: true });
  });
});

test.describe('UC-22 Manage Appointment Settings', () => {
  test('TC-SC-005 slot interval of a branch', async ({ page, request }) => {
    await staffLogin(page, 'owner.demo'); await page.goto('/booking-settings'); await page.waitForTimeout(1500);
    await page.getByLabel('Bước chia slot (phút)').fill('30');
    await page.getByRole('button', { name: 'Lưu cài đặt', exact: true }).click();
    await expect(page.getByText(/Đã lưu cấu hình/)).toBeVisible(); await shot(page, 'TC-SC-005');
    const g = await Api.as(request, 'guest.demo');
    const starts = ((await g.get(`/api/availability?branchId=1&dentistId=4&serviceId=1&date=${addDays(todayIso(), 2)}&durationMinutes=30`)).body.slots as any[]).map(s => s.start.slice(14, 16));
    await page.getByLabel('Bước chia slot (phút)').fill('15'); await page.getByRole('button', { name: 'Lưu cài đặt', exact: true }).click();
    await expect(page.getByText(/Đã lưu cấu hình/)).toBeVisible();
    note(`Slot step 15 -> 30 saved ("Đã lưu cấu hình..."); offered minutes: ${[...new Set(starts)].join(', ')}; reverted to 15`);
    expect(starts.every(m => m === '00' || m === '30')).toBeTruthy();
  });
  test('TC-SC-006 online bookable service and duration', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await page.goto('/booking-settings'); await page.waitForTimeout(1500);
    await page.getByRole('spinbutton', { name: 'Thời lượng dịch vụ 1', exact: true }).fill('45');
    await page.getByRole('button', { name: 'Lưu cài đặt', exact: true }).click(); await expect(page.getByText(/Đã lưu cấu hình/)).toBeVisible();
    await shot(page, 'TC-SC-006-settings');
    const p2 = await page.context().newPage();
    const { customerLogin } = await import('./helpers'); await customerLogin(p2, 'guest.demo');
    await p2.goto('/customer/book'); await p2.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
    await p2.getByRole('combobox', { name: 'Chi nhánh' }).click(); await p2.locator('.ant-select-dropdown:visible').getByText('S09 - Chi nhánh thử nghiệm', { exact: true }).click();
    await p2.waitForTimeout(800); const service = await p2.getByRole('textbox', { name: 'Dịch vụ' }).inputValue(); await shot(p2, 'TC-SC-006');
    await page.getByRole('spinbutton', { name: 'Thời lượng dịch vụ 1', exact: true }).fill('30');
    await page.getByRole('button', { name: 'Lưu cài đặt', exact: true }).click(); await expect(page.getByText(/Đã lưu cấu hình/)).toBeVisible();
    note(`Khám duration set to 45: patient booking screen shows service "${service}"; reverted to 30`); expect(service).toContain('45');
  });
  test('TC-SC-007 the branch always keeps an initial examination service', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await page.goto('/booking-settings'); await page.waitForTimeout(1500);
    const name = page.getByRole('textbox', { name: 'Tên dịch vụ 1', exact: true });
    const active = page.getByRole('checkbox', { name: 'Dịch vụ hoạt động 1', exact: true });
    await shot(page, 'TC-SC-007');
    const nameDisabled = await name.isDisabled(); const activeDisabled = await active.isDisabled();
    note(`Initial examination row "Khám": name editable=${!nameDisabled}, active checkbox disabled=${activeDisabled}, no option to remove the "Khám lần đầu / đợt mới" role`);
    expect(nameDisabled).toBeTruthy();
  });
});
