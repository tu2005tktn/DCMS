import { test, expect, type Page } from '@playwright/test';

type Purchase = { id: string; enteredOn: string; receivedOn: string | null; materialId: string; materialName: string; categoryName: string; quantity: number; unit: string;
  unitPriceVnd: number; totalVnd: number; paidVnd: number; dueVnd: number; version: number; isDeleted?: boolean };

async function prepare(page: Page) {
  const user = { id: 7, userName: 'synthetic', fullName: 'SYNTHETIC Dentist', accountType: 'Staff', roles: ['Dentist'], effectiveRoles: ['Dentist'],
    permissions: ['settings.read', 'catalog.materials.read', 'catalog.materials.create', 'catalog.materials.update', 'catalog.materials.deactivate'] };
  const materials = [{ id: 'm-1', name: 'Implant SYNTHETIC', categoryName: 'Implant', version: 1 }, { id: 'm-2', name: 'Composite SYNTHETIC', categoryName: 'Trám', version: 1 }];
  const state = { purchases: [] as Purchase[], requests: [] as any[], failNetwork: false };
  const live = () => state.purchases.filter(p => !p.isDeleted);
  await page.route('**/api/**', async route => {
    const r = route.request(), url = new URL(r.url()), path = url.pathname, method = r.method();
    const ok = (json: unknown) => route.fulfill({ json });
    if (path === '/api/auth/sign-in') return ok({ accessToken: 'synthetic', user });
    if (path === '/api/auth/me') return ok(user);
    if (path === '/api/system-settings/settings/capabilities') return ok({ sections: [{ key: 'materials', label: 'Dụng cụ / Vật liệu' }] });
    if (path === '/api/system-settings/materials') return ok({ items: materials, total: materials.length, page: 1, pageSize: 100 });
    if (path === '/api/system-settings/material-categories') return ok({ items: [], total: 0, page: 1, pageSize: 100 });
    const id = path.split('/')[4];
    if (!path.startsWith('/api/system-settings/material-purchases')) return route.fulfill({ status: 404, json: { code: 'NOT_MOCKED', message: path } });
    if (method === 'GET') {
      if (id) { const row = live().find(x => x.id === id); return row ? ok(row) : route.fulfill({ status: 404, json: { code: 'MSG113', message: 'Không tìm thấy.' } }); }
      const q = (url.searchParams.get('q') ?? '').toLowerCase(), pay = url.searchParams.get('payment');
      const items = live().filter(p => p.materialName.toLowerCase().includes(q) && (!pay || (pay === 'paid' ? p.dueVnd <= 0 : pay === 'unpaid' ? p.paidVnd === 0 : p.paidVnd > 0 && p.dueVnd > 0)));
      return ok({ items, total: items.length, page: 1, pageSize: 20, totals: { totalVnd: items.reduce((s, p) => s + p.totalVnd, 0), paidVnd: items.reduce((s, p) => s + p.paidVnd, 0), dueVnd: items.reduce((s, p) => s + p.dueVnd, 0) } });
    }
    const body = r.postDataJSON(); state.requests.push({ method, id, ...body });
    if (state.failNetwork) { state.failNetwork = false; return route.abort('failed'); }
    const row = live().find(x => x.id === id);
    if (method === 'DELETE') { row!.isDeleted = true; return ok({}); }
    const material = materials.find(m => m.id === body.materialId)!, total = Math.round(body.quantity * body.unitPriceVnd);
    if (body.paidVnd > total) return route.fulfill({ status: 400, json: { code: 'PURCHASE_PAID_EXCEEDS_TOTAL', message: 'Số đã thanh toán không được lớn hơn Tổng.', field: 'paidVnd' } });
    const next = { enteredOn: body.enteredOn, receivedOn: body.receivedOn, materialId: material.id, materialName: material.name, categoryName: material.categoryName, quantity: body.quantity,
      unit: body.unit, unitPriceVnd: body.unitPriceVnd, totalVnd: total, paidVnd: body.paidVnd, dueVnd: total - body.paidVnd };
    if (method === 'POST') { const created = { id: crypto.randomUUID(), version: 1, ...next }; state.purchases.push(created); return ok(created); }
    Object.assign(row!, next, { version: row!.version + 1 }); return ok(row);
  });
  await page.goto('/sign-in'); await page.getByLabel('Tên đăng nhập', { exact: true }).fill('synthetic'); await page.getByLabel('Mật khẩu', { exact: true }).fill('synthetic');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click(); await page.goto('/system-settings');
  await page.goto('/materials');
  return state;
}

