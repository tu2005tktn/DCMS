import { test, expect, type Page, type Locator } from '@playwright/test';
import { Api, key, note, shot, staffLogin } from './helpers';
import { newPatient } from './fx';
import { file, png } from './files';

const NL = String.fromCharCode(10);
async function service(page: Page, name: string) {
  const box = page.getByRole('combobox', { name: 'Điều trị/dịch vụ', exact: true });
  await box.focus(); await box.fill(name); await page.waitForTimeout(600);
  const opt = page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option').filter({ hasText: name }).first();
  await opt.click();
}
async function openTab(page: Page, pid: number) {
  await page.goto(`/patients/${pid}?tab=treatment`); await page.waitForTimeout(2000);
}
async function newSheet(page: Page) {
  const before = await page.locator('article').count();
  const sel = page.getByRole('combobox', { name: 'Chi nhánh tạo phiếu' });
  if (!(await page.getByText('S09 - Chi nhánh thử nghiệm').count())) { await sel.click(); await page.locator('.ant-select-dropdown:visible').last().getByText('S09 - Chi nhánh thử nghiệm', { exact: true }).click(); }
  const res = page.waitForResponse(r => /\/api\/patients\/\d+\/dossiers(\/quick)?$/.test(new URL(r.url()).pathname) && r.request().method() === 'POST');
  await page.getByRole('button', { name: /TẠO PHIẾU MỚI/i }).click(); const body = await (await res).json(); await page.waitForTimeout(1500);
  expect(await page.locator('article').count()).toBe(before + 1);
  return (body.dossier?.code ?? body.code ?? '') as string;
}
const sheetText = (page: Page) => page.locator('main').last().innerText();

