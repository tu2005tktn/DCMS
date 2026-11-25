import { test } from '@playwright/test';
import { API, Api, rnd } from '../helpers';
import { checkIn, newPatient, slots, staffBook } from '../fx';
import fs from 'node:fs';
const NL = String.fromCharCode(10); const M = 'http://localhost:8082';
const snap = async (page: any, label: string, n = 60) => console.log('=== ' + label + NL + (await page.locator('body').ariaSnapshot()).split(NL).filter((l: string) => !l.includes('/url')).slice(0, n).join(NL));
test('explore mobile linked', async ({ page, request }) => {
  test.setTimeout(300000); page.setDefaultTimeout(20000); await page.setViewportSize({ width: 390, height: 844 });
  const api = await Api.as(request, 'assistant.demo'); const p = await newPatient(api); const now = Date.now(); let s: any;
  for (const d of [4, 3, 6]) { s = (await slots(api, d, '2026-11-24', 15)).find((x: any) => Date.parse(x.start) >= now - 600000); if (s) break; }
  const a = await staffBook(api, s, p); await checkIn(api, a.appointmentId, p.id, a.version);
  const n = rnd(); const phone = `09${n}55`.slice(0, 10);
  await request.post(`${API}/api/auth/register`, { data: { fullName: p.fullName, phoneNumber: phone, email: `mob.${n}@example.com`, password: 'Abc12345', confirmPassword: 'Abc12345' } });
  const me = await Api.as(request, phone, 'Abc12345');
  const req = (await me.post('/api/patient-links/me/requests', { fullName: p.fullName, birthDate: p.birthDate, patientCode: p.patientCode, matchingInformation: null, confirmSelf: true })).body.request;
  const v = await api.post(`/api/patient-link-reviews/${req.id}/verify`, { version: req.version ?? 1, patientId: p.id, verificationNote: 'Đối chiếu CCCD' }); console.log('VERIFY', v.status, JSON.stringify(v.body).slice(0, 150));
  fs.writeFileSync('../outputs/system-test-frs/results/mob_account.json', JSON.stringify({ phone, p }));
  await page.goto(M + '/sign-in'); await page.waitForTimeout(8000);
  const boxes = page.getByRole('textbox'); await boxes.nth(0).fill(phone); await boxes.nth(1).fill('Abc12345'); await page.getByRole('button', { name: 'Đăng nhập' }).click(); await page.waitForTimeout(6000);
  await snap(page, 'HOME', 70);
  for (const u of ['/my-record', '/visit-results', '/prescriptions', '/notifications', '/book-appointment']) { await page.goto(M + u); await page.waitForTimeout(4000); await snap(page, u, 45); }
});
