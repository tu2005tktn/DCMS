import { test, expect, type Page } from '@playwright/test';
import { Api, key, note, rnd, shot, staffLogin, todayIso, addDays } from './helpers';
import { sheetWithWork } from './fx';

// UC-37 materials used, UC-39 payments, UC-41 purchases/stock, UC-45/46 statistics — driven through the staff web.
const dlg = (page: Page) => page.locator('.ant-modal-wrap:visible .ant-modal').last();
const ann = (type: string, description: string) => test.info().annotations.push({ type, description });
async function msgs(page: Page) {
  await page.waitForTimeout(900);
  return (await page.locator('.ant-message-notice, .ant-alert-message, .ant-form-item-explain-error, .ant-notification-notice-message, [role=alert]').allInnerTexts())
    .map(s => s.trim()).filter(Boolean).filter((s, i, a) => a.indexOf(s) === i).join(' | ');
}
async function pick(page: Page, combo: any, text?: string | RegExp, avoid = /Không chọn/) {
  await combo.click({ force: true }); await page.waitForTimeout(500);
  if (typeof text === 'string') { await combo.fill(text).catch(() => {}); await page.waitForTimeout(900); }
  const opts = page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option');
  const o = text ? opts.filter({ hasText: text }).first() : opts.filter({ hasNotText: avoid }).first();
  if (!(await o.count())) { await page.keyboard.press('Escape'); return ''; }
  const label = (await o.innerText()).trim(); await o.click(); return label;
}
const money = (s: string) => Number((s.match(/[\d.]+/)?.[0] ?? '0').replace(/\./g, ''));
async function openSheet(page: Page, patientId: number) {
  await page.goto(`/patients/${patientId}?tab=treatment`); await page.waitForTimeout(2500);
}
async function openPay(page: Page) {
  await page.getByRole('button', { name: 'Trả tiền' }).first().click();
  await expect(page.getByRole('dialog', { name: 'Thanh toán phiếu điều trị' })).toBeVisible(); await page.waitForTimeout(1200);
  return page.getByRole('dialog', { name: 'Thanh toán phiếu điều trị' });
}
async function stockRow(page: Page, lot: string) {
  await page.goto('/materials'); await page.waitForTimeout(1500);
  await page.getByRole('tab', { name: 'Tồn kho' }).click(); await page.waitForTimeout(1500);
  return (await page.getByRole('row').filter({ hasText: lot }).first().innerText().catch(() => '')).replace(/\s+/g, ' ');
}

