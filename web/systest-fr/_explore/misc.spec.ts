import { test } from '@playwright/test';
import { Api, staffLogin } from '../helpers';
import { newPatient, nextSlotToday, staffBook, sheetWithWork } from '../fx';
const NL = String.fromCharCode(10);
const dump = async (label: string, loc: any, n = 50) => console.log('=== ' + label + NL + (await loc.ariaSnapshot()).split(NL).filter((l: string) => !l.includes('/url') && !l.includes('- img') && !/^\s+- (cell|row)/.test(l)).slice(0, n).join(NL));
test('explore misc', async ({ page, request }) => {
  test.setTimeout(300000); page.setDefaultTimeout(10000);
  const api = await Api.as(request, 'assistant.demo'); const p = await newPatient(api); await staffBook(api, await nextSlotToday(api), p);
  const f = await sheetWithWork(request, 32, ['36'], false);
  await staffLogin(page, 'assistant.demo'); await page.goto('/reception'); await page.waitForTimeout(2000);
  await dump('RECEPTION', page.locator('main').last(), 12);
  await page.getByRole('searchbox', { name: 'Tìm người đến khám' }).fill(p.patientCode); await page.keyboard.press('Enter'); await page.waitForTimeout(1500);
  const row = page.getByRole('row').filter({ hasText: p.fullName }).first(); await row.getByRole('link').or(row.getByRole('button')).first().click(); await page.waitForTimeout(2000);
  await dump('CHECKIN', page.locator('main').last(), 70);
  await page.goto(`/patients/${f.patient.id}?tab=appointments`); await page.waitForTimeout(2500); await dump('APPTS TAB', page.locator('main').last(), 60);
  await staffLogin(page, 'owner.demo'); await page.goto(`/patients/${f.patient.id}?tab=treatment`); await page.waitForTimeout(2500);
  await page.getByRole('button', { name: 'Thêm quá trình điều trị' }).click().catch(e => console.log('E', String(e).slice(0, 150))); await page.waitForTimeout(1200);
  await dump('ACTUAL ROW', page.locator('main').last(), 40);
});
