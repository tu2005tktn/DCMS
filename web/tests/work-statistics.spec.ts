import { test, expect, type Page } from '@playwright/test';

const dossier = { dossierId: 'd-1', dossierCode: 'HS-001', startedOn: '2026-11-02', patientId: 1, patientCode: 'BN001', patientName: 'SYNTHETIC Patient 1', nextAppointmentAt: '2026-11-20T02:30:00+00:00',
  dentistName: 'SYNTHETIC Dentist', assistantNames: ['SYNTHETIC Assistant'], costVnd: 350000, paidVnd: 100000, dueVnd: 250000,
  state: 'Open', note: 'SYNTHETIC ghi chú phiếu', createdByName: 'SYNTHETIC Creator',
  lines: [{ activityId: 'a-1', serviceName: 'Trám răng', costVnd: 300000, paidVnd: 100000, dueVnd: 200000, performedAt: '2026-11-02T03:00:00+00:00', quantity: 1, unitPriceVnd: 320000, discountVnd: 20000 },
    { activityId: 'a-2', serviceName: 'Nhổ răng', costVnd: 50000, paidVnd: 0, dueVnd: 50000, performedAt: '2026-11-03T03:00:00+00:00', quantity: 1, unitPriceVnd: 50000, discountVnd: 0 }] };

async function prepare(page: Page) {
  const user = { id: 7, userName: 'synthetic', fullName: 'SYNTHETIC Owner', accountType: 'Staff', roles: ['ClinicOwner'], effectiveRoles: ['ClinicOwner'], permissions: ['finance.read', 'finance.work-statistics.read'] };
  const state = { queries: [] as URLSearchParams[], fail: true };
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url()), path = url.pathname, ok = (json: unknown) => route.fulfill({ json });
    if (path === '/api/auth/sign-in') return ok({ accessToken: 'synthetic', user });
    if (path === '/api/auth/me') return ok(user);
    if (path === '/api/finance/capabilities') return ok({ branches: [{ id: 1, name: 'Cơ sở 1' }] });
    if (path === '/api/work-statistics') {
      if (state.fail) { return route.fulfill({ status: 400, json: { code: 'MSG04', message: 'Không thể tải thử nghiệm.' } }); }
      state.queries.push(url.searchParams);
      const wanted = url.searchParams.get('dossierId');
      if (wanted && wanted !== dossier.dossierId) return ok({ items: [], lineCount: 0, total: 0, page: 1, pageSize: 20, totals: { costVnd: 0, paidVnd: 0, dueVnd: 0 } });
      return ok({ items: [dossier], lineCount: 2, total: 1, page: 1, pageSize: 20, totals: { costVnd: 350000, paidVnd: 100000, dueVnd: 250000 } });
    }
    return route.fulfill({ status: 404, json: { code: 'NOT_MOCKED', message: path } });
  });
  await page.goto('/sign-in'); await page.getByLabel('Tên đăng nhập', { exact: true }).fill('synthetic'); await page.getByLabel('Mật khẩu', { exact: true }).fill('synthetic');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await page.goto('/work-statistics');
  return state;
}

