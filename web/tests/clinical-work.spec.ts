import { expect, test } from '@playwright/test';

test('saving S63 review does not post an S22 activity', async ({ page }) => {
  const visitId = '11111111-1111-4111-8111-111111111111';
  const activityId = '22222222-2222-4222-8222-222222222222';
  const reviewId = '33333333-3333-4333-8333-333333333333';
  const user = { id: 9, userName: 'synthetic.dentist', fullName: 'SYNTHETIC Dentist',
    accountType: 'Staff', roles: ['Dentist'], effectiveRoles: ['Dentist'],
    permissions: ['patients.read'], hasVerifiedPatientLink: false };
  const activity = { id: activityId, visitId: '44444444-4444-4444-8444-444444444444',
    patientId: 123, branchId: 1, dossierId: null, itemId: null, stepId: null,
    planVersion: null, serviceId: 3, region: '25', unit: 'răng', quantity: 1,
    performedAt: '2026-10-20T07:00:00Z', elapsedMinutes: 30, outcome: 'Đã trám',
    nextSteps: null, chargingBasis: 'Included', chargingBasisEvidence: null,
    priceSnapshotVnd: 300000, reviewId: null, state: 'Posted', version: 2,
    postedAt: '2026-10-20T07:30:00Z' };
  let activityPosts = 0;
  let savedReview = false;
  await page.route('**/api/auth/me', route => route.fulfill({ json: user }));
  await page.route('**/api/auth/sign-in', route => route.fulfill({ json: { accessToken: 'synthetic-token', user } }));
  await page.route(`**/api/visits/${visitId}/treatment-context`, route => route.fulfill({ json: {
    visit: { available: true, inProgress: true, patientId: 123, branchId: 1, version: 1 },
    steps: [], activities: [activity],
  } }));
  await page.route(`**/api/visits/${visitId}/reviews`, async route => {
    if (route.request().method() === 'GET') return route.fulfill({ json: [] });
    savedReview = true;
    const input = route.request().postDataJSON();
    return route.fulfill({ status: 201, json: { ...input, id: reviewId,
      patientId: 123, branchId: 1, dentistAccountId: 9, recordedAt: '2026-10-25T10:00:00Z', version: 1 } });
  });
  await page.route('**/api/activities/*/post', route => { activityPosts++; return route.abort(); });
  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill('synthetic.dentist');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('synthetic');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await page.goto(`/patients/123/visits/${visitId}/review`);
  await expect(page.getByRole('heading', { name: 'Đánh giá sau điều trị' })).toBeVisible();
  await page.getByRole('combobox', { name: 'Hoạt động đã hoàn tất' }).click();
  await page.locator('.ant-select-item-option').filter({ hasText: 'Dịch vụ #3' }).click();
  await page.getByLabel('Người bệnh phản ánh').fill('Ê buốt');
  await page.getByLabel('Bác sĩ đánh giá').fill('Cần theo dõi');
  await page.getByRole('button', { name: 'Lưu đánh giá' }).click();
  await expect(page.getByText('Đã lưu đánh giá. Chưa ghi hoạt động điều trị hoặc khoản thu.')).toBeVisible();
  expect(savedReview).toBe(true);
  expect(activityPosts).toBe(0);
});
