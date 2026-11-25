import { test, expect, type Page } from '@playwright/test';
import fs from 'node:fs';
import { Api, note, rnd, shot, staffLogin, todayIso } from './helpers';
import { newPatient, slots, staffBook } from './fx';

// UC-26 Handle Care Tasks through the staff web (/customer-care).
test.describe.configure({ timeout: 180_000 });
test.beforeEach(({ page }) => { page.setDefaultTimeout(15_000); });
const ann = (type: string, description: string) => test.info().annotations.push({ type, description });
const dlg = (page: Page) => page.locator('.ant-modal-wrap:visible .ant-modal').last();
const drawer = (page: Page) => page.locator('.ant-drawer-open .ant-drawer-content').last();
async function msgs(page: Page) {
  await page.waitForTimeout(1000);
  return (await page.locator('.ant-message-notice, .ant-alert-message, .ant-form-item-explain-error, [role=alert]').allInnerTexts())
    .map(s => s.trim()).filter(Boolean).filter((s, i, a) => a.indexOf(s) === i).join(' | ').slice(0, 300);
}
async function choose(page: Page, combo: any, text?: string | RegExp, type?: string) {
  await combo.click({ force: true }); if (type) { await combo.fill(type); } await page.waitForTimeout(1200);
  const opts = page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option');
  const o = text ? opts.filter({ hasText: text }).first() : opts.first(); const label = (await o.innerText()).trim(); await o.click(); return label;
}
// Shared across worker restarts of one run (a failed test restarts the worker).
const STATE = `../outputs/system-test-frs/results/cc_state_${process.env.RUN ?? 'run'}.json`;
const saved = fs.existsSync(STATE) ? JSON.parse(fs.readFileSync(STATE, 'utf8')) : null;
const typeName: string = saved?.typeName ?? `Gọi nhắc tái khám ${rnd()}`;
let patient: { id: number; patientCode: string; fullName: string } = saved?.patient;

