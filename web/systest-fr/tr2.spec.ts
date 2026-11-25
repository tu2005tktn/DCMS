import { test, expect, type Page } from '@playwright/test';
import { Api, key, note, rnd, shot, staffLogin, todayIso } from './helpers';
import { newPatient, sheetWithWork } from './fx';

const modal = (page: Page) => page.locator('.ant-modal-wrap:visible .ant-modal').last();
async function pickBranch1(page: Page) {
  const sel = page.locator('.ant-select').filter({ hasText: /S09 - Chi nhánh/ }).first();
  if (await sel.count() && !(await sel.innerText()).includes('thử nghiệm')) {
    await sel.click(); await page.waitForTimeout(400);
    await page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option').filter({ hasText: 'S09 - Chi nhánh thử nghiệm' }).first().click(); await page.waitForTimeout(1500);
  }
}
async function firstOpt(page: Page, combo: any) {
  await combo.click({ force: true }); await page.waitForTimeout(500);
  await page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option').first().click();
}
async function selectOpt(page: Page, combo: any, text: string) {
  await combo.click(); await combo.fill?.(text).catch(() => {}); await page.waitForTimeout(600);
  await page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option').filter({ hasText: text }).first().click();
}

test.describe('UC-34 Manage Treatment Plan Templates', () => {
  const name = `Niềng răng cơ bản ${rnd()}`;
  test('TC-TR-016 create a template with items', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await page.goto('/treatment-templates'); await page.waitForTimeout(1500); await pickBranch1(page);
    const err = await page.getByText('Bạn không có quyền thực hiện thao tác này.').count();
    await page.getByRole('button', { name: 'Tạo kế hoạch', exact: true }).click();
    await modal(page).getByRole('textbox').first().fill(name); await modal(page).getByRole('button', { name: 'Lưu', exact: true }).click(); await page.waitForTimeout(1500);
    for (const [teeth, svc] of [['11, 21', 'Niềng răng mắc cài kim loại'], ['', 'Tái khám siết niềng răng']]) {
      await page.getByRole('button', { name: '+ Thêm hạng mục' }).click();
      if (teeth) await page.getByLabel('Răng', { exact: true }).fill(teeth);
      await selectOpt(page, page.getByRole('combobox', { name: 'Điều trị/dịch vụ' }), svc);
      await page.getByRole('button', { name: 'Đồng ý', exact: true }).click(); await page.waitForTimeout(1200);
    }
    const text = await page.locator('.ant-modal-wrap:visible, main').last().innerText(); await shot(page, 'TC-TR-016');
    note(`Owner (default branch view showed a permission error before switching to branch 1: ${err > 0}); template "${name}" created with 2 items: ${text.match(/\d+ hạng mục · Tổng chi phí: [^\n]*/)?.[0]}`);
    expect(text).toMatch(/2 hạng mục/);
  });
  test('TC-TR-017 rename, edit and delete', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await page.goto('/treatment-templates'); await page.waitForTimeout(1500); await pickBranch1(page);
    await page.getByLabel('Tìm tên kế hoạch').fill(name); await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click(); await page.waitForTimeout(1000);
    await page.getByRole('row').filter({ hasText: name }).getByRole('button').first().click(); await page.waitForTimeout(1200);
    await page.getByRole('button', { name: 'Sửa tên' }).click(); const m = page.locator('.ant-modal-wrap:visible .ant-modal').last();
    await m.getByRole('textbox').first().fill(name + ' v2'); await m.getByRole('button', { name: 'Lưu', exact: true }).click(); await page.waitForTimeout(1200);
    await page.getByRole('button', { name: 'Xóa', exact: true }).last().click(); await page.locator('.ant-popover:visible').getByRole('button', { name: 'Đồng ý' }).last().click(); await page.waitForTimeout(1200);
    const one = await page.getByText(/1 hạng mục/).count(); await shot(page, 'TC-TR-017-item');
    await page.getByRole('button', { name: 'Xóa kế hoạch' }).click(); await page.locator('.ant-popover:visible').getByRole('button', { name: 'Đồng ý' }).last().click(); await page.waitForTimeout(1500);
    await page.getByLabel('Tìm tên kế hoạch').fill(name); await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click(); await page.waitForTimeout(1000);
    const left = await page.getByRole('row').filter({ hasText: name }).count(); await shot(page, 'TC-TR-017');
    note(`Renamed to "${name} v2"; deleted one item (1 hạng mục left: ${one > 0}); deleted the template (rows left ${left})`); expect(left).toBe(0);
  });
});

