import { test, expect } from '@playwright/test';
test('smoke', async ({ page }) => {
  await page.goto('/sign-in');
  await expect(page.getByRole('heading', { name: 'Đăng nhập' })).toBeVisible();
});
