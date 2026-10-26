import { expect, test, type Page } from '@playwright/test';

const firstId = '11111111-1111-4111-8111-111111111111';
const secondId = '22222222-2222-4222-8222-222222222222';
const visitId = '33333333-3333-4333-8333-333333333333';

const user = { id: 7, userName: 'synthetic.dentist', fullName: 'Bác sĩ thử nghiệm',
  accountType: 'Staff', roles: ['Dentist'], effectiveRoles: ['Dentist'],
  permissions: ['patients.read'], hasVerifiedPatientLink: false };
const patient = { id: 123, patientCode: 'BN-SYNTHETIC', fullName: 'Bệnh nhân thử nghiệm',
  birthDate: '1990-05-06', gender: 'FEMALE', phone: null, address: null,
  guardianName: null, guardianRelationship: null, guardianPhone: null, note: null,
  isArchived: false, archivedAt: null, archiveReason: null, version: 1,
  createdAt: '2026-10-18T08:00:00Z', updatedAt: '2026-10-18T08:00:00Z',
  link: { verifiedAccountUserName: null, verifiedAt: null } };
const item = { id: '44444444-4444-4444-8444-444444444444', serviceId: 3,
  serviceName: 'Trám răng 25', region: '25', unit: 'răng', quantity: 1,
  material: null, estimatedStart: null, agreedUnitPriceVnd: 300000,
  decision: 'Agreed', discount: { status: 'Approved', amountVnd: 50000 } };
const detail = (id: string) => ({ id, code: id === firstId ? 'DT-001' : 'DT-002',
  patientId: 123, branchId: 1, dentistId: 7, diagnosis: 'Điều trị răng',
  note: 'Ghi chú ban đầu', state: 'Open', version: 1, planId: firstId, planVersion: 1,
  planState: 'Committed', items: [item], paymentTerms: [], timeline: [],
  displayCompleted: false,
  createdAt: '2026-10-18T08:00:00Z', updatedAt: '2026-10-18T08:00:00Z',
  financialSummary: { available: false, plannedVnd: 250000,
    postedChargeVnd: null, receivedVnd: null, allocatedVnd: null, outstandingVnd: null } });

async function prepare(page: Page) {
  const sheetDrafts = new Map<string, { id: string; kind: string; visitId: string | null;
    row: Record<string, unknown>; version: number }>();
  await page.route('**/api/dossiers/*/sheet-drafts**', route => {
    const url = new URL(route.request().url());
    const rowId = url.pathname.split('/').at(-1) ?? '';
    if (route.request().method() === 'GET') return route.fulfill({ json: [...sheetDrafts.values()] });
    if (route.request().method() === 'DELETE') {
      sheetDrafts.delete(rowId); return route.fulfill({ status: 204 });
    }
    const input = route.request().postDataJSON() as { kind: string; visitId: string | null;
      row: Record<string, unknown>; expectedVersion: number };
    const version = (sheetDrafts.get(rowId)?.version ?? 0) + 1;
    const saved = { id: rowId, kind: input.kind, visitId: input.visitId, row: input.row, version };
    sheetDrafts.set(rowId, saved);
    return route.fulfill({ json: saved });
  });
  await page.route('**/api/auth/me', route => route.fulfill({ json: user }));
  await page.route('**/api/auth/sign-in', route => route.fulfill({ json: { accessToken: 'synthetic-token', user } }));
  await page.route('**/api/patients/123', route => route.fulfill({ json: patient }));
  await page.route('**/api/patients/123/dossiers?**', route => route.fulfill({ json: {
    items: [firstId, secondId].map((id, index) => ({ id, code: `DT-00${index + 1}`,
      patientId: 123, branchId: 1, dentistId: 7, diagnosis: 'Điều trị răng',
      state: 'Open', displayCompleted: false, version: 1, planVersion: 1,
      updatedAt: '2026-10-18T08:00:00Z' })),
    total: 2, page: 1, pageSize: 20,
  } }));
  await page.route('**/api/appointments/options', route => route.fulfill({ json: {
    branches: [{ id: 1, name: 'Chi nhánh thử nghiệm' }],
    dentists: [{ id: 7, branchId: 1, name: 'Bác sĩ thử nghiệm' }],
    services: [{ id: 3, branchId: 1, name: 'Trám răng 25' }],
  } }));
  await page.route(`**/api/dossiers/${firstId}`, route => route.fulfill({ json: detail(firstId) }));
  await page.route(`**/api/dossiers/${secondId}`, route => route.fulfill({ json: detail(secondId) }));
  await page.route(`**/api/visits/${visitId}/treatment-context`, route => route.fulfill({ json: {
    visit: { patientId: 123 }, activities: [{ id: 'activity-1', dossierId: firstId,
      itemId: item.id, serviceId: 3, region: '25', quantity: 1,
      performedAt: '2026-10-19T07:00:00Z', outcome: 'Đã trám', state: 'Posted',
      priceSnapshotVnd: 300000 }],
  } }));
  await page.goto('http://127.0.0.1:5173/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill('synthetic.dentist');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('synthetic');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Tài khoản của tôi' })).toBeVisible();
}

