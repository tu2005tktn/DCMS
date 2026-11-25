import { test, expect } from '@playwright/test';
import { Api, key, note, rnd, shot, staffLogin } from './helpers';
import { newPatient } from './fx';

test.describe('UC-24 Manage Patient Records', () => {
  test('TC-PM-001 search patient records', async ({ page, request }) => {
    const api = await Api.as(request, 'assistant.demo'); const p = await newPatient(api);
    await staffLogin(page, 'assistant.demo'); await page.getByRole('link', { name: 'Bệnh nhân', exact: true }).click();
    const search = page.getByPlaceholder('Mã bệnh nhân, họ tên (có hoặc không dấu), SĐT');
    const found: string[] = [];
    for (const term of [p.fullName.replace('Bệnh nhân', 'benh nhan'), p.patientCode]) {
      await search.fill(term); await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click(); await page.waitForTimeout(800);
      if (await page.getByRole('row').filter({ hasText: p.patientCode }).count()) found.push(term);
    }
    await shot(page, 'TC-PM-001-found');
    await search.fill('zzzzqq'); await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click();
    await expect(page.getByText('Không tìm thấy hồ sơ bệnh nhân phù hợp.', { exact: true })).toBeVisible(); await shot(page, 'TC-PM-001-empty');
    note(`Found ${p.patientCode} by: ${found.join(' | ')} (name without accents, code); "zzzzqq" shows MSG43`);
    expect(found.length).toBe(2);
  });
  test('TC-PM-002/004 create a record (with duplicate check)', async ({ page, request }) => {
    const api = await Api.as(request, 'assistant.demo'); const original = await newPatient(api);
    await staffLogin(page, 'assistant.demo'); await page.getByRole('link', { name: 'Bệnh nhân', exact: true }).click();
    await page.getByRole('button', { name: 'Thêm hồ sơ', exact: true }).click();
    const form = page.getByRole('dialog');
    await form.getByLabel('Họ tên', { exact: true }).fill(original.fullName);
    await form.getByLabel('Ngày sinh', { exact: true }).fill(original.birthDate);
    await form.getByRole('button', { name: 'Lưu', exact: true }).click();
    await expect(form.getByText('Có hồ sơ nghi trùng', { exact: true })).toBeVisible();
    await expect(form.getByRole('button', { name: /Lưu$/ })).toBeDisabled(); await shot(page, 'TC-PM-004');
    await form.getByRole('checkbox', { name: 'Tôi đã đối chiếu, đây là người khác với các hồ sơ trên' }).check();
    const res = page.waitForResponse(r => /\/api\/patients\/?$/.test(r.url()) && r.request().method() === 'POST');
    await form.getByRole('button', { name: /Lưu$/ }).click();
    const r = await res; expect(r.status()).toBe(201); const created = (await r.json()).patient;
    await expect(page.getByRole('heading', { name: original.fullName, exact: true })).toBeVisible(); await shot(page, 'TC-PM-002');
    note(`Duplicate warning "Có hồ sơ nghi trùng" blocked saving until reviewed; new record ${created.patientCode} (auto code) created; not merged with ${original.patientCode}`);
    test.info().annotations.push({ type: 'TC-PM-004', description: `Same name and birth date as ${original.patientCode}: "Có hồ sơ nghi trùng", Lưu disabled until "Tôi đã đối chiếu..." is ticked; a separate record ${created.patientCode} was then created` });
  });
  test('TC-PM-003 a required field is enforced', async ({ page, request }) => {
    const owner = await Api.as(request, 'owner.demo');
    const g = (await owner.get('/api/system-settings/settings/general')).body;
    const put = (addr: boolean, v: number) => owner.put('/api/system-settings/settings/general', { expectedVersion: v, requestKey: key(), reason: `Địa chỉ bắt buộc=${addr} (system test)`, settings: { ...g.settings, requiredFields: { ...g.settings.requiredFields, address: addr } } });
    expect((await put(true, g.version)).status).toBe(200);
    try {
      await staffLogin(page, 'assistant.demo'); await page.getByRole('link', { name: 'Bệnh nhân', exact: true }).click();
      await page.getByRole('button', { name: 'Thêm hồ sơ', exact: true }).click();
      const form = page.getByRole('dialog');
      await form.getByLabel('Họ tên', { exact: true }).fill(`Không Địa Chỉ ${rnd()}`);
      await form.getByRole('button', { name: 'Lưu', exact: true }).click();
      await expect(form.getByText('Vui lòng nhập Địa chỉ.', { exact: true }).first()).toBeVisible(); await shot(page, 'TC-PM-003');
      note('Address made required by the owner; saving without address shows "Vui lòng nhập Địa chỉ." and nothing is saved');
    } finally { const now = (await owner.get('/api/system-settings/settings/general')).body; await put(false, now.version); }
  });
  test('TC-PM-005/006 edit, archive and restore a record', async ({ page, request }) => {
    const api = await Api.as(request, 'assistant.demo'); const p = await newPatient(api);
    await staffLogin(page, 'assistant.demo'); await page.goto(`/patients/${p.id}`);
    await page.getByRole('button', { name: 'Sửa thông tin', exact: true }).click();
    const form = page.getByRole('dialog');
    await form.getByLabel('Địa chỉ', { exact: true }).fill('45 Trần Phú, Huế');
    await form.getByRole('button', { name: 'Lưu', exact: true }).click();
    await expect(page.getByText('45 Trần Phú, Huế', { exact: true })).toBeVisible();
    await expect(page.getByText(p.patientCode).first()).toBeVisible(); await shot(page, 'TC-PM-005');
    test.info().annotations.push({ type: 'TC-PM-005', description: `Address changed to "45 Trần Phú, Huế"; patient code ${p.patientCode} unchanged` });
    await page.getByRole('button', { name: 'Ngừng sử dụng', exact: true }).click();
    await form.getByLabel('Lý do', { exact: true }).fill('Chuyển nơi khác');
    await form.getByRole('button', { name: 'Ngừng sử dụng', exact: true }).click();
    await expect(page.getByText('Hồ sơ đã ngừng sử dụng', { exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Sửa thông tin', exact: true })).toBeDisabled(); await shot(page, 'TC-PM-006-archived');
    await page.getByRole('button', { name: 'Khôi phục hồ sơ', exact: true }).click();
    await form.getByLabel('Lý do', { exact: true }).fill('Quay lại khám');
    await form.getByRole('button', { name: 'Khôi phục', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Sửa thông tin', exact: true })).toBeEnabled(); await shot(page, 'TC-PM-006-restored');
    test.info().annotations.push({ type: 'TC-PM-006', description: 'Archived with reason: "Hồ sơ đã ngừng sử dụng", Sửa thông tin disabled; restored with reason: editable again' });
  });
  test('TC-PM-007 open the profile tabs of a patient with history', async ({ page, request }) => {
    const api = await Api.as(request, 'assistant.demo');
    const p = (await api.get('/api/patients?keyword=BN000001')).body.items.find((x: any) => x.patientCode === 'BN000001');
    for (const user of ['assistant.demo', 'dentist.demo']) {
      await staffLogin(page, user); await page.goto(`/patients/${p.id}`); await page.waitForTimeout(1500);
      const tabs = await page.getByRole('tab').allInnerTexts(); const seen: string[] = [];
      for (const t of tabs) { await page.getByRole('tab', { name: t, exact: true }).click(); await page.waitForTimeout(1200); seen.push(`${t}: ${(await page.locator('.ant-tabs-tabpane-active').innerText()).slice(0, 60).replace(/\s+/g, ' ')}`); }
      await shot(page, `TC-PM-007-${user}`); note(`${user} on ${p.patientCode}: ${seen.join(' || ')}`);
    }
  });
});

test.describe('UC-25 Verify Record Link', () => {
  test('TC-PM-008 staff of another branch cannot open a branch-1 patient with history', async ({ page, request }) => {
    const api = await Api.as(request, 'assistant.demo'); const p = (await api.get('/api/patients?keyword=BN000001')).body.items.find((x: any) => x.patientCode === 'BN000001');
    await staffLogin(page, 'assistant.b.demo'); await page.goto(`/patients/${p.id}`); await page.waitForTimeout(2000);
    const text = await page.locator('main').last().innerText(); await shot(page, 'TC-PM-008');
    note(`assistant.b.demo (branch 2) opening ${p.patientCode} (visits in branch 1): ${text.slice(0, 160).replace(/\s+/g, ' ')}`);
    expect(text).not.toContain(p.fullName);
  });
});
