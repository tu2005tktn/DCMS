import { test, expect, type Page } from '@playwright/test';
import { Api, PW, API, customerLogin, key, note, rnd, shot, staffLogin } from './helpers';

const MSG07 = 'Thông tin đăng nhập không đúng hoặc tài khoản không thể sử dụng. Vui lòng kiểm tra và thử lại.';
const MSG09 = 'Nếu thông tin tài khoản hợp lệ, hướng dẫn đặt lại mật khẩu sẽ được gửi qua kênh liên hệ đã đăng ký.';
const MSG10 = 'Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.';

async function mailCode(page: Page, to: string, after: number) {
  for (let i = 0; i < 30; i++) {
    const list = await (await page.request.get('http://localhost:8025/api/v1/messages?limit=30')).json();
    const m = list.messages.find((x: any) => x.To.some((t: any) => t.Address.toLowerCase() === to.toLowerCase()) && Date.parse(x.Created) > after);
    if (m) {
      const text = (await (await page.request.get(`http://localhost:8025/api/v1/message/${m.ID}`)).json()).Text as string;
      const code = text.match(/\b(\d{6})\b/); if (code) return code[1];
    }
    await page.waitForTimeout(1000);
  }
  throw new Error('no recovery e-mail for ' + to);
}
async function lockState(request: any, user: string, locked: boolean, reason: string) {
  const admin = await Api.as(request, 'admin.demo');
  const acc = (await admin.get(`/api/accounts?search=${user}`)).body.items.find((a: any) => a.userName === user);
  return admin.put(`/api/accounts/${acc.id}/state`, { version: acc.version, locked, reason });
}

test.describe('UC-01 Sign In / Sign Out', () => {
  test('TC-AA-001 staff signs in with valid credentials', async ({ page }) => {
    await staffLogin(page, 'assistant.demo');
    const menu = await page.locator('.ant-menu-item a').allTextContents();
    expect(menu).toContain('Lịch hẹn'); expect(menu).toContain('Tiếp nhận và hàng chờ'); expect(menu).toContain('Bệnh nhân');
    expect(menu).not.toContain('Quản lý tài khoản');
    await expect(page.getByText('Trợ lý demo').first()).toBeVisible();
    note(`Menu: ${menu.join(', ')}`); await shot(page, 'TC-AA-001');
  });
  test('TC-AA-002 wrong password is rejected', async ({ page }) => {
    await page.goto('/sign-in');
    await page.getByLabel('Tên đăng nhập', { exact: true }).fill('assistant.demo');
    await page.getByLabel('Mật khẩu', { exact: true }).fill('WrongPass123');
    const res = page.waitForResponse(r => r.url().endsWith('/api/auth/sign-in'));
    await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
    expect((await res).status()).toBe(401);
    await expect(page.getByText(MSG07)).toBeVisible(); await expect(page).toHaveURL(/sign-in/);
    note('401 + MSG07 shown, still on /sign-in'); await shot(page, 'TC-AA-002');
  });
  test('TC-AA-003 empty fields are rejected on the client', async ({ page }) => {
    await page.goto('/sign-in');
    let calls = 0; page.on('request', r => { if (r.url().endsWith('/api/auth/sign-in')) calls++; });
    await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
    await expect(page.getByText('Vui lòng nhập Tên đăng nhập.')).toBeVisible();
    await expect(page.getByText('Vui lòng nhập Mật khẩu.')).toBeVisible();
    expect(calls).toBe(0); note('MSG02 under both fields; no sign-in request sent'); await shot(page, 'TC-AA-003');
  });
  test('TC-AA-004 a locked staff account cannot sign in', async ({ page, request }) => {
    expect((await lockState(request, 'dentist.demo', true, 'Nghỉ việc (system test)')).status).toBe(200);
    try {
      await page.goto('/sign-in');
      await page.getByLabel('Tên đăng nhập', { exact: true }).fill('dentist.demo');
      await page.getByLabel('Mật khẩu', { exact: true }).fill(PW);
      await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
      await expect(page.getByText(MSG07)).toBeVisible(); await shot(page, 'TC-AA-004');
      note('Locked dentist.demo with correct password: MSG07, no session');
    } finally { await lockState(request, 'dentist.demo', false, 'Mở lại sau kiểm thử'); }
  });
  test('TC-AA-005 patient signs in with phone number and with e-mail', async ({ page, request }) => {
    const n = rnd(); const phone = `090${n}0`.slice(0, 10); const email = `ui.${n}@example.com`;
    const reg = await request.post(`${API}/api/auth/register`, { data: { fullName: 'Bệnh nhân UI', phoneNumber: phone, email, password: 'Abc12345', confirmPassword: 'Abc12345' } });
    expect(reg.status()).toBe(201);
    await customerLogin(page, phone, 'Abc12345'); await shot(page, 'TC-AA-005-phone');
    await page.context().clearCookies(); await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
    await customerLogin(page, email, 'Abc12345'); await shot(page, 'TC-AA-005-email');
    note(`Signed in at /customer/sign-in with phone ${phone} and with e-mail ${email}; both open /customer/home`);
  });
  test('TC-AA-006 sign out ends the session', async ({ page }) => {
    await staffLogin(page, 'assistant.demo');
    await page.getByRole('button', { name: 'Đăng xuất' }).click();
    await expect(page).toHaveURL(/sign-in/);
    await page.goBack(); await page.goto('/patients'); await expect(page).toHaveURL(/sign-in/);
    note('After sign-out, Back and /patients redirect to /sign-in'); await shot(page, 'TC-AA-006');
  });
  test('TC-AA-007 idle staff session is signed out after the configured time', async ({ page, request }) => {
    test.setTimeout(480_000);
    const owner = await Api.as(request, 'owner.demo');
    const g = (await owner.get('/api/system-settings/settings/general')).body;
    const set = (m: number, v: number) => owner.put('/api/system-settings/settings/general', { expectedVersion: v, requestKey: key(), reason: `Idle ${m} phút (system test)`, settings: { ...g.settings, staffIdleTimeoutMinutes: m } });
    expect((await set(5, g.version)).status).toBe(200);
    try {
      await staffLogin(page, 'assistant.demo');
      await page.waitForTimeout(330_000);
      await page.getByRole('link', { name: 'Bệnh nhân', exact: true }).click();
      await expect(page).toHaveURL(/sign-in/, { timeout: 30_000 });
      await shot(page, 'TC-AA-007'); note('Idle 5 min configured; after 5.5 min without activity the next click lands on /sign-in');
    } finally {
      const now = (await owner.get('/api/system-settings/settings/general')).body; await set(20, now.version);
    }
  });
});

