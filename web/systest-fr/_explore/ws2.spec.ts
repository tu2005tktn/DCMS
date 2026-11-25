import { test } from '@playwright/test';
import { staffLogin } from '../helpers';
test('ws region', async ({ page }) => {
  await staffLogin(page, 'owner.demo'); await page.goto('/working-schedules'); await page.waitForTimeout(2500);
  const lines = (await page.getByRole('region', { name: 'Chỉnh lịch làm việc' }).ariaSnapshot()).split('\n').filter(l => /strong|group|button "Thêm|Chi nhánh Thứ|Lý do ngoại|textbox/.test(l));
  console.log(lines.join('\n'));
  console.log('CLASS ' + await page.getByRole('button', { name: 'Thêm khoảng' }).first().evaluate(e => { let p = e.parentElement; const out = []; for (let i = 0; i < 5 && p; i++) { out.push(p.className); p = p.parentElement; } return out.join(' <- '); }));
});
