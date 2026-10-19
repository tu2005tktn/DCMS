import { expect, test } from '@playwright/test';

const dossierId = '11111111-1111-4111-8111-111111111111';
const itemId = '22222222-2222-4222-8222-222222222222';
const dossier = {
  id: dossierId, code: 'HS-SYNTHETIC', patientId: 123, branchId: 1, dentistId: 2,
  startedOn: '2026-10-19', diagnosis: 'SYNTHETIC diagnosis', note: null,
  state: 'Open', closureOutcome: null, closureReason: null, version: 2,
  planId: '33333333-3333-4333-8333-333333333333', planVersion: 0,
  planState: 'Draft', goals: 'Mục tiêu cũ', paymentTerms: [],
  financialSummaryAvailable: false, createdAt: '2026-10-19T00:00:00Z', updatedAt: '2026-10-19T00:00:00Z',
  timeline: [{ id: '44444444-4444-4444-8444-444444444444', kind: 'DossierCreated',
    dossierVersion: 1, actorId: 9, occurredAt: '2026-10-19T00:00:00Z', payloadJson: '{}' }],
  items: [{ id: itemId, sourceDiagnosisId: null, serviceId: 3, serviceName: 'Trám răng', region: '25',
    unit: 'răng', quantity: 1, plannedOrder: 1, estimatedMinutes: 30, estimatedStart: null,
    estimatedEnd: null, agreedUnitPriceVnd: 300000, decision: 'Agreed', progress: 'NotStarted',
    counseling: 'Đã tư vấn', decisionMaker: 'Bệnh nhân', representativeBasis: null,
    decidedAt: '2026-10-19T00:00:00Z', consentDocumentId: null, consentPlanVersion: null,
    warrantyPolicy: null, warrantyPolicyVersion: null, warrantyScope: null,
    warrantyConfirmedAt: null, warrantyEndsOn: null }],
};

test('S43 opens S21 and a version conflict keeps the edited plan in the form', async ({ page }) => {
  const user = {
    id: 9, userName: 'synthetic.dentist', fullName: 'SYNTHETIC Dentist', accountType: 'Staff',
    roles: ['Dentist'], effectiveRoles: ['Dentist'], permissions: ['patients.read'], hasVerifiedPatientLink: false,
  };
  await page.route('**/api/auth/me', route => route.fulfill({ json: user }));
  await page.route('**/api/auth/sign-in', route => route.fulfill({ json: { accessToken: 'synthetic-token', user } }));
  await page.route(`**/api/dossiers/${dossierId}`, route => route.fulfill({ json: dossier }));
  await page.route('**/api/appointments/options', route => route.fulfill({ json: {
    branches: [{ id: 1, name: 'Chi nhánh thử nghiệm' }],
    dentists: [{ id: 2, branchId: 1, name: 'Bác sĩ thử nghiệm' }],
    services: [{ id: 3, branchId: 1, name: 'Trám răng' }],
  } }));
  await page.route(`**/api/dossiers/${dossierId}/plan/draft`, route => route.fulfill({
    status: 409, json: { code: 'MSG111', message: 'Phiên bản đã thay đổi.' },
  }));
  await page.goto('http://127.0.0.1:5173/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill('synthetic.dentist');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('synthetic');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Tài khoản của tôi' })).toBeVisible();
  await page.evaluate(path => { history.pushState(null, '', path); window.dispatchEvent(new PopStateEvent('popstate')); },
    `/patients/123/dossiers/${dossierId}`);
  await expect(page.getByRole('heading', { name: 'Đợt điều trị HS-SYNTHETIC' })).toBeVisible();
  await expect(page.getByText('Chưa có dữ liệu tài chính từ nguồn đối soát')).toBeVisible();
  await page.getByRole('link', { name: /Mở kế hoạch S21/ }).click();
  await expect(page.getByRole('heading', { name: 'Kế hoạch điều trị' })).toBeVisible();
  await expect(page.getByText('Trám răng')).toBeVisible();
  await page.getByRole('button', { name: 'Thêm hạng mục' }).click();
  const scope = page.getByRole('dialog');
  await expect(scope.locator('.ant-select-selection-item').filter({ hasText: /^1$/ })).toBeVisible();
  await scope.getByRole('combobox', { name: /Dịch vụ/ }).click();
  await page.locator('.ant-select-item-option').filter({ hasText: 'Trám răng' }).click();
  await scope.getByRole('combobox', { name: 'Răng hoặc vùng điều trị' }).fill('25');
  await page.locator('.ant-select-item-option').filter({ hasText: 'Răng 25' }).click();
  await expect(scope.locator('.ant-select-selection-item').filter({ hasText: /^răng$/ })).toBeVisible();
  await scope.locator('#quantityChoice').locator('xpath=../..').click();
  await page.locator('.ant-select-item-option').filter({ hasText: /^2$/ }).click();
  await scope.getByRole('spinbutton', { name: /Giá thỏa thuận mỗi đơn vị/ }).fill('300000');
  await expect(scope.getByText('600.000 ₫')).toBeVisible();
  await scope.getByRole('button', { name: 'Lưu hạng mục' }).click();
  await expect(page.getByText('2 răng × 300.000 ₫')).toBeVisible();
  await page.getByRole('button', { name: 'Ghi quyết định' }).last().click();
  const decision = page.getByRole('dialog');
  await decision.locator('#decision').locator('xpath=../..').click();
  await page.locator('.ant-select-item-option').filter({ hasText: 'Hoãn quyết định' }).click();
  await decision.getByLabel('Tư vấn: phương án, lợi ích/rủi ro, vật liệu và số buổi').fill('Đã tư vấn, bệnh nhân muốn cân nhắc.');
  await decision.getByLabel('Người đưa ra quyết định').fill('Bệnh nhân');
  await decision.getByRole('button', { name: 'Lưu quyết định' }).click();
  await expect(page.locator('.ant-tag').filter({ hasText: 'Hoãn quyết định' })).toBeVisible();
  await page.getByRole('button', { name: 'Đưa hạng mục 2 lên' }).click();
  await expect(page.getByText('2 răng × 300.000 ₫')).toBeVisible();
  await page.getByLabel('Mục tiêu và phương án').fill('Mục tiêu mới giữ lại khi xung đột');
  await page.getByRole('button', { name: 'Lưu nháp kế hoạch' }).click();
  await expect(page.getByText('Phiên bản đã thay đổi.')).toBeVisible();
  await expect(page.getByLabel('Mục tiêu và phương án')).toHaveValue('Mục tiêu mới giữ lại khi xung đột');
});
