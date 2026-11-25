import { test } from '@playwright/test';
import { staffLogin } from '../helpers';
import { sheetWithWork } from '../fx';
const NL = String.fromCharCode(10);
const dump = async (label: string, loc: any, n = 60) => console.log('=== ' + label + NL + (await loc.ariaSnapshot()).split(NL).filter((l: string) => !l.includes('/url') && !l.includes('- img')).slice(0, n).join(NL));
test('explore next visit', async ({ page, request }) => {
  test.setTimeout(200000); page.setDefaultTimeout(15000);
  const f = await sheetWithWork(request, 32, ['36'], false);
  await staffLogin(page, 'assistant.demo'); await page.goto(`/patients/${f.patient.id}?tab=appointments`);
  await page.getByRole('tab', { name: 'Lịch hẹn' }).waitFor(); await page.waitForTimeout(3000);
  await dump('APPTS TAB', page.getByRole('tabpanel').last(), 60);
  const b = page.getByRole('button', { name: /tái khám|Đặt lịch/ }); console.log('BTNS', JSON.stringify(await b.allInnerTexts()));
  await page.goto('/customer/sign-in'); await page.waitForTimeout(1500); console.log('GOOGLE', await page.getByText(/Google/).count(), JSON.stringify(await page.getByRole('button').allInnerTexts()));
});