test('phiếu điều trị mở rộng dùng giá kế hoạch và không giả số đã trả', async ({ page }) => {
  await prepare(page);
  await page.setViewportSize({ width: 1913, height: 908 });
  await page.goto('http://127.0.0.1:5173/patients/123');
  await page.getByRole('tab', { name: 'Điều trị & Thanh toán' }).click();
  await expect(page.getByRole('heading', { name: 'DS PHIẾU ĐIỀU TRỊ/DỊCH VỤ' })).toBeVisible();
  await expect(page.getByRole('tab')).toHaveCount(5);
  await expect(page.getByText('Trám răng 25')).toBeVisible();
  await expect(page.getByLabel('Trạng thái phiếu: Chưa xong').first()).toBeVisible();
  await page.getByRole('button', { name: 'Trạng thái phiếu: Chưa xong' }).first().click();
  await expect(page.getByRole('menuitem', { name: 'Chưa xong' })).toBeVisible();
  await expect(page.getByRole('menuitem', { name: 'Hoàn tất' })).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByText('−50.000 ₫')).toBeVisible();
  await expect(page.getByText('Chưa có dữ liệu đối soát').first()).toBeVisible();
  await expect(page.getByRole('button', { name: /TRẢ TIỀN/ })).toBeDisabled();
  await expect(page.getByText('Việc đã ghi trong lượt khám hiện tại')).toHaveCount(0);
  await expect(page.getByText('Cộng 10% VAT')).toHaveCount(0);
  await expect(page.getByText('Chưa có chính sách thuế để tính trên phiếu')).toHaveCount(0);
  await expect(page.getByText('Chưa có nguồn tổng hợp việc đã làm của toàn đợt.')).toBeVisible();
  await expect(page.getByText('Các chẩn đoán')).toHaveCount(0);
  await page.getByRole('button', { name: 'Chức năng Trám răng 25' }).click();
  await expect(page.getByRole('menuitem')).toHaveCount(4);
  await expect(page.getByRole('menuitem', { name: /Sửa/ })).toBeVisible();
  await page.waitForTimeout(250);
  await page.screenshot({ path: 'test-results/patient-treatment-actions.png' });
  await expect(page.getByRole('menuitem', { name: /Thực hiện/ })).not.toHaveAttribute('aria-disabled', 'true');
  await expect(page.getByRole('menuitem', { name: /Hoàn tất/ })).toHaveAttribute('aria-disabled', 'true');
  await expect(page.getByRole('menuitem', { name: /Xóa/ })).toHaveAttribute('aria-disabled', 'true');
  await expect(page.getByRole('link', { name: /Sửa/ })).toHaveAttribute('href',
    `/patients/123/dossiers/${firstId}/plan/revise/${item.id}?from=plan`);
  await page.getByRole('menuitem', { name: /Thực hiện/ }).click();
  const sourcedActualRow = page.locator('.treatment-sheet-actual-table .treatment-sheet-edit-row');
  await expect(sourcedActualRow).toBeVisible();
  await expect(page.getByText(/Đang nhập từ kế hoạch: Trám răng 25/)).toBeVisible();
  await expect(sourcedActualRow.getByRole('textbox', { name: 'Răng' })).toHaveValue('25');
  await expect(sourcedActualRow.getByRole('spinbutton', { name: 'Chi phí tham chiếu' })).toHaveValue('300000');
  await expect(sourcedActualRow.getByRole('textbox', { name: 'Ngày thực hiện' })).toBeEmpty();
  await sourcedActualRow.getByRole('button', { name: 'Đóng hàng nhập' }).click();
  await page.screenshot({ path: 'test-results/patient-treatment-desktop.png', fullPage: true });
  await page.getByRole('button', { name: 'Thu gọn phiếu DT-001' }).click();
  await expect(page.getByText('Trám răng 25')).toHaveCount(0);
  await page.getByRole('button', { name: 'Mở phiếu DT-002' }).click();
  await expect(page.getByText('Trám răng 25')).toBeVisible();
  await page.getByRole('button', { name: /TẠO PHIẾU MỚI/ }).click();
  await expect(page.getByRole('dialog', { name: 'Tạo đợt điều trị' })).toBeVisible();
});

