import { expect, test, type Page, type Locator } from '@playwright/test';
async function saveDrawer(drawer: Locator) {
  const save = drawer.locator('button[type="submit"]');
  await expect(save).not.toHaveClass(/ant-btn-loading/);
  await save.click();
}
const user = { id: 21, userName: 'synthetic', fullName: 'Người đặt mẫu', phoneNumber: '0912345678', accountType: 'Public', roles: ['Guest'], effectiveRoles: ['Guest'], permissions: [] };
const member = { id: '22222222-2222-4222-8222-222222222222', fullName: 'Con mẫu', relationship: 'Child', birthDate: '2015-03-12', version: 1, gender: 'MALE', contactPhone: '0987654321' };
async function mock(page: Page, options: { missing?: boolean; listError?: boolean; stale?: boolean; saveError?: boolean; many?: boolean } = {}) {
  let items = options.many ? Array.from({ length: 21 }, (_, i) => ({ ...member, id: `22222222-2222-4222-8222-${String(i + 1).padStart(12, '0')}`, fullName: `Người thân ${i + 1}` })) : [{ ...member }];
  const commands: { url: string; body: Record<string, any> }[] = [];
  const listReads: string[] = [];
  let stale = options.stale, saves = 0, bookings = 0;
  await page.addInitScript(() => sessionStorage.setItem('dcms.customer.accessToken', 'synthetic'));
  await page.route('**/api/**', async route => {
    const request = route.request(), url = new URL(request.url()), method = request.method(), path = url.pathname;
    if (path === '/api/auth/me') return route.fulfill({ json: user });
    if (path === '/api/auth/google/config') return route.fulfill({ json: { enabled: false } });
    if (path === '/api/availability/options') return route.fulfill({ json: { branches: [{ id: 1, name: 'Chi nhánh mẫu' }], services: [{ id: 3, branchId: 1, name: 'Khám', publicDurationMinutes: 30 }], employees: [{ id: 2, branchId: 1, name: 'Bác sĩ mẫu', canDentist: true }] } });
    if (path === '/api/availability') return route.fulfill({ json: { slots: [{ start: '2030-11-05T02:00:00Z', end: '2030-11-05T02:30:00Z', chairId: 1, dentistId: 2, scheduleVersion: 1, assistantScheduleVersion: null }] } });
    if (path === '/api/appointments' && method === 'POST') {
      const body = request.postDataJSON(); commands.push({ url: path, body }); bookings++;
      if (bookings === 1) return route.fulfill({ status: 409, json: { code: 'MSG25', message: 'Khung giờ không còn khả dụng.' } });
      return route.fulfill({ status: 201, json: { appointmentId: 'saved', code: 'AP-FAMILY', version: 1, status: 'Confirmed', startAt: '2030-11-05T02:00:00Z', endAt: '2030-11-05T02:30:00Z', notification: {} } });
    }
    if (method === 'GET' && path.endsWith('/record-access')) return route.fulfill({ json: { current: null, history: [], canRequest: true } });
    if (method === 'GET' && path.startsWith('/api/me/family-members/') && path.endsWith('/appointments')) return route.fulfill({ json: { items: [], total: 0, page: 1, pageSize: 10 } });
    if (path.startsWith('/api/me/family-members')) {
      if (method === 'GET') {
        if (path === '/api/me/family-members') {
          listReads.push(url.search);
          if (options.listError) return route.fulfill({ status: 503, json: { code: 'MSG144', message: 'Danh sách tạm thời không tải được.' } });
          const keyword = url.searchParams.get('keyword')?.toLowerCase() ?? '', p = Number(url.searchParams.get('page') ?? 1);
          const filtered = items.filter(m => m.fullName.toLowerCase().includes(keyword));
          return route.fulfill({ json: { items: filtered.slice((p - 1) * 20, p * 20), total: filtered.length, page: p, pageSize: 20 } });
        }
        const found = items.find(m => m.id === path.split('/').at(-1));
        return options.missing || !found ? route.fulfill({ status: 404, json: { code: 'FAMILY_MEMBER_NOT_FOUND', message: 'Không tìm thấy người thân.' } }) : route.fulfill({ json: stale ? { ...found, fullName: 'Tên mới trên server', version: 2 } : found });
      }
      const body = request.postDataJSON(); commands.push({ url: path, body }); saves++;
      if (options.saveError && saves === 1) return route.fulfill({ status: 503, json: { code: 'MSG144', message: 'Lưu chưa thành công.' } });
      if (stale && method === 'PUT' && body.expectedVersion === 1) return route.fulfill({ status: 409, json: { code: 'MSG111', message: 'Dữ liệu đã thay đổi.' } });
      if (method === 'POST') { const saved = { id: '33333333-3333-4333-8333-333333333333', ...body, version: 1 }; items.push(saved); return route.fulfill({ status: 201, json: saved }); }
      const index = items.findIndex(m => m.id === path.split('/').at(-1));
      if (method === 'DELETE') { items.splice(index, 1); return route.fulfill({ json: { id: path.split('/').at(-1), deleted: true, version: 2 } }); }
      const saved = { ...items[index], ...body, version: body.expectedVersion + 1 }; items[index] = saved; stale = false; return route.fulfill({ json: saved });
    }
    return route.fulfill({ json: {} });
  });
  return Object.assign(commands, { listReads });
}
async function pickSchedule(page: Page) {
  await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
  await page.locator('.ant-select').nth(0).click(); await page.getByText('Chi nhánh mẫu', { exact: true }).last().click();
  await page.locator('.ant-select').nth(1).click(); await page.getByText('Bác sĩ mẫu', { exact: true }).last().click();
  await page.locator('.customer-slots button').first().click(); await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
}