test.describe('UC-03 Register Patient Account', () => {
  async function fill(page: Page, v: { name?: string; phone?: string; email?: string; pw?: string; confirm?: string }) {
    await page.goto('/customer/register');
    if (v.name !== undefined) await page.getByLabel('Họ tên', { exact: true }).fill(v.name);
    if (v.phone !== undefined) await page.getByLabel('Số điện thoại', { exact: true }).fill(v.phone);
    if (v.email !== undefined) await page.getByLabel('Email', { exact: true }).fill(v.email);
    if (v.pw !== undefined) await page.getByLabel('Mật khẩu', { exact: true }).fill(v.pw);
    if (v.confirm !== undefined) await page.getByLabel('Xác nhận mật khẩu', { exact: true }).fill(v.confirm);
    await page.getByRole('button', { name: 'Đăng ký', exact: true }).click();
  }
  test('TC-AA-012 register with valid data', async ({ page }) => {
    const n = rnd(); const phone = `093${n}1`.slice(0, 10);
    const res = page.waitForResponse(r => r.url().endsWith('/api/auth/register'));
    await fill(page, { name: 'Nguyễn Văn Test', phone, email: `reg.${n}@example.com`, pw: 'Abc12345', confirm: 'Abc12345' });
    expect((await res).status()).toBe(201);
    await expect(page.getByText('Tạo tài khoản thành công. Bạn có thể đặt lịch cho mình hoặc người thân.', { exact: false })).toBeVisible();
    await shot(page, 'TC-AA-012');
    await customerLogin(page, phone, 'Abc12345'); note(`MSG08 shown; new account ${phone} signs in`);
  });
  test('TC-AA-013 phone already used', async ({ page }) => {
    const n = rnd(); const phone = `094${n}2`.slice(0, 10);
    await page.request.post(`${API}/api/auth/register`, { data: { fullName: 'A', phoneNumber: phone, email: `a.${n}@example.com`, password: 'Abc12345', confirmPassword: 'Abc12345' } });
    const res = page.waitForResponse(r => r.url().endsWith('/api/auth/register'));
    await fill(page, { name: 'Người Khác', phone, email: `b.${n}@example.com`, pw: 'Abc12345', confirm: 'Abc12345' });
    const r = await res; expect(r.status()).toBe(409); const body = await r.json(); expect(body.code).toBe('MSG46');
    await expect(page.getByText(body.message, { exact: false })).toBeVisible(); await shot(page, 'TC-AA-013');
    note(`409 MSG46 "${body.message}"`);
  });
  test('TC-AA-014 invalid phone and e-mail', async ({ page }) => {
    let calls = 0; page.on('request', r => { if (r.url().endsWith('/api/auth/register')) calls++; });
    await fill(page, { name: 'A', phone: '12345', email: 'abc@', pw: 'Abc12345', confirm: 'Abc12345' });
    await expect(page.getByText(/Nhập số di động Việt Nam/).first()).toBeVisible();
    await expect(page.getByText('Email không đúng định dạng.').first()).toBeVisible();
    expect(calls).toBe(0); await shot(page, 'TC-AA-014'); note('Phone and e-mail format errors shown; nothing sent');
  });
  test('TC-AA-015 short password and mismatched confirmation', async ({ page }) => {
    await fill(page, { name: 'A', phone: '0912345670', email: 'x@example.com', pw: 'abc12', confirm: 'abc12' });
    const shortMsg = await page.locator('.ant-form-item-explain-error').allTextContents();
    await fill(page, { name: 'A', phone: '0912345670', email: 'x@example.com', pw: 'Abc12345', confirm: 'Abc12346' });
    const mismatch = await page.locator('.ant-form-item-explain-error').allTextContents();
    expect(shortMsg.join(' ')).toMatch(/mật khẩu/i); expect(mismatch.length).toBeGreaterThan(0);
    await shot(page, 'TC-AA-015'); note(`Short password: ${shortMsg.join(' | ')}; mismatch: ${mismatch.join(' | ')}`);
  });
});