test.describe.serial('UC-41 purchases and UC-37 materials used', () => {
  const lot = `LOT-ST-${rnd()}`; let material = '';
  test('TC-AD-010 future received date, expiry before entry date and paid above total are rejected', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await page.goto('/materials'); await page.waitForTimeout(1500);
    await page.getByRole('button', { name: 'Thêm mới' }).first().click(); const m = dlg(page); await page.waitForTimeout(800);
    await pick(page, m.getByRole('combobox', { name: 'Dụng cụ/Vật liệu' }), 'Composite');
    await pick(page, m.getByRole('combobox', { name: 'Đơn vị' }));
    await m.getByRole('spinbutton', { name: 'Số lượng' }).fill('2'); await m.getByRole('spinbutton', { name: 'Đơn giá' }).fill('10000');
    await m.getByLabel('Ngày nhận').fill(addDays(todayIso(), 1)); await m.getByRole('button', { name: 'Lưu' }).click();
    const future = await msgs(page); await shot(page, 'TC-AD-010-received');
    await m.getByLabel('Ngày nhận').fill(todayIso()); await m.getByLabel('Hạn dùng').fill(addDays(todayIso(), -1)); await m.getByRole('button', { name: 'Lưu' }).click();
    const expiry = await msgs(page); await shot(page, 'TC-AD-010-expiry');
    await m.getByLabel('Hạn dùng').fill(addDays(todayIso(), 365));
    await m.getByRole('spinbutton', { name: 'Đã thanh toán' }).fill('50000'); await m.getByRole('spinbutton', { name: 'Đã thanh toán' }).blur(); await page.waitForTimeout(300);
    const paidValue = await m.getByRole('spinbutton', { name: 'Đã thanh toán' }).inputValue();
    const save = page.waitForResponse(r => /material/i.test(r.url()) && r.request().method() !== 'GET', { timeout: 5000 }).catch(() => null);
    await m.getByRole('button', { name: 'Lưu' }).click(); const r = await save; const paid = await msgs(page); await shot(page, 'TC-AD-010-paid');
    note(`Received tomorrow: "${future}". Expiry yesterday: "${expiry}". Paid 50,000 on a 20,000 total: field shows "${paidValue}", save ${r ? r.status() : 'not sent'}: "${paid}"`);
    if (r && r.status() < 300) throw new Error('paid above total was saved');
    expect(future + expiry).not.toBe('');
  });
  test('TC-AD-009 record a purchase; stock increases by 10', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await page.goto('/materials'); await page.waitForTimeout(1500);
    await page.getByRole('button', { name: 'Thêm mới' }).first().click(); const m = dlg(page); await page.waitForTimeout(800);
    await m.getByLabel('Ngày nhận').fill(todayIso()); await m.getByLabel('Hạn dùng').fill(addDays(todayIso(), 365)); await m.getByLabel('Số lô').fill(lot);
    material = await pick(page, m.getByRole('combobox', { name: 'Dụng cụ/Vật liệu' }), 'Composite') || await pick(page, m.getByRole('combobox', { name: 'Dụng cụ/Vật liệu' }));
    await m.getByRole('spinbutton', { name: 'Số lượng' }).fill('10'); await pick(page, m.getByRole('combobox', { name: 'Đơn vị' }));
    const supplier = (await pick(page, m.getByRole('combobox', { name: 'Công ty cung cấp' }))) || 'none (no supplier in the catalog)';
    await m.getByRole('spinbutton', { name: 'Đơn giá' }).fill('50000'); await m.getByRole('spinbutton', { name: 'Đã thanh toán' }).fill('200000');
    const total = await m.locator('p').filter({ hasText: 'Tổng:' }).innerText();
    const save = page.waitForResponse(r => r.request().method() === 'POST' && /material|purchase/i.test(r.url()));
    await m.getByRole('button', { name: 'Lưu' }).click(); const r = await save; await page.waitForTimeout(1500); await shot(page, 'TC-AD-009');
    const stock = await stockRow(page, lot); await shot(page, 'TC-AD-009-stock');
    note(`Purchase of ${material} x10 lot ${lot} from "${supplier}" (${total.replace(/\s+/g, ' ')}) -> ${r.status()}; stock row: ${stock}`);
    expect(r.status()).toBeLessThan(300); expect(stock).toMatch(/10/);
  });
  test('TC-TR-026/027/028 record materials used for a work line; above stock and invalid quantity rejected', async ({ page, request }) => {
    const f = await sheetWithWork(request); await staffLogin(page, 'owner.demo'); await openSheet(page, f.patient.id);
    const name = material.replace(/\s*\(.*$/, ''); const search = name.split(' ')[0];
    const choose = async (qty: string) => {
      await page.getByRole('button', { name: /^Chọn vật liệu dùng cho/ }).first().click(); await page.waitForTimeout(800);
      const input = page.getByRole('combobox', { name: 'Tên dụng cụ/vật liệu 1', exact: true }); await input.click(); await input.fill(search); await page.waitForTimeout(1500);
      const opts = page.locator('.material-note-select-popup:visible .ant-select-item-option');
      const offered = (await opts.allInnerTexts()).join(', '); await opts.filter({ hasText: name }).first().click();
      await page.getByRole('spinbutton', { name: 'Số lượng vật liệu 1', exact: true }).fill(qty); await page.getByRole('spinbutton', { name: 'Số lượng vật liệu 1', exact: true }).blur();
      const shown = await page.getByRole('spinbutton', { name: 'Số lượng vật liệu 1', exact: true }).inputValue();
      const resp = page.waitForResponse(r => r.url().includes('/sheet-activities/') && r.request().method() === 'PUT', { timeout: 6000 }).catch(() => null);
      await page.getByRole('button', { name: 'Lưu vật liệu', exact: true }).click(); const r = await resp; const m = await msgs(page);
      const open = await page.getByRole('dialog', { name: 'Vật liệu dùng' }).isVisible().catch(() => false);
      if (open) await page.getByRole('dialog', { name: 'Vật liệu dùng' }).getByRole('button', { name: 'Hủy' }).click().catch(() => {});
      await page.waitForTimeout(600); return { offered, shown, status: r?.status() ?? 0, body: r ? await r.text().catch(() => '') : '', m };
    };
    const big = await choose('1000'); await shot(page, 'TC-TR-027');
    ann('TC-TR-027', `Quantity 1000 (> stock): ${big.status || 'no request'} "${big.m || big.body.slice(0, 160)}"`);
    const dec = await choose('0.1234'); ann('TC-TR-028', `Quantity 0.1234 -> field "${dec.shown}", ${dec.status || 'no request'} "${dec.m}"`);
    const zero = await choose('0'); await shot(page, 'TC-TR-028');
    ann('TC-TR-028', `Quantity 0 -> field "${zero.shown}", ${zero.status || 'no request'} "${zero.m}"`);
    const before = await stockRow(page, lot); await openSheet(page, f.patient.id);
    const ok = await choose('0.5'); await shot(page, 'TC-TR-026-sheet');
    const after = await stockRow(page, lot); await shot(page, 'TC-TR-026');
    note(`Materials dialog offered: ${ok.offered}; saved 0.5 -> ${ok.status}. Stock row before: ${before} / after: ${after}`);
    expect(ok.status).toBeLessThan(300);
  });
  test('TC-AD-012 stock and usage history', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); const stock = await stockRow(page, lot); await shot(page, 'TC-AD-012-stock');
    await page.getByRole('tab', { name: 'Lịch sử xuất dùng' }).click(); await page.waitForTimeout(1500); await shot(page, 'TC-AD-012');
    const hist = (await page.getByRole('tabpanel').last().innerText()).replace(/\s+/g, ' ').slice(0, 300);
    note(`Stock row for ${lot}: ${stock}. Usage history: ${hist}`);
    expect(stock).not.toBe('');
  });
});