test('hồ sơ đã liên kết được xem ngay, không cần gửi yêu cầu và tự mở lịch sử khám', async ({ page }) => {
  await mock(page);
  const access = { id: 'automatic-access', familyMemberId: member.id, code: 'FA-SYNTHETIC', status: 'Verified', fullName: member.fullName,
    birthDate: member.birthDate, patientCode: 'BN-SYNTHETIC', requestedAt: '2026-11-09T01:00:00Z', expiresAt: '2099-01-01T00:00:00Z',
    scopes: ['Examinations', 'Treatment', 'Prescriptions', 'Documents'], version: 1,
    publicNote: 'Bạn được xem hồ sơ người thân mặc định từ lịch đặt hộ đã được phòng khám tiếp nhận.' };
  await page.route('**/api/me/family-members/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/record-access')) return route.fulfill({ json: { current: access, history: [access], canRequest: false } });
    if (path.endsWith('/record')) return route.fulfill({ json: { patientCode: access.patientCode, fullName: member.fullName, birthDate: member.birthDate,
      branchName: 'Chi nhánh thử nghiệm', scopes: access.scopes, expiresAt: access.expiresAt } });
    if (path.endsWith('/record/examinations')) return route.fulfill({ json: { items: [{ visitId: 'visit', arrivalAt: '2026-11-09T01:00:00Z',
      dentistName: 'Bác sĩ mẫu', branchName: 'Chi nhánh thử nghiệm', symptoms: 'Kiểm tra', diagnoses: ['Bình thường'], conclusion: 'Kết quả mẫu', advice: 'Lời dặn mẫu' }], total: 1, page: 1, pageSize: 20 } });
    return route.fallback();
  });
  await page.goto('/customer/family-members');
  await page.getByRole('button', { name: 'Xem hồ sơ', exact: true }).click();
  const modal = page.getByRole('dialog', { name: `Hồ sơ của ${member.fullName}`, exact: true });
  await expect(modal.getByRole('tab', { name: 'Lịch sử khám', exact: true })).toHaveAttribute('aria-selected', 'true');
  await expect(modal.getByRole('tab', { name: 'Đơn thuốc', exact: true })).toBeVisible();
  await expect(modal.getByRole('button', { name: 'Yêu cầu quyền xem hồ sơ', exact: true })).toHaveCount(0);
  await modal.locator('.family-record-section .ant-collapse-header').first().click();
  await expect(modal.getByText('Lời dặn mẫu', { exact: true })).toBeVisible();
});

