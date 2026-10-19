import { expect, test, type Page } from '@playwright/test';

const options = { branches: [{ id: 1, name: 'Chi nhánh A' }, { id: 2, name: 'Chi nhánh B' }], employees: [1, 2].map(branchId => ({ id: 10, branchId, name: 'Bác sĩ kiểm thử', canDentist: true, canEdit: true, self: true })), chairs: [], services: [1, 2].map(branchId => ({ id: branchId, branchId, name: `Khám ${branchId}`, publicDurationMinutes: 30 })) };
const empty = () => ({ dentistId: 10, version: 0, template: null, exception: null, futureChanges: [], legacyWeek: [] });
async function select(page: Page, label: string, text: string) {
  const box = page.getByRole('combobox', { name: label, exact: true });
  if (/^(Bắt đầu|Kết thúc)/.test(label)) await box.fill(text);
  else await box.press('ArrowDown');
  const list = await box.getAttribute('aria-controls');
  await page.locator('.ant-select-dropdown').filter({ has: page.locator(`[id="${list}"]`) }).getByText(text, { exact: true }).last().click();
}
async function workspace(page: Page) {
  const user = { id: 10, userName: 'dentist.test', fullName: 'Bác sĩ kiểm thử', accountType: 'Staff', roles: ['Dentist'], effectiveRoles: ['Dentist'], permissions: [] };
  await page.route('**/api/auth/sign-in', route => route.fulfill({ json: { accessToken: 'test-only', user } }));
  await page.route('**/api/auth/me', route => route.fulfill({ json: user }));
  await page.route('**/api/working-schedules/options', route => route.fulfill({ json: options }));
  await page.route('**/api/working-schedules/recurring?*', route => route.fulfill({ json: empty() }));
  await page.route('**/api/working-schedules/recurring/preview', route => route.fulfill({ json: { affectedAppointments: [] } }));
  await page.goto('/sign-in'); await page.getByLabel('Tên đăng nhập', { exact: true }).fill('dentist.test'); await page.getByLabel('Mật khẩu', { exact: true }).fill('test-only');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await page.getByRole('link', { name: 'Lịch làm việc', exact: true }).click();
  await expect(page.getByText('Chưa công bố', { exact: true })).toBeVisible();
  await page.getByLabel('Ngày áp dụng', { exact: true }).fill('2032-01-05');
  await expect(page.getByRole('button', { name: 'Lưu và công bố' })).toBeEnabled();
}