test.describe('UC-35/36 Lab work', () => {
  test('TC-TR-019/020 create a lab order for a work line; due date before order date rejected', async ({ page, request }) => {
    const f = await sheetWithWork(request); await staffLogin(page, 'owner.demo');
    await page.goto(`/patients/${f.patient.id}?tab=treatment`); await page.waitForTimeout(2000);
    await page.getByRole('button', { name: /^Phiếu labo cho/ }).first().click(); await page.waitForTimeout(1500);
    const m = modal(page); console.log('LAB FORM ' + (await m.ariaSnapshot()).split(String.fromCharCode(10)).filter(l => /textbox|combobox|spinbutton|button/.test(l)).join(' | '));
    await firstOpt(page, m.getByRole('combobox', { name: 'Cty cung cấp' }));
    await m.locator('input[aria-label="Công việc labo"], textarea[aria-label="Công việc labo"]').first().fill('Mão sứ kim loại răng 36');
    await m.getByLabel('Hẹn').fill('2026-01-01'); await m.getByRole('button', { name: 'LƯU LẠI' }).click(); await page.waitForTimeout(1000);
    const dueErr = (await m.locator('.ant-form-item-explain-error, [role=alert], .lab-order-error').allInnerTexts()).join(' | '); await shot(page, 'TC-TR-020');
    test.info().annotations.push({ type: 'TC-TR-020', description: `Due date 01/01/2026 (before the order date): "${dueErr}"` });
    await m.getByLabel('Hẹn').fill(new Date(Date.now() + 5 * 86400_000).toISOString().slice(0, 10));
    await m.getByLabel('Tổng phí').fill('500000'); await m.getByRole('button', { name: 'LƯU LẠI' }).click(); await page.waitForTimeout(2000);
    await shot(page, 'TC-TR-019'); const labText = await page.locator('main').last().innerText();
    note(`Lab order saved from the work line ("Phiếu labo"): ${labText.match(/Mão sứ kim loại răng 36/) ? 'shown on the sheet' : 'see screenshot'}`);
    expect(dueErr.length).toBeGreaterThan(0);
  });
  test('TC-TR-022/023/024/025 track lab orders through the statuses', async ({ page }) => {
    await staffLogin(page, 'owner.demo'); await page.goto('/lab-orders'); await page.waitForTimeout(1500);
    await page.getByRole('button', { name: /^Chưa gửi/ }).click(); await page.waitForTimeout(1000); await shot(page, 'TC-TR-022');
    const row = page.getByRole('button', { name: /^Mở phiếu labo Mão sứ kim loại răng 36/ }).first(); await expect(row).toBeVisible();
    await row.click(); await page.waitForTimeout(1000);
    const panel = page.locator('.ant-drawer-open .ant-drawer-content, .ant-modal-wrap:visible .ant-modal').last();
    const first = await panel.getByRole('button').allInnerTexts(); test.info().annotations.push({ type: 'TC-TR-025', description: `From "Chưa gửi" the only actions are: ${first.filter(x => x && x !== 'Close').join(', ')} (no direct "Giao bệnh nhân")` });
    for (const [action, note2] of [['Gửi xưởng', ''], ['Đã nhận về', ''], ['Yêu cầu làm lại', 'Sai màu răng'], ['Gửi xưởng', ''], ['Đã nhận về', ''], ['Giao bệnh nhân', '']]) {
      if (note2) await panel.getByLabel('Ghi chú đổi trạng thái').fill(note2);
      await panel.getByRole('button', { name: action, exact: true }).click(); await page.waitForTimeout(1200);
    }
    const t = await panel.innerText(); await shot(page, 'TC-TR-023');
    note(`Lab order moved Chưa gửi → Gửi xưởng → Đã nhận về → Làm lại ("Sai màu răng") → Gửi xưởng → Đã nhận về → Giao bệnh nhân; final ${t.match(/Đã giao bệnh nhân/)?.[0] ?? '?'}; history entries shown`);
    test.info().annotations.push({ type: 'TC-TR-024', description: 'From Đã nhận về chose "Yêu cầu làm lại" with note "Sai màu răng", then sent again' });
    expect(first).not.toContain('Giao bệnh nhân'); expect(t).toMatch(/Đã giao bệnh nhân/);
  });
});

