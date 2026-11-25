import { test } from '@playwright/test';
import { customerLogin } from '../helpers';

test('explore my appointments', async ({ page }) => {
  await customerLogin(page, 'guest.demo');
  await page.goto('/customer/appointments'); await page.waitForTimeout(1500);
  console.log('LIST ' + (await page.locator('main').ariaSnapshot()).slice(0, 2500));
  await page.goto('/customer/appointments/AP-6B21EF00EC62438B8FE42307C542F1F7'); await page.waitForTimeout(1500);
  console.log('DETAIL ' + await page.locator('main').ariaSnapshot());
  await page.getByRole('button', { name: /Đổi lịch/ }).first().click(); await page.waitForTimeout(1500);
  console.log('RESCHEDULE ' + await page.locator('body').ariaSnapshot());
});
