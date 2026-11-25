import { test } from '@playwright/test';
import { staffLogin } from '../helpers';
test('explore rp cards', async ({ page }) => {
  await staffLogin(page, 'owner.demo'); await page.goto('/finance/statistics'); await page.getByRole('heading', { level: 3 }).first().waitFor({ timeout: 60000 }).catch(e => console.log('NOH3', String(e).slice(0,200))); console.log(await page.locator('main').last().innerText().then(t => t.slice(0, 600)));
  console.log(await page.evaluate(() => Array.from(document.querySelectorAll('h3')).slice(0,7).map(h => { let p: any = h; const chain = []; for (let i = 0; i < 6 && p; i++) { chain.push(p.className); p = p.parentElement; } return h.textContent + ' <- ' + chain.join(' < '); }).join('\n')));
});