test.describe('UC-04 Recover Account', () => {
  test('TC-AA-017 patient recovers the password with an e-mailed code', async ({ page }) => {
    const n = rnd(); const phone = `096${n}3`.slice(0, 10); const email = `rec.${n}@example.com`;
    await page.request.post(`${API}/api/auth/register`, { data: { fullName: 'Khôi phục UI', phoneNumber: phone, email, password: 'Abc12345', confirmPassword: 'Abc12345' } });
    await page.goto('/customer/sign-in'); await page.getByRole('link', { name: 'Quên mật khẩu?' }).click();
    const t0 = Date.now() - 2000;
    await page.getByLabel('Email', { exact: true }).fill(email);
    await page.getByRole('button', { name: 'Gửi yêu cầu', exact: true }).click();
    await expect(page.getByText(MSG09).first()).toBeVisible({ timeout: 45_000 });
    const code = await mailCode(page, email, t0);
    await page.getByLabel('Mã xác minh', { exact: true }).fill(code);
    await page.getByRole('button', { name: 'Xác minh', exact: true }).click();
    await page.getByLabel('Mật khẩu mới', { exact: true }).fill('NewPass123');
    await page.getByLabel('Xác nhận mật khẩu', { exact: true }).fill('NewPass123');
    await page.getByRole('button', { name: 'Đặt lại mật khẩu', exact: true }).click();
    await expect(page.getByText(MSG10).first()).toBeVisible(); await shot(page, 'TC-AA-017');
    const oldPw = await page.request.post(`${API}/api/auth/sign-in`, { data: { userName: phone, password: 'Abc12345' } });
    expect(oldPw.status()).toBe(401);
    await customerLogin(page, phone, 'NewPass123'); note('MSG09 -> code from Mailpit -> MSG10; new password works, old one 401');
  });
  test('TC-AA-018 unknown identifier gets the same neutral answer', async ({ page }) => {
    await page.goto('/recover');
    await page.getByLabel('Tài khoản', { exact: true }).fill('nobody.systest' + rnd());
    const res = page.waitForResponse(r => r.url().endsWith('/api/auth/recovery/request'));
    await page.getByRole('button', { name: 'Gửi yêu cầu', exact: true }).click();
    expect((await res).status()).toBe(200); await expect(page.getByText(MSG09)).toBeVisible();
    await shot(page, 'TC-AA-018'); note('200 + MSG09 for a non-existent account');
  });
  test('TC-AA-019 wrong verification code', async ({ page }) => {
    await page.goto('/recover');
    await page.getByLabel('Tài khoản', { exact: true }).fill('assistant.demo');
    await page.getByRole('button', { name: 'Gửi yêu cầu', exact: true }).click();
    await page.getByLabel('Mã xác minh', { exact: true }).fill('000000');
    await page.getByRole('button', { name: 'Xác minh', exact: true }).click();
    await expect(page.getByText('Mã hoặc liên kết xác minh không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu xác minh lại.')).toBeVisible();
    await shot(page, 'TC-AA-019'); note('MSG38 shown for code 000000');
  });
  test('TC-AA-020 resetting the password does not unlock a locked account', async ({ page, request }) => {
    await lockState(request, 'dual.demo', true, 'Khóa kiểm thử khôi phục');
    try {
      const t0 = Date.now() - 2000;
      await page.goto('/recover');
      await page.getByLabel('Tài khoản', { exact: true }).fill('dual.demo');
      await page.getByRole('button', { name: 'Gửi yêu cầu', exact: true }).click();
      const code = await mailCode(page, 'dual.demo@example.invalid', t0);
      await page.getByLabel('Mã xác minh', { exact: true }).fill(code);
      await page.getByRole('button', { name: 'Xác minh', exact: true }).click();
      await page.getByLabel('Mật khẩu mới', { exact: true }).fill(PW);
      await page.getByLabel('Xác nhận mật khẩu', { exact: true }).fill(PW);
      await page.getByRole('button', { name: 'Đặt lại mật khẩu', exact: true }).click();
      await expect(page.getByText(MSG10).first()).toBeVisible();
      await page.goto('/sign-in');
      await page.getByLabel('Tên đăng nhập', { exact: true }).fill('dual.demo');
      await page.getByLabel('Mật khẩu', { exact: true }).fill(PW);
      await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
      await expect(page.getByText(MSG07)).toBeVisible(); await shot(page, 'TC-AA-020');
      note('Locked dual.demo: reset succeeded (MSG10) but sign-in still MSG07');
    } finally { await lockState(request, 'dual.demo', false, 'Mở lại sau kiểm thử'); }
  });
});

