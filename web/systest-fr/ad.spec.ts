import { test, expect, type Page } from '@playwright/test';
import { Api, note, rnd, shot, staffLogin } from './helpers';
import { newPatient } from './fx';

// UC-40..44 administration (catalogs, supply catalog, branches and chairs, general settings) through the staff web.
test.describe.configure({ timeout: 150_000 });
test.beforeEach(({ page }) => { page.setDefaultTimeout(12_000); });
const dlg = (page: Page) => page.locator('.ant-modal-wrap:visible .ant-modal, .ant-drawer-open .ant-drawer-content').last();
const ann = (type: string, description: string) => test.info().annotations.push({ type, description });
async function msgs(page: Page) {
  await page.waitForTimeout(1000);
  return (await page.locator('.ant-message-notice, .ant-alert-message, .ant-form-item-explain-error, .ant-notification-notice-message, .ant-modal-confirm-content, [role=alert]').allInnerTexts())
    .map(s => s.trim()).filter(Boolean).filter((s, i, a) => a.indexOf(s) === i).join(' | ').slice(0, 400);
}
async function pick(page: Page, combo: any, text?: string | RegExp) {
  await combo.click({ force: true }); await page.waitForTimeout(600);
  const opts = page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option');
  const o = text ? opts.filter({ hasText: text }).first() : opts.first();
  if (!(await o.count())) { await page.keyboard.press('Escape'); return ''; }
  const label = (await o.innerText()).trim(); await o.click(); return label;
}
async function options(page: Page, combo: any, search?: string) {
  await combo.click({ force: true }); if (search) await combo.fill(search).catch(() => {}); await page.waitForTimeout(1200);
  const list = await page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option').allInnerTexts();
  await page.keyboard.press('Escape'); return list.map(s => s.trim());
}
async function section(page: Page, key: string) { await page.goto('/settings/' + key); await page.waitForTimeout(2200); }
async function search(page: Page, text: string) {
  const box = page.getByRole('searchbox').first(); await box.fill(text); await box.press('Enter'); await page.waitForTimeout(1500);
}
const status = async (page: Page, label: string) => { const it = page.locator('.ant-segmented-item').filter({ hasText: new RegExp('^' + label + '$') }).first(); if (await it.count()) { await it.click(); await page.waitForTimeout(1300); } };
async function save(page: Page, name = 'Lưu') {
  const r = page.waitForResponse(x => x.url().includes('/api/') && ['POST', 'PUT', 'PATCH', 'DELETE'].includes(x.request().method()), { timeout: 8000 }).catch(() => null);
  await dlg(page).getByRole('button', { name, exact: true }).click(); const res = await r; const m = await msgs(page);
  return { status: res?.status() ?? 0, m };
}
/** Clicks the row action (e.g. "Ngừng dùng") and answers a confirmation with a reason when one is asked. */
async function rowAction(page: Page, rowText: string, action: string | RegExp, reason = 'Kiểm thử hệ thống') {
  const row = page.getByRole('row').filter({ hasText: rowText }).first();
  const r = page.waitForResponse(x => x.url().includes('/api/') && ['POST', 'PUT', 'PATCH', 'DELETE'].includes(x.request().method()), { timeout: 10000 }).catch(() => null);
  await row.getByRole('button', { name: action }).first().click(); await page.waitForTimeout(900);
  const box = page.locator('.ant-modal-wrap:visible, .ant-popover:visible').last();
  const confirmText = (await box.innerText().catch(() => '')).replace(/\s+/g, ' ').slice(0, 300);
  const ta = box.locator('textarea, input[type=text]').first(); if (await ta.count()) await ta.fill(reason).catch(() => {});
  const ok = box.getByRole('button', { name: /Đồng ý|Xác nhận|Ngừng dùng|Ngừng|Dùng lại|Kích hoạt|OK|Lưu/ }).last();
  if (await ok.count()) await ok.click().catch(() => {});
  const res = await r; const m = await msgs(page);
  if (await page.locator('.ant-modal-wrap:visible').count()) await page.keyboard.press('Escape');
  return { status: res?.status() ?? 0, body: res ? (await res.text().catch(() => '')).slice(0, 200) : '', m, confirmText };
}

