import { test, expect } from '@playwright/test';
import { Api, note, shot, staffLogin } from './helpers';
import { newPatient, sheetWithWork } from './fx';

// UC-19 Book Next Visit from the treatment plan (patient profile › Lịch hẹn), and UC-02 when Google is not configured.
test.describe.configure({ timeout: 150_000 });
test.beforeEach(({ page }) => { page.setDefaultTimeout(15_000); });
const ann = (type: string, description: string) => test.info().annotations.push({ type, description });

test.describe('UC-19 Book Next Visit', () => {
  test('TC-AP-019/020 book the next visit of an open plan item; consent is required', async ({ page, request }) => {
    const f = await sheetWithWork(request, 32, ['36'], false); await staffLogin(page, 'owner.demo');
    await page.goto(`/patients/${f.patient.id}?tab=appointments`); const panel = page.getByRole('region', { name: 'Hạng mục kế tiếp cần hẹn' });
    await panel.waitFor({ timeout: 20000 }); await page.waitForTimeout(1500); const card = (await panel.innerText()).replace(/\s+/g, ' ');
    await panel.getByRole('button', { name: 'Đặt lịch' }).click(); await page.waitForTimeout(2000);
    const d = page.locator('.ant-drawer-open .ant-drawer-content').last();
    const days = d.getByRole('group', { name: 'Ngày gần ngày dự kiến có lịch của bác sĩ' }).getByRole('button');
    if (await days.count()) { await days.first().click(); await page.waitForTimeout(1500); }
    const slotsGroup = d.getByRole('group', { name: 'Khung giờ bác sĩ có lịch làm' }); await slotsGroup.getByRole('button').first().click(); await page.waitForTimeout(500);
    await d.locator('#next-visit-booker').fill('Bệnh nhân tự đặt'); await d.locator('#next-visit-contact').fill('0987654321');
    const ex = d.locator('#next-visit-exception'); if (await ex.count()) await ex.fill('Khách yêu cầu');
    const confirm = d.getByRole('button', { name: 'Xác nhận đặt lịch' }); const consent = d.getByRole('checkbox').filter({ hasText: /đồng ý/ }).or(d.getByRole('checkbox', { name: /đồng ý/ })).first();
    const withoutConsent = await confirm.isDisabled(); let msg = '';
    if (!withoutConsent) { await confirm.click(); await page.waitForTimeout(1200); msg = (await d.locator('[role=alert], .ant-alert-message').allInnerTexts()).join(' | '); }
    await shot(page, 'TC-AP-020');
    ann('TC-AP-020', `Without ticking patient consent: "Xác nhận đặt lịch" ${withoutConsent ? 'is disabled' : `enabled -> "${msg}"`}`);
    await consent.check(); const r = page.waitForResponse(x => x.request().method() === 'POST' && x.url().endsWith('/api/appointments'));
    await confirm.click(); const res = await r; const body = await res.json().catch(() => ({})); await page.waitForTimeout(2000); await shot(page, 'TC-AP-019');
    const after = (await panel.innerText().catch(() => '')).replace(/\s+/g, ' ');
    note(`Candidate: ${card}; booked with consent -> ${res.status()} ${body.code ?? body.message ?? ''} (dossierId linked: ${body.dossierId ? 'yes' : 'see list'}); panel after: ${after}`);
    expect(withoutConsent || /đồng ý/.test(msg)).toBe(true); expect(res.status()).toBe(201);
  });
  test('TC-AP-021 no candidate without an open treatment sheet', async ({ page, request }) => {
    const p = await newPatient(await Api.as(request, 'assistant.demo')); await staffLogin(page, 'owner.demo');
    await page.goto(`/patients/${p.id}?tab=appointments`); await page.waitForTimeout(3000);
    const panel = page.getByRole('region', { name: 'Hạng mục kế tiếp cần hẹn' }); const shown = await panel.count();
    const text = shown ? (await panel.innerText()).replace(/\s+/g, ' ') : 'panel not shown'; const book = shown ? await panel.getByRole('button', { name: 'Đặt lịch' }).count() : 0;
    await shot(page, 'TC-AP-021'); note(`Patient ${p.patientCode} without a sheet: ${text}; "Đặt lịch" buttons: ${book}`); expect(book).toBe(0);
  });
});

test('TC-AA-011 Google sign-in when it is not configured', async ({ page }) => {
  await page.goto('/customer/sign-in'); await page.waitForTimeout(4000);
  const text = (await page.locator('body').innerText()).replace(/\s+/g, ' '); const iframe = await page.locator('iframe[src*="accounts.google.com"]').count();
  await shot(page, 'TC-AA-011');
  const m = text.match(/Đăng nhập Google[^.]*\.|Google[^.]*cấu hình[^.]*\./)?.[0] ?? '';
  note(`Customer sign-in: Google button frames ${iframe}; message "${m}"; page text: ${text.slice(text.indexOf('Hoặc'), text.indexOf('Hoặc') + 160)}`);
  expect(iframe === 0 ? /cấu hình|Google/.test(m + text) : true).toBe(true);
  if (iframe > 0) ann('TC-AA-011', 'Google sign-in IS configured on this server, so the "not configured" case could not be shown');
});
