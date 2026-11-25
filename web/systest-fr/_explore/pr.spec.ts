import { test } from '@playwright/test';
import { API, customerLogin, rnd, staffLogin } from '../helpers';
const NL = String.fromCharCode(10);
const dump = async (label: string, loc: any, n = 50) => console.log('=== ' + label + NL + (await loc.ariaSnapshot()).split(NL).filter((l: string) => !l.includes('/url') && !l.includes('- img')).slice(0, n).join(NL));
test('explore portal', async ({ page }) => {
  test.setTimeout(300000);
  const n = rnd(); const phone = `09${n}12`.slice(0, 10);
  await page.request.post(`${API}/api/auth/register`, { data: { fullName: 'Khách PR ' + n, phoneNumber: phone, email: `pr.${n}@example.com`, password: 'Abc12345', confirmPassword: 'Abc12345' } });
  await customerLogin(page, phone, 'Abc12345');
  await dump('HOME', page.locator('body'), 40);
  for (const u of ['patient-link', 'family-members', 'treatment-records', 'prescriptions']) { await page.goto('/customer/' + u); await page.waitForTimeout(2000); await dump(u, page.locator('main').last(), 45); }
  await page.goto('/customer/family-members'); await page.waitForTimeout(1500); await page.getByRole('button', { name: /Thêm người thân/ }).first().click(); await page.waitForTimeout(1000);
  await dump('FAMILY ADD', page.locator('.ant-modal-wrap:visible .ant-modal, .ant-drawer-open .ant-drawer-content').last(), 40);
  await staffLogin(page, 'assistant.demo'); await page.goto('/link-verification'); await page.waitForTimeout(2000); await dump('LINK VERIFY', page.locator('main').last(), 60);
});