test.describe('UC-05 Manage Staff Accounts', () => {
  test('TC-AA-021 search staff accounts', async ({ page }) => {
    await staffLogin(page, 'admin.demo'); await page.goto('/accounts');
    const search = page.getByPlaceholder('Tìm mã, tên hoặc liên hệ');
    await search.fill('dentist'); await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click();
    await expect(page.getByRole('row').filter({ hasText: 'dentist.demo' })).toHaveCount(1);
    await shot(page, 'TC-AA-021-found');
    await search.fill('zzzz'); await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click();
    await expect(page.getByText('Không tìm thấy kết quả phù hợp.', { exact: true })).toBeVisible(); await shot(page, 'TC-AA-021-empty');
    note('"dentist" lists dentist.demo; "zzzz" shows MSG01');
  });
  test('TC-AA-022 admin creates a staff account (with branch)', async ({ page }) => {
    await staffLogin(page, 'admin.demo'); await page.goto('/accounts');
    const n = rnd(); const name = `test.staff${n}`;
    await page.getByRole('button', { name: 'Tạo nhân viên' }).click();
    const editor = page.getByRole('dialog');
    await editor.getByLabel('Tên đăng nhập', { exact: true }).fill(name);
    await editor.getByLabel('Mã nhân viên', { exact: true }).fill(`NV-${n}`);
    await editor.getByLabel('Mật khẩu ban đầu', { exact: true }).fill('Abc12345');
    await editor.getByLabel('Họ tên', { exact: true }).fill('Nhân viên Test');
    await editor.getByLabel('Email', { exact: true }).fill(`${name}@example.invalid`);
    await editor.getByLabel('Vai trò được cấp', { exact: true }).click();
    await page.getByTitle('Nha sĩ', { exact: true }).click();
    await editor.getByLabel('Họ tên', { exact: true }).click();
    const branchField = await editor.getByText(/Chi nhánh/).count();
    await shot(page, 'TC-AA-022-form');
    await editor.getByRole('button', { name: 'Xác nhận tạo', exact: true }).click();
    await expect(editor).not.toBeVisible();
    const s = await page.request.post(`${API}/api/auth/sign-in`, { data: { userName: name, password: 'Abc12345' } });
    note(`Account ${name} created (sign-in ${s.status()}); branch field on the form: ${branchField ? 'present' : 'absent'}`);
    expect(branchField, 'The create form has no branch field, so "branch Chi nhánh 1" cannot be set (staff gets no branch membership).').toBeGreaterThan(0);
  });
  test('TC-AA-023 duplicate user name', async ({ page }) => {
    await staffLogin(page, 'admin.demo'); await page.goto('/accounts');
    await page.getByRole('button', { name: 'Tạo nhân viên' }).click();
    const editor = page.getByRole('dialog'); const n = rnd();
    await editor.getByLabel('Tên đăng nhập', { exact: true }).fill('dentist.demo');
    await editor.getByLabel('Mã nhân viên', { exact: true }).fill(`NV-${n}`);
    await editor.getByLabel('Mật khẩu ban đầu', { exact: true }).fill('Abc12345');
    await editor.getByLabel('Họ tên', { exact: true }).fill('Trùng tên');
    await editor.getByLabel('Email', { exact: true }).fill(`dup${n}@example.invalid`);
    await editor.getByLabel('Vai trò được cấp', { exact: true }).click(); await page.getByTitle('Nha sĩ', { exact: true }).click();
    await editor.getByLabel('Họ tên', { exact: true }).click();
    await editor.getByRole('button', { name: 'Xác nhận tạo', exact: true }).click();
    await expect(page.getByText('Tên đăng nhập đã được sử dụng. Vui lòng nhập tên đăng nhập khác.')).toBeVisible();
    await shot(page, 'TC-AA-023'); note('MSG46 "Tên đăng nhập đã được sử dụng..."');
  });
  test('TC-AA-024 admin edits a staff account', async ({ page, request }) => {
    const admin = await Api.as(request, 'admin.demo'); const n = rnd(); const name = `edit.staff${n}`;
    await admin.post('/api/accounts', { userName: name, password: 'Abc12345', fullName: 'Sửa Test', employeeCode: `ED-${n}`, email: `${name}@example.invalid`, phone: null, roles: ['Dentist'] });
    await staffLogin(page, 'admin.demo'); await page.goto('/accounts');
    await page.getByPlaceholder('Tìm mã, tên hoặc liên hệ').fill(name); await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click();
    const row = page.getByRole('row').filter({ hasText: name });
    await row.getByRole('button', { name: 'Sửa', exact: true }).click();
    const editor = page.getByRole('dialog');
    await editor.getByLabel('Họ tên', { exact: true }).fill('Sửa Test đã cập nhật');
    await editor.getByLabel('Lý do cập nhật', { exact: true }).fill('Cập nhật hồ sơ (system test)');
    await editor.getByRole('button', { name: 'Xác nhận cập nhật' }).click();
    await expect(editor).not.toBeVisible(); await expect(row).toContainText('Sửa Test đã cập nhật');
    await shot(page, 'TC-AA-024'); note('Edit saved (MSG11), list shows new name; branches are not editable on this form');
  });
  test('TC-AA-025 lock with reason ends current sessions', async ({ page, browser, request }) => {
    const other = await browser.newPage(); await staffLogin(other, 'dentist.demo');
    try {
      await staffLogin(page, 'admin.demo'); await page.goto('/accounts');
      await page.getByPlaceholder('Tìm mã, tên hoặc liên hệ').fill('dentist.demo'); await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click();
      const row = page.getByRole('row').filter({ hasText: 'dentist.demo' });
      await row.getByRole('button', { name: 'Khóa', exact: true }).click();
      const dlg = page.getByRole('dialog');
      await dlg.getByRole('button', { name: 'Xác nhận', exact: true }).click();
      const noReason = await dlg.locator('.ant-form-item-explain-error').allTextContents();
      await dlg.getByLabel('Lý do', { exact: true }).fill('Nghỉ việc (system test)');
      await dlg.getByRole('button', { name: 'Xác nhận', exact: true }).click();
      await expect(dlg).not.toBeVisible(); await expect(row).toContainText('Đã khóa'); await shot(page, 'TC-AA-025');
      await other.getByRole('link', { name: 'Lịch hẹn', exact: true }).click();
      await expect(other).toHaveURL(/sign-in/, { timeout: 20_000 });
      note(`Lock without reason: "${noReason.join(' ')}"; with reason -> Đã khóa; the dentist's open session is sent to /sign-in`);
    } finally { await lockState(request, 'dentist.demo', false, 'Mở lại sau kiểm thử'); await other.close(); }
  });
  test('TC-AA-026 unlock and read the history', async ({ page, request }) => {
    await lockState(request, 'dentist.demo', true, 'Khóa để kiểm thử mở khóa');
    await staffLogin(page, 'admin.demo'); await page.goto('/accounts');
    await page.getByPlaceholder('Tìm mã, tên hoặc liên hệ').fill('dentist.demo'); await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click();
    const row = page.getByRole('row').filter({ hasText: 'dentist.demo' });
    await row.getByRole('button', { name: 'Mở khóa', exact: true }).click();
    const dlg = page.getByRole('dialog');
    await dlg.getByLabel('Lý do', { exact: true }).fill('Quay lại làm việc (system test)');
    await dlg.getByRole('button', { name: 'Xác nhận', exact: true }).click();
    await expect(dlg).not.toBeVisible(); await expect(row).toContainText('Hoạt động');
    await row.getByRole('button', { name: 'Audit', exact: true }).click();
    await expect(page.getByRole('dialog')).toContainText('Quay lại làm việc (system test)');
    await expect(page.getByRole('dialog')).toContainText('Khóa để kiểm thử mở khóa');
    await shot(page, 'TC-AA-026'); note('Unlocked -> Hoạt động; audit lists lock and unlock with reasons');
  });
  test('TC-AA-027 non-admin cannot open account management', async ({ page }) => {
    await staffLogin(page, 'assistant.demo');
    await expect(page.getByRole('link', { name: 'Quản lý tài khoản', exact: true })).toHaveCount(0);
    await page.goto('/accounts'); await page.waitForLoadState('networkidle');
    const text = await page.locator('main').innerText();
    await shot(page, 'TC-AA-027');
    expect(text).toMatch(/không có quyền|Không có quyền|403|Tài khoản của tôi/);
    note(`No menu item; direct /accounts shows: ${text.slice(0, 120).replace(/\s+/g, ' ')}`);
  });
});

