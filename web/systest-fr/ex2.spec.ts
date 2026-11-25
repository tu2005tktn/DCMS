import { test, expect, type Page } from '@playwright/test';
import { Api, key, note, shot, staffLogin, todayIso } from './helpers';
import { freshVisit, dentistUser } from './fx';
import { file, png, XRAY } from './files';

const modal = (page: Page) => page.locator('.ant-modal-wrap:visible .ant-modal, .ant-drawer-open .ant-drawer-content').last();
async function openDocs(page: Page, visitId: string) {
  await page.goto(`/visits/${visitId}`); await page.getByRole('tab', { name: 'Ảnh & tài liệu', exact: true }).click(); await page.waitForTimeout(1200);
}
async function uploadDocs(page: Page, files: string[], type: string) {
  await page.getByRole('button', { name: 'Tải tài liệu lên', exact: true }).click();
  const d = modal(page);
  const current = await d.locator('.ant-select').first().innerText();
  if (!current.includes(type)) {
    await d.locator('.ant-select').first().click(); await page.waitForTimeout(400);
    const opts = page.locator('.ant-select-dropdown:visible').last().locator('.ant-select-item-option');
    const texts = await opts.allInnerTexts(); const i = texts.findIndex(t => t.trim() === type);
    if (i < 0) throw new Error(`type ${type} not in ${texts.join(', ')}`); await opts.nth(i).click();
  }
  await d.locator('input[type=file]').setInputFiles(files); await page.waitForTimeout(500);
  return d;
}

