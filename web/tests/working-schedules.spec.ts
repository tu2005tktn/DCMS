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
  const user = { id: 10, userName: 'dentist.test', fullName: 'Bác sĩ kiểm thử', accountType: 'Staff', roles: ['Dentist'], effectiveRoles: ['Dentist'], permissions: ['working-schedules.read', 'working-schedules.manage', 'appointments.read'] };
  await page.route('**/api/auth/sign-in', route => route.fulfill({ json: { accessToken: 'test-only', user } }));
  await page.route('**/api/auth/me', route => route.fulfill({ json: user }));
  await page.route('**/api/auth/session/**', route => route.fulfill({ json: { serverNow: new Date().toISOString(), deadlineAt: new Date(Date.now() + 1200000).toISOString(), activityVersion: 1 } }));
  await page.route('**/api/working-schedules/options', route => route.fulfill({ json: options }));
  await page.route('**/api/working-schedules/appointments?*', route => route.fulfill({ json: { items: [] } }));
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
  await expect(page.getByRole('group', { name: 'Khoảng 1 · 09:00 → 17:00', exact: true })).toHaveCount(0);
});

test('stale and network failures preserve draft; discard requires confirmation', async ({ page }) => {
  await workspace(page); await select(page, 'Chi nhánh Thứ 2', 'Chi nhánh A');
  await select(page, 'Bắt đầu 1', '09:00');
  await page.route('**/api/working-schedules/recurring/publish', route => route.fulfill({ status: 409, json: { code: 'MSG41', message: 'Dữ liệu đã thay đổi.' } }));
  await page.getByRole('button', { name: 'Lưu và công bố' }).click();
  await expect(page.getByRole('dialog').getByText('Xung đột · Cần kiểm tra lại · MSG41')).toBeVisible();
  await page.getByRole('dialog').getByRole('button', { name: 'Đóng', exact: true }).click();
  await expect(page.getByRole('group', { name: 'Khoảng 1 · 09:00 → 17:00', exact: true })).toBeVisible();
  await page.route('**/api/working-schedules/recurring/publish', route => route.abort());
  await page.getByRole('button', { name: 'Lưu và công bố' }).click();
  await expect(page.getByRole('dialog').getByText('Lỗi kết nối hoặc dịch vụ · MSG49')).toBeVisible();
  await page.getByRole('dialog').getByRole('button', { name: 'Đóng', exact: true }).click();
  page.once('dialog', dialog => dialog.dismiss());
  await page.getByRole('button', { name: 'Hủy thay đổi chưa lưu' }).click();
  await expect(page.getByRole('group', { name: 'Khoảng 1 · 09:00 → 17:00', exact: true })).toBeVisible();
  page.once('dialog', dialog => dialog.accept());
  await page.getByRole('button', { name: 'Hủy thay đổi chưa lưu' }).click();
  await expect(page.getByRole('combobox', { name: 'Bắt đầu 1', exact: true })).toHaveCount(0);
});

test('dated absence requires reason and submits an empty single-date exception', async ({ page }) => {
  await workspace(page); await select(page, 'Loại lịch', 'Ngoại lệ ngày cụ thể');
  await expect(page.getByRole('combobox', { name: /^Chi nhánh (Thứ|Chủ nhật)/ })).toHaveCount(1);
  await page.getByRole('button', { name: 'Lưu và công bố' }).click();
  await expect(page.getByRole('dialog').getByText('Nhập lý do ngoại lệ, kể cả ngày không làm việc.')).toBeVisible();
  await page.getByRole('dialog').getByRole('button', { name: 'Đóng', exact: true }).click();
  await page.getByLabel('Lý do ngoại lệ', { exact: true }).fill('Nghỉ phép');
  await page.route('**/api/working-schedules/recurring/publish', route => {
    const c = route.request().postDataJSON(); expect(c.kind).toBe('Exception'); expect(c.date).toBe('2032-01-05'); expect(c.intervals).toEqual([]); expect(c.reason).toBe('Nghỉ phép');
    return route.fulfill({ json: { schedule: { ...empty(), version: 1, exception: { ...c, id: 'x', version: 1, publishedBy: 10, publishedAt: '2032-01-05T00:00:00Z' } }, notification: { message: 'Đã cập nhật lịch làm việc.' } } });
  });
  await page.getByRole('button', { name: 'Lưu và công bố' }).click();
  await expect(page.getByText('Đã công bố', { exact: true })).toBeVisible();
});