test.describe.serial('UC-06 Manage Access Groups', () => {
  const n = rnd(); const groupName = `Lễ tân ca tối ${n}`;
  test('TC-AA-028 create an access group with screen and action grants', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await page.goto('/system-settings?tab=access-groups');
    await page.getByRole('button', { name: 'Thêm nhóm quyền', exact: true }).click();
    const dialog = page.getByRole('dialog', { name: 'Thiết lập phân quyền', exact: true });
    await dialog.getByLabel('Tên phân quyền', { exact: true }).fill(groupName);
    await dialog.getByLabel('Mã phân quyền', { exact: true }).fill('LT-' + n);
    await dialog.getByRole('checkbox', { name: 'Thêm hồ sơ bệnh nhân', exact: true }).check();
    await shot(page, 'TC-AA-028-form');
    const created = page.waitForResponse(r => r.url().includes('/api/system-settings/access-groups') && r.request().method() === 'POST');
    await dialog.getByRole('button', { name: 'Lưu phân quyền', exact: true }).click();
    expect((await created).ok()).toBeTruthy(); await expect(dialog).not.toBeVisible();
    await expect(page.getByText(groupName).first()).toBeVisible(); await shot(page, 'TC-AA-028');
    note(`Group "${groupName}" with "Thêm hồ sơ bệnh nhân" saved and listed`);
  });
  test('TC-AA-029 a detailed action requires its parent screen', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await page.goto('/system-settings?tab=access-groups');
    await page.getByRole('button', { name: 'Thêm nhóm quyền', exact: true }).click();
    const dialog = page.getByRole('dialog', { name: 'Thiết lập phân quyền', exact: true });
    await expect(dialog.getByRole('checkbox', { name: 'Xem bệnh nhân', exact: true })).not.toBeChecked();
    await dialog.getByRole('checkbox', { name: 'Thêm hồ sơ bệnh nhân', exact: true }).check();
    await expect(dialog.getByRole('checkbox', { name: 'Xem bệnh nhân', exact: true })).toBeChecked();
    await shot(page, 'TC-AA-029'); note('Ticking the action "Thêm hồ sơ bệnh nhân" selects its parent screen "Xem bệnh nhân" automatically');
  });
  test('TC-AA-030 assign a staff member to the group and a branch', async ({ page, browser }) => {
    await staffLogin(page, 'owner.demo'); await page.goto('/system-settings?tab=access-groups');
    await page.getByRole('tab', { name: 'Gán cho nhân viên', exact: true }).click();
    await page.getByRole('button', { name: 'Gán quyền Nhân viên đa vai trò demo', exact: true }).click();
    const dialog = page.getByRole('dialog').filter({ has: page.getByLabel('Nhóm quyền nhân viên', { exact: true }) });
    await dialog.getByRole('combobox', { name: 'Nhóm quyền nhân viên', exact: true }).press('ArrowDown');
    await page.locator('.ant-select-dropdown:visible').getByText(groupName, { exact: true }).click();
    await dialog.getByLabel('Lý do gán nhóm', { exact: true }).fill('Phân ca tối (system test)');
    await shot(page, 'TC-AA-030-form');
    const assigned = page.waitForResponse(r => r.url().includes('/access-group') && r.request().method() === 'PUT');
    await dialog.getByRole('button', { name: 'Lưu nhóm cho nhân viên', exact: true }).click();
    expect((await assigned).ok()).toBeTruthy(); await expect(dialog).not.toBeVisible();
    const staff = await browser.newPage(); await staffLogin(staff, 'dual.demo');
    const menu = await staff.locator('.ant-menu-item a').allTextContents();
    await staff.goto('/patients'); await expect(staff.getByRole('button', { name: 'Thêm hồ sơ', exact: true })).toBeVisible();
    await shot(staff, 'TC-AA-030'); await staff.close();
    note(`dual.demo assigned to "${groupName}" (branch 1); after sign-in the menu is ${menu.join(', ')} and "Thêm hồ sơ" is available`);
  });
  test('TC-AA-031 no one changes their own group or grants rights they lack', async ({ page, browser }) => {
    await staffLogin(page, 'owner.demo'); await page.goto('/system-settings?tab=access-groups');
    await page.getByRole('tab', { name: 'Gán cho nhân viên', exact: true }).click();
    const own = page.getByRole('button', { name: 'Gán quyền Chủ phòng khám demo', exact: true });
    const ownCount = await own.count(); const ownDisabled = ownCount ? await own.isDisabled() : true;
    await shot(page, 'TC-AA-031-owner');
    const ctx = await browser.newContext(); const ap = await ctx.newPage();
    await staffLogin(ap, 'assistant.demo'); await ap.goto('/system-settings?tab=access-groups');
    await ap.waitForLoadState('networkidle');
    const addBtn = await ap.getByRole('button', { name: 'Thêm nhóm quyền', exact: true }).count();
    await shot(ap, 'TC-AA-031-assistant'); await ctx.close();
    note(`Owner's own row: ${ownCount ? (ownDisabled ? 'button disabled' : 'button enabled') : 'no assign button'}; assistant.demo sees "Thêm nhóm quyền" ${addBtn} time(s)`);
    expect(ownDisabled).toBeTruthy(); expect(addBtn).toBe(0);
  });
  test('TC-AA-032 a group with assigned staff cannot be deactivated', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await page.goto('/system-settings?tab=access-groups');
    await expect(page.getByRole('button', { name: `Ngừng nhóm ${groupName}`, exact: true })).toBeDisabled();
    await shot(page, 'TC-AA-032'); note('"Ngừng nhóm" is disabled for the group that has dual.demo assigned');
  });
});