test('cờ Hoàn tất đổi ngay không yêu cầu điều kiện lâm sàng hoặc lý do', async ({ page }) => {
  await prepare(page);
  let closeCalled = false;
  let displayCompleted = false;
  let version = 1;
  await page.route('**/api/patients/123/dossiers?**', route => route.fulfill({ json: {
    items: [{ id: firstId, code: 'DT-001', patientId: 123, branchId: 1, dentistId: 7,
      diagnosis: 'Điều trị răng', state: 'Open', displayCompleted, version, planVersion: 1,
      updatedAt: '2026-10-18T08:00:00Z' }], total: 1, page: 1, pageSize: 20,
  } }));
  await page.route(`**/api/dossiers/${firstId}`, route => route.fulfill({
    json: { ...detail(firstId), version, displayCompleted },
  }));
  await page.route(`**/api/dossiers/${firstId}/close`, route => {
    closeCalled = true;
    return route.fulfill({ status: 500 });
  });
  await page.route(`**/api/dossiers/${firstId}/display-status`, route => {
    const body = route.request().postDataJSON() as { expectedVersion: number; completed: boolean;
      requestKey: string; reason?: string };
    expect(body.expectedVersion).toBe(version);
    expect(body.requestKey).toBeTruthy();
    expect(body.reason).toBeUndefined();
    displayCompleted = body.completed;
    version++;
    return route.fulfill({ json: { dossier: { ...detail(firstId), version, displayCompleted },
      notification: { code: 'MSG222', message: 'Đã cập nhật nhãn.' } } });
  });
  await page.goto('http://127.0.0.1:5173/patients/123');
  await page.getByRole('tab', { name: 'Điều trị & Thanh toán' }).click();
  await page.getByRole('button', { name: 'Trạng thái phiếu: Chưa xong' }).first().click();
  await page.getByRole('menuitem', { name: 'Hoàn tất' }).click();
  await expect(page.getByRole('button', { name: 'Trạng thái phiếu: Hoàn tất' }).first()).toBeVisible();
  await page.getByRole('button', { name: 'Trạng thái phiếu: Hoàn tất' }).click();
  await page.getByRole('menuitem', { name: 'Chưa xong' }).click();
  await expect(page.getByRole('button', { name: 'Trạng thái phiếu: Chưa xong' })).toBeVisible();
  await page.reload();
  await page.getByRole('tab', { name: 'Điều trị & Thanh toán' }).click();
  await expect(page.getByRole('button', { name: 'Trạng thái phiếu: Chưa xong' })).toBeVisible();
  await expect(page.getByRole('dialog', { name: /Hoàn tất phiếu/ })).toHaveCount(0);
  expect(closeCalled).toBe(false);
});

test('xóa phiếu trống cần xác nhận và tải lại danh sách', async ({ page }) => {
  await prepare(page);
  let deleted = false;
  await page.route('**/api/patients/123/dossiers?**', route => route.fulfill({ json: {
    items: deleted ? [] : [{ id: firstId, code: 'DT-001', patientId: 123, branchId: 1,
      dentistId: 7, diagnosis: 'Điều trị răng', state: 'Open', version: 1,
      planVersion: 0, updatedAt: '2026-10-18T08:00:00Z' }],
    total: deleted ? 0 : 1, page: 1, pageSize: 20,
  } }));
  await page.route(`**/api/dossiers/${firstId}`, route => {
    if (route.request().method() === 'DELETE') {
      const body = route.request().postDataJSON() as { expectedVersion: number; requestKey: string };
      expect(body.expectedVersion).toBe(1);
      expect(body.requestKey).toBeTruthy();
      deleted = true;
      return route.fulfill({ json: { dossier: { ...detail(firstId), state: 'Deleted' },
        notification: { code: 'MSG222', message: 'Đã xóa phiếu.' } } });
    }
    return route.fulfill({ json: detail(firstId) });
  });
  await page.goto('http://127.0.0.1:5173/patients/123');
  await page.getByRole('tab', { name: 'Điều trị & Thanh toán' }).click();
  await page.getByRole('button', { name: 'Xóa phiếu DT-001' }).click();
  await expect(page.getByRole('dialog', { name: 'Xóa phiếu DT-001?' })).toBeVisible();
  await page.getByRole('dialog', { name: 'Xóa phiếu DT-001?' }).getByRole('button', { name: 'Xóa phiếu' }).click();
  await expect(page.getByText('Chưa có phiếu điều trị.')).toBeVisible();
  expect(deleted).toBe(true);
});