test.describe('UC-38 Prescribe Medicine', () => {
  test('TC-TR-029/030/031 prescribe, edit, and quantity rules', async ({ page, request }) => {
    const p = await newPatient(await Api.as(request, 'assistant.demo'));
    await staffLogin(page, 'owner.demo'); await page.goto(`/patients/${p.id}?tab=prescriptions`); await page.waitForTimeout(1500);
    await page.getByRole('tab', { name: 'Đơn thuốc', exact: true }).click(); await page.waitForTimeout(800);
    await page.getByRole('button', { name: /Kê đơn thuốc/ }).first().click(); await page.waitForTimeout(1000);
    const m = page.locator('.ant-modal-wrap:visible .ant-modal').last();
    const branch = m.getByRole('combobox', { name: 'Chi nhánh kê đơn' });
    if (await branch.isEnabled()) { await branch.click({ force: true }); await page.waitForTimeout(400); await page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option').filter({ hasText: 'thử nghiệm' }).first().click(); }
    { const c = m.getByRole('combobox', { name: 'Tên thuốc dòng 1' }); await c.click({ force: true }); await c.fill('Amoxicillin'); await page.waitForTimeout(800); await page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option').filter({ hasText: 'Amoxicillin' }).first().click(); }
    await m.getByRole('spinbutton', { name: 'Số lượng dòng 1' }).fill('1.5'); await m.getByRole('spinbutton', { name: 'Số lượng dòng 1' }).blur();
    const frac = await m.getByRole('spinbutton', { name: 'Số lượng dòng 1' }).inputValue();
    await m.getByRole('spinbutton', { name: 'Số lượng dòng 1' }).fill('20');
    await m.getByRole('textbox', { name: 'Hướng dẫn sử dụng dòng 1' }).fill('Uống 1 viên x 2 lần/ngày sau ăn');
    await m.getByRole('textbox', { name: 'Đơn vị thuốc tùy chọn dòng 1' }).fill('viên');
    await m.getByRole('button', { name: '+ THÊM' }).click();
    const c2 = m.getByRole('combobox', { name: 'Tên thuốc dòng 2' }); await c2.click({ force: true }); await c2.fill('Nước súc miệng'); await page.waitForTimeout(800);
    const outside = await page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option').allInnerTexts();
    test.info().annotations.push({ type: 'evidence', description: `Typing an outside medicine "Nước súc miệng" in line 2 offers: ${outside.join(', ') || 'no option (free text not accepted)'}` });
    if (outside.length) { await page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option').first().click(); await m.getByRole('textbox', { name: 'Đơn vị thuốc tùy chọn dòng 2' }).fill('chai'); }
    else { await page.keyboard.press('Escape'); await m.getByRole('button', { name: 'Xóa dòng 2' }).click(); }
    await shot(page, 'TC-TR-029-form');
    const save = page.waitForResponse(r => r.url().includes('/api/prescriptions/patients/') && r.request().method() === 'POST');
    await m.getByRole('button', { name: 'Lưu đơn thuốc' }).click(); const r = await save; const b = await r.json(); await page.waitForTimeout(1500); await shot(page, 'TC-TR-029');
    note(`Prescription saved -> ${r.status()} with ${b.items?.length ?? '?'} line(s) (Amoxicillin x20 viên, dosage)`);
    test.info().annotations.push({ type: 'TC-TR-031', description: `Typing quantity 1.5 leaves "${frac}" (whole base units only)` });
    expect(r.status()).toBeLessThan(300); expect(frac).not.toBe('1.5');
  });
  test('TC-TR-032 an archived patient cannot be prescribed for', async ({ page, request }) => {
    const api = await Api.as(request, 'assistant.demo'); const p = await newPatient(api);
    await api.post(`/api/patients/${p.id}/archive`, { version: p.version, reason: 'Kiểm thử' });
    await staffLogin(page, 'owner.demo'); await page.goto(`/patients/${p.id}?tab=prescriptions`); await page.waitForTimeout(1500);
    await page.getByRole('tab', { name: 'Đơn thuốc', exact: true }).click(); await page.waitForTimeout(800);
    const btn = page.getByRole('button', { name: /Kê đơn thuốc/ }).first();
    const state = (await btn.count()) ? (await btn.isDisabled() ? 'disabled' : 'enabled') : 'absent'; await shot(page, 'TC-TR-032');
    note(`Archived ${p.patientCode}: "Kê đơn thuốc" is ${state}`); expect(state).not.toBe('enabled');
  });
});
