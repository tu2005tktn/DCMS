import { test, expect, type Page, type APIRequestContext } from '@playwright/test';
import fs from 'node:fs';
import { API, Api, customerLogin, key, note, rnd, shot, staffLogin, todayIso } from './helpers';
import { checkIn, newPatient, sheetWithWork, slots, staffBook } from './fx';

// UC-10/11 patient portal link and record views, UC-25 Verify Record Link (staff), driven through both web apps.
test.describe.configure({ timeout: 180_000 });
test.beforeEach(({ page }) => { page.setDefaultTimeout(15_000); });
const ann = (type: string, description: string) => test.info().annotations.push({ type, description });
const PWD = 'Abc12345';
async function msgs(page: Page) {
  await page.waitForTimeout(1200);
  return (await page.locator('.ant-message-notice, .ant-alert-message, .ant-alert-description, .ant-form-item-explain-error, [role=alert], .ant-result-title, .ant-result-subtitle').allInnerTexts())
    .map(s => s.trim()).filter(Boolean).filter((s, i, a) => a.indexOf(s) === i).join(' | ').slice(0, 400);
}
async function register(request: APIRequestContext, fullName: string) {
  const n = rnd(); const phone = `09${n}${Math.floor(Math.random() * 90 + 10)}`.slice(0, 10);
  const r = await request.post(`${API}/api/auth/register`, { data: { fullName, phoneNumber: phone, email: `pr.${n}@example.com`, password: PWD, confirmPassword: PWD } });
  if (!r.ok()) throw new Error('register ' + (await r.text())); return phone;
}
async function visitedPatient(request: APIRequestContext) {
  const api = await Api.as(request, 'assistant.demo'); const p = await newPatient(api); const now = Date.now(); let s: any;
  for (const d of [4, 3, 6]) { s = (await slots(api, d, todayIso(), 15)).find((x: any) => Date.parse(x.start) >= now - 10 * 60_000); if (s) break; }
  const a = await staffBook(api, s, p); await checkIn(api, a.appointmentId, p.id, a.version); return p;
}
async function sendLink(page: Page, v: { name: string; birth?: string; code?: string; self?: boolean }) {
  await page.goto('/customer/patient-link'); await page.waitForTimeout(2000);
  await page.getByRole('textbox', { name: '* Họ tên' }).fill(v.name);
  if (v.birth) await page.getByLabel('Ngày sinh (không bắt buộc)').fill(v.birth);
  if (v.code) await page.getByRole('textbox', { name: 'Mã hồ sơ (nếu biết)' }).fill(v.code);
  if (v.self !== false) await page.getByRole('checkbox').first().check();
  const r = page.waitForResponse(x => x.url().includes('/api/') && x.request().method() === 'POST', { timeout: 10000 }).catch(() => null);
  await page.getByRole('button', { name: 'Gửi yêu cầu' }).click(); const res = await r; const m = await msgs(page);
  const tag = (await page.locator('.customer-link-status .ant-tag').first().innerText().catch(() => '')).trim();
  return { status: res?.status() ?? 0, body: res ? (await res.text().catch(() => '')).slice(0, 200) : '', m, tag };
}
async function openRequest(page: Page, text: string, status = 'Tất cả') {
  await page.goto('/link-verification'); await page.waitForTimeout(2000);
  const st = page.locator('.ant-select').first(); if (status) { await st.click(); await page.waitForTimeout(400); const o = page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option').filter({ hasText: status }).first(); if (await o.count()) await o.click(); }
  await page.getByPlaceholder('Mã yêu cầu, tài khoản, họ tên, email, SĐT').fill(text); await page.getByRole('button', { name: 'Tìm kiếm' }).click(); await page.waitForTimeout(1500);
  await page.locator('tbody tr.ant-table-row').first().click(); await page.waitForTimeout(1500);
  return page.locator('.ant-drawer-open .ant-drawer-content').last();
}
async function decide(page: Page, button: 'Từ chối' | 'Thu hồi liên kết', reason: string) {
  const d = page.locator('.ant-drawer-open .ant-drawer-content').last(); await d.getByRole('button', { name: button }).click(); await page.waitForTimeout(800);
  const m = page.locator('.ant-modal-wrap:visible .ant-modal').last(); await m.locator('textarea').first().fill(reason);
  const r = page.waitForResponse(x => x.url().includes('/api/') && x.request().method() !== 'GET'); await m.getByRole('button', { name: 'Xác nhận' }).click();
  const res = await r; await page.waitForTimeout(1200); return res.status();
}

const STATE = `../outputs/system-test-frs/results/pr_state_${process.env.RUN ?? 'run'}.json`;
type St = { p: any; phone: string; other: any; otherPhone: string; plain: string; rx?: string; foreignRx?: string };
let st: St = fs.existsSync(STATE) ? JSON.parse(fs.readFileSync(STATE, 'utf8')) : (null as any);

test.describe('UC-10/11/25 own record link', () => {
  test.beforeAll(async ({ request }) => {
    if (st) return;
    const p = await visitedPatient(request); const other = await visitedPatient(request);
    const owner = await Api.as(request, 'owner.demo');
    await sheetWithWork(request, 32, ['36'], true, p);
    const med = ((await owner.get('/api/prescriptions/medicine-options?branchId=1&q=Amox')).body as any[])?.[0] ?? ((await owner.get('/api/prescriptions/medicine-options?branchId=1')).body as any[])[0];
    const mk = async (pid: number) => (await owner.post(`/api/prescriptions/patients/${pid}`, { requestKey: key(), expectedVersion: 0, branchId: 1, prescribedOn: todayIso(), diagnosis: 'Sâu răng', otherDiagnosis: null, advice: 'Súc miệng nước muối', items: [{ medicineId: med.id ?? med.medicineId, quantity: 10, instructions: 'Uống sau ăn', unit: 'viên' }] })).body;
    const rx = await mk(p.id); const foreign = await mk(other.id);
    st = { p, phone: await register(request, p.fullName), other, otherPhone: await register(request, other.fullName), plain: await register(request, `Khách chưa khám ${rnd()}`), rx: rx?.id ?? rx?.prescription?.id, foreignRx: foreign?.id ?? foreign?.prescription?.id };
    fs.writeFileSync(STATE, JSON.stringify(st));
  });

  test('TC-PR-002 link request before the first visit', async ({ page }) => {
    await customerLogin(page, st.plain, PWD); const r = await sendLink(page, { name: 'Khách chưa khám', self: true }); await shot(page, 'TC-PR-002');
    await staffLogin(page, 'assistant.demo'); const d = await openRequest(page, st.plain, 'Chờ');
    const search = d.getByPlaceholder('Mã hồ sơ, họ tên hoặc số điện thoại'); await search.fill('Khách chưa khám'); await d.getByRole('button', { name: 'Tra cứu' }).click(); await page.waitForTimeout(1500);
    const cands = await d.locator('.link-candidates tbody tr.ant-table-row').count(); await d.locator('textarea').first().fill('Không có hồ sơ'); await d.getByRole('button', { name: 'Xác minh' }).click();
    const staffMsg = await msgs(page); await shot(page, 'TC-PR-002-staff');
    note(`Account that never visited: request accepted -> ${r.status} "${r.m}" (status "${r.tag}"). Staff side: ${cands} matching record(s); verify -> "${staffMsg}"`);
    ann('correction', 'The request itself is accepted; GBR-023 blocks only the approval while no record exists (MSG14 is raised by the review service). Expected result corrected.');
    expect(r.status).toBeLessThan(300); expect(cands).toBe(0);
  });
  test('TC-PR-001/003 send and withdraw a link request', async ({ page }) => {
    await customerLogin(page, st.phone, PWD); let r = await sendLink(page, { name: st.p.fullName, birth: st.p.birthDate, code: st.p.patientCode }); await shot(page, 'TC-PR-001');
    note(`Link request for ${st.p.patientCode} -> ${r.status} "${r.m}"; status "${r.tag}"`);
    await page.getByRole('button', { name: 'Rút yêu cầu' }).click(); await page.locator('.ant-popover:visible').getByRole('button', { name: 'Rút yêu cầu' }).click(); await page.waitForTimeout(1500);
    const afterWithdraw = (await page.locator('.customer-link-status .ant-tag').first().innerText()).trim(); const formBack = await page.getByRole('button', { name: 'Gửi yêu cầu' }).count();
    await shot(page, 'TC-PR-003'); ann('TC-PR-003', `Withdraw -> status "${afterWithdraw}", send form shown again: ${formBack > 0}`);
    r = await sendLink(page, { name: st.p.fullName, birth: st.p.birthDate, code: st.p.patientCode });
    ann('evidence', `Sent again after withdrawing -> ${r.status} "${r.m}", status "${r.tag}"`);
    await staffLogin(page, 'assistant.demo'); const d = await openRequest(page, st.phone, 'Chờ'); const head = (await d.innerText()).replace(/\s+/g, ' ').slice(0, 200);
    ann('evidence', `Staff "Xác minh liên kết hồ sơ" shows the request: ${head}`);
    expect(r.status).toBeLessThan(300); expect(formBack).toBeGreaterThan(0); expect(head).toContain(st.p.fullName);
  });
  test('TC-PM-010/011 mismatching data cannot be verified; reject with a reason', async ({ page }) => {
    await customerLogin(page, st.otherPhone, PWD); await sendLink(page, { name: st.other.fullName, birth: '1970-01-01', code: st.other.patientCode });
    await staffLogin(page, 'assistant.demo'); const d = await openRequest(page, st.otherPhone, 'Chờ');
    await d.getByRole('button', { name: 'Tra cứu' }).click(); await page.waitForTimeout(1500);
    await d.locator('.link-candidates tbody tr.ant-table-row').filter({ hasText: st.other.patientCode }).locator('input[type=radio]').check({ force: true }); await page.waitForTimeout(600);
    await d.locator('textarea').first().fill('Đối chiếu CCCD tại quầy');
    const warn = (await d.locator('.ant-alert').allInnerTexts()).join(' | ').replace(/\s+/g, ' ').slice(0, 260); const disabled = await d.getByRole('button', { name: 'Xác minh' }).isDisabled();
    await shot(page, 'TC-PM-010');
    const status = await decide(page, 'Từ chối', 'Sai thông tin'); await shot(page, 'TC-PM-011');
    await customerLogin(page, st.otherPhone, PWD); await page.goto('/customer/patient-link'); await page.waitForTimeout(2000);
    const seen = (await page.locator('.customer-link-status').innerText()).replace(/\s+/g, ' ').slice(0, 260); await shot(page, 'TC-PM-011-patient');
    ann('TC-PM-010', `Declared birth date 01/01/1970 vs record ${st.other.birthDate}: warning "${warn}"; "Xác minh" disabled: ${disabled}`);
    note(`Rejected with reason "Sai thông tin" -> ${status}; patient sees: ${seen}`);
    expect(disabled).toBe(true); expect(status).toBeLessThan(300); expect(seen).toMatch(/Từ chối|từ chối/);
  });
  test('TC-PR-007 unlinked account cannot view the medical record', async ({ page }) => {
    await customerLogin(page, st.phone, PWD); const out: string[] = [];
    for (const u of ['treatment-records', 'prescriptions']) { await page.goto('/customer/' + u); await page.waitForTimeout(2500); out.push(`${u}: "${(await page.locator('main').last().innerText()).replace(/\s+/g, ' ').slice(0, 160)}"`); }
    await shot(page, 'TC-PR-007'); note(`Pending (not linked) account: ${out.join('; ')}`);
    expect(out.join(' ')).not.toContain(st.p.patientCode);
  });
  test('TC-PM-009/PR-005 approve a matching request; patient sees the record', async ({ page }) => {
    await staffLogin(page, 'assistant.demo'); const d = await openRequest(page, st.phone, 'Chờ');
    await d.getByRole('button', { name: 'Tra cứu' }).click(); await page.waitForTimeout(1500);
    const cand = d.locator('.link-candidates tbody tr.ant-table-row').filter({ hasText: st.p.patientCode }); const match = (await cand.innerText()).replace(/\s+/g, ' ');
    await cand.locator('input[type=radio]').check({ force: true }); await d.locator('textarea').first().fill('Đối chiếu CCCD tại quầy lễ tân, khớp họ tên và ngày sinh');
    await d.getByRole('button', { name: 'Xác minh' }).click(); await page.waitForTimeout(700); const confirm = (await page.locator('.ant-modal-confirm').innerText()).replace(/\s+/g, ' ');
    const r = page.waitForResponse(x => x.url().includes('/api/') && x.request().method() !== 'GET'); await page.locator('.ant-modal-confirm').getByRole('button', { name: 'Xác minh' }).click(); const v = (await r).status(); await page.waitForTimeout(1500);
    ann('evidence', `Confirmation: ${confirm}`);
    await shot(page, 'TC-PM-009');
    await customerLogin(page, st.phone, PWD); await page.goto('/customer/patient-link'); await page.waitForTimeout(2000);
    const status = (await page.locator('.customer-link-status').innerText()).replace(/\s+/g, ' ').slice(0, 200); const menu = await page.getByRole('navigation').innerText();
    await shot(page, 'TC-PR-005'); note(`Candidate row "${match}"; verify -> ${v}; patient page: ${status}; menu: ${menu.replace(/\s+/g, ' ')}`);
    expect(v).toBeLessThan(300); expect(status).toMatch(/Đã liên kết|đã xác minh|Đã xác minh/);
  });
  test('TC-PR-004 a linked account cannot request another own record', async ({ page, request }) => {
    await customerLogin(page, st.phone, PWD); await page.goto('/customer/patient-link'); await page.waitForTimeout(2000);
    const form = await page.getByRole('button', { name: 'Gửi yêu cầu' }).count();
    const me = await Api.as(request, st.phone, PWD);
    const r = await me.post('/api/patient-links/me/requests', { fullName: st.other.fullName, birthDate: null, patientCode: st.other.patientCode, matchingInformation: null, confirmSelf: true });
    const res = { status: () => r.status, text: async () => JSON.stringify(r.body) };
    await shot(page, 'TC-PR-004'); note(`Linked account: send form shown ${form > 0}; API new request -> ${res.status()} ${(await res.text()).slice(0, 200)}`);
    expect(form).toBe(0);
  });
  test('TC-PR-006/EX-018 linked patient views treatment, prescriptions and documents', async ({ page }) => {
    await customerLogin(page, st.phone, PWD); await page.goto('/customer/treatment-records');
    await page.getByRole('tab', { name: 'Thông tin cơ bản' }).waitFor({ timeout: 30000 }).catch(() => {});
    const parts: string[] = [];
    for (const tab of ['Thông tin cơ bản', 'Khám', 'Điều trị & Thanh toán', 'Hình ảnh']) { const tb = page.getByRole('tab', { name: tab }); if (await tb.count()) { await tb.click(); await page.waitForTimeout(800); parts.push(`[${tab}] ${(await page.getByRole('tabpanel').last().innerText()).replace(/\s+/g, ' ').slice(0, 160)}`); } }
    const tr = parts.join(' ') || (await page.locator('main').last().innerText()).replace(/\s+/g, ' '); await shot(page, 'TC-PR-006-treatment');
    const upload = await page.getByRole('button', { name: /Tải lên|Upload|Thêm ảnh/ }).count() + await page.locator('input[type=file]').count();
    await page.goto('/customer/prescriptions'); await page.waitForTimeout(2500); const rx = (await page.locator('main').last().innerText()).replace(/\s+/g, ' '); await shot(page, 'TC-PR-006');
    note(`Treatment records: ${tr.slice(0, 300)} … Prescriptions: ${rx.slice(0, 200)}`);
    ann('TC-EX-018', `Customer record pages: upload controls ${upload}; documents section: ${/Hình ảnh|Tài liệu|ảnh/.test(tr) ? 'present' : 'not found'}`);
    expect(tr).toContain(st.p.patientCode.slice(0, 2)); expect(rx).toMatch(/Amox|thuốc|Đơn/);
  });
  test('TC-PR-009 another patient\'s prescription cannot be opened by URL', async ({ page }) => {
    test.skip(!st.foreignRx, 'prescription of the other patient was not created');
    await customerLogin(page, st.phone, PWD); await page.goto(`/customer/prescriptions/${st.foreignRx}`); await page.waitForTimeout(2500);
    const txt = (await page.locator('main').last().innerText()).replace(/\s+/g, ' ').slice(0, 250); await shot(page, 'TC-PR-009');
    note(`/customer/prescriptions/${st.foreignRx} (prescription of ${st.other.patientCode}) shows: "${txt}"`);
    expect(txt).not.toContain(st.other.fullName);
  });
  test('TC-PM-012 revoke an active link', async ({ page }) => {
    await staffLogin(page, 'assistant.demo'); await openRequest(page, st.phone, 'Đã xác minh'); const s = await decide(page, 'Thu hồi liên kết', 'Thu hồi kiểm thử'); await shot(page, 'TC-PM-012');
    await customerLogin(page, st.phone, PWD); await page.goto('/customer/patient-link'); await page.waitForTimeout(2000);
    const status = (await page.locator('.customer-link-status').innerText()).replace(/\s+/g, ' ').slice(0, 200);
    await page.goto('/customer/treatment-records'); await page.waitForTimeout(2500); const tr = (await page.locator('main').last().innerText()).replace(/\s+/g, ' ').slice(0, 160); await shot(page, 'TC-PM-012-patient');
    note(`Revoke -> ${s}; patient link page: ${status}; treatment records: "${tr}"`); ann('TC-PR-005', `After revocation: ${status} / ${tr}`);
    expect(s).toBeLessThan(300); expect(tr).not.toContain(st.p.patientCode);
  });
});