test.describe('UC-40 Manage Clinic Catalogs', () => {
  test('TC-AD-001 add and edit a treatment service and price', async ({ page, request }) => {
    await staffLogin(page, 'owner.demo'); await section(page, 'treatment-services');
    const addButtons = await page.getByRole('button', { name: /Thêm/ }).allInnerTexts();
    await page.getByRole('button', { name: 'Cập nhật giá' }).nth(1).click(); await page.waitForTimeout(1000);
    const service = (await page.locator('main strong').nth(1).innerText()).trim();
    const price = dlg(page).getByRole('spinbutton').first(); const before = await price.inputValue();
    await price.fill('350000'); const reason = dlg(page).locator('textarea, input[type=text]'); if (await reason.count()) await reason.first().fill('Kiểm thử hệ thống');
    const s = await save(page, 'Lưu giá'); await shot(page, 'TC-AD-001');
    const api = await Api.as(request, 'owner.demo'); const p = (await api.get('/api/service-prices/?branchId=1')).body.find((x: any) => x.serviceName === service || x.name === service);
    note(`"Cập nhật giá" for ${service}: ${before} -> 350000 saved ${s.status} "${s.m}"; API reference price now ${p?.referenceUnitPriceVnd ?? '?'}. Add-service controls on the page: ${addButtons.length ? addButtons.join(', ') : 'none (services cannot be added from the web; only reference prices are editable)'}`);
    if (!addButtons.length) ann('defect', 'No screen to add a new treatment service (code/name/unit); UC-40 lists adding services.');
    expect(s.status).toBeLessThan(300); expect(addButtons.length).toBeGreaterThan(0);
  });
  test('TC-AD-002 duplicate code is rejected', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await section(page, 'customer-groups');
    const code = (await page.locator('tbody tr.ant-table-row').first().locator('td').first().innerText()).trim().split(/\s/)[0];
    await page.getByRole('button', { name: 'Thêm mới', exact: true }).click(); await page.waitForTimeout(800);
    await dlg(page).getByRole('textbox', { name: '* Mã' }).fill(code); await dlg(page).getByRole('textbox', { name: '* Tên' }).fill('Nhóm trùng mã ' + rnd());
    const s = await save(page); await shot(page, 'TC-AD-002');
    note(`Customer group with existing code "${code}" -> ${s.status}: "${s.m}"`); expect(s.status).toBeGreaterThanOrEqual(400); expect(s.m).toMatch(/đã tồn tại/);
  });
  test('TC-AD-003 deactivate and reactivate a diagnosis', async ({ page }) => {
    const name = `Chẩn đoán kiểm thử ${rnd()}`;
    await staffLogin(page, 'owner.demo'); await section(page, 'diagnoses');
    await page.getByRole('button', { name: 'Thêm mới', exact: true }).click(); await page.waitForTimeout(800);
    await dlg(page).getByRole('textbox', { name: '* Tên' }).fill(name); const add = await save(page);
    await search(page, name); const off = await rowAction(page, name, 'Ngừng dùng'); await shot(page, 'TC-AD-003-off');
    await status(page, 'Ngừng dùng'); await search(page, name); const listedOff = await page.getByRole('row').filter({ hasText: name }).count();
    await status(page, 'Đang dùng'); await search(page, name); const listedActive = await page.getByRole('row').filter({ hasText: name }).count();
    const deleteBtn = await page.getByRole('button', { name: /^Xóa/ }).count();
    await status(page, 'Ngừng dùng'); await search(page, name);
    const on = await rowAction(page, name, /Khôi phục|Dùng lại|Kích hoạt/); await status(page, 'Đang dùng'); await search(page, name);
    const back = await page.getByRole('row').filter({ hasText: name }).count(); await shot(page, 'TC-AD-003');
    note(`Added "${name}" (${add.status}); deactivate -> ${off.status} (confirm: "${off.confirmText.slice(0, 120)}"); listed under "Ngừng dùng": ${listedOff}, under "Đang dùng": ${listedActive}; reactivate -> ${on.status}; active again: ${back}. Delete buttons on the list: ${deleteBtn} (used entries can only be deactivated)`);
    expect(off.status).toBeLessThan(300); expect(listedActive).toBe(0); expect(back).toBeGreaterThan(0);
  });
  test('TC-AD-004 medicines and customer groups; export', async ({ page }) => {
    const code = `TST${rnd()}`; const name = `Thuốc kiểm thử ${code}`;
    await staffLogin(page, 'owner.demo'); await section(page, 'medicines');
    await page.getByRole('button', { name: 'Thêm mới', exact: true }).click(); await page.waitForTimeout(800); const m = dlg(page);
    await m.getByRole('textbox', { name: '* Mã' }).fill(code); await m.getByRole('textbox', { name: '* Tên' }).fill(name); await m.getByRole('textbox', { name: '* Đơn vị gốc' }).fill('viên');
    const add = await save(page); await status(page, 'Tất cả thuốc'); await search(page, code); const found = await page.getByRole('row').filter({ hasText: code }).count();
    const dl = page.waitForEvent('download', { timeout: 15000 }).catch(() => null); await page.getByRole('button', { name: 'Xuất Excel' }).click(); const file = await dl; await shot(page, 'TC-AD-004');
    await section(page, 'customer-groups'); await page.getByRole('button', { name: 'Thêm mới', exact: true }).click(); await page.waitForTimeout(800);
    const g = `NHOM${rnd()}`; await dlg(page).getByRole('textbox', { name: '* Mã' }).fill(g); await dlg(page).getByRole('textbox', { name: '* Tên' }).fill('Nhóm kiểm thử ' + g);
    const grp = await save(page); await search(page, g); const gfound = await page.getByRole('row').filter({ hasText: g }).count();
    note(`Medicine ${code} added -> ${add.status}, found by search: ${gfound >= 0 ? found : found}; export -> ${file ? file.suggestedFilename() : 'no download'}; customer group ${g} -> ${grp.status}, found: ${gfound}`);
    expect(add.status).toBeLessThan(300); expect(found).toBeGreaterThan(0); expect(file).not.toBeNull(); expect(gfound).toBeGreaterThan(0);
  });
  test('TC-AD-005 system-reserved entries cannot be deactivated', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await section(page, 'diagnoses');
    await page.getByRole('menuitem', { name: 'Loại chi' }).click(); await page.waitForTimeout(1500);
    const rows = page.locator('tbody tr.ant-table-row'); const out: string[] = []; let reservedWithButton = 0, reserved = 0;
    for (let i = 0; i < await rows.count(); i++) {
      const r = rows.nth(i); const text = (await r.innerText()).replace(/\s+/g, ' '); const sys = text.includes('Hệ thống'); const btn = await r.getByRole('button', { name: 'Ngừng dùng' }).count();
      if (sys) { reserved++; if (btn) reservedWithButton++; } out.push(`${text.split(' ')[0]}${sys ? ' [Hệ thống]' : ''}: ${btn ? 'has' : 'no'} "Ngừng dùng"`);
    }
    await shot(page, 'TC-AD-005');
    note(`Expense types: ${out.join('; ')}. System entries cannot be deactivated from the web (no button), so MSG87 is never reached here`);
    expect(reserved).toBeGreaterThan(0); expect(reservedWithButton).toBe(0);
  });
});

