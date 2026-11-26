import { expect, type Page } from '@playwright/test';

export const password = process.env.E2E_DEMO_PASSWORD ?? '';

export async function staffLogin(page: Page, name: string, pass = password) {
  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill(name);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(pass);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Tài khoản của tôi' })).toBeVisible();
}

export async function customerLogin(page: Page, identifier: string, pass = password) {
  await page.goto('/customer/sign-in');
  await page.getByLabel('Email hoặc tên đăng nhập').fill(identifier);
  await page.getByLabel('Mật khẩu').fill(pass);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
}

export const suffix = () => Date.now().toString(36) + Math.random().toString(36).slice(2, 5);
export const phone = () => '09' + Math.floor(10000000 + Math.random() * 89999999);