test.describe('UC-31/32/33 Treatment sheet (owner.demo as dentist)', () => {
  let pid = Number(process.env.TR_PID ?? 0); let code = '';
  test.beforeAll(async ({ request }) => { if (!pid) pid = (await newPatient(await Api.as(request, 'assistant.demo'))).id; console.log('TR PID ' + pid); });

  test('TC-TR-001 create a sheet and plan an item', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await openTab(page, pid);
    code = await newSheet(page);
    await page.getByRole('button', { name: 'Thêm hạng mục kế hoạch' }).first().click();
    await page.getByLabel('Răng', { exact: true }).fill('36');
    await service(page, 'Trám răng composite');
    await page.getByRole('button', { name: 'Đồng ý', exact: true }).click(); await page.waitForTimeout(1500);
    const t = await sheetText(page); await shot(page, 'TC-TR-001');
    note(`Sheet ${code} created with "TẠO PHIẾU MỚI"; plan row tooth 36 "Trám răng composite" shows price ${t.match(/Trám răng composite[\s\S]{0,80}?(\d{3}\.\d{3} ₫)/)?.[1] ?? '?'} and "Thành tiền"`);
    expect(t).toContain('Trám răng composite'); expect(t).toMatch(/400\.000 ₫/);
  });
  test('TC-TR-003 apply a plan template', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await openTab(page, pid);
    await page.getByRole('button', { name: 'Kế hoạch điều trị', exact: true }).first().click(); await page.waitForTimeout(1000);
    const dlg = page.getByRole('dialog').last(); console.log('TEMPLATE DIALOG ' + (await dlg.ariaSnapshot()).split(NL).slice(0, 40).join(NL));
    await dlg.getByRole('combobox', { name: 'Chọn kế hoạch điều trị' }).click(); await dlg.getByRole('combobox', { name: 'Chọn kế hoạch điều trị' }).fill('Cạo vôi'); await page.waitForTimeout(800);
    await page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option').filter({ hasText: 'Cạo vôi răng và viêm lợi nhẹ' }).first().click();
    await dlg.getByRole('button', { name: 'Áp dụng' }).click(); await page.waitForTimeout(2000);
    const t = await sheetText(page); await shot(page, 'TC-TR-003');
    note(`Template "Cạo vôi răng và viêm lợi nhẹ" applied: plan now contains ${['Cạo vôi', 'Bôi fluor', 'Tái khám'].filter(x => t.includes(x)).join(', ')}`);
    expect(t).toMatch(/Cạo vôi/);
  });
  test('TC-TR-004 copy an item and revise price and discount', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await openTab(page, pid);
    const before = (await sheetText(page)).split('Trám răng composite').length - 1;
    await page.getByRole('button', { name: 'Chức năng Trám răng composite', exact: true }).first().click();
    await page.getByRole('menuitem', { name: 'Sao chép', exact: true }).click(); await page.waitForTimeout(1500);
    const copies = (await sheetText(page)).split('Trám răng composite').length - 1;
    await page.getByRole('button', { name: 'Chức năng Trám răng composite', exact: true }).last().click();
    await page.getByRole('menuitem', { name: /Sửa/ }).first().click(); await page.waitForTimeout(800);
    console.log('EDIT FIELDS ' + (await page.locator('main').last().ariaSnapshot()).split(NL).filter(l => /textbox|spinbutton|combobox|radio|button "(Đồng ý|Hủy|×|✓)/.test(l)).join(NL));
    const price = page.getByLabel('Chi phí', { exact: true });
    await price.fill('-1000'); await price.blur(); await page.waitForTimeout(300);
    const clamped = await price.inputValue();
    await price.fill('450000');
    const pct = page.getByRole('radio', { name: '%' }); if (await pct.count()) await pct.check({ force: true });
    const disc = page.getByLabel(/Giảm giá/, { exact: false }).first(); if (await disc.count()) await disc.fill('120');
    await page.getByRole('button', { name: 'Đồng ý', exact: true }).click(); await page.waitForTimeout(1000);
    const over = (await page.locator('.ant-form-item-explain-error, .ant-message-notice, [role=alert]').allInnerTexts()).join(' | ');
    if (await disc.count()) await disc.fill('10');
    const reason = page.getByLabel(/Lý do giảm|Ghi chú giảm/).first(); if (await reason.count()) await reason.fill('Khách quen');
    await page.getByRole('button', { name: 'Đồng ý', exact: true }).click(); await page.waitForTimeout(1500);
    const t = await sheetText(page); await shot(page, 'TC-TR-004');
    note(`"Sao chép": composite rows ${before} -> ${copies}; typing -1000 in Chi phí leaves "${clamped}" (negative not accepted); 120% discount: "${over}"; saved 450,000 with a discount of 10 (Loại giảm giá default) and reason: ${/450\.000 ₫/.test(t)}`);
    expect(copies).toBe(before + 1); expect(clamped).not.toContain('-');
  });
  test('TC-TR-006 record and confirm the work done for a plan item', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await openTab(page, pid);
    await page.getByRole('button', { name: 'Chức năng Trám răng composite', exact: true }).first().click();
    await page.getByRole('menuitem', { name: 'Thực hiện', exact: true }).click(); await page.waitForTimeout(800);
    await page.getByLabel('Ghi chú việc đã làm').fill('Trám xong răng 36');
    await page.getByRole('button', { name: 'Đồng ý', exact: true }).click(); await page.waitForTimeout(2000);
    await shot(page, 'TC-TR-006'); const shown = await page.getByText('Trám xong răng 36').count();
    note(`"Thực hiện" on the plan row, note "Trám xong răng 36", "Đồng ý": work line dated today, tooth 36, Trám răng composite listed under QUÁ TRÌNH ĐIỀU TRỊ (${shown})`);
    expect(shown).toBeGreaterThan(0);
  });
  test('TC-TR-008 add a note and an image to the sheet', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await openTab(page, pid);
    await page.getByLabel('Ghi chú hồ sơ', { exact: true }).first().fill('Bệnh nhân hợp tác tốt');
    await page.getByRole('button', { name: 'Lưu ghi chú' }).first().click(); await page.waitForTimeout(1000);
    const chooser = page.waitForEvent('filechooser'); await page.getByRole('button', { name: 'Thêm ảnh' }).first().click();
    await (await chooser).setFiles(file('sau-tram.png', png(21))); await page.waitForTimeout(3000);
    await shot(page, 'TC-TR-008'); const figures = await page.getByRole('figure', { name: 'sau-tram.png' }).count();
    note(`Sheet note saved; image "sau-tram.png" shown on the sheet: ${figures}`); expect(figures).toBeGreaterThan(0);
  });
  test('TC-TR-009 print the treatment sheet', async ({ page, context }) => {
    await staffLogin(page, 'owner.demo'); await openTab(page, pid);
    const btn = page.getByRole('button', { name: 'In phiếu đang mở' }).first();
    const popup = context.waitForEvent('page', { timeout: 15_000 }).catch(() => null);
    await btn.click(); const p = await popup; await page.waitForTimeout(1500);
    const target = p ?? page; await target.waitForTimeout(1500); await shot(target, 'TC-TR-009');
    const t = await target.locator('body').innerText();
    note(`"In phiếu đang mở" -> ${p ? 'print window' : 'print view'}: ${t.slice(0, 200).replace(/\s+/g, ' ')}`);
    expect(t).toMatch(/Trám răng composite/);
  });
  test('TC-TR-011/010 close as completed; a closed sheet cannot change', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await openTab(page, pid);
    await newSheet(page);
    await page.getByRole('button', { name: 'Thêm hạng mục kế hoạch' }).first().click();
    await page.getByLabel('Răng', { exact: true }).fill('11'); await service(page, 'Bôi fluor');
    await page.getByRole('button', { name: 'Đồng ý', exact: true }).click(); await page.waitForTimeout(1200);
    await page.getByRole('button', { name: 'Chức năng Bôi fluor', exact: true }).first().click();
    await page.getByRole('menuitem', { name: 'Thực hiện', exact: true }).click(); await page.getByRole('button', { name: 'Đồng ý', exact: true }).click(); await page.waitForTimeout(1500);
    await page.getByRole('button', { name: 'Trạng thái phiếu: Chưa xong' }).first().click(); await page.getByRole('menuitem', { name: 'Hoàn tất', exact: true }).click(); await page.waitForTimeout(1000);
    const m = page.locator('.ant-modal-wrap:visible .ant-modal').last(); if (await m.count()) await m.getByRole('button', { name: /Đồng ý|Xác nhận|OK/ }).last().click();
    await page.waitForTimeout(1500); await shot(page, 'TC-TR-011');
    const status = await page.locator('article').first().innerText();
    const addDisabled = await page.getByRole('button', { name: 'Thêm hạng mục kế hoạch' }).first().isDisabled().catch(() => true);
    note(`Sheet with all work done set to "Hoàn tất": header shows ${status.match(/Trạng thái phiếu[^\n]*|Hoàn tất/)?.[0]}`);
    test.info().annotations.push({ type: 'TC-TR-010', description: `On the completed sheet "Thêm hạng mục kế hoạch" is disabled/absent: ${addDisabled}` });
    expect(status).toContain('Hoàn tất');
  });

  test('TC-TR-002 quick-create a sheet', async ({ page, request }) => {
    const p2 = await newPatient(await Api.as(request, 'assistant.demo'));
    await staffLogin(page, 'owner.demo'); await openTab(page, p2.id);
    const req = page.waitForRequest(r => r.url().includes('/dossiers') && r.method() === 'POST');
    const c = await newSheet(page); const r = await req; await shot(page, 'TC-TR-002');
    note(`"TẠO PHIẾU MỚI" created ${c} in one step (POST ${new URL(r.url()).pathname})`); expect(c).toMatch(/^HS/);
  });
  test('TC-TR-005 whole-number quantity and FDI teeth', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await openTab(page, pid);
    await page.getByRole('button', { name: 'Thêm hạng mục kế hoạch' }).first().click();
    await page.getByLabel('Răng', { exact: true }).fill('99'); await service(page, 'Trám răng composite');
    await page.getByRole('button', { name: 'Đồng ý', exact: true }).click(); await page.waitForTimeout(1200);
    const err = (await page.locator('.ant-form-item-explain-error, .ant-message-notice, [role=alert]').allInnerTexts()).join(' | ');
    await shot(page, 'TC-TR-005'); const saved = await page.getByRole('cell', { name: '99', exact: true }).count();
    note(`Tooth "99": ${err || 'no message'}; row with tooth 99 saved: ${saved}`); expect(saved).toBe(0);
  });
  test('TC-TR-012 stop a sheet before completion with a reason', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await openTab(page, pid);
    await page.locator('button[aria-label^="Trạng thái phiếu"]').first().click(); await page.waitForTimeout(400);
    const items = await page.getByRole('menuitem').allInnerTexts(); await shot(page, 'TC-TR-012'); await page.keyboard.press('Escape');
    note(`Status menu offers: ${items.join(', ')}`);
    expect(items.some(x => /Dừng|Ngừng/.test(x)), 'The UI has no "stop before completion" outcome (and no reason field); only Chưa xong / Hoàn tất / Hủy phiếu.').toBeTruthy();
  });
  test('TC-TR-013 reopen a completed sheet with a reason', async ({ page, request }) => {
    const p3 = await newPatient(await Api.as(request, 'assistant.demo'));
    await staffLogin(page, 'owner.demo'); await openTab(page, p3.id); await newSheet(page);
    await page.getByRole('button', { name: 'Thêm hạng mục kế hoạch' }).first().click();
    await page.getByLabel('Răng', { exact: true }).fill('21'); await service(page, 'Bôi fluor');
    await page.getByRole('button', { name: 'Đồng ý', exact: true }).click(); await page.waitForTimeout(1200);
    await page.getByRole('button', { name: 'Chức năng Bôi fluor', exact: true }).first().click();
    await page.getByRole('menuitem', { name: 'Thực hiện', exact: true }).click(); await page.getByRole('button', { name: 'Đồng ý', exact: true }).click(); await page.waitForTimeout(1500);
    await page.getByRole('button', { name: 'Trạng thái phiếu: Chưa xong' }).first().click(); await page.getByRole('menuitem', { name: 'Hoàn tất', exact: true }).click(); await page.waitForTimeout(1500);
    await page.getByRole('button', { name: 'Trạng thái phiếu: Hoàn tất' }).first().click();
    const req = page.waitForRequest(r => r.url().endsWith('/reopen'), { timeout: 10_000 }).catch(() => null);
    await page.getByRole('menuitem', { name: 'Chưa xong', exact: true }).click(); await page.waitForTimeout(800);
    const dialog = await page.locator('.ant-modal-wrap:visible').count(); const r = await req; await page.waitForTimeout(1200); await shot(page, 'TC-TR-013');
    const body = r ? JSON.parse(r.postData() ?? '{}') : {};
    note(`"Chưa xong" on a completed sheet reopened it without asking for a reason (reason dialog shown: ${dialog > 0}); reason sent by the client: "${body.reason ?? ''}"`);
    expect(dialog, 'Reopen does not ask the user for a reason; the client sends the fixed text "Mở lại phiếu điều trị" (GBR-055 requires a reason).').toBeGreaterThan(0);
  });
  test('TC-TR-014 cancel a sheet with payments', async ({ page, request }) => {
    await staffLogin(page, 'owner.demo'); await openTab(page, pid);
    const art = page.locator('article').first();
    await art.locator('button[aria-label^="Trạng thái phiếu"]').click(); await page.getByRole('menuitem', { name: 'Hủy phiếu', exact: true }).click();
    const m = page.locator('.ant-modal-wrap:visible .ant-modal').last(); await page.waitForTimeout(500);
    const hasReason = await m.getByRole('textbox').count(); const txt = await m.innerText(); await shot(page, 'TC-TR-014-dialog');
    await m.getByRole('button', { name: 'Không hủy' }).click();
    note(`"Hủy phiếu" confirmation: "${txt.replace(/\s+/g, ' ').slice(0, 160)}"; reason field present: ${hasReason > 0}`);
    expect(hasReason, 'The cancel dialog has no reason field; the client sends the fixed reason "Hủy phiếu" (test case requires reason "Khách đổi ý").').toBeGreaterThan(0);
  });
  test('TC-TR-015 delete only an empty sheet opened by mistake', async ({ page, request }) => {
    const p2 = await newPatient(await Api.as(request, 'assistant.demo'));
    await staffLogin(page, 'owner.demo'); await openTab(page, p2.id);
    const c = await newSheet(page);
    await page.getByRole('button', { name: `Xóa phiếu ${c}` }).click();
    const m = page.locator('.ant-modal-wrap:visible .ant-modal').last(); await m.getByRole('button', { name: /Xóa/ }).last().click(); await page.waitForTimeout(1500);
    const gone = await page.getByRole('button', { name: `Xóa phiếu ${c}` }).count(); await shot(page, 'TC-TR-015-empty');
    await openTab(page, pid);
    const withWork = page.locator('article').filter({ hasText: /HS/ }).first(); const code2 = ((await withWork.innerText()).match(/HS[0-9A-F]{12}/) ?? [''])[0];
    await page.getByRole('button', { name: `Xóa phiếu ${code2}` }).click().catch(() => {});
    const m2 = page.locator('.ant-modal-wrap:visible .ant-modal').last(); if (await m2.count()) await m2.getByRole('button', { name: /Xóa/ }).last().click();
    await page.waitForTimeout(1500); const msg = (await page.locator('.ant-modal-wrap:visible, [role=alert], .ant-message-notice').allInnerTexts()).join(' | ');
    await shot(page, 'TC-TR-015'); note(`Empty sheet ${c} deleted (still listed: ${gone}); deleting ${code2} which has work: "${msg.replace(/\s+/g, ' ').slice(0, 160)}"`);
    expect(gone).toBe(0);
  });
});