test.describe.serial('UC-29/30 Examination documents and AI', () => {
  let v: { visitId: string; patientId: number; code: string; name: string; user: string | undefined };
  test.beforeAll(async ({ request }) => {
    if (process.env.EX2_VISIT) { const [visitId, patientId, code, user] = process.env.EX2_VISIT.split('|'); v = { visitId, patientId: Number(patientId), code, name: '', user }; }
    else v = await freshVisit(request);
    const api = await Api.as(request, v.user!); const w = (await api.get(`/api/visits/${v.visitId}/examination`)).body;
    if (w?.visit?.status === 'Queued') await api.post(`/api/visits/${v.visitId}/start`, { patientId: v.patientId, expectedVersion: w.visit.visitVersion, requestKey: key() });
  });
  test('TC-EX-014 upload images to an open visit', async ({ page }) => {
    await staffLogin(page, 'assistant.demo'); await openDocs(page, v.visitId);
    const d = await uploadDocs(page, [file('trong-mieng-1.png', png(11)), file('trong-mieng-2.png', png(12))], 'Ảnh trong miệng');
    await d.getByRole('button', { name: 'Tải lên 2 tệp' }).click(); await page.waitForTimeout(3000);
    await d.getByRole('button', { name: /Đóng|Close/ }).first().click().catch(() => {});
    await page.getByRole('button', { name: 'Tải lại', exact: true }).click(); await page.waitForTimeout(1200);
    const rows = await page.getByRole('row').filter({ hasText: /trong-mieng-[12]\.png/ }).count(); await shot(page, 'TC-EX-014');
    await page.getByText('trong-mieng-1.png', { exact: true }).first().click(); await page.waitForTimeout(1500); await shot(page, 'TC-EX-014-detail');
    note(`Assistant uploaded 2 PNG "Ảnh trong miệng" to the visit: ${rows} rows listed with type, uploader and time; detail drawer opens`); expect(rows).toBe(2);
  });
  test('TC-EX-015 invalid file type or size is rejected', async ({ page }) => {
    await staffLogin(page, 'assistant.demo'); await openDocs(page, v.visitId);
    const fake = file('virus.jpg', Buffer.concat([Buffer.from('MZ'), Buffer.alloc(300)]));
    const big = file('qua-lon.png', Buffer.concat([png(1), Buffer.alloc(21 * 1024 * 1024)]));
    const d = await uploadDocs(page, [fake, big], 'Ảnh trong miệng'); await page.waitForTimeout(1500);
    const text = await d.innerText(); await shot(page, 'TC-EX-015');
    const upBtn = d.getByRole('button', { name: /^Tải lên \d+ tệp$/ }); const label = await upBtn.innerText();
    if (await upBtn.isEnabled()) { await upBtn.click(); await page.waitForTimeout(3000); }
    const after = await d.innerText(); await shot(page, 'TC-EX-015-after');
    note(`EXE content named .jpg and a 21 MB PNG: drawer shows "${(after.match(/[^\n]*(JPEG\/PNG\/PDF|20[.,]0 MB|không|Không|vượt|lỗi)[^\n]*/g) ?? []).slice(0, 3).join(' | ')}"; upload button "${label}"`);
    expect(after).toMatch(/Chỉ nhận JPEG\/PNG\/PDF|vượt|quá|20[.,]0 MB|không hợp lệ|Không hợp lệ/);
  });
  test('TC-EX-016 view and download documents from the patient profile', async ({ page }) => {
    await staffLogin(page, 'assistant.demo'); await page.goto(`/patients/${v.patientId}/documents`); await page.waitForTimeout(1500);
    await page.getByText('trong-mieng-1.png', { exact: true }).first().click(); await page.waitForTimeout(1200);
    const dl = page.waitForEvent('download'); await modal(page).getByRole('button', { name: 'Tải về' }).click();
    const download = await dl; const p = await download.path(); const size = p ? (await import('node:fs')).statSync(p).size : 0;
    await shot(page, 'TC-EX-016'); note(`/patients/${v.patientId}/documents: opened trong-mieng-1.png, "Tải về" downloaded ${download.suggestedFilename()} (${size} bytes)`);
    expect(size).toBeGreaterThan(100);
  });
  test('TC-EX-019/022 run AI caries detection on an X-ray; diagnosis unchanged', async ({ page }) => {
    await staffLogin(page, v.user!); await openDocs(page, v.visitId);
    const d = await uploadDocs(page, [XRAY], 'X-quang'); await d.getByRole('button', { name: 'Tải lên 1 tệp' }).click(); await page.waitForTimeout(3000);
    await d.getByRole('button', { name: /Đóng|Close/ }).first().click().catch(() => {});
    await page.getByRole('button', { name: 'Tải lại', exact: true }).click(); await page.waitForTimeout(1200);
    await page.getByText('03_Benh_an_R018_7_sau_rang.jpg', { exact: true }).first().click(); await page.waitForTimeout(1200);
    await modal(page).getByRole('tab', { name: 'AI sâu răng' }).click();
    await modal(page).getByRole('button', { name: /Phân tích AI|Phân tích lại/ }).click();
    await expect(modal(page).getByText(/%|Độ tin cậy|nghi sâu/i).first()).toBeVisible({ timeout: 60_000 });
    await page.waitForTimeout(1500); const text = await modal(page).innerText(); await shot(page, 'TC-EX-019');
    note(`AI tab on the panoramic X-ray: ${text.replace(/\s+/g, ' ').slice(0, 260)}`);
    await page.keyboard.press('Escape'); await page.getByRole('tab', { name: 'Tổng kết khám', exact: true }).click(); await page.waitForTimeout(800);
    const diag = (await page.locator('.ant-select-selection-item').allInnerTexts()).join(', '); await shot(page, 'TC-EX-022');
    test.info().annotations.push({ type: 'TC-EX-022', description: `After the AI analysis the examination diagnoses are still "${diag || 'none'}" (AI suggestions are not added)` });
    expect(diag).not.toMatch(/caries/i);
  });
  test('TC-EX-020 no AI tab for a non-X-ray image', async ({ page }) => {
    await staffLogin(page, v.user!); await openDocs(page, v.visitId);
    await page.getByText('trong-mieng-2.png', { exact: true }).first().click(); await page.waitForTimeout(1200);
    const tabs = await modal(page).getByRole('tab').allInnerTexts(); await shot(page, 'TC-EX-020');
    note(`Intraoral photo detail tabs: ${tabs.join(', ')}`); expect(tabs).not.toContain('AI sâu răng');
  });
  test('TC-EX-017 remove an image with a reason; investigation results cannot be removed', async ({ page }) => {
    await staffLogin(page, 'assistant.demo'); await page.goto(`/patients/${v.patientId}?tab=images`); await page.waitForTimeout(1500);
    if (!(await page.getByRole('button', { name: 'Xóa ảnh trong-mieng-2.png' }).count())) { await page.getByRole('tab', { name: 'Hình ảnh', exact: true }).click(); await page.waitForTimeout(1200); }
    await page.getByRole('button', { name: 'Xóa ảnh trong-mieng-2.png' }).first().click();
    const m = page.locator('.ant-modal-wrap:visible .ant-modal').last();
    await m.getByRole('textbox', { name: 'Lý do xóa ảnh' }).fill('Ảnh mờ, chụp lại');
    await m.getByRole('button', { name: 'Xóa ảnh' }).click(); await page.waitForTimeout(1500);
    const gone = await page.getByRole('button', { name: 'Xóa ảnh trong-mieng-2.png' }).count(); await shot(page, 'TC-EX-017');
    // investigation result image of BN000001 (TC-EX-011)
    const api = await Api.as(page.request as any, 'assistant.demo');
    const bn1 = (await api.get('/api/patients?keyword=BN000001')).body.items.find((x: any) => x.patientCode === 'BN000001');
    await page.goto(`/patients/${bn1.id}?tab=images`); await page.waitForTimeout(1500);
    const resultImg = page.getByRole('button', { name: /^Xóa ảnh 03_Benh_an_R018/ });
    let blocked = 'no delete button';
    if (await resultImg.count()) {
      await resultImg.first().click(); const m2 = page.locator('.ant-modal-wrap:visible .ant-modal').last();
      await m2.getByRole('textbox', { name: 'Lý do xóa ảnh' }).fill('Thử xóa kết quả'); await m2.getByRole('button', { name: 'Xóa ảnh' }).click(); await page.waitForTimeout(1500);
      const body = await page.locator('body').innerText();
      blocked = body.includes('Không xóa được') ? 'Không xóa được hình ảnh/tài liệu là kết quả cận lâm sàng (MSG74)' : 'see screenshot';
    }
    await shot(page, 'TC-EX-017-result');
    note(`"Xóa ảnh" with reason removed trong-mieng-2.png (delete buttons left for it: ${gone}); deleting the investigation result image of BN000001: ${blocked}`);
    expect(gone).toBe(0); expect(blocked).toMatch(/no delete button|Không xóa được/);
  });
});

