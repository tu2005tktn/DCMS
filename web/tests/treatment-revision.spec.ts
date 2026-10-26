import { expect, test } from '@playwright/test';

const dossierId = '11111111-1111-4111-8111-111111111111';
const itemId = '22222222-2222-4222-8222-222222222222';
const path = `/patients/12/dossiers/${dossierId}/plan/revise/${itemId}`;
const base = `/api/dossiers/${dossierId}/plan`;
const user = { id: 9, userName: 'synthetic.dentist', fullName: 'SYNTHETIC Dentist',
  accountType: 'Staff', roles: ['Dentist'], effectiveRoles: ['Dentist'], permissions: ['patients.read'] };
const item = { id: itemId, serviceId: 3, serviceName: 'Trám răng', material: null,
  region: '25', unit: 'răng', quantity: 1, estimatedMinutes: 30, agreedUnitPriceVnd: 300000,
  decision: 'Agreed', progress: 'NotStarted', decisionMaker: 'Bệnh nhân',
  consentDocumentId: null, consentPlanVersion: null };
const dossier = { id: dossierId, patientId: 12, branchId: 1, code: 'HS-SYNTHETIC',
  state: 'Open', planState: 'Committed', version: 4, planVersion: 1, items: [item] };

test('S62 keeps a server draft through reload and shows unavailable finance as a blocker', async ({ page }) => {
  let draft: Record<string, unknown> | null = null;
  let saves = 0;
  await page.route('**/api/auth/me', route => route.fulfill({ json: user }));
  await page.route('**/api/auth/sign-in', route => route.fulfill({ json: { accessToken: 'synthetic', user } }));
  await page.route(`**${base}/revisions/context?itemId=${itemId}`, route => route.fulfill({ json: {
    dossier, item, steps: [], instructions: [], bookings: [],
    services: [{ id: 3, name: 'Trám răng' }],
  } }));
  await page.route(`**${base}/revision-drafts/${itemId}`, async route => {
    if (route.request().method() === 'GET') {
      await route.fulfill(draft ? { json: draft } : { status: 404, json: { code: 'MSG113' } });
      return;
    }
    if (route.request().method() === 'PUT') {
      saves++;
      draft = { ...route.request().postDataJSON(), dossierId, itemId, actorId: 9,
        version: saves, updatedAt: '2026-10-25T00:00:00Z' };
      await route.fulfill({ json: draft });
      return;
    }
    await route.fulfill({ status: 204 });
  });
  await page.route(`**${base}/revisions/preview`, route => route.fulfill({ json: {
    dossierId, itemId, draftVersion: saves, dossierVersion: 4, planVersion: 1,
    current: item, proposed: (draft as Record<string, unknown>)?.proposed,
    steps: [], instructions: [], bookings: [],
    actual: { available: true, versionToken: 'not-required', reason: null },
    finance: { available: false, versionToken: null, reason: 'Nguồn tài chính chưa kết nối.' },
    proposedDifferenceVnd: 100000, canSave: false, blockers: ['MSG245'],
  } }));
  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill('synthetic.dentist');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('synthetic');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await page.goto(path);
  await expect(page.getByRole('heading', { name: 'Điều chỉnh kế hoạch điều trị' })).toBeVisible();
  await page.getByLabel('Lý do điều chỉnh (1–1000 ký tự)').fill('Thay đổi phương án theo tư vấn');
  await page.getByRole('button', { name: 'Lưu bản nháp' }).click();
  await expect(page.getByText('Bản nháp đã lưu · phiên bản 1')).toBeVisible();
  await page.reload();
  await expect(page.getByLabel('Lý do điều chỉnh (1–1000 ký tự)'))
    .toHaveValue('Thay đổi phương án theo tư vấn');
  await page.getByRole('button', { name: 'Tiếp tục tư vấn' }).click();
  await page.getByLabel('Phương án, lợi ích/rủi ro, thời gian và điều khoản')
    .fill('Đã giải thích lợi ích, rủi ro và chi phí.');
  await page.getByLabel('Người đưa ra quyết định').fill('Bệnh nhân');
  await page.getByRole('button', { name: 'Xem ảnh hưởng' }).click();
  await expect(page.getByText('Cần đối chiếu tài chính ở màn hình riêng')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Lưu phiên bản mới' })).toBeDisabled();
});

test('S62 rejects direct access without dentist permission', async ({ page }) => {
  await page.route('**/api/auth/me', route => route.fulfill({ json: user }));
  await page.route('**/api/auth/sign-in', route => route.fulfill({ json: { accessToken: 'synthetic', user } }));
  await page.route(`**${base}/revisions/context?itemId=${itemId}`,
    route => route.fulfill({ status: 403, json: { code: 'MSG06', message: 'Không có quyền.' } }));
  await page.route(`**${base}/revision-drafts/${itemId}`,
    route => route.fulfill({ status: 404, json: { code: 'MSG113' } }));
  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill('synthetic.dentist');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('synthetic');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await page.goto(path);
  await expect(page.getByText('Không có quyền.')).toBeVisible();
});
