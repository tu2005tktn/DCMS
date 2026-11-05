import { test, expect, type Page } from '@playwright/test';

const dossier = { dossierId: 'd-1', dossierCode: 'HS-001', startedOn: '2026-11-02', patientId: 1, patientCode: 'BN001', patientName: 'SYNTHETIC Patient 1', nextAppointmentAt: '2026-11-20T02:30:00+00:00',
  dentistName: 'SYNTHETIC Dentist', assistantNames: ['SYNTHETIC Assistant'], costVnd: 300000, paidVnd: 100000, dueVnd: 200000,
  lines: [{ activityId: 'a-1', serviceName: 'Trám răng', costVnd: 300000, paidVnd: 100000, dueVnd: 200000 }, { activityId: 'a-2', serviceName: 'Nhổ răng', costVnd: 50000, paidVnd: 0, dueVnd: 50000 }] };

async function prepare(page: Page) {
  const user = { id: 7, userName: 'synthetic', fullName: 'SYNTHETIC Owner', accountType: 'Staff', roles: ['ClinicOwner'], effectiveRoles: ['ClinicOwner'], permissions: ['finance.read'] };
  const state = { queries: [] as URLSearchParams[], fail: true };
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url()), path = url.pathname, ok = (json: unknown) => route.fulfill({ json });
    if (path === '/api/auth/sign-in') return ok({ accessToken: 'synthetic', user });
    if (path === '/api/auth/me') return ok(user);
    if (path === '/api/finance/capabilities') return ok({ branches: [{ id: 1, name: 'Cơ sở 1' }] });
    if (path === '/api/work-statistics') {
      if (state.fail) { return route.fulfill({ status: 400, json: { code: 'MSG04', message: 'Không thể tải thử nghiệm.' } }); }
      state.queries.push(url.searchParams);
      return ok({ items: [dossier], lineCount: 2, total: 1, page: 1, pageSize: 20, totals: { costVnd: 300000, paidVnd: 100000, dueVnd: 200000 } });
    }
    return route.fulfill({ status: 404, json: { code: 'NOT_MOCKED', message: path } });
  });
  await page.goto('/sign-in'); await page.getByLabel('Tên đăng nhập', { exact: true }).fill('synthetic'); await page.getByLabel('Mật khẩu', { exact: true }).fill('synthetic');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await page.goto('/work-statistics');
  return state;
}

test('thống kê công việc/dịch vụ: lỗi thử lại, đủ cột, tổng và bộ lọc gửi lên máy chủ', async ({ page }) => {
  const state = await prepare(page);
  await expect(page.getByText('Không thể tải thử nghiệm.')).toBeVisible();
  state.fail = false; await page.getByRole('button', { name: 'Thử lại', exact: true }).click();
  for (const title of ['Ngày', 'MSKH', 'Khách hàng', 'Lịch hẹn tiếp theo', 'Điều trị/dịch vụ', 'Bác sĩ & phụ tá', 'Chi phí', 'Đã trả', 'Còn lại'])
    await expect(page.getByRole('columnheader', { name: title, exact: true })).toBeVisible();
  // Dossier-level cells appear once (merged); each service has its own row with money.
  await expect(page.getByText('SYNTHETIC Patient 1', { exact: true })).toHaveCount(1);
  await expect(page.getByText('BN001', { exact: true })).toHaveCount(1);
  await expect(page.getByText('Phụ tá: SYNTHETIC Assistant')).toHaveCount(1);
  await expect(page.getByRole('link', { name: 'SYNTHETIC Patient 1' })).toHaveAttribute('href', '/patients/1');
  await expect(page.getByRole('columnheader', { name: 'Ghi chú' })).toHaveCount(0);
  const first = page.getByRole('row', { name: /Trám răng/ }), second = page.getByRole('row', { name: /Nhổ răng/ });
  await expect(first).toContainText('300.000'); await expect(first).toContainText('100.000'); await expect(first).toContainText('200.000');
  await expect(second).toContainText('50.000'); await expect(first).not.toContainText('Răng 36');
  // Summary strip: totals on the left, number of service lines on the right.
  const strip = page.getByLabel('Tổng hợp', { exact: true });
  await expect(strip).toContainText('Tổng cộng:'); await expect(strip).toContainText('300.000'); await expect(strip).toContainText('Đã trả:'); await expect(strip).toContainText('Còn lại:');
  await expect(strip).toContainText('Tìm thấy 2 công việc/dịch vụ.');
  // One period dropdown sends from/to for the chosen period; custom reveals the date inputs.
  const pick = async (name: string) => { await page.locator('div.ant-select[aria-label="Thời gian"]').click(); await page.locator('.ant-select-item-option', { hasText: new RegExp('^' + name + '$') }).click(); };
  await expect(page.getByLabel('Từ ngày', { exact: true })).toHaveCount(0);
  await pick('Tháng này');
  await expect.poll(() => { const q = state.queries.at(-1); return q?.get('from')?.endsWith('-01') && q.get('to') !== null && q.get('from')!.slice(0, 7) === q.get('to')!.slice(0, 7); }).toBe(true);
  await pick('Tuần này');
  await expect.poll(() => { const q = state.queries.at(-1); if (!q?.get('from') || !q.get('to')) return false; const a = new Date(q.get('from')!), b = new Date(q.get('to')!); return a.getUTCDay() === 1 && (b.getTime() - a.getTime()) / 86400000 === 6; }).toBe(true);
  await pick('Năm này');
  await expect.poll(() => { const q = state.queries.at(-1); return q?.get('from')?.endsWith('-01-01') && q.get('to')?.endsWith('-12-31'); }).toBe(true);
  await pick('Hôm nay');
  await expect.poll(() => { const q = state.queries.at(-1); return !!q?.get('from') && q.get('from') === q.get('to'); }).toBe(true);
  await pick('Tất cả');
  await expect.poll(() => { const q = state.queries.at(-1); return q?.get('from') === null && q.get('to') === null; }).toBe(true);
  await pick('Tùy chọn');
  await page.getByLabel('Từ ngày', { exact: true }).fill('2026-11-01');
  await page.getByLabel('Tìm công việc', { exact: true }).fill('Trám');
  await expect.poll(() => state.queries.some(q => q.get('from') === '2026-11-01' && q.get('q') === 'Trám')).toBe(true);
});
