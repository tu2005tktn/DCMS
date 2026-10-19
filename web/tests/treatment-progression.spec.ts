import { expect, test } from '@playwright/test';

test('dentist sees the active S58 template and can map its step to an agreed S21 item', async ({ page }) => {
  const dossierId = '11111111-1111-4111-8111-111111111111';
  const itemId = '22222222-2222-4222-8222-222222222222';
  const templateId = '33333333-3333-4333-8333-333333333333';
  const stepId = '44444444-4444-4444-8444-444444444444';
  const user = { id: 9, userName: 'synthetic.dentist', fullName: 'SYNTHETIC Dentist',
    accountType: 'Staff', roles: ['Dentist'], effectiveRoles: ['Dentist'], permissions: ['patients.read'] };
  const step = { logicalStepId: stepId, itemId: null, name: 'Trám răng 25', region: '25',
    estimatedMinutes: 20, reservedMinutes: 30, predecessorIds: [], capability: null,
    labRequirement: null, earliestOffsetDays: 1, targetOffsetDays: 3, latestOffsetDays: 5,
    chargingCoverage: 'IncludedInItem' };
  const template = { id: templateId, branchId: 1, name: 'Điều trị nhiều buổi', version: 2,
    versions: [{ number: 1, state: 'Active', steps: [step], createdAt: '2026-10-19T00:00:00Z',
      activatedAt: '2026-10-19T00:00:00Z', retiredAt: null }] };
  const dossier = { id: dossierId, code: 'HS-SYNTHETIC', patientId: 123, branchId: 1,
    dentistId: 2, startedOn: '2026-10-19', diagnosis: 'SYNTHETIC diagnosis', note: null,
    state: 'Open', closureOutcome: null, closureReason: null, version: 2, planId: crypto.randomUUID(),
    planVersion: 0, planState: 'Draft', goals: 'Điều trị', paymentTerms: [], timeline: [],
    financialSummaryAvailable: false, createdAt: '2026-10-19T00:00:00Z', updatedAt: '2026-10-19T00:00:00Z',
    items: [{ id: itemId, sourceDiagnosisId: null, serviceId: 3, serviceName: 'Trám răng',
      region: '25', unit: 'răng', quantity: 1, plannedOrder: 1, estimatedMinutes: 30,
      estimatedStart: null, estimatedEnd: null, agreedUnitPriceVnd: 300000, decision: 'Agreed',
      progress: 'NotStarted', counseling: 'Đã tư vấn', decisionMaker: 'Bệnh nhân',
      representativeBasis: null, decidedAt: '2026-10-19T00:00:00Z', consentDocumentId: null,
      consentPlanVersion: null, warrantyPolicy: null, warrantyPolicyVersion: null,
      warrantyScope: null, warrantyConfirmedAt: null, warrantyEndsOn: null }],
  };
  await page.route('**/api/auth/me', route => route.fulfill({ json: user }));
  await page.route('**/api/auth/sign-in', route => route.fulfill({ json: { accessToken: 'synthetic-token', user } }));
  await page.route('**/api/appointments/options', route => route.fulfill({ json: {
    branches: [{ id: 1, name: 'Chi nhánh thử nghiệm' }],
    dentists: [{ id: 2, branchId: 1, name: 'Bác sĩ thử nghiệm' }],
    services: [{ id: 3, branchId: 1, name: 'Trám răng' }],
  } }));
  await page.route('**/api/treatment-templates?branchId=1', route => route.fulfill({ json: [
    { id: templateId, branchId: 1, name: template.name, version: 2, activeVersion: 1,
      createdAt: '2026-10-19T00:00:00Z' },
  ] }));
  await page.route(`**/api/treatment-templates/${templateId}`, route => route.fulfill({ json: template }));
  await page.route(`**/api/dossiers/${dossierId}`, route => route.fulfill({ json: dossier }));
  await page.route(`**/api/dossiers/${dossierId}/steps`, route => route.fulfill({ json: [] }));
  await page.goto('http://127.0.0.1:5173/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill('synthetic.dentist');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('synthetic');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await page.getByRole('link', { name: 'Mẫu điều trị' }).click();
  await expect(page.getByRole('heading', { name: 'Mẫu điều trị S58' })).toBeVisible();
  await page.getByRole('button', { name: 'Mở', exact: true }).click();
  await expect(page.getByText('Trám răng 25')).toBeVisible();
  await page.evaluate(path => { history.pushState(null, '', path); window.dispatchEvent(new PopStateEvent('popstate')); },
    `/patients/123/dossiers/${dossierId}/plan`);
  await page.getByRole('tab', { name: 'Các bước' }).click();
  await expect(page.getByText('Áp dụng mẫu vào hồ sơ này')).toBeVisible();
  await page.getByRole('combobox', { name: 'Chọn mẫu đang dùng' }).click();
  await page.locator('.ant-select-item-option').filter({ hasText: 'Điều trị nhiều buổi' }).click();
  await expect(page.getByText('Nguồn: Điều trị nhiều buổi, phiên bản 1.')).toBeVisible();
  await expect(page.locator('.ant-select-selection-item').filter({ hasText: 'Trám răng · 25' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Sao chép vào kế hoạch' })).toBeEnabled();
});