test('recurring weekday rows choose branch or Not working with half-hour time entry', async ({ page }) => {
  await workspace(page);
  await expect(page.getByRole('combobox', { name: /^Chi nhánh (Thứ|Chủ nhật)/ })).toHaveCount(7);
  await select(page, 'Chi nhánh Thứ 2', 'Chi nhánh A');
  await select(page, 'Chi nhánh Thứ 3', 'Chi nhánh B');
  await select(page, 'Bắt đầu 1', '08:30');
  await page.route('**/api/working-schedules/recurring/publish', async route => {
    const c = route.request().postDataJSON();
    expect(c.intervals.map((p: { branchId: number }) => p.branchId)).toEqual([1, 2]);
    expect(c.intervals[0].startMinute).toBe(510); expect(c.kind).toBe('Template');
    await route.fulfill({ json: { schedule: { ...empty(), version: 1, template: { ...c, id: 'r1', version: 1, publishedBy: 10, publishedAt: '2032-01-05T00:00:00Z' } }, notification: { message: 'Đã cập nhật lịch làm việc.' } } });
  });
  await page.getByRole('button', { name: 'Lưu và công bố' }).click();
  await expect(page.getByText('Đã công bố', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Hủy thay đổi chưa lưu' })).toBeDisabled();
  await select(page, 'Chi nhánh Thứ 2', 'Không làm việc');
  await expect(page.getByText('Khoảng 1 · 09:00 → 17:00')).toHaveCount(0);
});

test('stale and network failures preserve draft; discard requires confirmation', async ({ page }) => {
  await workspace(page); await select(page, 'Chi nhánh Thứ 2', 'Chi nhánh A');
  await select(page, 'Bắt đầu 1', '09:00');
  await page.route('**/api/working-schedules/recurring/publish', route => route.fulfill({ status: 409, json: { code: 'MSG111', message: 'Dữ liệu đã thay đổi.' } }));
  await page.getByRole('button', { name: 'Lưu và công bố' }).click();
  await expect(page.getByText('Xung đột · Cần kiểm tra lại · MSG111')).toBeVisible();
  await expect(page.getByText('Khoảng 1 · 09:00 → 17:00')).toBeVisible();
  await page.route('**/api/working-schedules/recurring/publish', route => route.abort());
  await page.getByRole('button', { name: 'Lưu và công bố' }).click();
  await expect(page.getByText('Lỗi kết nối hoặc dịch vụ · MSG144')).toBeVisible();
  page.once('dialog', dialog => dialog.dismiss());
  await page.getByRole('button', { name: 'Hủy thay đổi chưa lưu' }).click();
  await expect(page.getByText('Khoảng 1 · 09:00 → 17:00')).toBeVisible();
  page.once('dialog', dialog => dialog.accept());
  await page.getByRole('button', { name: 'Hủy thay đổi chưa lưu' }).click();
  await expect(page.getByRole('combobox', { name: 'Bắt đầu 1', exact: true })).toHaveCount(0);
});

test('dated absence requires reason and submits an empty single-date exception', async ({ page }) => {
  await workspace(page); await select(page, 'Loại lịch', 'Ngoại lệ ngày cụ thể');
  await expect(page.getByRole('combobox', { name: /^Chi nhánh (Thứ|Chủ nhật)/ })).toHaveCount(1);
  await page.getByRole('button', { name: 'Lưu và công bố' }).click();
  await expect(page.getByText('Nhập lý do ngoại lệ, kể cả ngày không làm việc.')).toBeVisible();
  await page.getByLabel('Lý do ngoại lệ', { exact: true }).fill('Nghỉ phép');
  await page.route('**/api/working-schedules/recurring/publish', route => {
    const c = route.request().postDataJSON(); expect(c.kind).toBe('Exception'); expect(c.date).toBe('2032-01-05'); expect(c.intervals).toEqual([]); expect(c.reason).toBe('Nghỉ phép');
    return route.fulfill({ json: { schedule: { ...empty(), version: 1, exception: { ...c, id: 'x', version: 1, publishedBy: 10, publishedAt: '2032-01-05T00:00:00Z' } }, notification: { message: 'Đã cập nhật lịch làm việc.' } } });
  });
  await page.getByRole('button', { name: 'Lưu và công bố' }).click();
  await expect(page.getByText('Đã công bố', { exact: true })).toBeVisible();
});

test('affected future appointments block publication and link to S12', async ({ page }) => {
  await workspace(page);
  let writes = 0;
  await page.route('**/api/working-schedules/recurring/publish', route => { writes++; return route.abort(); });
  await page.route('**/api/working-schedules/recurring/preview', route => route.fulfill({ json: { affectedAppointments: [{ id: 'r1', code: 'AP-001', start: '2032-05-03T08:00:00+07:00', end: '2032-05-03T09:00:00+07:00', attendee: 'Synthetic', status: 'Confirmed' }] } }));
  await page.getByRole('button', { name: 'Lưu và công bố' }).click();
  await expect(page.getByRole('link', { name: 'AP-001 · S12' })).toHaveAttribute('href', '/appointments/r1'); expect(writes).toBe(0);
});

test('duration correction and range preview preserve draft; invalid range sends nothing', async ({ page }) => {
  await workspace(page); await select(page, 'Chi nhánh Thứ 2', 'Chi nhánh A');
  let calls = 0;
  await page.route('**/api/working-schedules/recurring/preview-availability', route => {
    calls++; const c = route.request().postDataJSON(); expect(c.durationMinutes).toBe(75); expect(c.from).toBe('2032-01-05'); expect(c.to).toBe('2032-01-19');
    return route.fulfill({ json: { days: [{ timeZone: 'Asia/Ho_Chi_Minh', date: c.from, slots: [] }] } });
  });
  await page.getByLabel('Preview từ ngày', { exact: true }).fill('2032-01-05');
  await page.getByLabel('Preview đến ngày', { exact: true }).fill('2032-01-19');
  await page.getByRole('spinbutton', { name: 'Thời lượng phút' }).fill('0');
  await page.getByRole('button', { name: 'Preview slot', exact: true }).click();
  await expect(page.getByRole('alert').filter({ hasText: '480' }).first()).toBeVisible(); expect(calls).toBe(0);
  await page.getByRole('spinbutton', { name: 'Thời lượng phút' }).fill('');
  await page.getByRole('button', { name: 'Preview slot', exact: true }).click(); expect(calls).toBe(0);
  await page.getByRole('spinbutton', { name: 'Thời lượng phút' }).fill('75');
  await page.getByRole('button', { name: 'Preview slot', exact: true }).click();
  await expect(page.getByText('Không có khung giờ đáp ứng tài nguyên')).toBeVisible(); expect(calls).toBe(1);
  await page.getByLabel('Preview đến ngày', { exact: true }).fill('2032-02-05');
  await page.getByRole('button', { name: 'Preview slot', exact: true }).click();
  await expect(page.getByText('Chọn khoảng preview từ 1 đến 31 ngày.')).toBeVisible(); expect(calls).toBe(1);
  await expect(page.getByText('Khoảng 1 · 08:00 → 17:00')).toBeVisible();
});

test('retry after a lost publish response reuses receipt key without stale preflight', async ({ page }) => {
  await workspace(page); await select(page, 'Chi nhánh Thứ 2', 'Chi nhánh A');
  let previews = 0; let key: string | undefined; let writes = 0;
  await page.route('**/api/working-schedules/recurring/preview', route => { previews++; return route.fulfill({ json: { affectedAppointments: [] } }); });
  await page.route('**/api/working-schedules/recurring/publish', route => {
    const c = route.request().postDataJSON(); writes++;
    if (writes === 1) { key = c.requestKey; return route.abort(); }
    expect(c.requestKey).toBe(key);
    return route.fulfill({ json: { schedule: { ...empty(), version: 1, template: { ...c, id: 'r', version: 1, publishedBy: 10, publishedAt: '2032-01-05T00:00:00Z' } }, notification: { message: 'Đã cập nhật lịch làm việc.' } } });
  });
  await page.getByRole('button', { name: 'Lưu và công bố' }).click();
  await expect(page.getByText('Lỗi kết nối hoặc dịch vụ · MSG144')).toBeVisible();
  await page.getByRole('button', { name: 'Lưu và công bố' }).click();
  await expect(page.getByText('Đã công bố', { exact: true })).toBeVisible(); expect(previews).toBe(1);
});

test('public preview uses published GET and catalog duration while keeping an invalid draft', async ({ page }) => {
  await workspace(page); await select(page, 'Chi nhánh Thứ 2', 'Chi nhánh A');
  await page.getByRole('button', { name: 'Thêm khoảng', exact: true }).first().click(); // Overlap remains an unsaved draft.
  await select(page, 'Chế độ preview', 'Công khai · bản đã công bố');
  await page.getByLabel('Preview từ ngày', { exact: true }).fill('2032-01-05'); await page.getByLabel('Preview đến ngày', { exact: true }).fill('2032-01-05');
  await page.route('**/api/availability?*', route => {
    expect(new URL(route.request().url()).searchParams.get('durationMinutes')).toBe('30');
    return route.fulfill({ json: { date: '2032-01-05', timeZone: 'Asia/Ho_Chi_Minh', slots: [] } });
  });
  await expect(page.getByRole('spinbutton', { name: 'Thời lượng phút' })).toBeDisabled();
  await page.getByRole('button', { name: 'Preview slot', exact: true }).click();
  await expect(page.getByText('Không có khung giờ đáp ứng tài nguyên')).toBeVisible();
  await expect(page.getByText('Có thay đổi chưa lưu', { exact: false })).toBeVisible();
});

test('denied load is distinct from an empty schedule and retry reloads current data', async ({ page }) => {
  await workspace(page);
  await page.route('**/api/working-schedules/recurring?*', route => route.fulfill({ status: 403, json: { code: 'MSG06', message: 'Bạn không có quyền.' } }));
  await page.getByLabel('Ngày áp dụng', { exact: true }).fill('2032-01-06');
  await expect(page.getByText('Không có quyền truy cập · MSG06')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Lưu và công bố' })).toHaveCount(0);
  await page.route('**/api/working-schedules/recurring?*', route => route.fulfill({ json: empty() }));
  await page.getByRole('button', { name: 'Tải lại', exact: true }).click();
  await expect(page.getByText('Chưa công bố', { exact: true })).toBeVisible();
});

test('real editor previews a recurring sample in a later week without publishing', async ({ page }) => {
  test.skip(!process.env.E2E_DEMO_PASSWORD, 'Requires local API and demo password');
  await page.goto('/sign-in'); await page.getByLabel('Tên đăng nhập', { exact: true }).fill('dentist.demo'); await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.E2E_DEMO_PASSWORD!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click(); await page.getByRole('link', { name: 'Lịch làm việc', exact: true }).click();
  await expect(page.getByLabel('Ngày áp dụng', { exact: true })).toBeVisible();
  await page.getByLabel('Ngày áp dụng', { exact: true }).fill('2035-01-01');
  const branchSelect = page.getByRole('combobox', { name: 'Chi nhánh Thứ 2', exact: true });
  await expect(branchSelect).toBeEnabled(); await branchSelect.press('ArrowDown');
  await page.locator('.ant-select-dropdown:visible .ant-select-item-option').filter({ hasNotText: 'Không làm việc' }).first().click();
  await page.getByRole('button', { name: 'Thêm khoảng', exact: true }).first().click();
  await select(page, 'Loại khoảng 2', 'Nghỉ'); await select(page, 'Bắt đầu 2', '12:00'); await select(page, 'Kết thúc 2', '13:00'); await page.getByLabel('Lý do 2', { exact: true }).fill('Nghỉ trưa');
  await page.getByRole('button', { name: 'Thêm khoảng', exact: true }).first().click();
  await select(page, 'Loại khoảng 3', 'Block'); await select(page, 'Bắt đầu 3', '15:00'); await select(page, 'Kết thúc 3', '15:30'); await page.getByLabel('Lý do 3', { exact: true }).fill('Họp nội bộ');
  await page.getByLabel('Preview từ ngày', { exact: true }).fill('2035-01-08'); await page.getByLabel('Preview đến ngày', { exact: true }).fill('2035-01-08');
  const response = page.waitForResponse(r => r.url().endsWith('/recurring/preview-availability'));
  await page.getByRole('button', { name: 'Preview slot', exact: true }).click();
  const result = await response; expect(result.status()).toBe(200);
  const data = await result.json(); expect(data.days[0].slots.length).toBeGreaterThan(0);
  expect(data.days[0].slots.some((s: { start: string }) => s.start.includes('T12:00'))).toBe(false);
  await expect(page.getByRole('button', { name: 'Preview slot', exact: true })).toBeEnabled();
  await page.screenshot({ path: '../.artifacts/test-results/s09-recurring/editor.png', fullPage: true });
});
