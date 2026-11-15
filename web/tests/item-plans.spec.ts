import { test, expect, type Page } from '@playwright/test';
const root = '/api/treatment-templates/item-plans';
const planId = '11111111-1111-4111-8111-111111111111';
const initialRow = { id: '22222222-2222-4222-8222-222222222222', position: 1, date: null, teeth: ['25', '26', '46'],
  serviceId: 3, serviceName: 'Trám răng', estimatedMinutes: 25, note: 'Ghi chú cũ', dentistId: 2, dentistName: 'Bác sĩ mẫu', assistantId: null, assistantName: null, costVnd: 600000 };
async function mock(page: Page, config: { existing?: boolean; error?: boolean; stale?: boolean; forbidden?: boolean; listError?: boolean } = {}) {
  let plan: any = config.existing ? { id: planId, name: 'Kế hoạch mẫu', branchId: 1, version: 1, items: [{ ...initialRow }] } : null;
  let failed = false;
  let staleTriggered = false;
  const calls: { method: string; path: string; body: any }[] = [];
  const dto = () => plan ? { ...plan, itemCount: plan.items.length, totalCostVnd: plan.items.reduce((sum: number, row: any) => sum + row.costVnd, 0) } : null;
  await page.addInitScript(() => sessionStorage.setItem('dcms.web.access-token', 'synthetic'));
  await page.route('**/api/**', async route => {
    const req = route.request(), url = new URL(req.url()), path = url.pathname, method = req.method();
    if (path === '/api/auth/me') return route.fulfill({ json: { id: 1, fullName: 'Nha sĩ mẫu', userName: 'synthetic', accountType: 'Staff', roles: config.forbidden ? ['ClinicAssistant'] : ['Dentist'], effectiveRoles: config.forbidden ? ['ClinicAssistant'] : ['Dentist', 'ClinicOwner'], permissions: config.forbidden ? ['patients.read'] : ['patients.read', 'treatment.templates.read', 'treatment.templates.manage'] } });
    if (path === '/api/auth/session/idle') return route.fulfill({ json: { deadlineAt: new Date(Date.now() + 600000).toISOString(), serverNow: new Date().toISOString(), activityVersion: 1 } });
    if (path === '/api/appointments/options') return route.fulfill({ json: { branches: [{ id: 1, name: 'Chi nhánh A' }, { id: 2, name: 'Chi nhánh B' }], services: [], employees: [] } });
    if (path === '/api/treatment-templates') return route.fulfill({ json: [] });
    if (path === `${root}/options`) return route.fulfill({ json: { services: [{ id: 3, name: 'Trám răng', referenceUnitPriceVnd: 600000 }], members: [{ id: 2, name: 'Bác sĩ mẫu', canDentist: true, canAssistant: false }, { id: 4, name: 'Phụ tá mẫu', canDentist: false, canAssistant: true }] } });
    if (path.startsWith(root)) {
      if (method === 'GET') {
        if (path === root) {
          if (config.listError) return route.fulfill({ status: 503, json: { code: 'MSG144', message: 'Chưa tải được kế hoạch.' } });
          const value = dto(), keyword = url.searchParams.get('keyword')?.toLowerCase() ?? '';
          const items = value && value.name.toLowerCase().includes(keyword) && value.branchId === Number(url.searchParams.get('branchId')) ? [value] : [];
          return route.fulfill({ json: { items, total: items.length, page: 1, pageSize: 20 } });
        }
        return route.fulfill({ json: staleTriggered ? { ...dto(), name: 'Tên mới trên máy chủ', version: 5 } : dto() });
      }
      const body = req.postDataJSON(); calls.push({ method, path, body });
      if (config.error && !failed) { failed = true; return route.fulfill({ status: 503, json: { code: 'MSG144', message: 'Lưu chưa thành công.' } }); }
      if (config.stale && body.expectedVersion !== 5) { staleTriggered = true; return route.fulfill({ status: 409, json: { code: 'MSG111', message: 'Dữ liệu đã thay đổi.' } }); }
      if (path === root) plan = { id: planId, branchId: body.branchId, name: body.name, version: 1, items: [] };
      else if (method === 'DELETE' && !path.includes('/items/')) { plan = null; return route.fulfill({ json: { id: planId, version: 2, deleted: true } }); }
      else {
        plan.version = (body.expectedVersion ?? plan.version) + 1;
        if (path.includes('/items')) {
          if (method === 'DELETE') plan.items = plan.items.filter((x: any) => !path.endsWith(x.id));
          else {
            const row = { ...initialRow, ...body, id: method === 'POST' ? crypto.randomUUID() : path.split('/').at(-1), serviceName: 'Trám răng', dentistName: body.dentistId ? 'Bác sĩ mẫu' : null, assistantName: body.assistantId ? 'Phụ tá mẫu' : null };
            if (method === 'POST') plan.items.push(row); else plan.items = plan.items.map((x: any) => x.id === row.id ? row : x);
          }
        } else plan.name = body.name;
      }
      return route.fulfill({ json: dto() });
    }
    return route.fulfill({ json: {} });
  });
  return calls;
}
async function open(page: Page) { await page.goto('/treatment-templates'); await page.getByRole('button', { name: 'Mở', exact: true }).click(); }
async function fillRow(page: Page) {
  const row = page.getByRole('row', { name: 'Thêm hạng mục', exact: true });
  await row.getByLabel('Răng', { exact: true }).fill('25, 26, 46');
  await row.getByRole('combobox', { name: 'Điều trị/dịch vụ', exact: true }).click(); await page.getByText('Trám răng', { exact: true }).last().click();
  await row.getByRole('combobox', { name: 'Bác sĩ', exact: true }).click(); await page.getByText('Bác sĩ mẫu', { exact: true }).last().click();
  await row.getByLabel('Thời gian dự kiến (phút)').fill('25'); await row.getByLabel('Ghi chú', { exact: true }).fill('Nhập tay');
  return row;
}
test('tạo tên trước, CRUD hàng và tên; xóa xác nhận; không autosave', async ({ page }) => {
  const calls = await mock(page); await page.goto('/treatment-templates');
  await page.getByRole('button', { name: 'Tạo kế hoạch', exact: true }).click(); const modal = page.getByRole('dialog', { name: 'Tạo kế hoạch điều trị' });
  await modal.getByLabel('Tên kế hoạch', { exact: true }).fill('Kế hoạch mới'); expect(calls).toHaveLength(0);
  await modal.getByRole('button', { name: 'Lưu', exact: true }).click(); await expect(modal).not.toBeVisible();
  await expect(page.getByText('Chưa có hạng mục. Bấm Thêm hạng mục để nhập.')).toBeVisible();
  await page.getByRole('button', { name: '+ Thêm hạng mục', exact: true }).click(); const row = await fillRow(page);
  await expect(row.getByLabel('Chi phí', { exact: true })).toHaveValue('600000'); expect(calls).toHaveLength(1);
  await row.getByRole('button', { name: 'Đồng ý', exact: true }).click(); await expect(page.locator('.item-plan-table tbody')).toContainText('25, 26, 46');
  expect(calls[1].body.costVnd).toBe(600000); expect(calls[1].body.date).toBeNull(); expect(calls[1].body.teeth).toEqual(['25', '26', '46']);
  expect(calls[1].body).not.toHaveProperty('quantity'); expect(calls[1].body).not.toHaveProperty('discountValue');
  await page.reload(); await page.getByRole('button', { name: 'Mở', exact: true }).click();
  await page.locator('.item-plan-table').getByRole('button', { name: 'Sửa', exact: true }).click();
  const edit = page.getByRole('row', { name: 'Sửa hạng mục' }); await expect(edit.getByLabel('Chi phí', { exact: true })).toHaveValue('600000');
  await edit.getByLabel('Ghi chú', { exact: true }).fill('Đã sửa'); await edit.getByRole('button', { name: 'Đồng ý', exact: true }).click();
  await expect(page.locator('.item-plan-table')).toContainText('Đã sửa');
  await page.getByRole('button', { name: 'Sửa tên', exact: true }).click(); const rename = page.getByRole('dialog', { name: 'Sửa tên kế hoạch' }); await rename.getByLabel('Tên kế hoạch', { exact: true }).fill('Đổi tên'); await rename.getByRole('button', { name: 'Lưu', exact: true }).click();
  await page.locator('.item-plan-table').getByRole('button', { name: 'Xóa', exact: true }).click(); await page.getByRole('button', { name: 'Không đồng ý', exact: true }).click(); await expect(page.locator('.item-plan-table')).toContainText('Đã sửa');
  await page.locator('.item-plan-table').getByRole('button', { name: 'Xóa', exact: true }).click(); await page.getByRole('button', { name: 'Đồng ý', exact: true }).click(); await expect(page.getByText('Chưa có hạng mục. Bấm Thêm hạng mục để nhập.')).toBeVisible();
  await page.getByRole('button', { name: 'Xóa kế hoạch', exact: true }).click(); await page.getByRole('button', { name: 'Đồng ý', exact: true }).click(); await expect(page.getByText('Chưa có kế hoạch điều trị.', { exact: true })).toBeVisible();
  expect(calls.every(x => x.path.startsWith(root))).toBe(true);
});
test('validation không làm tròn, hủy giữ dữ liệu đã lưu và không phát sinh write', async ({ page }) => {
  const calls = await mock(page, { existing: true }); await open(page);
  await page.getByRole('button', { name: '+ Thêm hạng mục', exact: true }).click(); const row = await fillRow(page);
  await row.getByLabel('Răng', { exact: true }).fill('99'); await row.getByLabel('Thời gian dự kiến (phút)').fill('25.5');
  await row.getByRole('button', { name: 'Đồng ý', exact: true }).click(); await expect(row.getByRole('alert')).toContainText('Nhập mã răng FDI'); await expect(row.getByRole('alert')).toContainText('số phút nguyên');
  await expect(row.getByLabel('Thời gian dự kiến (phút)')).toHaveValue('25.5'); expect(calls).toHaveLength(0);
  page.once('dialog', d => d.dismiss()); await row.getByRole('button', { name: 'Hủy sửa hạng mục' }).click(); await expect(row).toBeVisible();
  page.once('dialog', d => d.accept()); await row.getByRole('button', { name: 'Hủy sửa hạng mục' }).click(); await expect(row).not.toBeVisible(); expect(calls).toHaveLength(0);
});
test('bác sĩ, phụ tá và ghi chú được để trống hoặc bỏ chọn khi sửa', async ({ page }) => {
  const calls = await mock(page, { existing: true }); await open(page);
  await page.getByRole('button', { name: '+ Thêm hạng mục', exact: true }).click();
  let row = page.getByRole('row', { name: 'Thêm hạng mục', exact: true });
  await row.getByRole('combobox', { name: 'Điều trị/dịch vụ', exact: true }).click(); await page.getByText('Trám răng', { exact: true }).last().click();
  await row.getByRole('button', { name: 'Đồng ý', exact: true }).click(); await expect(row).not.toBeVisible();
  expect(calls[0].body.dentistId).toBeNull(); expect(calls[0].body.assistantId).toBeNull(); expect(calls[0].body.note).toBeNull();
  await page.reload(); await page.getByRole('button', { name: 'Mở', exact: true }).click();
  const savedRow = page.locator('.item-plan-table tbody tr').last(); await expect(savedRow).not.toContainText('Bác sĩ mẫu');
  await page.locator('.item-plan-table tbody tr').first().getByRole('button', { name: 'Sửa', exact: true }).click();
  row = page.getByRole('row', { name: 'Sửa hạng mục', exact: true });
  await row.locator('.ant-select').nth(1).hover(); await row.locator('.ant-select').nth(1).locator('.ant-select-clear').click();
  await row.getByLabel('Ghi chú', { exact: true }).fill(''); await row.getByRole('button', { name: 'Đồng ý', exact: true }).click();
  expect(calls.at(-1)!.body.dentistId).toBeNull(); expect(calls.at(-1)!.body.note).toBeNull();
});
test('lỗi mạng giữ bản nhập và retry cùng requestKey', async ({ page }) => {
  const calls = await mock(page, { existing: true, error: true }); await open(page);
  await page.locator('.item-plan-table').getByRole('button', { name: 'Sửa', exact: true }).click(); const row = page.getByRole('row', { name: 'Sửa hạng mục' });
  await row.getByLabel('Ghi chú', { exact: true }).fill('Giữ bản nhập'); await row.getByRole('button', { name: 'Đồng ý', exact: true }).click();
  await expect(page.getByText('MSG144: Lưu chưa thành công.')).toBeVisible(); await expect(row.getByLabel('Ghi chú', { exact: true })).toHaveValue('Giữ bản nhập');
  await row.getByRole('button', { name: 'Đồng ý', exact: true }).click(); await expect(row).not.toBeVisible(); expect(calls[0].body.requestKey).toBe(calls[1].body.requestKey);
});
test('MSG111 đọc so sánh rồi giữ bản nhập với version mới', async ({ page }) => {
  const calls = await mock(page, { existing: true, stale: true }); await open(page);
  await page.locator('.item-plan-table').getByRole('button', { name: 'Sửa', exact: true }).click(); const row = page.getByRole('row', { name: 'Sửa hạng mục' });
  await row.getByLabel('Ghi chú', { exact: true }).fill('Phiên bản đang nhập'); await row.getByRole('button', { name: 'Đồng ý', exact: true }).click();
  await page.getByRole('button', { name: 'Xem phiên bản mới' }).click(); await expect(page.getByText(/Phiên bản mới: Tên mới trên máy chủ/)).toBeVisible();
  await page.getByRole('button', { name: 'Giữ bản nhập để lưu lại' }).click(); await expect(row.getByLabel('Ghi chú', { exact: true })).toHaveValue('Phiên bản đang nhập');
  await row.getByRole('button', { name: 'Đồng ý', exact: true }).click(); expect(calls.at(-1)!.body.expectedVersion).toBe(5);
});
test('responsive, bàn phím, đổi chi nhánh dirty và lối vào mẫu cũ', async ({ page }) => {
  let discard = false;
  page.on('dialog', dialog => discard ? dialog.accept() : dialog.dismiss());
  await mock(page, { existing: true }); await open(page);
  for (const width of [390, 768, 1000, 1440]) {
    await page.setViewportSize({ width, height: 1000 });
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width + 1);
    await expect.poll(() => page.evaluate(() => { const w = document.querySelector('.item-plan-list .ant-table') as HTMLElement; const c = w.querySelector('.ant-table-content') as HTMLElement; return Math.max(0, c.scrollWidth - c.clientWidth); })).toBeLessThanOrEqual(1);
    // The plan table must show every field without its own horizontal scrollbar.
    await expect.poll(() => page.evaluate(() => { const t = document.querySelector('.item-plan-table') as HTMLElement; const c = t.parentElement as HTMLElement; return Math.max(t.scrollWidth - c.clientWidth, c.scrollWidth - c.clientWidth); })).toBeLessThanOrEqual(1);
    await page.screenshot({ path: `../.artifacts/item-plans-${width}.png`, fullPage: true });
  }
  await page.getByRole('button', { name: '+ Thêm hạng mục', exact: true }).click(); const row = await fillRow(page);
  await page.getByRole('combobox', { name: 'Chi nhánh', exact: true }).focus(); await page.keyboard.press('ArrowDown'); await page.getByText('Chi nhánh B', { exact: true }).click(); await expect(row).toBeVisible();

  discard = true; await row.getByRole('button', { name: 'Hủy sửa hạng mục' }).click();
  await expect(page.getByRole('link', { name: 'Mẫu tiến trình cũ', exact: true })).toHaveCount(0); await page.goto('/treatment-templates/legacy'); await expect(page).toHaveURL(/\/treatment-templates$/);
});
test('role khác bị chặn URL', async ({ page }) => {
  await mock(page, { forbidden: true }); await page.goto('/treatment-templates'); await expect(page.getByText('Bạn không có quyền thực hiện thao tác này.')).toBeVisible();
});
test('lỗi tải có thử lại và không hiển thị trống thành công', async ({ page }) => {
  const config = { existing: true, listError: true }; await mock(page, config); await page.goto('/treatment-templates');
  await expect(page.getByText('Chưa tải được kế hoạch.', { exact: true })).toBeVisible();
  await expect(page.getByText('Chưa có kế hoạch điều trị.', { exact: true })).toHaveCount(0);
  config.listError = false; await page.getByRole('button', { name: 'Thử lại', exact: true }).click(); await expect(page.getByRole('button', { name: 'Mở', exact: true })).toBeVisible();
});
