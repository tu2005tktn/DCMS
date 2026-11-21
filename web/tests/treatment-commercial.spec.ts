import { expect, test } from '@playwright/test';

test('dentist opens reference pricing and updates one service price', async ({ page }) => {
  const user = { id: 5, userName: 'synthetic.dentist', fullName: 'SYNTHETIC Dentist',
    accountType: 'Staff', roles: ['Dentist'], effectiveRoles: ['Dentist'],
    permissions: ['patients.read', 'treatment.prices.read', 'treatment.prices.manage'], hasVerifiedPatientLink: false };
  let referenceUnitPriceVnd = 1000000;
  let priceVersion = 2;
  let submitted: { expectedVersion: number; referenceUnitPriceVnd: number; unit?: string; requestKey: string } | null = null;
  await page.route('**/api/auth/me', route => route.fulfill({ json: user }));
  await page.route('**/api/auth/sign-in', route => route.fulfill({ json: { accessToken: 'synthetic-token', user } }));
  await page.route('**/api/service-prices', route => route.fulfill({ json: [
    { serviceId: 3, branchId: 1, name: 'Chụp sứ', unit: 'răng', referenceUnitPriceVnd, priceVersion },
  ] }));
  await page.route('**/api/service-prices/3', route => {
    submitted = route.request().postDataJSON();
    referenceUnitPriceVnd = submitted?.referenceUnitPriceVnd ?? referenceUnitPriceVnd;
    priceVersion++;
    return route.fulfill({ json: { serviceId: 3, branchId: 1, name: 'Chụp sứ', unit: submitted?.unit ?? 'răng', referenceUnitPriceVnd, priceVersion } });
  });

  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill('synthetic.dentist');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('synthetic');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await page.getByRole('link', { name: 'Giá và duyệt điều trị' }).click();
  await expect(page.getByRole('heading', { name: 'Giá tham chiếu điều trị' })).toBeVisible();
  await expect(page.getByText('Chụp sứ')).toBeVisible();
  await page.getByRole('button', { name: 'Cập nhật giá', exact: true }).click();
  await page.getByLabel('Giá tham chiếu mỗi đơn vị (VND)', { exact: true }).fill('1200000');
  await page.getByRole('button', { name: 'Lưu giá', exact: true }).click();
  await expect(page.getByText('1.200.000 ₫')).toBeVisible();
  expect(submitted).toMatchObject({ expectedVersion: 2, referenceUnitPriceVnd: 1200000 });
});