test.describe('UC-26 Handle Care Tasks', () => {
  test.beforeAll(async ({ request }) => {
    if (patient) return;
    // Precondition: a patient who has been examined at branch 1 (the care form only offers such patients).
    const api = await Api.as(request, 'owner.demo');
    const opts = (await api.get('/api/care-tasks/patient-options?branchId=1&search=BN')).body as any[];
    const tasks = JSON.stringify((await api.get('/api/care-tasks/?pageSize=500')).body);
    const p = opts.find(x => !tasks.includes(`"patientId":${x.patientId},`)) ?? opts[0];
    patient = { id: p.patientId, patientCode: p.patientCode, fullName: p.fullName };
    fs.writeFileSync(STATE, JSON.stringify({ typeName, patient }));
  });
  test('TC-CC-001 create a care task', async ({ page }) => {
    await staffLogin(page, 'owner.demo');
    // A care type is needed first (Thiết lập hệ thống › Chẩn đoán / Tên bệnh › Loại chăm sóc khách hàng).
    await page.goto('/settings/diagnoses'); await page.waitForTimeout(2000); await page.getByRole('menuitem', { name: 'Loại chăm sóc khách hàng' }).click(); await page.waitForTimeout(1200);
    await page.getByRole('button', { name: 'Thêm mới', exact: true }).click(); await page.waitForTimeout(800);
    const code = dlg(page).getByRole('textbox', { name: '* Mã' }); if (await code.count()) await code.fill(`CS${rnd()}`);
    await dlg(page).getByRole('textbox', { name: '* Tên' }).fill(typeName); await dlg(page).getByRole('button', { name: 'Lưu', exact: true }).click(); await page.waitForTimeout(1500);
    await page.goto('/customer-care'); await page.waitForTimeout(2000);
    await page.getByRole('button', { name: /TẠO MỚI/ }).click(); await page.waitForTimeout(1000); const m = dlg(page);
    const branch = m.getByRole('combobox', { name: 'Chi nhánh tạo phiếu' }); if (await branch.count()) await choose(page, branch, /thử nghiệm/);
    const who = await choose(page, m.getByRole('combobox', { name: 'Khách hàng' }), patient.patientCode, patient.patientCode);
    await choose(page, m.getByRole('combobox', { name: 'Người phụ trách' }), /Trợ thủ|assistant|Chủ phòng khám/);
    await choose(page, m.getByRole('combobox', { name: 'Loại chăm sóc' }), typeName);
    await m.getByRole('textbox', { name: 'Nội dung' }).fill('Gọi nhắc tái khám sau điều trị');
    const r = page.waitForResponse(x => x.url().includes('/api/care-tasks') && x.request().method() === 'POST');
    await m.getByRole('button', { name: 'Lưu', exact: true }).click(); const res = await r; await page.waitForTimeout(1500);
    await page.getByRole('searchbox', { name: 'Khách hàng' }).fill(patient.patientCode); await page.waitForTimeout(1500);
    const row = (await page.getByRole('row').filter({ hasText: typeName }).first().innerText().catch(() => '')).replace(/\s+/g, ' ');
    await shot(page, 'TC-CC-001'); note(`Care type "${typeName}" added; task for ${who} -> ${res.status()}; work list row: ${row}`);
    expect(res.status()).toBeLessThan(300); expect(row).toContain('Mở');
  });
  test('TC-CC-002 record contact attempts', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await page.goto('/customer-care'); await page.waitForTimeout(2000);
    await page.getByRole('searchbox', { name: 'Khách hàng' }).fill(patient.patientCode); await page.waitForTimeout(1500);
    await page.getByRole('row').filter({ hasText: typeName }).filter({ hasNotText: 'Phiếu sẽ hủy' }).first().click(); await page.waitForTimeout(1500); const d = drawer(page);
    const out: string[] = [];
    for (const [channel, result] of [['Điện thoại', 'Không nghe máy'], ['Điện thoại', 'Đã liên lạc được']]) {
      await choose(page, d.getByRole('combobox', { name: 'Kênh liên hệ' }), channel); await choose(page, d.getByRole('combobox', { name: 'Kết quả liên hệ' }), result);
      await d.getByRole('textbox', { name: 'Bước tiếp theo' }).fill(`Ghi chú: ${result}`);
      const r = page.waitForResponse(x => x.url().includes('/api/care-tasks') && x.request().method() === 'POST'); await d.getByRole('button', { name: 'Lưu liên hệ' }).click();
      out.push(`${channel}/${result} -> ${(await r).status()}`); await page.waitForTimeout(1500);
    }
    const resultChoices = await (async () => { await d.getByRole('combobox', { name: 'Kết quả liên hệ' }).click({ force: true }); await page.waitForTimeout(600); const l = await page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option').allInnerTexts(); await page.keyboard.press('Escape'); return l; })();
    const text = (await d.innerText()).replace(/\s+/g, ' '); await shot(page, 'TC-CC-002');
    note(`Attempts: ${out.join('; ')}. Drawer: ${text.match(/Trạng thái[^A-Z]{0,30}/)?.[0]} … ${text.slice(text.indexOf('Không nghe máy') - 60, text.indexOf('Không nghe máy') + 80)}. Result choices: ${resultChoices.join(', ')} ("Đã đặt lại lịch" is not offered)`);
    ann('correction', `Status after the attempts: "${text.match(/Trạng thái (Mở|Đang xử lý|Đã xong|Đã hủy)/)?.[1]}". S60 §4 sets the status by hand in the edit form; contact attempts do not move it to In progress (expected result corrected)`);
    expect(text).toContain('Không nghe máy'); expect(text).toContain('Đã liên lạc được');
  });
  test('TC-CC-003 done and cancelled tasks are locked', async ({ page, request }) => {
    await staffLogin(page, 'owner.demo'); await page.goto('/customer-care'); await page.waitForTimeout(2000);
    await page.getByRole('searchbox', { name: 'Khách hàng' }).fill(patient.patientCode); await page.waitForTimeout(1500);
    await page.getByRole('row').filter({ hasText: typeName }).filter({ hasNotText: 'Phiếu sẽ hủy' }).first().click(); await page.waitForTimeout(1500); let d = drawer(page);
    const r = page.waitForResponse(x => x.url().includes('/api/care-tasks') && x.request().method() !== 'GET'); await d.getByRole('button', { name: 'Đánh dấu đã xong' }).click(); const done = (await r).status(); await page.waitForTimeout(1500);
    const lockedDone = { edit: await d.getByRole('button', { name: 'Sửa', exact: true }).count(), contact: await d.getByRole('button', { name: 'Lưu liên hệ' }).count() };
    await shot(page, 'TC-CC-003-done'); await page.locator('.ant-drawer-open .ant-drawer-close').last().click(); await page.waitForTimeout(800);
    // Second task: cancel it from the edit form.
    const api = await Api.as(request, 'owner.demo'); const opts = (await api.get('/api/care-tasks/options')).body;
    await page.getByRole('button', { name: /TẠO MỚI/ }).click(); await page.waitForTimeout(1000); let m = dlg(page);
    const branch = m.getByRole('combobox', { name: 'Chi nhánh tạo phiếu' }); if (await branch.count()) await choose(page, branch, /thử nghiệm/);
    await choose(page, m.getByRole('combobox', { name: 'Khách hàng' }), patient.patientCode, patient.patientCode); await choose(page, m.getByRole('combobox', { name: 'Loại chăm sóc' }), typeName);
    await m.getByRole('textbox', { name: 'Nội dung' }).fill('Phiếu sẽ hủy'); await m.getByRole('button', { name: 'Lưu', exact: true }).click(); await page.waitForTimeout(1500);
    await page.getByRole('combobox', { name: 'Trạng thái' }).click({ force: true }); await page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option').filter({ hasText: /^Mở$/ }).click(); await page.waitForTimeout(1500);
    await page.getByRole('row').filter({ hasText: 'Phiếu sẽ hủy' }).first().click(); await page.waitForTimeout(1200); d = drawer(page);
    await d.getByRole('button', { name: 'Sửa', exact: true }).click(); await page.waitForTimeout(1000); m = dlg(page);
    await choose(page, m.getByRole('combobox', { name: 'Trạng thái' }), 'Đã hủy'); const r2 = page.waitForResponse(x => x.url().includes('/api/care-tasks') && x.request().method() !== 'GET');
    await m.getByRole('button', { name: 'Lưu', exact: true }).click(); const cancel = (await r2).status(); await page.waitForTimeout(1500);
    const lockedCancel = { edit: await drawer(page).getByRole('button', { name: 'Sửa', exact: true }).count().catch(() => -1) };
    await shot(page, 'TC-CC-003'); void opts;
    note(`Mark done -> ${done}; then edit buttons ${lockedDone.edit}, contact form ${lockedDone.contact}. Cancel second task -> ${cancel}; edit buttons after cancel ${lockedCancel.edit}`);
    expect(done).toBeLessThan(300); expect(lockedDone.edit + lockedDone.contact).toBe(0); expect(cancel).toBeLessThan(300);
  });
  test('TC-CC-004 a No-show creates a care task', async ({ page, request }) => {
    // An appointment that has started and was not checked in: free slot that already started, else a running confirmed one.
    const api = await Api.as(request, 'assistant.demo'); const now = Date.now(); let p: any; let a: any;
    for (const d of [4, 3, 6]) { const s = (await slots(api, d, todayIso(), 15)).find((x: any) => Date.parse(x.start) <= now && Date.parse(x.end) > now + 60_000); if (s) { p = await newPatient(api); a = await staffBook(api, s, p).catch(() => null); if (a) break; } }
    if (!a) {
      const cal = (await api.get(`/api/appointments/calendar?branchId=1&from=${todayIso()}&to=${todayIso()}`)).body.appointments as any[];
      for (const x of cal.filter(x => x.status === 'Confirmed' && Date.parse(x.startAt) <= now && Date.parse(x.endAt) > now + 60_000)) {
        const hit = (await api.get(`/api/patients?keyword=${encodeURIComponent(x.attendeeName)}`)).body.items.find((y: any) => y.fullName === x.attendeeName);
        if (hit) { p = hit; a = x; break; }
      }
    }
    test.skip(!a, 'No started, not-checked-in appointment at execution time');
    await staffLogin(page, 'assistant.demo'); await page.goto(`/patients/${p.id}?tab=appointments`); await page.waitForTimeout(2000);
    const row = page.locator('.ant-table-tbody > tr').filter({ hasText: a.code }); await row.getByRole('button', { name: 'Khách không tới', exact: true }).click();
    const modal = page.getByRole('dialog', { name: 'Khách không tới', exact: true }); const reason = modal.getByRole('textbox'); if (await reason.count()) await reason.first().fill('Gọi không nghe máy');
    await modal.getByRole('button', { name: 'Xác nhận khách không tới', exact: true }).click(); await page.waitForTimeout(2000);
    await page.goto('/customer-care'); await page.waitForTimeout(2000); await page.getByRole('searchbox', { name: 'Khách hàng' }).fill(p.patientCode); await page.waitForTimeout(2000);
    const rows = await page.getByRole('row').filter({ hasText: p.patientCode }).allInnerTexts(); await shot(page, 'TC-CC-004');
    note(`${a.code} for ${p.patientCode} marked No-show; care tasks listed for the patient: ${rows.length} ${rows.map(x => x.replace(/\s+/g, ' ')).join(' / ')}`);
    expect(rows.length).toBe(1);
  });
  test('TC-CC-005 filter, escalate and export', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await page.goto('/customer-care'); await page.waitForTimeout(2000);
    await page.getByRole('combobox', { name: 'Xem theo' }).click({ force: true }); await page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option').filter({ hasText: 'Xem theo tháng' }).click(); await page.waitForTimeout(1500);
    const all = (await page.getByText(/Tìm thấy \d+ phiếu/).innerText());
    await page.getByRole('combobox', { name: 'Trạng thái' }).click({ force: true }); await page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option').filter({ hasText: /^Đã xong$/ }).click(); await page.waitForTimeout(1500);
    const done = (await page.getByText(/Tìm thấy \d+ phiếu/).innerText()); const statuses = [...new Set((await page.locator('tbody tr.ant-table-row').allInnerTexts()).map(r => (r.match(/Mở|Đang xử lý|Đã xong|Đã hủy/) ?? [''])[0]))];
    const owner = await choose(page, page.getByRole('combobox', { name: 'Người phụ trách' })).catch(() => ''); await page.waitForTimeout(1500); const byOwner = (await page.getByText(/Tìm thấy \d+ phiếu/).innerText());
    const escalate = await page.getByRole('button', { name: /Chuyển cấp|Báo cáo lên|Ưu tiên|Leo thang/ }).count();
    const dl = page.waitForEvent('download', { timeout: 15000 }).catch(() => null); await page.getByRole('button', { name: '[Excel]' }).click(); const file = await dl; await shot(page, 'TC-CC-005');
    note(`Month view: ${all}; status "Đã xong": ${done} (row statuses: ${statuses.join(',')}); + owner "${owner}": ${byOwner}; export -> ${file ? file.suggestedFilename() : 'no download'}; escalate controls: ${escalate}`);
    if (!escalate) ann('defect', 'No escalate action in the care work list (UC-26 lists escalation)');
    expect(statuses.every(s => s === 'Đã xong')).toBe(true); expect(file).not.toBeNull(); expect(escalate).toBeGreaterThan(0);
  });
});