test.describe.serial('UC-41 Manage Supply Catalog', () => {
  const n = rnd(); const supplier = `Công ty vật tư KT ${n}`; const material = `Vật liệu KT ${n}`; const group = `Nhóm KT ${n}`;
  test('TC-AD-006 add a material group, material, supplier, lab company and lab work type', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); const out: string[] = [];
    await section(page, 'material-suppliers'); await page.getByRole('button', { name: 'Thêm mới', exact: true }).click(); await page.waitForTimeout(800);
    let m = dlg(page); await m.getByRole('textbox', { name: '* Mã' }).fill(`NCC${n}`); await m.getByRole('textbox', { name: '* Tên' }).fill(supplier);
    const kinds = await options(page, m.getByRole('combobox', { name: '* Loại công ty' })); await pick(page, m.getByRole('combobox', { name: '* Loại công ty' }), /vật liệu|Vật liệu|cung cấp/);
    out.push(`supplier ${(await save(page)).status} (kinds: ${kinds.join('/')})`);
    await section(page, 'lab-providers'); await page.getByRole('button', { name: 'Thêm mới', exact: true }).click(); await page.waitForTimeout(800);
    m = dlg(page); await m.getByRole('textbox', { name: '* Mã' }).fill(`LAB${n}`); await m.getByRole('textbox', { name: '* Tên' }).fill(`Labo KT ${n}`);
    await pick(page, m.getByRole('combobox', { name: '* Loại công ty' }), /labo|Labo/); out.push(`lab company ${(await save(page)).status}`);
    await section(page, 'lab-work-types'); await page.getByRole('button', { name: 'Thêm mới', exact: true }).click(); await page.waitForTimeout(800);
    m = dlg(page); await m.getByRole('textbox', { name: '* Mã' }).fill(`LW${n}`); await m.getByRole('textbox', { name: '* Tên' }).fill(`Răng sứ KT ${n}`);
    await pick(page, m.getByRole('combobox', { name: '* Đơn vị' })); await m.getByRole('spinbutton', { name: 'Giá tham khảo (VND)' }).fill('800000'); out.push(`lab work type ${(await save(page)).status}`);
    await section(page, 'materials'); await page.getByRole('button', { name: 'Nhóm dụng cụ / vật liệu' }).click(); await page.waitForTimeout(1000);
    await page.getByRole('button', { name: '+ Thêm mới' }).click(); await page.waitForTimeout(800); await dlg(page).getByRole('textbox').first().fill(group);
    out.push(`material group ${(await save(page, (await dlg(page).getByRole('button', { name: 'Đồng ý', exact: true }).count()) ? 'Đồng ý' : 'Lưu')).status}`);
    await page.getByRole('button', { name: 'Dụng cụ / Vật liệu', exact: true }).click(); await page.waitForTimeout(1000);
    await page.getByRole('button', { name: '+ Thêm mới' }).click(); await page.waitForTimeout(800); m = dlg(page);
    await m.getByRole('textbox', { name: '* Tên' }).fill(material); await pick(page, m.getByRole('combobox', { name: 'Nhóm' }), group); await m.getByRole('textbox', { name: 'Đơn vị sử dụng' }).fill('cái');
    out.push(`material ${(await save(page, 'Đồng ý')).status}`); await shot(page, 'TC-AD-006');
    await page.goto('/materials'); await page.waitForTimeout(1500); await page.getByRole('button', { name: 'Thêm mới' }).first().click(); await page.waitForTimeout(800);
    const mats = await options(page, dlg(page).getByRole('combobox', { name: 'Dụng cụ/Vật liệu' }), material); const sups = await options(page, dlg(page).getByRole('combobox', { name: 'Công ty cung cấp' }));
    note(`Saved: ${out.join('; ')}. Purchase form offers material: ${mats.some(x => x.includes(material))}, supplier: ${sups.some(x => x.includes(supplier))}`);
    expect(out.every(x => /\b2\d\d\b/.test(x))).toBe(true); expect(mats.some(x => x.includes(material))).toBe(true);
  });
  test('TC-AD-007 duplicate supplier name rejected', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await section(page, 'material-suppliers');
    await page.getByRole('button', { name: 'Thêm mới', exact: true }).click(); await page.waitForTimeout(800); const m = dlg(page);
    await m.getByRole('textbox', { name: '* Mã' }).fill(`NCD${n}`); await m.getByRole('textbox', { name: '* Tên' }).fill(supplier); await pick(page, m.getByRole('combobox', { name: '* Loại công ty' }), /vật liệu|Vật liệu|cung cấp/);
    const s = await save(page); await shot(page, 'TC-AD-007'); note(`Supplier with the existing name "${supplier}" -> ${s.status}: "${s.m}"`);
    expect(s.status).toBeGreaterThanOrEqual(400); expect(s.m).toMatch(/tồn tại/);
  });
  test('TC-AD-008 a group with materials cannot be removed', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await section(page, 'materials'); await page.getByRole('button', { name: 'Nhóm dụng cụ / vật liệu' }).click(); await page.waitForTimeout(1000);
    const r = await rowAction(page, group, /Ngừng dùng|Xóa/); await shot(page, 'TC-AD-008');
    note(`Removing group "${group}" that holds "${material}" -> ${r.status} "${r.m || r.body}" (confirm: "${r.confirmText.slice(0, 120)}")`);
    expect(r.status === 0 || r.status >= 400).toBe(true); expect(r.m + r.body + r.confirmText).toMatch(/đang có|vật liệu/);
  });
  test('TC-AD-011 inactive material and lab-only company are not offered for purchases', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await section(page, 'materials'); await search(page, material);
    const off = await rowAction(page, material, 'Ngừng dùng');
    await page.goto('/materials'); await page.waitForTimeout(1500); await page.getByRole('button', { name: 'Thêm mới' }).first().click(); await page.waitForTimeout(800);
    const mats = await options(page, dlg(page).getByRole('combobox', { name: 'Dụng cụ/Vật liệu' }), material); const sups = await options(page, dlg(page).getByRole('combobox', { name: 'Công ty cung cấp' }));
    await shot(page, 'TC-AD-011');
    note(`Material "${material}" deactivated -> ${off.status}; purchase form offers it: ${mats.some(x => x.includes(material))}; supplier options: ${sups.join(', ') || 'none'} (lab company "Labo KT ${n}" offered: ${sups.some(x => x.includes('Labo KT'))})`);
    expect(mats.some(x => x.includes(material))).toBe(false); expect(sups.some(x => x.includes('Labo KT'))).toBe(false);
  });
});

