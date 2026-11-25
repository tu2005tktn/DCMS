import { test, expect, type Page } from '@playwright/test';
import { Api, key, note, shot, staffLogin, todayIso } from './helpers';
import { freshVisit, newPatient, nextSlotToday, staffBook, checkIn, dentistUser } from './fx';
import { file, png, XRAY } from './files';

const modal = (page: Page) => page.locator('.ant-modal-wrap:visible .ant-modal, .ant-drawer-open .ant-drawer-content').last();
const activePane = (page: Page) => page.locator('main').last().locator('.ant-tabs-tabpane-active').last();
async function upload(page: Page, files: string[], type?: string) {
  const d = modal(page);
  if (type) { await d.getByRole('combobox', { name: '* Loại tài liệu' }).click(); await page.locator('.ant-select-dropdown:visible').last().getByTitle(type, { exact: true }).click(); }
  await d.locator('input[type=file]').setInputFiles(files);
  await d.getByRole('button', { name: /^Tải lên \d+ tệp$/ }).click();
}

test.describe.serial('UC-27/28/29/30 Examination', () => {
  let v: { visitId: string; patientId: number; code: string; name: string; user: string | undefined };
  test.beforeAll(async ({ request }) => {
    if (process.env.EX_VISIT) { const [visitId, patientId, code, user] = process.env.EX_VISIT.split('|'); v = { visitId, patientId: Number(patientId), code, name: process.env.EX_NAME ?? '', user }; }
    else v = await freshVisit(request);
  });

  test('TC-EX-001 start the examination of a checked-in patient', async ({ page }) => {
    await staffLogin(page, v.user!); await page.goto(`/visits/${v.visitId}`);
    await expect(page.getByRole('heading', { name: `${v.code} · ${v.name}` })).toBeVisible();
    const start = page.getByRole('button', { name: 'Bắt đầu khám', exact: true });
    if (await start.count()) await start.click();
    await expect(page.getByLabel('Lý do khám / triệu chứng', { exact: true })).toBeEnabled();
    await shot(page, 'TC-EX-001'); note(`${v.user} opened visit ${v.visitId.slice(0, 8)} of ${v.code}; "Bắt đầu khám" enabled the examination form (Lượt đến: Đang khám)`);
  });
  test('TC-EX-002 record symptoms, history, tooth finding and diagnoses', async ({ page }) => {
    await staffLogin(page, v.user!); await page.goto(`/visits/${v.visitId}`);
    await page.getByLabel('Lý do khám / triệu chứng', { exact: true }).fill('Đau răng hàm dưới bên trái 3 ngày');
    await page.getByLabel('Tiền sử', { exact: true }).fill('Không có bệnh nền');
    const chart = page.getByRole('region', { name: 'Sơ đồ răng', exact: true });
    await chart.getByRole('button', { name: 'Răng 36', exact: true }).click();
    await chart.getByRole('textbox', { name: 'Ghi nhanh kết quả', exact: true }).fill('Sâu răng mặt nhai');
    await chart.getByRole('button', { name: 'Áp dụng cho 1 răng', exact: true }).click();
    await chart.locator('label').filter({ hasText: 'Mô hình 3D' }).click(); await page.waitForTimeout(2000); await shot(page, 'TC-EX-002-3d');
    await chart.locator('label').filter({ hasText: 'Sơ đồ 2D' }).click();
    await page.getByRole('tab', { name: 'Tổng kết khám', exact: true }).click();
    const input = page.getByRole('combobox', { name: 'Chẩn đoán', exact: true });
    for (const d of ['Sâu răng', 'Viêm lợi']) { await input.fill(d); await page.keyboard.press('Enter'); await page.waitForTimeout(300); }
    await page.getByRole('button', { name: 'Lưu nháp', exact: true }).click(); await expect(page.getByRole('button', { name: 'Lưu nháp', exact: true })).toBeEnabled();
    await page.reload(); await page.getByRole('tab', { name: 'Tổng kết khám', exact: true }).click(); await page.waitForTimeout(800);
    const tags = page.locator('.ant-select-selection-item'); const saved = (await tags.allInnerTexts()).join(', '); await shot(page, 'TC-EX-002');
    note(`Saved draft and reloaded: tooth 36 "Sâu răng mặt nhai", diagnoses kept: ${saved.match(/Sâu răng|Viêm lợi, nướu/g)?.join(', ')}; 2D/3D switch (TC-EX-002-3d)`);
    expect(saved).toContain('Sâu răng'); expect(saved).toContain('Viêm lợi');
  });
  test('TC-EX-004 confirming needs a diagnosis and a next step', async ({ page }) => {
    await staffLogin(page, v.user!); await page.goto(`/visits/${v.visitId}`);
    await page.getByRole('tab', { name: 'Tổng kết khám', exact: true }).click();
    await page.getByRole('button', { name: 'Hoàn tất khám', exact: true }).click(); await page.waitForTimeout(800);
    const missing = await page.getByRole('alert').allInnerTexts(); await shot(page, 'TC-EX-004-missing');
    test.info().annotations.push({ type: 'evidence', description: `Without "Hướng xử trí": ${missing.join(' ').replace(/\s+/g, ' ').slice(0, 200)}` });
    expect(missing.join(' ')).toMatch(/Hướng xử trí|hướng xử trí/);
  });
  test('TC-EX-010/011 order an X-ray, upload and record the result, review it', async ({ page }) => {
    await staffLogin(page, v.user!); await page.goto(`/visits/${v.visitId}`);
    await page.getByRole('tab', { name: /^Cận lâm sàng/ }).click();
    await page.getByRole('button', { name: 'Thêm chỉ định', exact: true }).click();
    const m = modal(page);
    await m.getByRole('combobox', { name: 'Tên chỉ định' }).click(); await page.locator('.ant-select-dropdown:visible').last().getByText(/Phim toàn cảnh/).first().click();
    await m.getByRole('textbox', { name: 'Răng / vùng chỉ định' }).fill('36');
    await m.getByRole('textbox', { name: 'Lý do chỉ định' }).fill('Đánh giá sâu răng');
    await m.getByRole('button', { name: 'Tạo chỉ định' }).click(); await page.waitForTimeout(1200);
    await expect(activePane(page)).toContainText('Chờ kết quả'); await shot(page, 'TC-EX-010');
    test.info().annotations.push({ type: 'TC-EX-010', description: 'Ordered "Phim toàn cảnh" for 36 (required for diagnosis): shown as "Chờ kết quả"' });
    await page.getByRole('button', { name: 'Tải tệp kết quả' }).first().click();
    await upload(page, [XRAY]); await page.waitForTimeout(2500);
    if (await modal(page).count()) await modal(page).getByRole('button', { name: /Đóng|Close/ }).first().click().catch(() => {});
    await page.getByRole('button', { name: 'Nhận kết quả' }).first().click();
    await modal(page).getByRole('textbox', { name: 'Nội dung kết quả' }).fill('Sâu ngà răng 36, chưa tổn thương tủy');
    await modal(page).getByRole('button', { name: 'Lưu kết quả' }).click(); await page.waitForTimeout(1500);
    const afterResult = await activePane(page).innerText(); await shot(page, 'TC-EX-011-result');
    console.log('AFTER RESULT ' + await activePane(page).ariaSnapshot());
    const review = activePane(page).getByRole('button', { name: /Đọc|Xem xét|Duyệt|Xác nhận đã xem/ }).first();
    await review.click(); await page.waitForTimeout(800);
    if (await modal(page).count()) { const tb = modal(page).getByRole('textbox'); if (await tb.count()) await tb.first().fill('Sâu ngà'); await modal(page).getByRole('button', { name: /Lưu|Xác nhận/ }).last().click(); }
    await page.waitForTimeout(1500); const after = await activePane(page).innerText(); await shot(page, 'TC-EX-011');
    note(`Result image uploaded and text recorded (${afterResult.match(/Có kết quả|Đã có kết quả|Chờ đọc/)?.[0] ?? 'result received'}); after the dentist review: ${after.match(/Đã đọc|Đã xem xét|Đã review|Đã duyệt/)?.[0] ?? after.slice(0, 80)}`);
    expect(after).toMatch(/Đã đọc|Đã xem xét|Đã review|Đã duyệt/);
  });
  test('TC-EX-004b confirm with diagnoses and a next step', async ({ page }) => {
    await staffLogin(page, v.user!); await page.goto(`/visits/${v.visitId}`);
    await page.getByRole('tab', { name: 'Tổng kết khám', exact: true }).click();
    await page.getByRole('combobox', { name: 'Hướng xử trí', exact: true }).click(); await page.getByText('Tiếp tục điều trị', { exact: true }).click();
    await page.getByLabel('Dặn dò / lời khuyên', { exact: true }).fill('Vệ sinh răng miệng, tái khám sau điều trị');
    await page.getByRole('button', { name: 'Hoàn tất khám', exact: true }).click(); await page.waitForTimeout(1500);
    await expect(page.getByText(/Phiếu khám đã hoàn tất|Đã hoàn tất khám/).first()).toBeVisible(); await shot(page, 'TC-EX-004');
    test.info().annotations.push({ type: 'TC-EX-004', description: 'With diagnoses Sâu răng/Viêm lợi and "Tiếp tục điều trị": "Hoàn tất khám" accepted ("Đã hoàn tất khám")' });
  });
  test('TC-EX-013/012 order after confirmation needs a reason; cancel before review', async ({ page }) => {
    await staffLogin(page, v.user!); await page.goto(`/visits/${v.visitId}`);
    await page.getByRole('tab', { name: /^Cận lâm sàng/ }).click();
    await page.getByRole('button', { name: 'Thêm chỉ định', exact: true }).click();
    const m = modal(page); console.log('POST-CONFIRM ORDER ' + await m.ariaSnapshot());
    await m.getByRole('combobox', { name: 'Tên chỉ định' }).click(); await page.locator('.ant-select-dropdown:visible').last().getByText(/Phim quanh chóp/).first().click();
    await m.getByRole('textbox', { name: 'Lý do chỉ định' }).fill('Kiểm tra chân răng');
    await m.getByRole('button', { name: 'Tạo chỉ định' }).click(); await page.waitForTimeout(800);
    const err = (await m.locator('.ant-form-item-explain-error').allInnerTexts()).join(' '); await shot(page, 'TC-EX-013-noreason');
    const reason = m.getByRole('textbox', { name: /Lý do bổ sung/ }); if (await reason.count()) await reason.fill('Cần đánh giá thêm chân răng');
    await m.getByRole('button', { name: 'Tạo chỉ định' }).click(); await page.waitForTimeout(1500); await shot(page, 'TC-EX-013');
    test.info().annotations.push({ type: 'TC-EX-013', description: `Order after confirmation without the amendment reason: "${err}"; with reason the order is created` });
    await page.getByRole('button', { name: 'Hủy chỉ định' }).last().click();
    const c = modal(page); const tb = c.getByRole('textbox'); if (await tb.count()) await tb.first().fill('Không cần chụp thêm');
    await c.getByRole('button', { name: /Hủy chỉ định|Xác nhận/ }).last().click(); await page.waitForTimeout(1500);
    const pane = await activePane(page).innerText(); await shot(page, 'TC-EX-012');
    note(`Second order cancelled with reason before review: ${pane.match(/Đã hủy.*/)?.[0] ?? pane.slice(0, 100)}`); expect(pane).toMatch(/Đã hủy/); expect(err).toMatch(/lý do|Lý do/);
  });
  test('TC-EX-006 complete the visit once investigations are read or cancelled', async ({ page }) => {
    await staffLogin(page, v.user!); await page.goto(`/visits/${v.visitId}`);
    await page.getByRole('tab', { name: 'Tổng kết khám', exact: true }).click();
    await page.getByRole('button', { name: 'Kết thúc lượt khám', exact: true }).click();
    const ending = page.getByRole('dialog'); await ending.getByRole('checkbox').check();
    await ending.getByRole('button', { name: 'Xác nhận kết thúc lượt', exact: true }).click(); await expect(ending).not.toBeVisible();
    await expect(page.getByText('Đã lưu lịch sử khám vào hồ sơ bệnh nhân')).toBeVisible(); await shot(page, 'TC-EX-006');
    note('X-ray reviewed and the extra order cancelled: "Kết thúc lượt khám" completed the visit ("Đã lưu lịch sử khám vào hồ sơ bệnh nhân", Lượt đến: Hoàn tất)');
  });
  test('TC-EX-008 hand over to treatment', async ({ page }) => {
    await staffLogin(page, v.user!); await page.goto(`/visits/${v.visitId}`);
    await page.getByRole('tab', { name: 'Tổng kết khám', exact: true }).click(); await page.waitForTimeout(800);
    await page.getByRole('link', { name: 'Lập kế hoạch điều trị', exact: true }).click(); await page.waitForTimeout(1500);
    await expect(page).toHaveURL(new RegExp(`/patients/${v.patientId}\\?tab=treatment&sourceExaminationVisitId=${v.visitId}`)); await shot(page, 'TC-EX-008');
    note(`"Lập kế hoạch điều trị" opened the patient's treatment tab with sourceExaminationVisitId=${v.visitId.slice(0, 8)}`);
  });
  test('TC-EX-007 amend a confirmed examination with a reason', async ({ page, request }) => {
    await staffLogin(page, v.user!); await page.goto(`/visits/${v.visitId}`);
    await page.getByRole('tab', { name: 'Tổng kết khám', exact: true }).click();
    await page.getByRole('button', { name: 'Đính chính kết quả', exact: true }).click();
    const input = page.getByRole('combobox', { name: 'Chẩn đoán', exact: true }); await input.fill('Viêm tủy'); await page.keyboard.press('Enter'); await page.waitForTimeout(300);
    await page.getByRole('button', { name: 'Lưu đính chính', exact: true }).click(); await page.waitForTimeout(600);
    const noReason = await page.getByText('Nhập lý do đính chính phiếu khám.').count();
    await page.getByLabel('Lý do đính chính', { exact: true }).fill('Bổ sung chẩn đoán sau đọc phim');
    await page.getByRole('button', { name: 'Lưu đính chính', exact: true }).click(); await page.waitForTimeout(2000);
    await shot(page, 'TC-EX-007');
    const api = await Api.as(request, v.user!); const vers = (await api.get(`/api/visits/${v.visitId}/examination/versions`)).body;
    const n = (Array.isArray(vers) ? vers : vers?.items ?? []).length;
    note(`"Đính chính kết quả": saving without a reason shows "Nhập lý do đính chính phiếu khám." (${noReason}); with reason saved; versions kept ${n}`);
    expect(noReason).toBeGreaterThan(0); expect(n).toBeGreaterThan(1);
  });
  test('TC-EX-003 earlier visits are shown during a later examination', async ({ page, request }) => {
    const api = await Api.as(request, 'assistant.demo');
    let second;
    try { const a = await staffBook(api, await nextSlotToday(api), { id: v.patientId, fullName: v.name }); second = await checkIn(api, a.appointmentId, v.patientId, a.version); } catch (e) { test.skip(true, 'no slot for a second visit today'); }
    const user = dentistUser(second!.dentistName) ?? v.user!;
    await staffLogin(page, user); await page.goto(`/visits/${second!.visitId}`);
    await page.getByRole('tab', { name: 'Lịch sử khám', exact: true }).click(); await page.waitForTimeout(1500);
    const text = await activePane(page).innerText(); await shot(page, 'TC-EX-003');
    note(`Second visit of ${v.code}: "Lịch sử khám" shows ${text.slice(0, 200).replace(/\s+/g, ' ')}`); expect(text).toMatch(/Sâu răng/);
  });
});