test('TC-EX-009 a treatment visit cannot be examined as a new examination', async ({ page, request }) => {
  const api = await Api.as(request, 'assistant.demo');
  const q = (await api.get(`/api/reception/queue?branchId=1&date=${todayIso()}&pageSize=100`)).body.items as any[];
  const t = q.find(x => x.purpose === 'TreatmentContinuation' && x.status === 'Queued' && dentistUser(x.dentistName) !== 'dual.demo');
  test.skip(!t, 'no queued treatment-continuation visit');
  await staffLogin(page, dentistUser(t.dentistName)!); await page.goto(`/visits/${t.visitId}`); await page.waitForTimeout(1500);
  const btn = page.getByRole('button', { name: 'Bắt đầu khám', exact: true });
  if (await btn.count()) { await btn.click(); await page.waitForTimeout(1200); }
  await page.waitForTimeout(3000); const url = page.url(); const msg = await page.locator('main').last().innerText(); await shot(page, 'TC-EX-009');
  note(`Opening visit ${t.visitId.slice(0, 8)} (checked in as "Điều trị tiếp") does not open an examination: redirected to ${url.replace('http://localhost:5173', '')}; ${msg.slice(0, 120).replace(/\s+/g, ' ')}`);
  expect(url.includes('/patients/') || /dành cho điều trị/.test(msg)).toBeTruthy();
  expect(await page.getByRole('button', { name: 'Bắt đầu khám' }).count()).toBe(0);
});

test('TC-EX-005 an inactive diagnosis cannot be chosen', async ({ page, request, browser }) => {
  const owner = await Api.as(request, 'owner.demo');
  const opts = (await owner.get('/api/system-settings/reference-options?kind=diagnoses')).body as any[]; const target = opts[opts.length - 1];
  const cat = (await owner.get(`/api/system-settings/settings/catalogs/diagnoses/${target.id}`)).body;
  const set = (active: boolean, v: number) => owner.post(`/api/system-settings/settings/catalogs/diagnoses/${target.id}/state`, { expectedVersion: v, requestKey: key(), active, reason: active ? 'Dùng lại' : 'Không dùng nữa (system test)' });
  const r = await set(false, cat.version); expect(r.status).toBe(200);
  try {
    const v = await freshVisit(request);
    const dp = await (await browser.newContext()).newPage(); await staffLogin(dp, v.user!); await dp.goto(`/visits/${v.visitId}`);
    if (await dp.getByRole('button', { name: 'Bắt đầu khám' }).count()) await dp.getByRole('button', { name: 'Bắt đầu khám' }).click();
    await dp.getByRole('tab', { name: 'Tổng kết khám', exact: true }).click();
    await dp.getByRole('combobox', { name: 'Chẩn đoán', exact: true }).fill(target.name.slice(0, 12)); await dp.waitForTimeout(1200);
    const offered = await dp.locator('.ant-select-dropdown:visible .ant-select-item-option').allInnerTexts(); await shot(dp, 'TC-EX-005');
    note(`Diagnosis "${target.name}" deactivated by the owner; the picker for "${target.name.slice(0, 12)}" offers: ${offered.join(', ') || 'nothing'}`);
    expect(offered).not.toContain(target.name);
  } finally { const now = (await owner.get(`/api/system-settings/settings/catalogs/diagnoses/${target.id}`)).body; await set(true, now.version); }
});