test('thống kê công việc/dịch vụ: lỗi thử lại, dòng thu gọn xổ chi tiết chỉ đọc, tổng và bộ lọc gửi lên máy chủ', async ({ page }) => {
  const state = await prepare(page);
  await expect(page.getByText('Không thể tải thử nghiệm.')).toBeVisible();
  state.fail = false; await page.getByRole('button', { name: 'Thử lại', exact: true }).click();
  for (const title of ['Ngày', 'Khách hàng', 'Bác sĩ', 'Chi phí', 'Đã trả', 'Còn lại'])
    await expect(page.getByRole('columnheader', { name: title, exact: true })).toBeVisible();
  // Collapsed: one summary row per dossier with its totals; services stay hidden until the row is opened.
  const summary = page.locator('tr.work-stats-row', { hasText: 'SYNTHETIC Patient 1' });
  await expect(summary).toHaveCount(1);
  await expect(summary).toContainText('SYNTHETIC Patient 1'); await expect(summary).toContainText('SYNTHETIC Dentist');
  await expect(summary).toContainText('350.000'); await expect(summary).toContainText('100.000'); await expect(summary).toContainText('250.000');
  await expect(summary).toHaveAttribute('aria-haspopup', 'dialog');
  await expect(page.getByText('Trám răng')).toHaveCount(0);
  // Expanded: read-only detail with creator, dentist, services, note and what the patient owes and has paid.
  await summary.click();
  await expect(page.getByRole('dialog')).toBeVisible();
  const detail = page.getByLabel('Chi tiết phiếu HS-001', { exact: true });
  await expect(detail.getByRole('link', { name: 'BN001 - SYNTHETIC Patient 1' })).toHaveAttribute('href', '/patients/1');
  await expect(detail.getByRole('link', { name: 'Mở phiếu điều trị' })).toHaveAttribute('href', '/patients/1?tab=treatment&dossierId=d-1');
  await expect(detail).toContainText('Đang điều trị'); await expect(detail).toContainText('SYNTHETIC Creator'); await expect(detail).toContainText('SYNTHETIC Dentist');
  await expect(detail).toContainText('SYNTHETIC Assistant'); await expect(detail).toContainText('SYNTHETIC ghi chú phiếu');
  const first = detail.getByRole('row', { name: /Trám răng/ }), second = detail.getByRole('row', { name: /Nhổ răng/ });
  await expect(first).toContainText('02/11/2026'); await expect(first).toContainText('320.000'); await expect(first).toContainText('20.000');
  await expect(first).toContainText('300.000'); await expect(first).toContainText('100.000'); await expect(first).toContainText('200.000');
  await expect(second).toContainText('50.000');
  const sum = detail.locator('.work-stats-sum');
  await expect(sum).toContainText('Tổng tiền dịch vụ (2)'); await expect(sum).toContainText('370.000');
  await expect(sum.locator('div', { hasText: 'Khách cần trả' })).toContainText('350.000');
  await expect(sum.locator('div', { hasText: 'Khách đã trả' })).toContainText('100.000');
  await expect(detail.getByRole('button')).toHaveCount(0); await expect(detail.getByRole('textbox')).toHaveCount(0);
  // The close button closes the dialog.
  await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click(); await expect(page.getByRole('dialog')).toBeHidden();
  await expect(detail).toBeHidden();
  // Summary strip: totals on the left, number of service lines on the right.
  const strip = page.getByLabel('Tổng hợp', { exact: true });
  await expect(strip).toContainText('Tổng cộng:'); await expect(strip).toContainText('350.000'); await expect(strip).toContainText('Đã trả:'); await expect(strip).toContainText('Còn lại:');
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

test('mở từ thống kê tài chính: chỉ một phiếu, tự xổ chi tiết, thoát về danh sách và báo khi phiếu chưa có dịch vụ', async ({ page }) => {
  const state = await prepare(page); state.fail = false;
  await page.goto('/work-statistics?dossierId=d-1&patientId=1');
  // Only the requested dossier is asked for; the other filters are not sent.
  await expect.poll(() => state.queries.at(-1)?.get('dossierId')).toBe('d-1');
  expect(state.queries.at(-1)!.get('from')).toBeNull(); expect(state.queries.at(-1)!.get('q')).toBeNull();
  await expect(page.getByRole('status')).toContainText('Đang xem phiếu HS-001.');
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(page.getByLabel('Chi tiết phiếu HS-001', { exact: true })).toBeVisible();
  await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click(); await expect(page.getByRole('dialog')).toBeHidden();
  await page.getByRole('button', { name: 'Xem tất cả phiếu', exact: true }).click();
  await expect(page).toHaveURL(/\/work-statistics$/);
  await expect.poll(() => state.queries.at(-1)?.get('dossierId')).toBeNull();
  await expect(page.getByRole('status')).toHaveCount(0);
  // Changing a filter also leaves the single-dossier view.
  await page.goto('/work-statistics?dossierId=d-1&patientId=1');
  await expect(page.getByRole('status')).toContainText('Đang xem phiếu HS-001.');
  await page.getByLabel('Tìm công việc', { exact: true }).fill('Trám');
  await expect(page).toHaveURL(/\/work-statistics$/);
  await expect.poll(() => { const q = state.queries.at(-1); return q?.get('q') === 'Trám' && q.get('dossierId') === null; }).toBe(true);
  // A dossier without performed services is not in these statistics: say so and offer the dossier itself.
  await page.goto('/work-statistics?dossierId=d-404&patientId=9');
  await expect(page.getByRole('status')).toContainText('Phiếu này chưa có dịch vụ đã thực hiện nên chưa có trong thống kê công việc.');
  await expect(page.getByRole('link', { name: 'Mở phiếu điều trị' })).toHaveAttribute('href', '/patients/9?tab=treatment&dossierId=d-404');
});