test('leave over several days publishes one empty exception per date', async ({ page }) => {
  await workspace(page); await select(page, 'Loại lịch', 'Ngoại lệ ngày cụ thể');
  await page.getByLabel('Lý do ngoại lệ', { exact: true }).fill('Nghỉ phép');
  await page.getByLabel('Đến ngày', { exact: true }).fill('2032-01-07');
  const published: { date: string; version: number; intervals: unknown[] }[] = [];
  await page.route('**/api/working-schedules/recurring/preview', route => route.fulfill({ json: { affectedAppointments: [] } }));
  await page.route('**/api/working-schedules/recurring/publish', route => {
    const c = route.request().postDataJSON(); published.push({ date: c.date, version: c.expectedVersion, intervals: c.intervals });
    return route.fulfill({ json: { schedule: { ...empty(), version: c.expectedVersion + 1 }, notification: { message: 'Đã cập nhật lịch làm việc.' } } });
  });
  await page.getByRole('button', { name: 'Lưu và công bố' }).click();
  await expect.poll(() => published.length).toBe(3);
  expect(published.map(p => p.date)).toEqual(['2032-01-07', '2032-01-06', '2032-01-05']);
  expect(published.map(p => p.version)).toEqual([0, 1, 2]);
  expect(published.every(p => p.intervals.length === 0)).toBe(true);
});

test('affected future appointments block publication and link to S12', async ({ page }) => {
  await workspace(page);
  let writes = 0;
  await page.route('**/api/working-schedules/recurring/publish', route => { writes++; return route.abort(); });
  await page.route('**/api/working-schedules/recurring/preview', route => route.fulfill({ json: { affectedAppointments: [{ id: 'r1', code: 'AP-001', start: '2032-05-03T08:00:00+07:00', end: '2032-05-03T09:00:00+07:00', attendee: 'Synthetic', status: 'Confirmed' }] } }));
  await page.getByRole('button', { name: 'Lưu và công bố' }).click();
  await expect(page.getByRole('link', { name: 'AP-001 · S12' })).toHaveAttribute('href', '/appointments/r1'); expect(writes).toBe(0);
});

test('not enough chairs blocks publication with a pop-up', async ({ page }) => {
  await workspace(page);
  let writes = 0;
  await page.route('**/api/working-schedules/recurring/publish', route => { writes++; return route.abort(); });
  await page.route('**/api/working-schedules/recurring/preview', route => route.fulfill({ json: { affectedAppointments: [], capacityConflicts: [{ branchId: 1, day: 0, startMinute: 420, endMinute: 480, doctors: 3, chairs: 2 }] } }));
  await page.getByRole('button', { name: 'Lưu và công bố' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByText('Thứ 2 07:00–08:00: 3 bác sĩ cùng ca, chi nhánh chỉ có 2 ghế')).toBeVisible();
  expect(writes).toBe(0);
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
  await expect(page.getByRole('dialog').getByText('Lỗi kết nối hoặc dịch vụ · MSG49')).toBeVisible();
  await page.getByRole('dialog').getByRole('button', { name: 'Đóng', exact: true }).click();
  await page.getByRole('button', { name: 'Lưu và công bố' }).click();
  await expect(page.getByText('Đã công bố', { exact: true })).toBeVisible(); expect(previews).toBe(1);
});

test('denied load is distinct from an empty schedule and retry reloads current data', async ({ page }) => {
  await workspace(page);
  await page.route('**/api/working-schedules/recurring?*', route => route.fulfill({ status: 403, json: { code: 'MSG05', message: 'Bạn không có quyền.' } }));
  await page.getByLabel('Ngày áp dụng', { exact: true }).fill('2032-01-06');
  await expect(page.getByText('Không có quyền truy cập · MSG05')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Lưu và công bố' })).toHaveCount(0);
  await page.route('**/api/working-schedules/recurring?*', route => route.fulfill({ json: empty() }));
  await page.getByRole('button', { name: 'Tải lại', exact: true }).click();
  await expect(page.getByText('Chưa công bố', { exact: true })).toBeVisible();
});