test('tự lưu hàng nháp nhiều răng và ghi chú hồ sơ', async ({ page }) => {
  await prepare(page);
  let note = 'Ghi chú ban đầu';
  const writes: string[] = [];
  page.on('request', request => {
    if (request.method() !== 'GET' && request.url().includes('/api/dossiers/')) writes.push(request.url());
  });
  await page.route(`**/api/dossiers/${firstId}/note`, route => {
    const body = route.request().postDataJSON() as { note: string; expectedVersion: number; requestKey: string };
    expect(body.requestKey).toBeTruthy();
    note = body.note;
    return route.fulfill({ json: { dossier: { ...detail(firstId), note },
      notification: { code: 'MSG222', message: 'Đã lưu.' } } });
  });
  await page.route(`**/api/dossiers/${firstId}`, route => route.fulfill({ json: { ...detail(firstId), note } }));
  await page.goto(`http://127.0.0.1:5173/patients/123?visitId=${visitId}`);
  await page.getByRole('tab', { name: 'Điều trị & Thanh toán' }).click();
  await page.getByRole('textbox', { name: 'Ghi chú hồ sơ' }).fill('Ghi chú tự lưu');
  await expect(page.getByText('Đã lưu ghi chú')).toBeVisible();
  await page.getByRole('button', { name: 'Thêm hạng mục kế hoạch' }).click();
  const planRow = page.locator('.treatment-sheet-plan-table .treatment-sheet-edit-row');
  await planRow.getByRole('textbox', { name: 'Ngày dự kiến' }).fill('2026-10-27');
  await planRow.getByRole('combobox', { name: 'Điều trị/dịch vụ' }).click();
  await page.locator('.ant-select-dropdown:visible .ant-select-item-option').filter({ hasText: 'Trám răng 25' }).click();
  await planRow.getByRole('textbox', { name: 'Răng' }).fill('11, 12, 21');
  await planRow.getByRole('spinbutton', { name: 'SL' }).fill('2');
  await planRow.getByRole('spinbutton', { name: 'Chi phí' }).fill('120000');
  await expect(planRow.getByText('Đã lưu nháp')).toBeVisible();
  await expect(page.locator('.treatment-sheet-plan-table .treatment-sheet-preview-row')).toContainText('11, 12, 21');
  await expect(page.getByText('Bản xem trước · Chưa lưu')).toHaveCount(0);
  await page.reload();
  await page.getByRole('tab', { name: 'Điều trị & Thanh toán' }).click();
  await expect(page.getByRole('textbox', { name: 'Ghi chú hồ sơ' })).toHaveValue('Ghi chú tự lưu');
  await expect(page.locator('.treatment-sheet-plan-table .treatment-sheet-preview-row')).toContainText('11, 12, 21');
  await page.getByRole('button', { name: 'Thêm quá trình điều trị' }).click();
  const actualRow = page.locator('.treatment-sheet-actual-table .treatment-sheet-edit-row');
  await actualRow.getByRole('textbox', { name: 'Ngày thực hiện' }).fill('2026-10-26');
  await actualRow.getByRole('combobox', { name: 'Điều trị/dịch vụ' }).click();
  await page.locator('.ant-select-dropdown:visible .ant-select-item-option').filter({ hasText: 'Trám răng 25' }).click();
  await actualRow.getByRole('textbox', { name: 'Răng' }).fill('11, 12');
  await actualRow.getByRole('textbox', { name: 'Ghi chú việc đã làm' }).fill('Đã tư vấn');
  await actualRow.getByRole('spinbutton', { name: 'Chi phí tham chiếu' }).fill('90000');
  await expect(actualRow.getByText('Đã lưu nháp')).toBeVisible();
  await expect(page.locator('.treatment-sheet-actual-table .treatment-sheet-preview-row')).toContainText('11, 12');
  await page.reload();
  await page.getByRole('tab', { name: 'Điều trị & Thanh toán' }).click();
  await expect(page.locator('.treatment-sheet-actual-table .treatment-sheet-preview-row')).toContainText('11, 12');
  expect(writes.some(url => url.includes('/sheet-drafts/'))).toBe(true);
  expect(writes.some(url => url.includes('/activities/') || url.includes('/plan/commit'))).toBe(false);
});
test('phiếu trong lượt khám chỉ hiện activity của phiếu đó và giữ vùng in gọn', async ({ page }) => {
  await prepare(page);
  await page.setViewportSize({ width: 1913, height: 908 });
  await page.goto(`http://127.0.0.1:5173/patients/123?visitId=${visitId}`);
  await page.getByRole('tab', { name: 'Điều trị & Thanh toán' }).click();
  await expect(page.getByText('Đã trám')).toBeVisible();
  await page.getByRole('button', { name: 'Chức năng Trám răng 25' }).click();
  await page.getByRole('menuitem', { name: /Thực hiện/ }).click();
  const sourcedActualRow = page.locator('.treatment-sheet-actual-table .treatment-sheet-edit-row');
  await expect(sourcedActualRow).toBeVisible();
  await expect(sourcedActualRow.getByRole('textbox', { name: 'Răng' })).toHaveValue('25');
  await sourcedActualRow.getByRole('button', { name: 'Đóng hàng nhập' }).click();
  await page.screenshot({ path: 'test-results/patient-treatment-with-activity.png' });
  await page.locator('.treatment-sheet-actual-table tbody .treatment-sheet-money span').first().hover();
  await expect(page.getByRole('tooltip', { name: 'Giá tham chiếu của việc đã làm' })).toBeVisible();
  await page.getByRole('button', { name: 'Thêm quá trình điều trị' }).click();
  await expect(page.locator('.treatment-sheet-edit-row')).toBeVisible();
  await page.getByRole('button', { name: 'Đóng hàng nhập' }).click();
  await expect(page.locator('.treatment-sheet-edit-row')).toHaveCount(0);
  await page.evaluate(() => { window.print = () => { document.body.dataset.printInvoked = 'yes'; }; });
  await page.getByRole('button', { name: 'In phiếu đang mở', disabled: false }).click();
  await expect(page.locator('body')).toHaveAttribute('data-print-invoked', 'yes');
  await page.emulateMedia({ media: 'print' });
  await expect(page.getByRole('heading', { name: 'Phiếu điều trị DT-001' })).toBeVisible();
  await expect(page.locator('.workspace-header')).toBeHidden();
  await page.emulateMedia({ media: 'screen' });
  for (const width of [390, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    if (width === 390) await page.screenshot({ path: 'test-results/patient-treatment-mobile.png', fullPage: true });
    const layout = await page.evaluate(() => ({ viewport: window.innerWidth,
      scroll: document.documentElement.scrollWidth,
      overflowing: [...document.querySelectorAll<HTMLElement>('body *')]
        .filter(element => element.getBoundingClientRect().right > window.innerWidth + 1)
        .slice(0, 8).map(element => ({ tag: element.tagName, className: element.className })) }));
    expect(layout.scroll, JSON.stringify(layout)).toBeLessThanOrEqual(width + 1);
  }
});

test('lỗi tải phiếu có thể thử lại và lượt khám chưa có activity hiện trạng thái trống', async ({ page }) => {
  await prepare(page);
  let allowRead = false;
  await page.route(`**/api/dossiers/${firstId}`, route => {
    if (!allowRead) return route.fulfill({ status: 503, json: { code: 'MSG144', message: 'Dịch vụ tạm thời không khả dụng.' } });
    return route.fulfill({ json: detail(firstId) });
  });
  await page.route(`**/api/visits/${visitId}/treatment-context`, route => route.fulfill({ json: {
    visit: { patientId: 123 }, activities: [],
  } }));
  await page.goto(`http://127.0.0.1:5173/patients/123?visitId=${visitId}`);
  await page.getByRole('tab', { name: 'Điều trị & Thanh toán' }).click();
  await expect(page.getByText('Dịch vụ tạm thời không khả dụng.')).toBeVisible();
  allowRead = true;
  await page.getByRole('button', { name: 'Thử lại' }).click();
  await expect(page.getByText('Lượt khám này chưa ghi việc điều trị cho phiếu.')).toBeVisible();
});


