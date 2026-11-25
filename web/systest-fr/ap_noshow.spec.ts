import { test, expect } from '@playwright/test';
import { Api, note, shot, staffLogin, todayIso } from './helpers';

// UC-18 cases that depend on the clock: an appointment that has started and is still running.
test.describe.serial('UC-18 Cancel Appointment or Mark No-show (time-dependent)', () => {
  let target: any; let patientId = 0;
  test('TC-AP-016 mark No-show after the appointment has started', async ({ page, request }) => {
    const api = await Api.as(request, 'assistant.demo');
    const cal = (await api.get(`/api/appointments/calendar?branchId=1&from=${todayIso()}&to=${todayIso()}`)).body;
    const items: any[] = cal.appointments;
    const now = Date.now();
    const running = items.filter(a => a.status === 'Confirmed' && Date.parse(a.startAt) <= now && Date.parse(a.endAt) > now + 5 * 60_000);
    for (const a of running) {
      const hit = (await api.get(`/api/patients?keyword=${encodeURIComponent(a.attendeeName)}`)).body.items.find((x: any) => x.fullName === a.attendeeName);
      if (hit) { target = a; patientId = hit.id; break; }
    }
    test.skip(!target, 'No running, not-checked-in appointment with a patient record at execution time');
    await staffLogin(page, 'assistant.demo'); await page.goto(`/patients/${patientId}?tab=appointments`);
    const row = page.locator('.ant-table-tbody > tr').filter({ hasText: target.code });
    await row.getByRole('button', { name: 'Khách không tới', exact: true }).click();
    const modal = page.getByRole('dialog', { name: 'Khách không tới', exact: true });
    await shot(page, 'TC-AP-016-modal');
    const reason = modal.getByRole('textbox'); if (await reason.count()) await reason.first().fill('Gọi không nghe máy');
    await modal.getByRole('button', { name: 'Xác nhận khách không tới', exact: true }).click();
    await expect(row).toContainText('Khách không tới'); await shot(page, 'TC-AP-016');
    const tasks = JSON.stringify((await api.get('/api/care-tasks/?pageSize=200')).body);
    note(`${target.code} (started ${target.startAt ?? target.start}, not checked in) marked "Khách không tới" from the patient profile; care task for the patient exists=${tasks.includes(target.code) || tasks.includes(String(patientId))}`);
  });
  test('TC-AP-018 book a walk-in replacement in the freed time', async ({ page }) => {
    test.skip(!target, 'depends on TC-AP-016');
    await staffLogin(page, 'assistant.demo'); await page.goto(`/patients/${patientId}?tab=appointments`);
    const row = page.locator('.ant-table-tbody > tr').filter({ hasText: target.code });
    await row.getByRole('button', { name: 'Đặt lịch thay thế', exact: true }).click();
    const form = page.getByRole('dialog', { name: 'Đặt lịch thay thế', exact: true });
    await form.getByLabel('Họ tên người đặt', { exact: true }).fill('Khách vãng lai');
    await form.getByLabel('SĐT (10 số) hoặc email', { exact: true }).fill('0987654321');
    await form.getByLabel('Họ tên người khám', { exact: true }).fill('Khách thay thế ST');
    await shot(page, 'TC-AP-018-form');
    await form.getByRole('button', { name: 'Xem lại', exact: true }).click();
    const booked = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/api/appointments'));
    await form.getByRole('button', { name: 'Xác nhận đặt lịch', exact: true }).click();
    const res = await booked; const body = await res.json(); await shot(page, 'TC-AP-018');
    note(`Replacement walk-in in the freed No-show time -> ${res.status()} ${body.code ?? ''} ${body.status ?? body.message ?? ''}`);
    expect(res.status()).toBe(201);
  });
});