test('người thân chưa tiếp nhận giải thích quyền mặc định, đóng bằng bàn phím/nút và giữ lịch đặt hộ', async ({ page }) => {
  const calls = await mock(page, { many: true });
  const requests: string[] = [];
  page.on('request', request => { if (new URL(request.url()).pathname.startsWith('/api/')) requests.push(request.url()); });
  await page.goto('/customer/family-members');
  await expect(page.getByRole('row').filter({ hasText: /^Người thân 1Con/ })).toBeVisible();
  await page.getByLabel('Tìm theo họ tên').fill('Người thân 21');
  await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click();
  await expect(page.getByRole('row').filter({ hasText: 'Người thân 21' })).toBeVisible();
  for (const width of [390, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    const container = page.locator(width <= 680 ? '.customer-family-mobile' : '.customer-family-table');
    const trigger = container.getByRole('button', { name: 'Xem hồ sơ', exact: true });
    await trigger.focus(); await trigger.press('Enter');
    const modal = page.getByRole('dialog', { name: 'Hồ sơ của Người thân 21', exact: true });
    await expect(modal).toBeVisible();
    await expect(modal.getByText('Chưa có hồ sơ liên kết', { exact: true })).toBeVisible();
    await expect(modal.getByText(/Hồ sơ được xem mặc định sau khi phòng khám tiếp nhận/)).toBeVisible();
    await modal.getByText('Liên kết hồ sơ đã có tại phòng khám', { exact: true }).click();
    await expect(modal.getByRole('button', { name: 'Yêu cầu quyền xem hồ sơ', exact: true })).toBeVisible();
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width + 1);
    const box = await modal.boundingBox(); expect(box).not.toBeNull();
    expect(box!.x).toBeGreaterThanOrEqual(0); expect(box!.x + box!.width).toBeLessThanOrEqual(width);
    await expect.poll(() => modal.evaluate(element => element.contains(document.activeElement))).toBe(true);
    await expect(modal).not.toHaveClass(/ant-zoom-enter/);
    await page.screenshot({ path: `../.artifacts/family-records-${width}.png`, fullPage: true });
    await page.keyboard.press('Escape'); await expect(modal).not.toBeVisible(); await expect(trigger).toBeFocused();
    await trigger.click(); await modal.getByRole('button', { name: 'Đóng', exact: true }).click(); await expect(modal).not.toBeVisible();
    await trigger.click(); await modal.locator('.ant-modal-close').click(); await expect(modal).not.toBeVisible();
    await expect(page.getByLabel('Tìm theo họ tên')).toHaveValue('Người thân 21');
    expect(requests.some(url => url.includes('/record-access'))).toBe(true); expect(calls).toHaveLength(0);
  }
  await page.getByLabel('Tìm theo họ tên').fill('Người thân 2'); await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click();
  await page.getByRole('row').filter({ hasText: /^Người thân 2Con/ }).getByRole('button', { name: 'Xem hồ sơ' }).click();
  await expect(page.getByRole('dialog', { name: 'Hồ sơ của Người thân 2', exact: true })).toBeVisible();
});

test('CRUD, hủy xóa, retry giữ payload/key và responsive', async ({ page }) => {
  const calls = await mock(page, { saveError: true }); await page.goto('/customer/family-members');
  await page.getByRole('button', { name: 'Thêm người thân', exact: true }).click();
  const drawer = page.getByRole('dialog'); await drawer.getByLabel('Họ tên', { exact: true }).fill('Người thân mới');
  await drawer.getByLabel('Quan hệ', { exact: true }).click(); await page.getByText('Mẹ', { exact: true }).last().click();
  await drawer.getByLabel('Ngày sinh (không bắt buộc)').fill('1980-01-02');
  await drawer.getByLabel('Giới tính').click(); await page.getByText('Nữ', { exact: true }).last().click();
  await drawer.getByLabel('Số điện thoại liên hệ').fill('0987654321');
  await saveDrawer(drawer); await expect(drawer.getByText('Lưu chưa thành công.')).toBeVisible();
  await expect(drawer.getByLabel('Họ tên', { exact: true })).toHaveValue('Người thân mới');
  await saveDrawer(drawer); await expect(drawer).not.toBeVisible();
  expect(calls[0].body.requestKey).toBe(calls[1].body.requestKey);
  let row = page.getByRole('row').filter({ hasText: 'Người thân mới' }); await expect(row).toBeVisible();
  await expect(row).toContainText('Nữ'); await expect(row).toContainText('0987654321');
  await page.reload(); row = page.getByRole('row').filter({ hasText: 'Người thân mới' }); await expect(row).toContainText('0987654321');
  await row.getByRole('button', { name: 'Sửa' }).click(); await expect(drawer.getByLabel('Số điện thoại liên hệ')).toHaveValue('0987654321');
  await drawer.getByLabel('Họ tên', { exact: true }).fill('Đổi tên'); await drawer.getByLabel('Số điện thoại liên hệ').fill('0912345678'); await saveDrawer(drawer);
  row = page.getByRole('row').filter({ hasText: 'Đổi tên' }); await row.getByRole('button', { name: 'Xóa', exact: true }).click(); await page.getByRole('button', { name: 'Không đồng ý', exact: true }).click(); await expect(row).toBeVisible();
  await row.getByRole('button', { name: 'Xóa', exact: true }).click(); await page.getByRole('button', { name: 'Đồng ý', exact: true }).click(); await expect(row).toHaveCount(0);
  for (const width of [390, 768, 1440]) { await page.setViewportSize({ width, height: 900 }); await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width + 1); await page.screenshot({ path: `../.artifacts/family-members-${width}.png`, fullPage: true }); }
});

