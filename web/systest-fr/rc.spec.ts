import { test, expect } from '@playwright/test';
import { Api, addDays, note, shot, staffLogin, todayIso } from './helpers';
import { checkIn, newPatient, nextSlotToday, slots, staffBook } from './fx';

const CONFIRM = 'Đã đối chiếu đúng người đến khám với lịch và hồ sơ đã chọn';

test.describe('UC-23 Check In Patient', () => {
  test('TC-RC-001 check in a confirmed appointment of today', async ({ page, request }) => {
    const api = await Api.as(request, 'assistant.demo'); const p = await newPatient(api);
    const a = await staffBook(api, await nextSlotToday(api), p);
    await staffLogin(page, 'assistant.demo'); await page.goto('/reception'); await page.waitForTimeout(1500);
    const listRow = page.getByRole('row').filter({ hasText: p.fullName }).first();
    await expect(listRow).toBeVisible({ timeout: 15_000 }); await shot(page, 'TC-RC-001-list');
    await listRow.getByRole('link').or(listRow.getByRole('button')).first().click();
    await expect(page.getByText(`${p.patientCode} · ${p.fullName}`, { exact: true }).last()).toBeVisible({ timeout: 15_000 });
    await page.getByRole('checkbox', { name: CONFIRM }).check();
    await page.getByLabel('Ghi chú thêm', { exact: true }).fill('Đau răng hàm dưới');
    const saved = page.waitForResponse(r => r.url().includes('/check-in') && r.request().method() === 'POST');
    await page.getByRole('button', { name: 'Xác nhận Check-in', exact: true }).click();
    expect((await saved).status()).toBe(200);
    await page.getByRole('link', { name: 'Xem hàng chờ', exact: true }).click(); await page.getByRole('tab', { name: 'Hàng chờ và lượt khám' }).click();
    const row = page.getByRole('row').filter({ hasText: p.patientCode });
    await expect(row).toHaveCount(1); await expect(row).toContainText('Chờ khám');
    await shot(page, 'TC-RC-001'); note(`${a.code} for ${p.patientCode} checked in from the reception screen; queue row shows "Chờ khám"`);
  });

  test('TC-RC-002 create the patient record at the desk during check-in', async ({ page, request }) => {
    const api = await Api.as(request, 'assistant.demo'); const n = Date.now() % 100000;
    const a = await staffBook(api, await nextSlotToday(api), { id: null, fullName: `Khách Mới ${n}` });
    await staffLogin(page, 'assistant.demo'); await page.goto(`/reception/appointments/${a.appointmentId}`);
    await page.getByRole('button', { name: /Tạo hồ sơ/ }).first().click();
    const drawer = page.getByRole('dialog');
    await drawer.getByLabel('Họ tên', { exact: true }).fill(`Khách Mới ${n}`);
    await drawer.getByLabel('Số điện thoại', { exact: true }).fill(`035${n}12`.slice(0, 10)).catch(() => {});
    await shot(page, 'TC-RC-002-form'); console.log('RC002 ' + await drawer.ariaSnapshot());
    const created = page.waitForResponse(r => r.url().endsWith('/api/patients') && r.request().method() === 'POST');
    await drawer.getByRole('button', { name: 'Lưu', exact: true }).click();
    const res = await created; const body = await res.json();
    if (res.status() !== 201) { const dup = drawer.getByRole('checkbox').filter({ hasText: /đối chiếu|nghi trùng/ }); if (await dup.count()) await dup.first().check(); await drawer.getByRole('button', { name: 'Lưu', exact: true }).click(); }
    await page.getByRole('checkbox', { name: CONFIRM }).check();
    const saved = page.waitForResponse(r => r.url().includes('/check-in') && r.request().method() === 'POST');
    await page.getByRole('button', { name: 'Xác nhận Check-in', exact: true }).click();
    expect((await saved).status()).toBe(200); await shot(page, 'TC-RC-002');
    note(`Record created in the drawer (${body?.patient?.patientCode ?? 'see screenshot'}) and the visit checked in`);
  });

  test('TC-RC-003 appointment of another day cannot be checked in', async ({ page, request }) => {
    const api = await Api.as(request, 'assistant.demo'); const p = await newPatient(api);
    const tomorrow = (await slots(api, 3, addDays(todayIso(), 2)))[5];
    const a = await staffBook(api, tomorrow, p);
    await staffLogin(page, 'assistant.demo'); await page.goto(`/reception/appointments/${a.appointmentId}`);
    await expect(page.getByText('Lịch chưa đủ điều kiện tiếp nhận', { exact: true })).toBeVisible();
    await expect(page.getByText('Chỉ tiếp nhận lịch trong ngày hiện tại của phòng khám.', { exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Xác nhận Check-in', exact: true })).toHaveCount(0);
    await shot(page, 'TC-RC-003'); note(`Appointment ${a.code} on ${addDays(todayIso(), 2)}: "Lịch chưa đủ điều kiện tiếp nhận", no check-in button`);
  });

  test('TC-RC-004 patient does not match the appointment', async ({ page, request }) => {
    const api = await Api.as(request, 'assistant.demo'); const p = await newPatient(api); const other = await newPatient(api, { fullName: 'Người Khác Hoàn Toàn', birthDate: '1970-01-01' });
    const a = await staffBook(api, await nextSlotToday(api), p);
    await staffLogin(page, 'assistant.demo'); await page.goto(`/reception/appointments/${a.appointmentId}`);
    await page.getByLabel('Hồ sơ bệnh nhân', { exact: true }).fill(other.patientCode);
    await page.locator('.ant-select-dropdown:visible').getByText(new RegExp(other.patientCode)).click();
    await page.getByRole('checkbox', { name: CONFIRM }).check();
    const rejected = page.waitForResponse(r => r.url().includes('/check-in') && r.request().method() === 'POST');
    await page.getByRole('button', { name: 'Xác nhận Check-in', exact: true }).click();
    const r = await rejected; const body = await r.json(); await page.waitForTimeout(500); await shot(page, 'TC-RC-004');
    note(`Selected ${other.patientCode} for an appointment of ${p.patientCode} -> ${r.status()} ${body.code} "${body.message}"`);
    expect(body.code).toBe('MSG35');
  });

  test('TC-RC-005 archived record cannot be checked in', async ({ page, request }) => {
    const api = await Api.as(request, 'assistant.demo'); const p = await newPatient(api);
    const a = await staffBook(api, await nextSlotToday(api), p);
    await api.post(`/api/patients/${p.id}/archive`, { version: p.version, reason: 'Kiểm thử lưu trữ' });
    await staffLogin(page, 'assistant.demo'); await page.goto(`/reception/appointments/${a.appointmentId}`); await page.waitForTimeout(1500);
    const btn = page.getByRole('button', { name: 'Xác nhận Check-in', exact: true });
    const canCheck = (await btn.count()) && await btn.isEnabled();
    let outcome = (await btn.count()) ? 'confirmation checkbox and "Xác nhận Check-in" are disabled' : 'no check-in button';
    if (canCheck) {
      const box = page.getByRole('checkbox', { name: CONFIRM }); if (await box.count()) await box.check();
      const res = page.waitForResponse(r => r.url().includes('/check-in') && r.request().method() === 'POST');
      await page.getByRole('button', { name: 'Xác nhận Check-in', exact: true }).click();
      const r = await res; const b = await r.json(); outcome = `${r.status()} ${b.code} "${b.message}"`; expect(r.status()).toBeGreaterThanOrEqual(400);
    }
    await shot(page, 'TC-RC-005'); note(`Archived ${p.patientCode}: ${outcome}`);
  });

  test('TC-RC-007 the same appointment gives only one visit', async ({ page, request }) => {
    const api = await Api.as(request, 'assistant.demo'); const p = await newPatient(api);
    const a = await staffBook(api, await nextSlotToday(api), p);
    await staffLogin(page, 'assistant.demo'); await page.goto(`/reception/appointments/${a.appointmentId}`);
    await page.getByRole('checkbox', { name: CONFIRM }).check();
    const visits: string[] = []; page.on('response', async r => { if (r.url().includes('/check-in') && r.request().method() === 'POST' && r.ok()) visits.push((await r.json()).visitId); });
    await page.getByRole('button', { name: 'Xác nhận Check-in', exact: true }).dblclick(); await page.waitForTimeout(2500);
    const q = await api.get(`/api/reception/queue?branchId=1&date=${todayIso()}`);
    const rows = q.body.items.filter((x: any) => x.patientId === p.id).length;
    await shot(page, 'TC-RC-007'); note(`Double-click on "Xác nhận Check-in": responses with visit ids ${[...new Set(visits)].join(', ')}; queue rows for the patient: ${rows}`);
    expect(rows).toBe(1);
  });
});