test('thêm, sửa, lọc và xóa lần nhập hàng; tổng và còn nợ tính đúng', async ({ page }) => {
  const state = await prepare(page);
  await expect(page.getByText('Chưa có lần nhập hàng nào.')).toBeVisible();
  await page.getByRole('button', { name: 'Thêm mới', exact: true }).click();
  await page.getByRole('combobox', { name: 'Dụng cụ/Vật liệu', exact: true }).click();
  await page.locator('.ant-select-dropdown:visible').getByText('Implant SYNTHETIC', { exact: true }).click();
  await page.getByLabel('Số lượng', { exact: true }).fill('2');
  await page.getByRole('combobox', { name: 'Đơn vị', exact: true }).fill('cái');
  await page.getByLabel('Đơn giá', { exact: true }).fill('1500000');
  await page.getByLabel('Đã thanh toán', { exact: true }).fill('4000000');
  await expect(page.getByText('Số đã thanh toán lớn hơn Tổng.')).toBeVisible();
  await page.getByLabel('Đã thanh toán', { exact: true }).fill('1000000');
  await expect(page.getByText('Còn nợ: 2.000.000 ₫')).toBeVisible();
  await page.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Sửa nhập hàng Implant SYNTHETIC' })).toBeVisible();
  expect(state.purchases[0]).toMatchObject({ totalVnd: 3_000_000, paidVnd: 1_000_000, dueVnd: 2_000_000, unit: 'cái' });
  await expect(page.locator('.ant-table').getByText('Chưa nhận', { exact: true })).toBeVisible();
  await expect(page.getByText('Còn nợ', { exact: true }).first()).toBeVisible();

  await page.getByRole('button', { name: 'Sửa nhập hàng Implant SYNTHETIC' }).click();
  await page.getByLabel('Đã thanh toán', { exact: true }).fill('3000000');
  await page.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(page.getByText('Đã thanh toán đủ').first()).toBeVisible();

  await page.getByRole('button', { name: 'Xóa nhập hàng Implant SYNTHETIC' }).click();
  await page.getByRole('button', { name: 'Không đồng ý', exact: true }).click();
  expect(state.purchases[0].isDeleted).toBeFalsy();
  await page.getByRole('button', { name: 'Xóa nhập hàng Implant SYNTHETIC' }).click();
  await page.getByRole('button', { name: 'Đồng ý', exact: true }).click();
  await expect(page.getByText('Chưa có lần nhập hàng nào.')).toBeVisible();
});

test('mất mạng giữ form và gửi lại cùng requestKey', async ({ page }) => {
  const state = await prepare(page);
  await page.getByRole('button', { name: 'Thêm mới', exact: true }).click();
  await page.getByRole('combobox', { name: 'Dụng cụ/Vật liệu', exact: true }).click();
  await page.locator('.ant-select-dropdown:visible').getByText('Composite SYNTHETIC', { exact: true }).click();
  await page.getByRole('combobox', { name: 'Đơn vị', exact: true }).fill('tuýp');
  await page.getByLabel('Đơn giá', { exact: true }).fill('200000');
  state.failNetwork = true;
  await page.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(page.getByLabel('Đơn giá', { exact: true })).toHaveValue('200000');
  await page.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Sửa nhập hàng Composite SYNTHETIC' })).toBeVisible();
  expect(state.requests).toHaveLength(2);
  expect(state.requests[0].requestKey).toBe(state.requests[1].requestKey);
  expect(state.purchases).toHaveLength(1);
});