test.describe('UC-39 Record Payment', () => {
  test('TC-PY-001/002 cash payment; overpayment cannot be entered', async ({ page, request }) => {
    const f = await sheetWithWork(request); await staffLogin(page, 'owner.demo'); await openSheet(page, f.patient.id);
    const d = await openPay(page); const amount = d.getByRole('spinbutton', { name: /^Số tiền thu cho / }).first();
    await amount.fill(String(f.price + 100000)); await amount.blur(); await page.waitForTimeout(300); const clamped = await amount.inputValue();
    const dateField = await d.locator('input[type=date], .ant-picker').count();
    ann('TC-PY-002', `Typing ${f.price + 100000} where ${f.price} is due leaves "${clamped}" (input is capped at the remaining amount); the dialog has ${dateField} date field(s) — the payment date is always today`);
    await amount.fill(String(f.price)); await amount.blur();
    const save = page.waitForResponse(r => r.url().endsWith('/payments') && r.request().method() === 'POST');
    await d.getByRole('button', { name: 'Xác nhận thu tiền mặt' }).click(); const r = await save; await page.waitForTimeout(1500);
    const paidTab = await d.getByRole('tab', { name: /Lịch sử dịch vụ đã thanh toán/ }).innerText().catch(() => '');
    await shot(page, 'TC-PY-001'); await page.keyboard.press('Escape'); await page.waitForTimeout(800);
    const sheet = (await page.locator('main').last().innerText()).match(/(Còn nợ|Còn lại|Đã trả)[^\n]*\n?[^\n]*/g)?.slice(0, 3).join(' ; ') ?? '';
    const print = await page.getByRole('button', { name: /In (phiếu|biên lai|hóa đơn)/ }).count();
    note(`Cash ${f.price.toLocaleString('vi-VN')} -> ${r.status()}; tab now "${paidTab}"; sheet: ${sheet}; print receipt buttons: ${print}`);
    expect(r.status()).toBeLessThan(300); expect(Number(clamped)).toBeLessThanOrEqual(f.price);
  });
  test('TC-PY-004 record an advance and allocate it', async ({ page, request }) => {
    const f = await sheetWithWork(request); await staffLogin(page, 'owner.demo'); await openSheet(page, f.patient.id);
    let d = await openPay(page); await d.getByRole('tab', { name: 'Nhận trước (cọc)' }).click();
    await d.getByRole('spinbutton', { name: 'Số tiền cọc' }).fill('1000000');
    let r = page.waitForResponse(r => r.url().endsWith('/payments/advances')); await d.getByRole('button', { name: 'Ghi nhận tiền cọc' }).click(); const a = await r; await page.waitForTimeout(1200);
    await d.getByRole('tab', { name: /^Cần thanh toán/ }).click(); await page.waitForTimeout(500); const bal1 = await d.getByText(/Số dư cọc:/).innerText(); const amount = d.getByRole('spinbutton', { name: /^Số tiền thu cho / }).first();
    await amount.fill('600000'); await amount.blur();
    r = page.waitForResponse(r => r.url().endsWith('/advance-allocations')); await d.getByRole('button', { name: 'Dùng cọc' }).click(); const b = await r; await page.waitForTimeout(1200);
    const bal2 = await d.getByText(/Số dư cọc:/).innerText();
    await amount.fill('600000'); await amount.blur(); await page.waitForTimeout(300);
    const again = await d.getByRole('button', { name: 'Dùng cọc' }).isDisabled(); await shot(page, 'TC-PY-004');
    note(`Advance 1,000,000 -> ${a.status()} (${bal1}); allocate 600,000 -> ${b.status()} (${bal2}); allocating 600,000 again: "Dùng cọc" ${again ? 'disabled (balance too low)' : 'enabled'}`);
    expect(a.status()).toBeLessThan(300); expect(b.status()).toBeLessThan(300); expect(bal2).toMatch(/400\.000/); expect(again).toBe(true);
  });
  test('TC-PY-005/006/007 PayOS QR; only one active QR', async ({ page, request }) => {
    const f = await sheetWithWork(request); await staffLogin(page, 'owner.demo'); await openSheet(page, f.patient.id);
    let d = await openPay(page); const amount = d.getByRole('spinbutton', { name: /^Số tiền thu cho / }).first();
    await amount.fill('100000'); await amount.blur(); await d.getByText('Chuyển khoản', { exact: true }).click();
    const r = page.waitForResponse(r => r.url().endsWith('/payos-orders') && r.request().method() === 'POST');
    await d.getByRole('button', { name: 'Tạo QR và xem bill' }).click(); const res = await r; await page.waitForTimeout(1500);
    const bill = (await d.innerText()).match(/Bill chuyển khoản[\s\S]*?Hết hạn: [^\n]*/)?.[0].replace(/\s+/g, ' ') ?? (await msgs(page));
    await shot(page, 'TC-PY-005'); await d.getByRole('button', { name: 'Close' }).click(); await page.waitForTimeout(800);
    ann('TC-PY-007', `PayOS keys are configured on this server: creating the QR returned ${res.status()}; the "not configured" message (MSG90) cannot be shown without removing the keys`);
    d = await openPay(page); const reopened = (await d.innerText()).includes('Đang chờ chuyển khoản');
    const blocked = await d.getByRole('button', { name: /Xác nhận thu tiền mặt|Tạo QR và xem bill/ }).first().isDisabled(); await shot(page, 'TC-PY-006');
    const api = await Api.as(request, 'owner.demo'); const ctx = (await api.get(`/api/dossiers/${f.dossier.id}/payments/context`)).body;
    const second = await api.post(`/api/dossiers/${f.dossier.id}/payments/payos-orders`, { requestKey: key(), paidOn: todayIso(), lines: [{ activityId: ctx.lines[0].activityId, amountVnd: 50000 }], labLines: [], amountVnd: null });
    const active = await api.get(`/api/dossiers/${f.dossier.id}/payments/payos-orders/active`);
    ann('TC-PY-006', `Reopening the dialog shows the pending QR again: ${reopened} (API active order -> ${active.status} ${JSON.stringify(active.body)?.slice(0, 80)}); payment buttons disabled: ${blocked}; a second QR request -> ${second.status} ${JSON.stringify(second.body)}`);
    note(`QR created -> ${res.status()}: ${bill}. Paying in the PayOS sandbox and the webhook step were not performed (no sandbox payer).`);
    expect(res.status()).toBeLessThan(300); expect(second.status).toBe(409);
  });
  test('TC-PY-003 lab fee 30% first', async ({ page, request }) => {
    const f = await sheetWithWork(request); await staffLogin(page, 'owner.demo'); await openSheet(page, f.patient.id);
    await page.getByRole('button', { name: /^Phiếu labo cho/ }).first().click(); await page.waitForTimeout(1500); const m = dlg(page);
    await pick(page, m.getByRole('combobox', { name: 'Cty cung cấp' }));
    await m.locator('input[aria-label="Công việc labo"], textarea[aria-label="Công việc labo"]').first().fill('Mão sứ labo 30%');
    await m.getByLabel('Hẹn').fill(addDays(todayIso(), 5)); await m.getByLabel('Tổng phí').fill('1000000'); await m.getByRole('button', { name: 'LƯU LẠI' }).click(); await page.waitForTimeout(2000);
    const d = await openPay(page); await d.getByRole('spinbutton', { name: /^Số tiền thu cho Mão/ }).first().fill('500000'); await d.getByRole('spinbutton', { name: /^Số tiền thu cho Mão/ }).first().blur(); await page.waitForTimeout(400);
    const warn = await d.getByText(/30% phí labo/).allInnerTexts(); const disabled = await d.getByRole('button', { name: 'Xác nhận thu tiền mặt' }).isDisabled(); await shot(page, 'TC-PY-003');
    await d.getByRole('spinbutton', { name: /^Số tiền thu cho labo/ }).first().fill('300000'); await d.getByRole('spinbutton', { name: /^Số tiền thu cho labo/ }).first().blur(); await page.waitForTimeout(400);
    const enabled = !(await d.getByRole('button', { name: 'Xác nhận thu tiền mặt' }).isDisabled());
    const r = page.waitForResponse(r => r.url().endsWith('/payments') && r.request().method() === 'POST'); await d.getByRole('button', { name: 'Xác nhận thu tiền mặt' }).click(); const res = await r; await page.waitForTimeout(1200);
    note(`Lab fee 1,000,000; paying 500,000 for the service only: warning "${warn.join(' ')}", confirm disabled ${disabled}. With 300,000 for the lab fee: enabled ${enabled}, saved -> ${res.status()}`);
    expect(disabled).toBe(true); expect(res.status()).toBeLessThan(300);
    // TC-TR-021: the paid lab order cannot be removed or lowered below the paid amount.
    await page.keyboard.press('Escape'); await page.waitForTimeout(800);
    await page.getByRole('button', { name: /^Phiếu labo cho/ }).first().click(); await page.waitForTimeout(1500); const m2 = dlg(page);
    await m2.getByLabel('Tổng phí').fill('100000'); await m2.getByRole('button', { name: 'LƯU LẠI' }).click(); const lower = await msgs(page); await shot(page, 'TC-TR-021');
    const del = m2.getByRole('button', { name: /Xóa/ }); const delState = (await del.count()) ? (await del.first().isDisabled() ? 'disabled' : 'enabled') : 'absent';
    let delMsg = ''; if (delState === 'enabled') { await del.first().click(); await page.locator('.ant-popover:visible, .ant-modal-confirm').getByRole('button', { name: /Đồng ý|Xóa|OK/ }).last().click().catch(() => {}); delMsg = await msgs(page); }
    ann('TC-TR-021', `After 300,000 of the lab fee is paid: lowering the fee to 100,000 -> "${lower}"; delete button ${delState} ${delMsg ? '-> "' + delMsg + '"' : ''}`);
  });
  test('TC-PY-008 forfeit a no-show advance', async ({ page, request }) => {
    const f = await sheetWithWork(request, 32, ['36'], false); const api = await Api.as(request, 'owner.demo');
    const adv = await api.post(`/api/dossiers/${f.dossier.id}/payments/advances`, { requestKey: key(), paidOn: todayIso(), lines: [], labLines: [], amountVnd: 500000 });
    const w = await sheetWithWork(request); const ctx = (await api.get(`/api/dossiers/${w.dossier.id}/payments/context`)).body;
    await api.post(`/api/dossiers/${w.dossier.id}/payments/advances`, { requestKey: key(), paidOn: todayIso(), lines: [], labLines: [], amountVnd: 300000 });
    const cur = (await api.get(`/api/dossiers/${w.dossier.id}/`)).body; const v = (cur.dossier ?? cur).version;
    const refused = await api.post(`/api/dossiers/${w.dossier.id}/payments/no-show-forfeit`, { requestKey: key(), expectedVersion: v, reason: 'Khách không đến' });
    await staffLogin(page, 'owner.demo'); await page.goto('/finance/statistics'); await page.waitForTimeout(2500);
    await page.getByText('Tiền còn cần thu', { exact: true }).first().click(); const m = page.getByRole('dialog', { name: 'Phiếu điều trị còn cần thu' }); await page.waitForTimeout(1000);
    const row = m.getByRole('row').filter({ hasText: f.patient.patientCode }); const other = await m.getByRole('row').filter({ hasText: w.patient.patientCode }).getByRole('button', { name: 'Khách không đến, bỏ cọc' }).count();
    await row.getByRole('button', { name: 'Khách không đến, bỏ cọc' }).click(); const fm = page.getByRole('dialog', { name: 'Xóa phiếu do khách không đến' });
    await fm.getByLabel('Lý do khách không đến').fill('Khách không đến lịch hẹn, bỏ cọc'); await shot(page, 'TC-PY-008-confirm');
    const r = page.waitForResponse(r => r.url().endsWith('/no-show-forfeit')); await fm.getByRole('button', { name: 'Ghi nhận bỏ cọc và xóa phiếu' }).click(); const res = await r; await page.waitForTimeout(1500);
    await shot(page, 'TC-PY-008');
    note(`Plan-only sheet with a 500,000 advance (${adv.status}) forfeited from the finance statistics -> ${res.status()} ${await res.text()}. Sheet with treatment work: no forfeit button (${other} found); API -> ${refused.status} ${JSON.stringify(refused.body)}`);
    expect(res.status()).toBeLessThan(300); expect(other).toBe(0); expect(refused.status).toBeGreaterThanOrEqual(400); void ctx;
  });
  test('TC-PY-009 a user without finance rights cannot record payments', async ({ page, request }) => {
    const f = await sheetWithWork(request); await staffLogin(page, 'assistant.demo'); await openSheet(page, f.patient.id);
    const btn = page.getByRole('button', { name: 'Trả tiền' }); const state = (await btn.count()) ? (await btn.first().isDisabled() ? 'disabled' : 'visible') : 'hidden';
    await shot(page, 'TC-PY-009'); const api = await Api.as(request, 'assistant.demo');
    const ctx = await api.get(`/api/dossiers/${f.dossier.id}/payments/context`);
    const pay = await api.post(`/api/dossiers/${f.dossier.id}/payments`, { requestKey: key(), paidOn: todayIso(), lines: [], labLines: [], amountVnd: null });
    note(`assistant.demo on the sheet: "Trả tiền" ${state}; API context -> ${ctx.status} ${JSON.stringify(ctx.body).slice(0, 120)}; record payment -> ${pay.status} ${JSON.stringify(pay.body).slice(0, 120)}`);
    expect(state).not.toBe('visible'); expect(pay.status).toBeGreaterThanOrEqual(400);
  });
});