test.describe.serial('UC-43 Manage Branches and Dental Chairs', () => {
  const n = rnd(); const code = `CN-T${n}`; const name = `Chi nhánh Test ${n}`;
  const openBranch = async (page: Page, text: string) => { await section(page, 'branches'); await page.getByRole('row').filter({ hasText: text }).getByRole('button', { name: 'Sửa' }).click(); await page.waitForTimeout(1500); };
  test('TC-AD-013 create and edit a branch', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await section(page, 'branches');
    await page.getByRole('button', { name: 'Thêm chi nhánh' }).click(); await page.waitForTimeout(800); let m = dlg(page);
    await m.getByRole('textbox', { name: '* Mã chi nhánh' }).fill(code); await m.getByRole('textbox', { name: '* Tên chi nhánh' }).fill(name);
    await m.getByRole('textbox', { name: 'Địa chỉ' }).fill('1 Lê Lợi, Huế'); await m.getByRole('textbox', { name: 'Điện thoại' }).fill('0234123456');
    const add = await save(page, 'Lưu chi nhánh'); await page.keyboard.press('Escape'); await page.waitForTimeout(500);
    await openBranch(page, code); m = dlg(page); await m.getByRole('textbox', { name: '* Tên chi nhánh' }).fill(name + ' (sửa)');
    const edit = await save(page, 'Lưu chi nhánh'); await page.keyboard.press('Escape'); await section(page, 'branches');
    const row = (await page.getByRole('row').filter({ hasText: code }).innerText().catch(() => '')).replace(/\s+/g, ' '); await shot(page, 'TC-AD-013');
    note(`Branch ${code} created -> ${add.status}, renamed -> ${edit.status}; list row: ${row}`);
    expect(add.status).toBeLessThan(300); expect(row).toContain('(sửa)');
  });
  test('TC-AD-015 add, edit and deactivate chairs', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await openBranch(page, code); const m = dlg(page);
    await m.getByRole('button', { name: 'Thêm ghế' }).click(); await page.waitForTimeout(800); const c = page.locator('.ant-modal-wrap:visible .ant-modal').last();
    const tb = c.getByRole('textbox'); await tb.nth(0).fill(`G3-${n}`); if (await tb.count() > 1) await tb.nth(1).fill('Ghế 3');
    const add = await save(page, (await c.getByRole('button', { name: 'Lưu ghế' }).count()) ? 'Lưu ghế' : 'Lưu');
    await page.waitForTimeout(800); const chairs = (await dlg(page).locator('table').innerText().catch(() => '')).replace(/\s+/g, ' ');
    const off = await (async () => { const row = dlg(page).getByRole('row').filter({ hasText: `G3-${n}` }); if (!(await row.count())) return { status: 0, m: 'chair row not found', confirmText: '' } as any;
      const r = page.waitForResponse(x => x.url().includes('/api/') && x.request().method() !== 'GET', { timeout: 8000 }).catch(() => null);
      await row.getByRole('button', { name: 'Ngừng' }).click(); await page.waitForTimeout(800); const box = page.locator('.ant-modal-wrap:visible, .ant-popover:visible').last();
      const t = (await box.innerText()).replace(/\s+/g, ' ').slice(0, 200); const ta = box.locator('textarea'); if (await ta.count()) await ta.first().fill('Kiểm thử');
      await box.getByRole('button', { name: /Đồng ý|Xác nhận|Ngừng|OK/ }).last().click().catch(() => {}); const res = await r; return { status: res?.status() ?? 0, m: await msgs(page), confirmText: t }; })();
    await shot(page, 'TC-AD-015'); await page.keyboard.press('Escape');
    await openBranch(page, 'BR-01'); const booked = dlg(page).getByRole('row').filter({ hasText: 'CHAIR-01' });
    await booked.getByRole('button', { name: 'Ngừng' }).click(); await page.waitForTimeout(1200);
    const impact = (await page.locator('.ant-modal-wrap:visible, .ant-popover:visible').last().innerText()).replace(/\s+/g, ' ').slice(0, 300); await shot(page, 'TC-AD-015-booked');
    await page.keyboard.press('Escape');
    note(`New chair G3-${n} -> ${add.status}; chairs: ${chairs.slice(0, 160)}; deactivate free chair -> ${off.status} "${off.m}" (${off.confirmText}). Deactivating CHAIR-01 of BR-01 shows: "${impact}" (cancelled, not confirmed)`);
    expect(add.status).toBeLessThan(300); expect(off.status).toBeLessThan(300);
  });
  test('TC-AD-014 deactivating a branch with open matters is blocked', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await section(page, 'branches');
    await page.getByRole('row').filter({ hasText: 'BR-01' }).getByRole('button', { name: 'Ngừng' }).click(); await page.waitForTimeout(1500);
    const box = page.locator('.ant-modal-wrap:visible, .ant-popover:visible').last(); const text = (await box.innerText()).replace(/\s+/g, ' ').slice(0, 500);
    const confirm = box.getByRole('button', { name: /Ngừng|Đồng ý|Xác nhận/ }).last(); const canConfirm = (await confirm.count()) ? !(await confirm.isDisabled()) : false;
    await shot(page, 'TC-AD-014'); await page.keyboard.press('Escape'); await page.waitForTimeout(500);
    const still = (await page.getByRole('row').filter({ hasText: 'BR-01' }).innerText()).replace(/\s+/g, ' ');
    note(`Deactivate BR-01 (has future appointments): dialog "${text}"; confirm button enabled: ${canConfirm} (not clicked); branch row after cancel: ${still}`);
    expect(text).toMatch(/lịch hẹn|ảnh hưởng|không thể/i); expect(canConfirm).toBe(false);
  });
});

