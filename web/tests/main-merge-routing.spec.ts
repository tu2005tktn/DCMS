import { expect, test, type Page } from '@playwright/test';

const prescription = {
  id: '11111111-1111-4111-8111-111111111111', branchId: 1,
  branchName: 'SYNTHETIC Clinic', prescribedOn: '2026-11-10',
  diagnosis: 'SYNTHETIC diagnosis', prescriberName: 'SYNTHETIC Dentist',
  prescriberEmployeeId: 7, version: 1, patientCode: 'BN-SYNTHETIC',
  patientName: 'SYNTHETIC Patient', advice: 'SYNTHETIC advice',
  items: [{ medicineId: '22222222-2222-4222-8222-222222222222',
    medicineName: 'SYNTHETIC medicine', quantity: 2, unit: 'viên' }],
};

async function session(page: Page, accountType: 'Public' | 'Staff') {
  await page.addInitScript(type => {
    sessionStorage.setItem(type === 'Public' ? 'dcms.customer.accessToken' : 'dcms.web.access-token', 'synthetic-token');
  }, accountType);
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/auth/me') return route.fulfill({ json: {
      id: 7, userName: 'synthetic', fullName: 'SYNTHETIC User', accountType,
      roles: [], effectiveRoles: [], permissions: [], hasVerifiedPatientLink: true,
    } });
    if (path === '/api/auth/session/idle') return route.fulfill({ json: {
      serverNow: new Date().toISOString(), deadlineAt: new Date(Date.now() + 1200000).toISOString(), activityVersion: 1,
    } });
    if (path === '/api/patients/me/prescriptions') return route.fulfill({ json: [prescription] });
    if (path === `/api/patients/me/prescriptions/${prescription.id}`) return route.fulfill({ json: prescription });
    return route.fulfill({ status: 404, json: { code: 'MSG113', message: 'Synthetic endpoint not configured' } });
  });
}

test('customer prescription list opens detail and survives direct reload', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await session(page, 'Public');
  await page.goto('/customer/prescriptions');
  await expect(page.getByRole('heading', { name: 'Đơn thuốc của tôi', exact: true })).toBeVisible();
  await page.locator('.customer-rx-card').click();
  await expect(page).toHaveURL(new RegExp(`/customer/prescriptions/${prescription.id}$`));
  await expect(page.getByRole('heading', { name: 'ĐƠN THUỐC', exact: true })).toBeVisible();
  await expect(page.getByText(prescription.patientName, { exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByText(prescription.items[0].medicineName, { exact: true })).toBeVisible();
  expect(errors).toEqual([]);
});

test('legacy commercial URL keeps query and destination permission guard', async ({ page }) => {
  await session(page, 'Staff');
  await page.goto('/commercial/treatment?dossier=SYNTHETIC&branchId=1');
  await expect(page).toHaveURL(/\/settings\/treatment-services\?dossier=SYNTHETIC&branchId=1$/);
  await expect(page.getByText('Bạn không có quyền thực hiện thao tác này.', { exact: true })).toBeVisible();
});

test('customer prescription direct URL requires customer authentication', async ({ page }) => {
  await page.goto(`/customer/prescriptions/${prescription.id}`);
  await expect(page).toHaveURL(/\/customer\/sign-in$/);
});