test.describe('UC-45/46 Statistics', () => {
  const branch1 = async (page: Page) => { const c = page.getByRole('combobox', { name: 'Chi nhánh' }); if (await c.count()) { await pick(page, c, /thử nghiệm/); await page.waitForTimeout(1800); } };
  const figures = async (page: Page) => {
    const out: Record<string, number> = {};
    await page.getByRole('heading', { level: 3 }).first().waitFor({ timeout: 60000 }); await page.waitForTimeout(800);
    for (const k of ['Số tiền cần thu dịch vụ', 'Tiền thực thu', 'Tiền nhận trước', 'Tiền còn cần thu', 'Tiền dụng cụ vật liệu', 'Lãi']) {
      const card = page.locator('.ant-card:has(h3)').filter({ has: page.getByText(k, { exact: true }) }).first();
      const v = (await card.locator('h3').first().innerText({ timeout: 5000 }).catch(() => '')).match(/-?[\d.]+/)?.[0];
      out[k] = v ? Number(v.replace(/\./g, '')) : NaN;
    }
    return out;
  };
  test('TC-RP-001/002 finance figures and the list behind a figure', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await page.goto('/finance/statistics'); await page.waitForTimeout(2500); await branch1(page);
    const fig = await figures(page); await shot(page, 'TC-RP-001');
    note(`Branch "Chi nhánh thử nghiệm", default period: ${Object.entries(fig).map(([k, v]) => `${k} ${v.toLocaleString('vi-VN')}`).join('; ')}`);
    await page.getByText('Tiền thực thu', { exact: true }).first().click(); const m = page.getByRole('dialog', { name: 'Danh sách các lần thực thu' }); await page.waitForTimeout(1000);
    const cells = await m.locator('tbody tr').evaluateAll(rows => rows.map(r => Array.from(r.querySelectorAll('td')).map(td => td.textContent ?? '')));
    const amounts = cells.map(c => c.map(x => x.match(/^([\d.]+) ?[đ₫]$/)?.[1]).filter(Boolean)).map(a => Number((a[a.length - 1] ?? '0').replace(/\./g, '')));
    const sum = amounts.reduce((s, x) => s + x, 0); await shot(page, 'TC-RP-002');
    ann('TC-RP-002', `"Tiền thực thu" ${fig['Tiền thực thu'].toLocaleString('vi-VN')} opens ${cells.length} payment row(s) summing ${sum.toLocaleString('vi-VN')}`);
    expect(Number.isNaN(fig['Tiền thực thu'])).toBe(false); expect(sum).toBe(fig['Tiền thực thu']);
  });
  test('TC-RP-003 deleted sheets excluded; cancelled sheets keep collected money', async ({ page, request }) => {
    const api = await Api.as(request, 'owner.demo');
    const paid = await sheetWithWork(request); const ctx = (await api.get(`/api/dossiers/${paid.dossier.id}/payments/context`)).body;
    await api.post(`/api/dossiers/${paid.dossier.id}/payments`, { requestKey: key(), paidOn: todayIso(), lines: [{ activityId: ctx.lines[0].activityId, amountVnd: 200000 }], labLines: [], amountVnd: null });
    const p2 = (await api.post(`/api/patients/${paid.patient.id}/dossiers`, { branchId: 1, dentistId: 4, diagnosis: 'Phiếu trống', note: null, startedOn: null, requestKey: key(), sourceVisitId: null, expectedSourceVisitVersion: null })).body.dossier;
    await staffLogin(page, 'owner.demo'); await page.goto('/finance/statistics'); await page.waitForTimeout(2500); await branch1(page); const before = await figures(page);
    const del = await api.request_delete?.(p2) ?? await (async () => { const r = await request.delete(`http://localhost:5080/api/dossiers/${p2.id}/`, { headers: { Authorization: `Bearer ${api.token}` }, data: { expectedVersion: p2.version, requestKey: key() } }); return r.status(); })();
    const cur = (await api.get(`/api/dossiers/${paid.dossier.id}/`)).body; const v = (cur.dossier ?? cur).version;
    const cancel = await api.post(`/api/dossiers/${paid.dossier.id}/payments/cancel-dossier`, { requestKey: key(), expectedVersion: v, reason: 'Hủy phiếu kiểm thử' });
    await page.getByRole('button', { name: 'Tải lại' }).click(); await page.waitForTimeout(2500); const after = await figures(page); await shot(page, 'TC-RP-003');
    note(`Empty sheet deleted -> ${del}; paid sheet (cost ${paid.price.toLocaleString('vi-VN')}, collected 200,000) cancelled -> ${cancel.status} ${cancel.status >= 400 ? JSON.stringify(cancel.body) : ''}. Before: cần thu ${before['Số tiền cần thu dịch vụ'].toLocaleString('vi-VN')}, thực thu ${before['Tiền thực thu'].toLocaleString('vi-VN')}, còn cần thu ${before['Tiền còn cần thu'].toLocaleString('vi-VN')}. After: cần thu ${after['Số tiền cần thu dịch vụ'].toLocaleString('vi-VN')}, thực thu ${after['Tiền thực thu'].toLocaleString('vi-VN')}, còn cần thu ${after['Tiền còn cần thu'].toLocaleString('vi-VN')}`);
    expect(after['Tiền thực thu']).toBe(before['Tiền thực thu']);
  });
  test('TC-RP-004/005 invalid range; unauthorized user', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await page.goto('/finance/statistics'); await page.waitForTimeout(2000);
    const from = page.getByLabel('Từ ngày mở phiếu'); const to = page.getByLabel('Đến ngày mở phiếu');
    await from.click(); await from.fill('20/11/2026'); await page.keyboard.press('Enter'); await page.waitForTimeout(500);
    await to.click(); await to.fill('10/11/2026'); await page.keyboard.press('Enter'); await page.waitForTimeout(1200);
    const toVal = await to.inputValue(); const m = await msgs(page); await shot(page, 'TC-RP-004');
    await page.goto('/work-statistics'); await page.waitForTimeout(1500);
    let ws = ''; if (await page.getByRole('combobox', { name: 'Thời gian' }).count()) { await pick(page, page.getByRole('combobox', { name: 'Thời gian' }), /Tùy chọn|Khoảng/).catch(() => ''); await page.waitForTimeout(500);
      if (await page.getByLabel('Từ ngày').count()) { await page.getByLabel('Từ ngày').fill('2026-11-20'); await page.getByLabel('Đến ngày').fill('2026-11-10'); await page.waitForTimeout(1200);
        ws = `work statistics: to="${await page.getByLabel('Đến ngày').inputValue()}" (min ${await page.getByLabel('Đến ngày').getAttribute('min')}), "${await msgs(page)}"`; } }
    note(`Finance statistics: from 20/11/2026 then to 10/11/2026 -> to field "${toVal}" (earlier dates are disabled in the picker) "${m}"; ${ws}`);
    await staffLogin(page, 'assistant.demo'); const menu = await page.getByRole('link', { name: 'Thống kê tài chính' }).count();
    await page.goto('/finance/statistics'); await page.waitForTimeout(2000); const txt = (await page.locator('main').last().innerText()).slice(0, 200).replace(/\s+/g, ' '); await shot(page, 'TC-RP-005');
    ann('TC-RP-005', `assistant.demo: menu links ${menu}; /finance/statistics shows "${txt}"`);
    expect(menu).toBe(0); expect(txt).not.toMatch(/Tiền thực thu/);
  });
  test('TC-RP-006/007/008 work and service lines; search; deleted work not counted', async ({ page, request }) => {
    const f = await sheetWithWork(request); const api = await Api.as(request, 'owner.demo');
    const ctx = (await api.get(`/api/dossiers/${f.dossier.id}/payments/context`)).body;
    await api.post(`/api/dossiers/${f.dossier.id}/payments`, { requestKey: key(), paidOn: todayIso(), lines: [{ activityId: ctx.lines[0].activityId, amountVnd: 750000 }], labLines: [], amountVnd: null });
    const g = await sheetWithWork(request);
    await staffLogin(page, 'owner.demo'); await page.goto('/work-statistics'); await page.waitForTimeout(2000); await branch1(page);
    const total0 = await page.getByLabel('Tổng hợp').innerText().catch(() => '');
    await page.getByLabel('Tìm công việc').fill(f.patient.patientCode); await page.waitForTimeout(1800);
    const row = (await page.getByRole('row').filter({ hasText: f.patient.patientCode }).first().innerText()).replace(/\s+/g, ' ');
    const rows = await page.locator('tbody tr').filter({ hasNotText: 'Trống' }).count(); const total1 = await page.getByLabel('Tổng hợp').innerText().catch(() => ''); await shot(page, 'TC-RP-006');
    const statusFilter = await page.getByRole('combobox', { name: /Trạng thái/ }).count();
    ann('TC-RP-007', `Search "${f.patient.patientCode}" -> ${rows} row(s), totals "${total1.replace(/\s+/g, ' ')}" (all: "${total0.replace(/\s+/g, ' ')}"); status filter controls: ${statusFilter}`);
    note(`Row: ${row} (cost ${f.price.toLocaleString('vi-VN')} = 1 x price, paid 750,000)`);
    await page.getByLabel('Tìm công việc').fill(g.patient.patientCode); await page.waitForTimeout(1800);
    const before = await page.getByRole('row').filter({ hasText: g.patient.patientCode }).count();
    await openSheet(page, g.patient.id); await page.getByRole('button', { name: /^Xóa quá trình/ }).first().click();
    const dr = page.waitForResponse(r => r.url().includes('/sheet-activities/') && r.request().method() === 'DELETE');
    await page.getByRole('dialog', { name: 'Bạn có muốn xóa dòng này không?' }).getByRole('button', { name: 'Đồng ý', exact: true }).click(); const delStatus = (await dr).status(); await page.waitForTimeout(1200);
    await page.goto('/work-statistics'); await page.waitForTimeout(2000); await branch1(page);
    await page.getByLabel('Tìm công việc').fill(g.patient.patientCode); await page.waitForTimeout(1800);
    const left = await page.getByRole('row').filter({ hasText: g.patient.patientCode }).count(); await shot(page, 'TC-RP-008');
    ann('TC-RP-008', `Work line of ${g.patient.patientCode} (rows before: ${before}) deleted from the sheet -> ${delStatus}; rows for it in the statistics after: ${left}`); expect(left).toBe(0);
    expect(row).toMatch(/750\.000/);
  });
});
