import { expect, test } from '@playwright/test';

const dossierId = '11111111-1111-4111-8111-111111111111';
const itemId = '22222222-2222-4222-8222-222222222222';

test('dentist opens commercial review and approves one item discount', async ({ page }) => {
  const user = { id: 5, userName: 'synthetic.dentist', fullName: 'SYNTHETIC Dentist',
    accountType: 'Staff', roles: ['Dentist'], effectiveRoles: ['Dentist'],
    permissions: ['patients.read'], hasVerifiedPatientLink: false };
  let discountStatus = 'Proposed';
  let submitted: { expectedVersion: number; decision: string; requestKey: string } | null = null;
  await page.route('**/api/auth/me', route => route.fulfill({ json: user }));
  await page.route('**/api/auth/sign-in', route => route.fulfill({ json: {
    accessToken: 'synthetic-token', user,
  } }));
  await page.route('**/api/commercial/dossiers', route => route.fulfill({ json: [
    { dossierId, code: 'HS-SYNTHETIC', branchId: 1, version: 3,
      pendingDiscounts: discountStatus === 'Proposed' ? 1 : 0 },
  ] }));
  await page.route('**/api/service-prices', route => route.fulfill({ json: [
    { serviceId: 3, branchId: 1, name: 'Chụp sứ', referenceUnitPriceVnd: 1000000,
      priceVersion: 2 },
  ] }));
  await page.route(`**/api/commercial/dossiers/${dossierId}/plan`, route => route.fulfill({ json: {
    dossierId, code: 'HS-SYNTHETIC', branchId: 1, version: discountStatus === 'Proposed' ? 3 : 4,
    planState: 'Draft', depositExceptionReason: null,
    items: [{ id: itemId, serviceName: 'Chụp sứ', region: '25', agreedUnitPriceVnd: 1000000,
      quantity: 1, discount: { type: 'Fixed', value: 300000, reason: 'Ưu đãi theo ca',
        qualifyingItemId: null, status: discountStatus, amountVnd: discountStatus === 'Approved' ? 300000 : 0 } }],
    preview: { grossVnd: 1000000, approvedDiscountVnd: discountStatus === 'Approved' ? 300000 : 0,
      netBeforeTaxVnd: discountStatus === 'Approved' ? 700000 : 1000000 },
  } }));
  await page.route(`**/api/commercial/dossiers/${dossierId}/items/${itemId}/discount/decision`, route => {
    submitted = route.request().postDataJSON();
    discountStatus = 'Approved';
    return route.fulfill({ json: { notification: { code: 'MSG222', message: 'Đã duyệt giảm trừ' } } });
  });

  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill('synthetic.dentist');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('synthetic');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await page.getByRole('link', { name: 'Giá và duyệt điều trị' }).click();
  await expect(page.getByRole('heading', { name: 'Duyệt thương mại điều trị' })).toBeVisible();
  await page.getByRole('button', { name: 'Xem' }).click();
  await expect(page.getByText('Ưu đãi theo ca')).toBeVisible();
  await page.getByRole('button', { name: 'Duyệt', exact: true }).click();
  await expect(page.getByText('Dự kiến trước thuế: 700.000 ₫')).toBeVisible();
  expect(submitted).toMatchObject({ expectedVersion: 3, decision: 'Approved' });
});