test.describe.serial('UC-44 Manage General Settings', () => {
  const general = async (page: Page, user: string) => { await staffLogin(page, user); await section(page, 'general'); };
  const commit = async (page: Page, reason: string) => {
    const d = page.getByRole('dialog').filter({ has: page.getByText('Lý do') }).last(); const changes = (await d.innerText()).replace(/\s+/g, ' ').slice(0, 260);
    await d.locator('textarea').fill(reason); const r = page.waitForResponse(x => x.url().includes('/api/') && x.request().method() !== 'GET');
    await d.getByRole('button', { name: 'Lưu', exact: true }).click(); const res = await r; await page.waitForTimeout(1500); return { status: res.status(), changes };
  };
  test('TC-AD-016 idle sign-out time with reason and version history', async ({ page }) => {
    await general(page, 'admin.demo'); const adminText = (await page.locator('main').last().innerText()).slice(0, 160).replace(/\s+/g, ' ');
    const adminCan = await page.getByRole('button', { name: 'Xem trước và lưu' }).count(); let who = 'admin.demo';
    if (!adminCan) { ann('TC-AD-016', `admin.demo on /settings/general: "${adminText}"`); await general(page, 'owner.demo'); who = 'owner.demo'; }
    const combo = page.getByRole('combobox', { name: 'Tự đăng xuất khi không thao tác sau' }); const cur = (await page.locator('.ant-select').filter({ has: combo }).innerText()).trim();
    const target = cur.startsWith('10') ? /^30 phút/ : /^10 phút/; const chosen = await pick(page, combo, target);
    await page.locator('h2').first().click(); await page.waitForTimeout(800);
    ann('TC-AD-016-debug', `after pick: ${(await page.locator('.ant-select').filter({ has: combo }).innerText()).trim()}, save disabled ${await page.getByRole('button', { name: 'Xem trước và lưu' }).isDisabled()}`);
    await page.getByRole('button', { name: 'Xem trước và lưu' }).click(); await page.waitForTimeout(1200); const c = await commit(page, `Đổi thời gian chờ ${chosen} (kiểm thử)`);
    const top = (await page.getByRole('row').filter({ hasText: 'kiểm thử' }).first().innerText()).replace(/\s+/g, ' '); await shot(page, 'TC-AD-016');
    note(`${who}: idle ${cur} -> ${chosen}; preview "${c.changes}"; saved -> ${c.status}; history row: ${top}`);
    expect(c.status).toBeLessThan(300); expect(top).toContain('kiểm thử');
  });
  test('TC-AD-017 required patient fields and patient code prefix', async ({ page, request }) => {
    await general(page, 'owner.demo'); const fields = await page.locator('.ant-checkbox-wrapper').allInnerTexts();
    const occupation = fields.some(f => f.includes('Nghề nghiệp')); const prefix = await page.getByRole('textbox', { name: 'Tiền tố' }).inputValue();
    const mode = await page.getByRole('radio', { checked: true }).filter({ hasText: /Tự động|Chỉ/ }).first().getAttribute('aria-label').catch(() => '') ?? '';
    const next = (await page.locator('code').first().innerText().catch(() => '')).trim(); await shot(page, 'TC-AD-017');
    const p = await newPatient(await Api.as(request, 'assistant.demo'));
    note(`Required-field options: ${fields.slice(0, 7).join(', ')}; "Nghề nghiệp" offered: ${occupation}. Code prefix "${prefix}", next code shown ${next}; new patient got ${p.patientCode}`);
    if (!occupation) ann('TC-AD-017', '"Nghề nghiệp" is not in the list of fields that can be made required');
    expect(p.patientCode.startsWith(prefix)).toBe(true);
  });
  test('TC-AD-018 saving without changes', async ({ page }) => {
    await general(page, 'owner.demo'); const btn = page.getByRole('button', { name: 'Xem trước và lưu' });
    const disabled = await btn.isDisabled(); await shot(page, 'TC-AD-018');
    note(`With no change "Xem trước và lưu" is ${disabled ? 'disabled, so nothing can be saved (MSG88 is not reached from the web)' : 'enabled'}`);
    expect(disabled).toBe(true);
  });
  test('TC-AD-019 restore an older version', async ({ page }) => {
    await general(page, 'owner.demo'); const rows = page.getByRole('row').filter({ has: page.getByRole('button', { name: 'Khôi phục' }) });
    const src = (await rows.first().innerText()).replace(/\s+/g, ' '); await rows.first().getByRole('button', { name: 'Khôi phục' }).click(); await page.waitForTimeout(1200);
    const c = await commit(page, 'Khôi phục phiên bản cũ (kiểm thử)'); const head = (await page.getByRole('row').filter({ hasText: 'Hiện hành' }).first().innerText()).replace(/\s+/g, ' ');
    await shot(page, 'TC-AD-019'); note(`Restore from row "${src}": preview "${c.changes}"; saved -> ${c.status}; current version row: ${head}`);
    expect(c.status).toBeLessThan(300); expect(head).toMatch(/Khôi phục từ/);
  });
  test('TC-AD-020 receipt print options', async ({ page }) => {
    await general(page, 'owner.demo'); const box = page.getByRole('checkbox', { name: 'Địa chỉ bệnh nhân' }); const was = await box.isChecked();
    await box.click(); await page.getByRole('button', { name: 'Xem trước và lưu' }).click(); await page.waitForTimeout(1200);
    const c = await commit(page, `${was ? 'Ẩn' : 'Hiện'} địa chỉ bệnh nhân trên phiếu thu (kiểm thử)`); await shot(page, 'TC-AD-020');
    note(`Receipt option "Địa chỉ bệnh nhân" ${was} -> ${!was}: preview "${c.changes}", saved -> ${c.status}. Printing a receipt after the change was not checked automatically (browser print dialog).`);
    expect(c.status).toBeLessThan(300);
  });
});