test('stale cho đối chiếu bản mới, giữ bản nhập rồi lưu có version mới', async ({ page }) => {
  const calls = await mock(page, { stale: true }); await page.goto('/customer/family-members');
  await page.getByRole('row').filter({ hasText: 'Con mẫu' }).getByRole('button', { name: 'Sửa' }).click();
  const drawer = page.getByRole('dialog'); await drawer.getByLabel('Họ tên', { exact: true }).fill('Tên đang nhập'); await saveDrawer(drawer);
  await drawer.getByRole('button', { name: 'Xem phiên bản mới' }).click(); await expect(drawer.getByText('Tên mới trên server')).toBeVisible();
  await expect(drawer.getByLabel('Họ tên', { exact: true })).toHaveValue('Tên đang nhập'); await drawer.getByRole('button', { name: 'Giữ bản nhập để lưu lại' }).click();
  await saveDrawer(drawer); await expect(drawer).not.toBeVisible(); expect(calls.at(-1)!.body.expectedVersion).toBe(2);
});

test('deep link chọn người thân, giữ định danh chỉ đọc và retry slot không sửa danh sách', async ({ page }) => {
  const calls = await mock(page); await page.goto(`/customer/book?familyMemberId=${member.id}`);
  await expect(page.getByLabel('Người thân đã lưu')).toBeVisible(); await expect(page.getByText(/Con mẫu · Con/).first()).toBeVisible();
  await pickSchedule(page); await expect(page.getByLabel('Họ tên người khám')).toHaveValue(member.fullName); await expect(page.getByLabel('Ngày sinh người khám (không bắt buộc)')).toHaveValue(member.birthDate);
  await expect(page.getByLabel('Họ tên người đặt')).toHaveValue(user.fullName); await expect(page.getByLabel('Số điện thoại Việt Nam hoặc email')).toHaveValue(user.phoneNumber);
  await expect(page.getByLabel('Họ tên người khám')).toHaveJSProperty('readOnly', true); await expect(page.getByLabel('Ngày sinh người khám (không bắt buộc)')).toHaveJSProperty('readOnly', true);
  await page.getByRole('button', { name: 'Xem lại', exact: true }).click(); await expect(page.getByText('12/03/2015', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Sửa thông tin' }).click(); await expect(page.getByLabel('Họ tên người khám')).toHaveValue(member.fullName); await page.getByRole('button', { name: 'Xem lại', exact: true }).click();
  await page.getByRole('button', { name: 'Xác nhận đặt lịch', exact: true }).click(); await expect(page.getByText(/MSG25:/)).toBeVisible();
  await page.locator('.customer-slots button').first().click(); await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click(); await expect(page.getByLabel('Họ tên người khám')).toHaveValue(member.fullName);
  await page.getByRole('button', { name: 'Xem lại', exact: true }).click(); await page.getByRole('button', { name: 'Xác nhận đặt lịch', exact: true }).click(); await expect(page.getByText('AP-FAMILY', { exact: true })).toBeVisible();
  expect(calls).toHaveLength(2); expect(calls[1].body.attendee).toEqual({ patientId: null, name: member.fullName, birthDate: '2015-03-12' }); expect(calls[1].body.attendeeRelation).toBe('Relative'); expect(calls[1].body.familyMemberId).toBe(member.id);
});

test('booking chỉ chọn người thân đã lưu, không nhập/thêm tại chỗ; lỗi danh sách không chặn Self', async ({ page }) => {
  const calls = await mock(page, { listError: true, missing: true }); await page.goto(`/customer/book?familyMemberId=${member.id}`);
  await expect(page.getByText('Người thân đã xóa hoặc không còn khả dụng. Hãy chọn lại người thân đã lưu.')).toBeVisible(); await expect(page.getByText('Danh sách tạm thời không tải được.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Thêm người thân', exact: true })).toHaveCount(0);
  await expect(page.getByText('Nhập người thân khác', { exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Tiếp tục', exact: true })).toBeDisabled();
  await page.locator('.customer-relation-option').filter({ hasText: 'Tôi là người đến khám' }).click();
  await pickSchedule(page); await expect(page.getByLabel('Họ tên người khám')).toHaveValue(user.fullName);
  await expect(page.getByLabel('Ngày sinh người khám (không bắt buộc)')).toHaveCount(0); expect(calls).toHaveLength(0);
});

test('phân trang và tìm kiếm đúng danh sách', async ({ page }) => {
  await mock(page, { many: true }); await page.goto('/customer/family-members'); await expect(page.getByRole('row').filter({ hasText: /^Người thân 21/ })).toHaveCount(0);
  await page.locator('.ant-pagination-next button').click(); await expect(page.getByRole('row').filter({ hasText: 'Người thân 21' })).toBeVisible();
  await page.getByLabel('Tìm theo họ tên').fill('Người thân 3'); await page.getByRole('button', { name: 'Tìm kiếm' }).click(); await expect(page.getByRole('row').filter({ hasText: 'Người thân 3' })).toBeVisible();
});

test('URL người thân yêu cầu phiên; validation và dirty drawer không ghi khi hủy', async ({ page }) => {
  const calls = await mock(page); await page.goto('/customer/family-members'); await page.getByRole('button', { name: 'Thêm người thân', exact: true }).click(); const drawer = page.getByRole('dialog');
  await saveDrawer(drawer); await expect(drawer.getByText('Nhập họ tên người thân.')).toBeVisible(); await expect(drawer.getByText('Chọn giới tính.', { exact: true })).toBeVisible(); await expect(drawer.getByText('Nhập số điện thoại liên hệ.', { exact: true })).toBeVisible(); expect(calls).toHaveLength(0);
  await drawer.getByLabel('Họ tên', { exact: true }).fill('Chưa lưu'); page.once('dialog', dialog => dialog.dismiss()); await drawer.getByRole('button', { name: 'Hủy', exact: true }).click(); await expect(drawer).toBeVisible();
  await drawer.getByLabel('Quan hệ', { exact: true }).click(); await page.getByText('Con', { exact: true }).last().click();
  await drawer.getByLabel('Số điện thoại liên hệ').fill('abc'); await saveDrawer(drawer); await expect(drawer.getByText('Nhập số di động Việt Nam gồm 10 chữ số.')).toBeVisible(); expect(calls).toHaveLength(0);
  page.once('dialog', dialog => dialog.accept()); await drawer.getByRole('button', { name: 'Hủy', exact: true }).click(); await expect(drawer).not.toBeVisible(); expect(calls).toHaveLength(0);
  const anonymous = await page.context().newPage(); await anonymous.goto('/customer/family-members'); await expect(anonymous).toHaveURL(/\/customer\/sign-in$/); await anonymous.close();
});

test('dropdown người thân tải thêm trang và tìm kiếm phía server', async ({ page }) => {
  const calls = await mock(page, { many: true }); await page.goto('/customer/book');
  await page.locator('.customer-relation-option').filter({ hasText: 'Tôi đặt lịch cho một người khác' }).click();
  await expect(page.getByRole('button', { name: 'Tiếp tục', exact: true })).toBeDisabled();
  await page.getByLabel('Người thân đã lưu').focus(); await page.getByLabel('Người thân đã lưu').press('ArrowDown');
  await expect(page.getByText('Người thân 1 · Con · 12/03/2015', { exact: true }).last()).toBeVisible();
  await expect(page.locator('.customer-family-picker .ant-select')).not.toHaveClass(/ant-select-loading/);
  await page.locator('.ant-select-dropdown:visible .rc-virtual-list-holder').evaluate(element => { element.scrollTop = element.scrollHeight; element.dispatchEvent(new Event('scroll', { bubbles: true })); });
  await expect.poll(() => calls.listReads.some(q => new URLSearchParams(q).get('page') === '2')).toBe(true);
  await page.getByLabel('Người thân đã lưu').fill('Người thân 21');
  await expect.poll(() => calls.listReads.some(q => new URLSearchParams(q).get('keyword') === 'Người thân 21')).toBe(true);
  await page.getByText('Người thân 21 · Con · 12/03/2015', { exact: true }).last().click();
  await expect(page.getByRole('button', { name: 'Tiếp tục', exact: true })).toBeEnabled();
  await pickSchedule(page); await expect(page.getByLabel('Họ tên người khám')).toHaveValue('Người thân 21');
});
