import { expect, test, type Page } from '@playwright/test';
const dossierId = '11111111-1111-4111-8111-111111111111';
const itemId = '22222222-2222-4222-8222-222222222222';
const visitId = '33333333-3333-4333-8333-333333333333';
const secondVisit = '44444444-4444-4444-8444-444444444444';
const patient = { id: 123, patientCode: 'SYNTHETIC-123', fullName: 'Bệnh nhân thử nghiệm', birthDate: null,
  gender: null, phone: null, address: null, guardianName: null, guardianRelationship: null, guardianPhone: null,
  note: null, profileDate: null, source: null, customerGroup: null, occupation: null, intakeHistory: [],
  isArchived: false, version: 1, createdAt: '2026-10-27T01:00:00Z', updatedAt: '2026-10-27T01:00:00Z',
  link: { verifiedAccountUserName: null, verifiedAt: null } };
const item = { id: itemId, serviceId: 3, serviceName: 'Trám răng', region: '25, 26', unit: 'răng', quantity: 2,
  agreedUnitPriceVnd: 300000, decision: 'Agreed', progress: 'NotStarted', estimatedStart: null,
  material: null, note: 'Ghi chú kế hoạch', discount: null, responsibleDentistId: 7 };
async function prepare(page: Page, withItem = true, withFinance = false) {
  const user = { id: 7, userName: 'synthetic.dentist', fullName: 'Bác sĩ thử nghiệm', accountType: 'Staff',
    roles: ['Dentist'], effectiveRoles: ['Dentist'], permissions: ['patients.read'] };
  const state = { version: 1, items: (withItem ? [{ ...item }] : []) as any[], drafts: new Map<string, any>(),
    activities: [] as any[], activityEdits: new Map<string, any>(), conflict: false, notes: '', collection: null as any, accountVersion: 1, requests: [] as any[], materialReads: [] as {q:string;page:number}[], failMaterialOnce: false,
    activities: [] as any[], activityEdits: new Map<string, any>(), conflict: false, notes: '', payments: [] as any[], requests: [] as any[], materialReads: [] as {q:string;page:number}[], failMaterialOnce: false,
    materials: [
      { id:'mat-1', name:'Composite A2', categoryId:'group-1', categoryName:'Vật liệu trám', version:1 },
      { id:'mat-2', name:'Gương khám', categoryId:'group-2', categoryName:'Dụng cụ khám', version:1 },
      { id:'mat-3', name:'Composite A2', categoryId:'group-3', categoryName:'Vật liệu khác', version:1 },
      ...Array.from({length:22},(_,i)=>({id:`extra-${i}`,name:`Dụng cụ ${String(i+1).padStart(2,'0')}`,categoryId:'group-2',categoryName:'Dụng cụ khám',version:1}))
    ] };
  const detail = () => ({ id: dossierId, code: 'DT-001', patientId: 123, branchId: 1, dentistId: 7,
    state: 'Open', version: state.version, planVersion: 1, planState: 'Committed', items: state.items,
    paymentTerms: [], timeline: [], createdAt: '2026-10-27T01:00:00Z', updatedAt: '2026-10-27T01:00:00Z',
    note: state.notes, financialSummary: { available: withFinance, plannedVnd: 600000,
      postedChargeVnd: withFinance ? 600000 : null, allocatedVnd: withFinance ? 0 : null, outstandingVnd: withFinance ? 600000 : null } });
  await page.route('**/api/**', async route => {
    const r = route.request(), url = new URL(r.url()), path = url.pathname;
    const ok = (json: unknown) => route.fulfill({ json });
    if (path === '/api/auth/me') return ok(user);
    if (path === '/api/auth/sign-in') return ok({ accessToken: 'synthetic', user });
    if (path === '/api/system-settings/materials') {
      const q = url.searchParams.get('q') ?? '', number = Number(url.searchParams.get('page') ?? 1);
      state.materialReads.push({q,page:number});
      if (state.failMaterialOnce) { state.failMaterialOnce = false; return route.fulfill({status:503,json:{code:'MSG144',message:'Không thể tải danh mục thử nghiệm.'}}); }
      const items = state.materials.filter(m=>m.name.toLowerCase().includes(q.toLowerCase()));
      return ok({items:items.slice((number-1)*20,number*20),total:items.length,page:number,pageSize:20});
    }
    if (path === '/api/patients/123') return ok(patient);
    if (path.endsWith('/dossiers/create-context')) return ok({dentistId:7,branches:[{id:1,name:'CN thử nghiệm'}]});
    if (path === '/api/appointments/options') return ok({ branches: [{ id: 1, name: 'CN thử nghiệm' }], dentists: [{ id: 7, branchId: 1, name: user.fullName }], services: [{ id: 3, branchId: 1, name: 'Trám răng' }] });
    if (path.endsWith('/dossiers/summary')) return ok({ dossierCount: 2, plannedNetVnd: 900000, financialAvailable: false, pendingFeeCount: state.activities.length });
    if (path === '/api/patients/123/dossiers') return ok({ items: [{ id: dossierId, code: 'DT-001', version: state.version, state: 'Open' }], total: 1, page: 1, pageSize: 20 });
    if (path.endsWith('/sheet-context')) return ok({ services: [{ id: 3, branchId: 1, name: 'Trám răng', unit: 'răng', referenceUnitPriceVnd: 300000, priceVersion: 1 }],
      members: [{ id: 7, name: user.fullName, canDentist: true }, { id: 8, name: 'Phụ tá thử nghiệm', canDentist: false }],
      visits: [], steps: [], allowedActions: ['Draft', 'Decide', 'Perform', 'Close'] });
    if (path.endsWith('/activities')) { const items = state.activities.map(a => ({ ...a, ...state.activityEdits.get(a.id) })).filter(a => !a.sheetDeleted); return ok({ items, total: items.length, page: 1, pageSize: 20 }); }
    if (path.endsWith('/sheet-drafts')) return ok([...state.drafts.values()]);
    if (/sheet-drafts\/[^/]+$/.test(path)) {
      const id = path.split('/').at(-1)!;
      if (r.method() === 'DELETE') { state.drafts.delete(id); return route.fulfill({ status: 204 }); }
      const data = r.postDataJSON(); state.requests.push(data);
      const v = { id, ...data, version: (state.drafts.get(id)?.version ?? 0) + 1 }; state.drafts.set(id, v); return ok(v);
    }
    if (path.endsWith('/confirm-plan')) {
      if (state.conflict) return route.fulfill({ status: 409, json: { code: 'MSG111', message: 'MSG111 — phiên bản đã thay đổi' } });
      const id = path.split('/').at(-2)!, draft = state.drafts.get(id); const body = r.postDataJSON(); state.requests.push(body);
      state.items.push({ ...item, id, serviceId: draft.row.serviceId, serviceName: draft.row.serviceName,
        region: draft.row.regions.join(', '), quantity: draft.row.quantity, decision: body.decision?.decision ?? 'Proposed',
        entrySource: 'ClinicianEntry', estimatedStart: draft.row.date ?? null, estimatedMinutes: draft.row.estimatedMinutes,
        note: draft.row.note, responsibleDentistId: draft.row.dentistId, assistantId: draft.row.assistantId,
        displayCompleted: false, progress: 'NotStarted', agreedUnitPriceVnd: draft.row.unitPriceVnd,
        discount: draft.row.discountValue > 0 ? { type: draft.row.discountType === 'Percent' ? 'Percent' : 'Fixed', value: draft.row.discountValue,
          reason: draft.row.discountNote, status: 'Approved', amountVnd: draft.row.discountType === 'Percent'
            ? Math.round(draft.row.costVnd * draft.row.discountValue / 100) : draft.row.discountValue } : null });
      state.drafts.delete(id); state.version++; return ok({ dossier: detail() });
    }
    if (path.endsWith('/confirm-actual')) {
      const id = path.split('/').at(-2)!, draft = state.drafts.get(id), body = r.postDataJSON(); state.requests.push(body);
      const activity = { id, visitId: body.visitId, dossierId, itemId: draft.row.sourceItemId ?? itemId, planVersion: 1, serviceId: draft.row.serviceId, region: draft.row.regions.join(', '), quantity: draft.row.quantity, version: 2, sheetVersion: 0,
        unit: 'răng', performerId: 7, performedAt: '2026-10-27T02:00:00Z', outcome: draft.row.note, labWorkSnapshot: draft.row.labWork, sourceKind: 'Dossier', serviceNameSnapshot: draft.row.serviceName, feeStatus: 'Pending', priceSnapshotVnd: draft.row.unitPriceVnd, discountSnapshotVnd: 0 };
      if (body.planTransfer) state.items.find(i => i.id === draft.row.sourceItemId).displayCompleted = true;
      state.activities.push(activity); state.drafts.delete(id); state.version++; return ok({ dossier: detail(), activity });
    }
    if (path.includes('/sheet-activities/')) {
      const id = path.split('/').at(-1)!, before = { ...state.activities.find(a => a.id === id), ...state.activityEdits.get(id) }, body = r.postDataJSON();
      state.requests.push({ ...body, command: 'activity', method: r.method() });
      const after = r.method() === 'DELETE' ? { ...before, sheetDeleted: true } : { ...before,
        outcome: body.row.note, region: body.row.regions.join(', '), serviceId: body.row.serviceId, serviceNameSnapshot: body.row.serviceName,
        priceSnapshotVnd: body.row.unitPriceVnd, labWorkSnapshot: body.row.labWork, sheetDiscountValue: body.row.discountValue,
        sheetDiscountType: body.row.discountType, sheetDiscountReason: body.row.discountNote };
      after.sheetVersion = (before.sheetVersion ?? 0)+1;
      state.activityEdits.set(id, after); state.version++; return ok({ dossier: detail(), activity: after });
    }
    if (path.includes('/sheet-items/')) {
      const parts = path.split('/'), id = parts[parts.indexOf('sheet-items')+1], i = state.items.find(x => x.id === id), body = r.postDataJSON();
      state.requests.push({ ...body, command: path.split('/').at(-1), method: r.method() });
      if (path.endsWith('/perform')) {
        if (!state.activities.some(a => a.itemId === id)) state.activities.push({ id: 'performed-1', itemId: id, dossierId, visitId: null, version: 2, sheetVersion: 0, sourceKind: 'Dossier', serviceNameSnapshot: body.row?.serviceName ?? i.serviceName,
          planVersion: 1, serviceId: body.row?.serviceId ?? i.serviceId, region: body.row?.regions.join(', ') ?? i.region, quantity: i.quantity, unit: i.unit, performerId: 7,
          performedAt: '2026-10-27T02:00:00Z', outcome: body.row?.note ?? i.note, feeStatus: 'Pending', priceSnapshotVnd: body.row?.unitPriceVnd ?? i.agreedUnitPriceVnd, discountSnapshotVnd: 0 });
        i.displayCompleted = true;
      } else if (path.endsWith('/display-completion')) i.displayCompleted = body.completed;
      else if (r.method() === 'DELETE') i.sheetDeleted = true;
      else Object.assign(i, { note: body.row.note, region: body.row.regions.join(', '), estimatedMinutes: body.row.estimatedMinutes, agreedUnitPriceVnd: body.row.unitPriceVnd });
      state.version++; return ok({ dossier: detail(), activity: state.activities.at(-1) });
    }
    if (path.endsWith('/close')) return route.fulfill({ status: 409, json: { code: 'MSG243', message: 'MSG243 — Còn hạng mục chưa được xử lý' } });
    if (path.endsWith('/note')) { state.notes = r.postDataJSON().note; state.version++; return ok({ dossier: detail() }); }
    if (path.endsWith('/finance')) return ok({ available: true, sourceVersion: String(state.accountVersion), asOf: '2026-10-27T02:00:00Z', allowedActions: ['Collect', 'Confirm', 'Cancel'], unusedAdvanceVnd: 0,
      charges: [{ id: 'charge-1', version: 1, sourceCode: 'A', postedAt: '2026-10-27T01:00:00Z', service: 'Trám răng', dentist: user.fullName, netVnd: 600000, confirmedAllocatedVnd: 0, dueVnd: 600000 }], receipts: state.collection ? [state.collection] : [] });
    if (path.endsWith('/collections')) { state.accountVersion++; state.collection = { ...r.postDataJSON(), id: 'collection-1', version: 1, code: 'PT-SYNTHETIC', collector: user.fullName, status: 'Pending', unusedAdvanceVnd: 0 };
      state.requests.push(state.collection); return ok(state.collection); }
    if (path === '/api/finance/payments/collection-1/confirm') { state.accountVersion++; state.collection = { ...state.collection, version: 2, status: 'Confirmed' }; return ok(state.collection); }
    if (path.endsWith('/payments/context')) {
      const lines = state.activities.map(a => { const cost = Math.round(a.quantity * a.priceSnapshotVnd), discount = a.discountSnapshotVnd ?? 0,
        paid = state.payments.flatMap(p => p.lines).filter(l => l.activityId === a.id).reduce((s, l) => s + l.amountVnd, 0);
        return { activityId: a.id, performedAt: a.performedAt, dentistName: user.fullName, serviceName: a.serviceNameSnapshot ?? 'Trám răng',
          costVnd: cost, discountVnd: discount, paidVnd: paid, dueVnd: Math.max(0, cost - discount - paid) }; });
      return ok({ paidVnd: lines.reduce((s, l) => s + l.paidVnd, 0), lines, collectors: [{ id: 7, name: user.fullName }], defaultCollectorId: 7 });
    }
    if (path.endsWith('/payments') && r.method() === 'POST') {
      const body = r.postDataJSON(); state.requests.push({ command: 'payment', ...body });
      const saved = { id: `payment-${state.payments.length + 1}`, paidOn: body.paidOn, method: body.method, collectorId: body.collectorId, collectorName: user.fullName, note: body.note,
        totalVnd: body.lines.reduce((s: number, l: any) => s + l.amountVnd, 0), createdAt: '2026-11-03T03:00:00Z', lines: body.lines.map((l: any) => ({ ...l, serviceName: 'Trám răng' })) };
      state.payments.push(saved); return ok(saved);
    }
    if (path.endsWith('/payments')) return ok([...state.payments].reverse());
    if (path === `/api/dossiers/${dossierId}`) return ok(detail());
    return route.fulfill({ status: 404, json: { code: 'NOT_MOCKED', message: `Unexpected mock path ${path}` } });
  });
  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill('synthetic'); await page.getByLabel('Mật khẩu', { exact: true }).fill('synthetic');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Đăng xuất', exact: true })).toBeVisible();
  await page.goto('/patients/123'); await page.getByRole('tab', { name: 'Điều trị & Thanh toán' }).click();
  await expect(page.locator('.staff-sidebar')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'KẾ HOẠCH ĐIỀU TRỊ/DỊCH VỤ' })).toBeVisible();
  return state;
}

test('ngày giảm dần, thiếu ngày cuối, sửa giữ vị trí và không đổi dữ liệu gốc', async ({page})=>{
  const state=await prepare(page);
  state.items.push({...item,id:'date-old',serviceName:'Dịch vụ cũ',estimatedStart:'2026-01-01',plannedOrder:2},{...item,id:'date-new',serviceName:'Dịch vụ mới',estimatedStart:'2026-11-02',plannedOrder:3});
  await page.reload();await page.getByRole('tab',{name:'Điều trị & Thanh toán'}).click();
  const rows=page.locator('.treatment-sheet-plan-table tbody tr');await expect(rows.nth(0)).toContainText('Dịch vụ mới');await expect(rows.nth(1)).toContainText('Dịch vụ cũ');await expect(rows.nth(2)).toContainText('Trám răng');
  await page.getByRole('button',{name:'Chức năng Dịch vụ cũ',exact:true}).click();await page.getByRole('menuitem',{name:'Sửa',exact:true}).click();
  await expect(rows.nth(1)).toHaveAttribute('aria-label','Nhập hạng mục kế hoạch');
  await page.getByLabel('Ngày dự kiến',{exact:true}).fill('2027-01-01');await expect(rows.nth(1)).toHaveAttribute('aria-label','Nhập hạng mục kế hoạch');
});
test('tạo phiếu nhanh không Modal hoặc chuyển trang, retry cùng key và mở đúng ID', async ({page})=>{
  await prepare(page);const id='99999999-9999-4999-8999-999999999999';const calls:any[]=[];let fail=true;
  const dossier={id,code:'DT-NEW',patientId:123,branchId:1,dentistId:7,state:'Open',version:1,planVersion:0,planState:'Empty',items:[],timeline:[],paymentTerms:[],note:null,createdAt:'2026-11-02T00:00:00Z'};
  await page.route('**/api/patients/123/dossiers/quick',async r=>{calls.push(r.request().postDataJSON());if(fail){fail=false;return r.fulfill({status:503,json:{code:'MSG144',message:'Lỗi tạo thử nghiệm'}});}return r.fulfill({json:{dossier}});});
  await page.route(`**/api/dossiers/${id}`,r=>r.fulfill({json:dossier}));
  await page.getByRole('button',{name:'Tạo phiếu mới',exact:true}).click();await expect(page.getByText('Lỗi tạo thử nghiệm',{exact:true})).toBeVisible();
  await page.getByRole('button',{name:'Tạo phiếu mới',exact:true}).click();
  await expect(page.locator(`#treatment-sheet-${id}`)).toBeVisible();await expect(page).toHaveURL(/\/patients\/123$/);await expect(page.getByRole('dialog',{name:'Tạo đợt điều trị'})).toHaveCount(0);
  expect(calls[0].requestKey).toBe(calls[1].requestKey);expect(calls[1]).not.toHaveProperty('diagnosis');expect(calls[1]).not.toHaveProperty('dentistId');
});

test('áp kế hoạch thêm cuối bảng, giữ ghi chú và retry cùng key', async ({ page }) => {
  const state = await prepare(page); let fail = true; const requests: any[] = [];
  const source = { id: '55555555-5555-4555-8555-555555555555', name: 'Kế hoạch nguồn', branchId: 1, version: 2, itemCount: 1, totalCostVnd: 1000000,
    items: [{ id: 'source-row', serviceId: 3, serviceName: 'Dịch vụ từ kế hoạch', teeth: ['25', '26'], date: null, estimatedMinutes: 25, note: null, dentistId: null, assistantId: null, costVnd: 1000000 }] };
  await page.route('**/api/treatment-templates/item-plans?*', r => r.fulfill({ json: { items: [source], total: 1, page: 1, pageSize: 20 } }));
  await page.route(`**/api/treatment-templates/item-plans/${source.id}`, r => r.fulfill({ json: source }));
  await page.route('**/sheet-items/from-item-plan', async r => {
    requests.push(r.request().postDataJSON());
    if (fail) { fail = false; return r.fulfill({ status: 503, json: { code: 'MSG144', message: 'Lỗi thử nghiệm nhập kế hoạch' } }); }
    state.items.push({ ...item, id: source.id, serviceName: source.items[0].serviceName, quantity: 1, agreedUnitPriceVnd: 1000000 }); state.version++;
    return r.fulfill({ json: { createdItemIds: [source.id] } });
  });
  await expect(page.getByRole('button', { name: 'Lịch sử phiếu', exact: true })).toHaveCount(0);
  await page.getByLabel('Ghi chú hồ sơ', { exact: true }).fill('Ghi chú chưa lưu cần giữ');
  await page.getByRole('button', { name: 'Kế hoạch điều trị', exact: true }).click();
  const modal = page.getByRole('dialog', { name: 'Chọn kế hoạch điều trị' });
  await modal.getByRole('combobox').fill('nguồn'); await page.getByText('Kế hoạch nguồn · 1 hạng mục', { exact: true }).click();
  await expect(modal.getByText('Dịch vụ từ kế hoạch', { exact: true })).toBeVisible();
  for (const width of [390, 768, 1440]) {
    await page.setViewportSize({ width, height: 1000 });
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width + 1);
    await page.screenshot({ path: `../.artifacts/import-item-plan-${width}.png`, fullPage: true });
  }
  await modal.getByRole('button', { name: 'Áp dụng', exact: true }).click(); await expect(modal.getByText('Lỗi thử nghiệm nhập kế hoạch')).toBeVisible();
  await modal.getByRole('button', { name: 'Áp dụng', exact: true }).click(); await expect(modal).not.toBeVisible();
  expect(requests[0].requestKey).toBe(requests[1].requestKey);
  await expect(page.locator('.treatment-sheet-plan-table')).toContainText('Dịch vụ từ kế hoạch'); await expect(page.locator('.treatment-sheet-plan-table')).toContainText('Trám răng');
  await expect(page.getByLabel('Ghi chú hồ sơ', { exact: true })).toHaveValue('Ghi chú chưa lưu cần giữ');
});

test('hàng chỉ có răng, thời gian và text; lưu một lần bấm, lỗi giữ form và không nhân nháp', async ({ page }) => {
  const state = await prepare(page, false);
  await page.getByRole('button', { name: 'Thêm hạng mục kế hoạch' }).click();
  await expect(page.getByLabel('Vùng điều trị', { exact: true })).toHaveCount(0);
  await expect(page.getByLabel('SL', { exact: true })).toHaveCount(0);
  await expect(page.getByLabel('Quyết định của bệnh nhân')).toHaveCount(0);
  await page.getByLabel('Răng', { exact: true }).fill('25, 26, 46');
  await page.getByRole('combobox', { name: 'Điều trị/dịch vụ', exact: true }).focus(); await page.keyboard.press('ArrowDown'); await page.keyboard.press('Enter');
  await page.getByLabel('Ghi chú', { exact: true }).fill('Kế hoạch thử nghiệm');
  await page.getByLabel('Thời gian dự kiến (phút)', { exact: true }).fill('25.5');
  await page.getByRole('button', { name: 'Đồng ý', exact: true }).click();
  await expect(page.getByText('Nhập số phút nguyên từ 1 đến 480.')).toBeVisible();
  expect(state.requests).toHaveLength(0);
  await page.getByLabel('Thời gian dự kiến (phút)', { exact: true }).fill('25');
  await page.setViewportSize({ width: 1440, height: 950 });
  await page.screenshot({ path: 'test-results/treatment-direct-entry-form.png', fullPage: true });
  await page.waitForTimeout(1000); expect(state.requests).toHaveLength(0);
  state.conflict = true; await page.getByRole('button', { name: 'Đồng ý', exact: true }).click();
  await expect(page.getByText('MSG111 — phiên bản đã thay đổi')).toBeVisible();
  await expect(page.getByLabel('Răng', { exact: true })).toHaveValue('25, 26, 46');
  expect([...state.drafts.values()][0].row.regions).toEqual(['25','26','46']);
  state.conflict = false; await page.getByRole('button', { name: 'Đồng ý', exact: true }).click();
  await expect.poll(() => state.items.length).toBe(1);
  expect(state.requests.filter(r => r.kind === 'Plan')).toHaveLength(1);
  expect(state.items[0].quantity).toBe(1); expect(state.items[0].estimatedMinutes).toBe(25);
  expect(state.items[0].decision).toBe('Proposed');
  await expect(page.locator('.ant-drawer')).toHaveCount(0);
});

test('Thực hiện mở hàng sửa; × hủy và ✓ mới lưu/gạch ngang; xóa kế hoạch giữ actual', async ({ page }) => {
  const state = await prepare(page);
  await page.getByRole('button', { name: 'Chức năng Trám răng', exact: true }).click();
  await expect(page.locator('.ant-dropdown:visible').getByRole('menuitem')).toHaveCount(5);
  await page.getByRole('menuitem', { name: 'Thực hiện', exact: true }).click();
  await expect(page.getByLabel('Nhập quá trình điều trị')).toBeVisible();
  await expect(page.getByLabel('Ghi chú việc đã làm')).toHaveValue('Ghi chú kế hoạch');
  expect(state.activities).toHaveLength(0); expect(state.requests).toHaveLength(0);
  await page.getByRole('button', { name: 'Đóng hàng nhập', exact: true }).click();
  await expect(page.locator('.treatment-sheet-row-completed')).toHaveCount(0);
  expect(state.activities).toHaveLength(0);
  await page.getByRole('button', { name: 'Chức năng Trám răng', exact: true }).click();
  await page.getByRole('menuitem', { name: 'Thực hiện', exact: true }).click();
  await page.getByLabel('Ghi chú việc đã làm').fill('Việc đã làm sau khi chỉnh');
  await page.getByLabel('Chi phí', { exact: true }).fill('800000');
  await page.getByRole('button', { name: 'Đồng ý', exact: true }).click();
  await expect(page.getByText('Chưa ghi nhận phí', { exact: true })).toBeVisible();
  expect(state.activities).toHaveLength(1); expect(state.activities[0].visitId).toBeNull();
  const row = page.locator('.treatment-sheet-plan-table tbody tr').first();
  await expect(row).toHaveClass('treatment-sheet-row-completed');
  expect(await row.locator('td').first().evaluate(e => getComputedStyle(e).textDecorationLine)).toContain('line-through');
  await page.reload(); await page.getByRole('tab', { name: 'Điều trị & Thanh toán' }).click();
  await expect(page.locator('.treatment-sheet-plan-table tbody tr').first()).toHaveClass('treatment-sheet-row-completed');
  await page.getByRole('button', { name: 'Chức năng Trám răng', exact: true }).click(); await page.getByRole('menuitem', { name: 'Xóa', exact: true }).click();
  await page.getByRole('button', { name: 'Không đồng ý', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Chức năng Trám răng', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Chức năng Trám răng', exact: true }).click(); await page.getByRole('menuitem', { name: 'Xóa', exact: true }).click();
  await page.getByRole('button', { name: 'Đồng ý', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Chức năng Trám răng', exact: true })).toHaveCount(0);
  expect(state.activities).toHaveLength(1);
});

test('sửa/xóa actual inline, × bỏ sửa, reload giữ dữ liệu; không còn Xem nguồn', async ({ page }) => {
  const state = await prepare(page, false);
  await page.getByRole('button', { name: 'Thêm quá trình điều trị' }).click();
  await page.getByRole('combobox', { name: 'Điều trị/dịch vụ', exact: true }).focus(); await page.keyboard.press('ArrowDown'); await page.keyboard.press('Enter');
  await page.getByLabel('Ghi chú việc đã làm').fill('Bản gốc');
  await page.getByRole('button', { name: 'Đồng ý', exact: true }).click();
  await expect(page.getByText('Bản gốc', {exact:true})).toBeVisible();
  await expect(page.getByRole('button', { name:'Xem nguồn',exact:true })).toHaveCount(0);
  await page.getByRole('button', { name:'Sửa quá trình Trám răng',exact:true }).click();
  await page.getByLabel('Ghi chú việc đã làm').fill('Bỏ thay đổi');
  page.once('dialog', d => d.accept()); await page.getByRole('button', { name:'Đóng hàng nhập',exact:true }).click();
  await expect(page.getByText('Bản gốc', {exact:true})).toBeVisible();
  expect(state.requests.filter(r => r.command === 'activity')).toHaveLength(0);
  await page.getByRole('button', { name:'Sửa quá trình Trám răng',exact:true }).click();
  await page.getByLabel('Ghi chú việc đã làm').fill('Đã chỉnh hàng quá trình');
  await page.getByRole('button', { name:'Đồng ý',exact:true }).click();
  await expect(page.getByText('Đã chỉnh hàng quá trình', {exact:true})).toBeVisible();
  await page.reload(); await page.getByRole('tab', { name:'Điều trị & Thanh toán' }).click();
  await expect(page.getByText('Đã chỉnh hàng quá trình', {exact:true})).toBeVisible();
  await page.getByRole('button', { name:'Xóa quá trình Trám răng',exact:true }).click();
  await page.getByRole('button', { name:'Không đồng ý',exact:true }).click();
  await expect(page.getByText('Đã chỉnh hàng quá trình', {exact:true})).toBeVisible();
  await page.getByRole('button', { name:'Xóa quá trình Trám răng',exact:true }).click();
  await page.getByRole('button', { name:'Đồng ý',exact:true }).click();
  await expect(page.getByText('Đã chỉnh hàng quá trình', {exact:true})).toHaveCount(0);
  expect(state.activities[0].outcome).toBe('Bản gốc');
});

test('Hoàn tất chỉ đánh dấu; sửa ngay trên hàng giữ quantity cũ và snapshot actual', async ({ page }) => {
  const state = await prepare(page);
  const menu = async (name: string) => { await page.getByRole('button', { name: 'Chức năng Trám răng', exact: true }).click(); await page.getByRole('menuitem', { name, exact: true }).click(); };
  await menu('Hoàn tất'); await expect(page.locator('.treatment-sheet-row-completed')).toHaveCount(1);
  expect(state.items[0].progress).toBe('NotStarted'); expect(state.activities).toHaveLength(0);
  await menu('Bỏ hoàn tất'); await expect(page.locator('.treatment-sheet-row-completed')).toHaveCount(0);
  await menu('Sửa'); await page.getByLabel('Ghi chú', { exact: true }).fill('Đã sửa');
  await page.getByLabel('Chi phí', { exact: true }).fill('800000');
  await page.getByRole('button', { name: 'Đồng ý', exact: true }).click();
  await expect(page.getByText('Đã sửa', { exact: true })).toBeVisible();
  expect(state.items[0].quantity).toBe(2); expect(state.items[0].agreedUnitPriceVnd).toBe(400000);
});

test('Thêm việc đã làm không cần kế hoạch/Visit; không còn bộ lọc và Đã trả chỉ đọc', async ({ page }) => {
  const state = await prepare(page, false);
  await expect(page.getByRole('combobox', { name: 'Lọc lượt khám' })).toHaveCount(0);
  await expect(page.getByLabel('Từ ngày', { exact: true })).toHaveCount(0);
  await expect(page.getByRole('columnheader', { name: 'Đã trả', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Thêm quá trình điều trị' }).click();
  await page.getByRole('combobox', { name: 'Điều trị/dịch vụ', exact: true }).focus(); await page.keyboard.press('ArrowDown'); await page.keyboard.press('Enter');
  await page.getByLabel('Ghi chú việc đã làm').fill('Ghi trực tiếp theo phiếu');
  await page.getByLabel('Ghi chú Labo').fill('Mô tả Labo');
  await page.getByRole('button', { name: 'Đồng ý', exact: true }).click();
  await expect(page.getByText('Ghi trực tiếp theo phiếu', { exact: true })).toBeVisible();
  expect(state.activities[0].visitId).toBeNull();
  await expect(page.getByText('Chưa có dữ liệu', { exact: true })).toBeVisible();
});

test('đọc quá trình nhiều buổi, tổng toàn bộ phiếu và đóng có kiểm tra lâm sàng', async ({ page }) => {
  const state = await prepare(page);
  state.activities.push({ id: 'a1', visitId, dossierId, itemId, planVersion: 1, serviceId: 3, region: '25', quantity: 1, unit: 'răng', performerId: 7, performedAt: '2026-10-26T02:00:00Z', outcome: 'Buổi đầu', feeStatus: 'Pending', priceSnapshotVnd: 300000 },
    { id: 'a2', visitId: secondVisit, dossierId, itemId, planVersion: 1, serviceId: 3, region: '26', quantity: 1, unit: 'răng', performerId: 7, performedAt: '2026-10-27T02:00:00Z', outcome: 'Buổi sau', feeStatus: 'Pending', priceSnapshotVnd: 300000 });
  await page.reload(); await page.getByRole('tab', { name: 'Điều trị & Thanh toán' }).click();
  await expect(page.getByText('Buổi đầu', { exact: true })).toBeVisible(); await expect(page.getByText('Buổi sau', { exact: true })).toBeVisible();
  await expect(page.getByText('900.000 ₫', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Trạng thái phiếu: Chưa xong' }).click(); await page.getByRole('menuitem', { name: 'Hoàn tất', exact: true }).click();
  await expect(page.getByText(/Còn 1 hạng mục chưa hoàn tất/)).toBeVisible();
  await page.getByRole('button', { name: 'Xác nhận kết thúc' }).click();
  await expect(page.getByText('MSG243 — Còn hạng mục chưa được xử lý', { exact: true })).toBeVisible();
});

test('Đã trả mặc định 0, Còn nợ bằng Thành tiền trừ Đã trả và form chưa có việc đã làm không cho lưu', async ({ page }) => {
  await prepare(page);
  await expect(page.locator('.treatment-sheet-payment')).toContainText('Đã trả: 0 ₫');
  await expect(page.locator('.treatment-sheet-payment')).not.toContainText('Phí đã ghi nhận');
  await page.getByRole('button', { name: 'TRẢ TIỀN...' }).click();
  await expect(page.getByRole('dialog', { name: 'Tạo thanh toán mới' })).toBeVisible();
  await expect(page.getByText('Phiếu chưa có việc đã làm để thanh toán.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'LƯU LẠI' })).toBeDisabled();
});

test('form Tạo thanh toán mới điền sẵn việc đã làm, tự cộng tổng, chặn vượt nợ và cập nhật Đã trả/Còn nợ', async ({ page }) => {
  const state = await prepare(page);
  state.activities.push({ id: 'a1', visitId, dossierId, itemId, planVersion: 1, serviceId: 3, region: '25', quantity: 1, unit: 'răng', performerId: 7, performedAt: '2026-10-26T02:00:00Z', outcome: 'Buổi đầu', feeStatus: 'Pending', priceSnapshotVnd: 300000, serviceNameSnapshot: 'Trám răng' });
  await page.reload(); await page.getByRole('tab', { name: 'Điều trị & Thanh toán' }).click();
  await page.getByRole('button', { name: 'TRẢ TIỀN...' }).click();
  await page.getByLabel('Tiền thực nhận (VND)').fill('300000');
  await page.getByRole('button', { name: 'Xác nhận tiền thực nhận' }).click();
  await expect(page.getByText('MSG256 — Phân bổ chưa khớp tiền nhận hoặc vượt khoản nợ.')).toBeVisible();
  await page.getByLabel('Phân bổ Trám răng').fill('300000');
  await page.getByLabel('Phương thức', { exact: true }).focus(); await page.keyboard.press('ArrowDown'); await page.keyboard.press('ArrowDown'); await page.keyboard.press('Enter');
  await page.getByRole('button', { name: 'Xác nhận tiền thực nhận' }).click();
  await expect(page.getByText('Chứng từ PT-SYNTHETIC')).toBeVisible();
  expect(state.collection.status).toBe('Pending'); expect(state.collection.allocations[0].amountVnd).toBe(300000);
  await expect(page.getByRole('button', { name: 'In chứng từ' })).toBeDisabled();
  await page.getByRole('button', { name: 'Xác nhận tiền vào' }).click();
  await page.getByLabel('Mã giao dịch ngân hàng').fill('SYNTHETIC-BANK-1');
  await page.getByLabel('Nội dung đối soát').fill('SYNTHETIC đã đối soát');
  await page.getByRole('dialog').getByRole('button', { name: 'OK' }).click();
  await expect.poll(() => state.collection.status).toBe('Confirmed');
  const dialog = page.getByRole('dialog', { name: 'Tạo thanh toán mới' });
  await expect(dialog.getByText('Trám răng', { exact: true })).toBeVisible();
  await expect(dialog.getByText('Còn nợ 300.000 ₫')).toBeVisible();
  await expect(dialog.getByRole('button', { name: 'LƯU LẠI' })).toBeDisabled();
  const amount = dialog.getByLabel(/Số tiền thanh toán Trám răng/);
  await amount.fill('999999'); await amount.blur();
  await expect(dialog.getByLabel('Tổng tiền thanh toán (đ)')).toHaveValue('300.000');
  await amount.fill('100000'); await amount.blur();
  await expect(dialog.getByLabel('Tổng tiền thanh toán (đ)')).toHaveValue('100.000');
  const total = dialog.getByLabel('Tổng tiền thanh toán (đ)');
  await total.fill('120000'); await total.blur();
  await expect(amount).toHaveValue('120.000');
  await total.fill('100000'); await total.blur();
  await expect(amount).toHaveValue('100.000');
  await dialog.getByRole('button', { name: 'LƯU LẠI' }).click();
  await expect(dialog).toBeHidden();
  const sent = state.requests.find(x => x.command === 'payment');
  expect(sent.lines).toEqual([{ activityId: 'a1', amountVnd: 100000 }]); expect(sent.method).toBe('Cash'); expect(sent.collectorId).toBe(7);
  await expect(page.locator('.treatment-sheet-payment')).toContainText('Đã trả: 100.000 ₫');
  await expect(page.locator('.treatment-sheet-payment')).toContainText(/Còn nợ: [\d.]+ ₫/);
});

test('ghi chú chỉ lưu bằng nút xác nhận, bàn phím và responsive không tràn viewport', async ({ page }) => {
  const state = await prepare(page);
  await page.getByLabel('Ghi chú hồ sơ', { exact: true }).fill('Ghi chú được giữ');
  await page.waitForTimeout(1000); expect(state.notes).toBe('');
  await page.getByRole('button', { name: 'Lưu ghi chú', exact: true }).click();
  await expect.poll(() => state.notes).toBe('Ghi chú được giữ');
  for (const width of [390,768,1440]) {
    await page.setViewportSize({ width, height: 950 });
    const scroll = await page.evaluate(() => document.documentElement.scrollWidth);
    const overflow = await page.evaluate(()=>Array.from(document.querySelectorAll('body *')).filter(e=>e.getBoundingClientRect().right>innerWidth+1).slice(0,16).map(e=>({tag:e.tagName,cls:e.className,width:e.getBoundingClientRect().width,right:e.getBoundingClientRect().right})));
    expect(scroll,JSON.stringify(overflow)).toBeLessThanOrEqual(width+1);
    await page.screenshot({ path: `test-results/treatment-v25-${width}.png`, fullPage: true });
  }
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.getByRole('button', { name: 'Thêm hạng mục kế hoạch' }).focus();
  await page.keyboard.press('Enter'); await expect(page.getByLabel('Nhập hạng mục kế hoạch')).toBeVisible();
});


async function selectNoteMaterial(page: import('@playwright/test').Page, index: number, name: string, search: string) {
  const input = page.getByRole('combobox',{name:`Tên dụng cụ/vật liệu ${index}`,exact:true});
  await input.click(); await input.fill(search);
  await page.locator('.material-note-select-popup:visible').getByText(name,{exact:true}).click();
}

test('vật liệu nối text, số lẻ và tên trùng khác nhóm; Modal không ghi, ✓ hàng mới lưu và reload giữ text', async ({page})=>{
  const state = await prepare(page,false);
  await page.getByRole('button',{name:'Thêm quá trình điều trị'}).click();
  await page.getByRole('combobox',{name:'Điều trị/dịch vụ',exact:true}).focus(); await page.keyboard.press('ArrowDown'); await page.keyboard.press('Enter');
  await page.getByLabel('Ghi chú việc đã làm').fill('Đã trám răng 25.');
  await page.getByRole('button',{name:'Chọn vật liệu dùng',exact:true}).click();
  await selectNoteMaterial(page,1,'Composite A2 (Vật liệu trám)','Composite');
  await page.getByRole('spinbutton',{name:'Số lượng vật liệu 1',exact:true}).fill('0.5');
  await page.getByRole('button',{name:'Thêm vật liệu',exact:true}).click();
  await selectNoteMaterial(page,2,'Composite A2 (Vật liệu khác)','Composite');
  await page.getByRole('button',{name:'Lưu vật liệu',exact:true}).evaluate(button=>{(button as HTMLButtonElement).click();(button as HTMLButtonElement).click();});
  const expected = 'Đã trám răng 25.\n\nVật liệu dùng: Composite A2 (Vật liệu trám) × 0,5; Composite A2 (Vật liệu khác) × 1.';
  await expect(page.getByLabel('Ghi chú việc đã làm')).toHaveValue(expected);
  expect(state.requests).toHaveLength(0);
  await page.getByRole('button',{name:'Đồng ý',exact:true}).click();
  await expect.poll(()=>state.activities.length).toBe(1); expect(state.activities[0].outcome).toBe(expected);
  state.materials[0].name = 'Catalog đã đổi tên';
  await page.reload(); await page.getByRole('tab',{name:'Điều trị & Thanh toán'}).click();
  await expect(page.getByText(expected,{exact:true})).toBeVisible();
});

test('hủy Modal không đổi note; append làm dirty, × hàng bỏ thay đổi; icon không có ở kế hoạch', async ({page})=>{
  const state = await prepare(page,false);
  await page.getByRole('button',{name:'Thêm hạng mục kế hoạch'}).click();await expect(page.getByRole('button',{name:'Chọn vật liệu dùng',exact:true})).toHaveCount(0);
  await page.getByRole('button',{name:'Đóng hàng nhập',exact:true}).click();
  await page.getByRole('button',{name:'Thêm quá trình điều trị'}).click();await page.getByLabel('Ghi chú việc đã làm').fill('Giữ nguyên');
  await page.getByRole('button',{name:'Chọn vật liệu dùng',exact:true}).click();await selectNoteMaterial(page,1,'Gương khám (Dụng cụ khám)','Gương');
  await page.keyboard.press('Escape'); await expect(page.getByLabel('Ghi chú việc đã làm')).toHaveValue('Giữ nguyên');
  await page.getByRole('button',{name:'Chọn vật liệu dùng',exact:true}).click();await expect(page.getByRole('spinbutton',{name:'Số lượng vật liệu 1',exact:true})).toHaveValue('1');
  await selectNoteMaterial(page,1,'Gương khám (Dụng cụ khám)','Gương');await page.getByRole('button',{name:'Lưu vật liệu',exact:true}).click();
  const dialog = page.waitForEvent('dialog'); const cancel = page.getByRole('button',{name:'Đóng hàng nhập',exact:true}).click();
  await (await dialog).accept(); await cancel;
  await expect(page.getByLabel('Nhập quá trình điều trị')).toHaveCount(0);expect(state.requests).toHaveLength(0);expect(state.activities).toHaveLength(0);
});

test('vật liệu chặn thiếu/0/âm/thập phân quá3/trùng; overflow giữ note và lựa chọn, xóa dòng không xóa catalog', async({page})=>{
  const state=await prepare(page,false);await page.getByRole('button',{name:'Thêm quá trình điều trị'}).click();
  await page.getByLabel('Ghi chú việc đã làm').fill('a'.repeat(990));await page.getByRole('button',{name:'Chọn vật liệu dùng',exact:true}).click();
  await page.getByRole('button',{name:'Lưu vật liệu',exact:true}).click();await expect(page.getByText('Vui lòng chọn dụng cụ/vật liệu.',{exact:true})).toBeVisible();
  await selectNoteMaterial(page,1,'Gương khám (Dụng cụ khám)','Gương');
  for (const qty of ['','0','-1','1.2345']) {
    await page.getByRole('spinbutton',{name:'Số lượng vật liệu 1',exact:true}).fill(qty);await page.getByRole('button',{name:'Lưu vật liệu',exact:true}).click();
    await expect(page.getByText('Nhập số lượng dương, tối đa 3 chữ số thập phân.',{exact:true})).toBeVisible();
  }
  await page.getByRole('spinbutton',{name:'Số lượng vật liệu 1',exact:true}).fill('1');await page.getByRole('button',{name:'Thêm vật liệu',exact:true}).click();
  const select2=page.getByRole('combobox',{name:'Tên dụng cụ/vật liệu 2',exact:true});await select2.click();await select2.fill('Gương');
  await expect(page.locator('.material-note-select-popup:visible .ant-select-item-option').filter({hasText:'Gương khám (Dụng cụ khám)'})).toHaveClass(/disabled/);
  await page.keyboard.press('Escape');await page.getByRole('button',{name:'Xóa dòng vật liệu 2',exact:true}).click();
  await page.getByRole('button',{name:'Lưu vật liệu',exact:true}).click();await expect(page.getByText(/vượt giới hạn 1.000/)).toBeVisible();
  await expect(page.getByLabel('Ghi chú việc đã làm')).toHaveValue('a'.repeat(990));
  await expect(page.getByRole('dialog',{name:'Vật liệu dùng',exact:true}).getByText('Gương khám (Dụng cụ khám)',{exact:true})).toBeVisible();
  expect(state.requests).toHaveLength(0);expect(state.materials).toHaveLength(25);
});

test('vật liệu remote pagination và retry giữ lựa chọn; bàn phím và responsive',async({page})=>{
  const state=await prepare(page,false);await page.getByRole('button',{name:'Thêm quá trình điều trị'}).click();
  await page.getByRole('button',{name:'Chọn vật liệu dùng',exact:true}).focus();await page.keyboard.press('Enter');
  await selectNoteMaterial(page,1,'Gương khám (Dụng cụ khám)','Gương');
  await page.getByRole('combobox',{name:'Tên dụng cụ/vật liệu 1',exact:true}).press('ArrowDown');
  await expect(page.getByRole('button',{name:'Tải thêm vật liệu',exact:true})).toBeVisible();
  state.failMaterialOnce=true;await page.getByRole('button',{name:'Tải thêm vật liệu',exact:true}).click();
  await expect(page.getByText('Không thể tải danh mục thử nghiệm.',{exact:true})).toBeVisible();
  await expect(page.getByRole('dialog',{name:'Vật liệu dùng',exact:true}).getByText('Gương khám (Dụng cụ khám)',{exact:true}).first()).toBeVisible();
  await page.getByRole('button',{name:'Thử lại danh mục',exact:true}).click();
  await expect.poll(()=>state.materialReads.filter(r=>r.page===2).length).toBe(2);
  await page.getByRole('combobox',{name:'Tên dụng cụ/vật liệu 1',exact:true}).press('ArrowDown');
  await page.locator('.material-note-select-popup:visible').getByText('Dụng cụ 22 (Dụng cụ khám)',{exact:true}).click();
  for(const width of [390,768,1440]){await page.setViewportSize({width,height:950});await expect(page.getByRole('dialog',{name:'Vật liệu dùng',exact:true})).toBeVisible();await expect.poll(()=>page.evaluate(()=>document.documentElement.scrollWidth)).toBeLessThanOrEqual(width+1);await page.screenshot({path:`test-results/material-note-${width}.png`,fullPage:true});}
  await page.getByRole('button',{name:'Lưu vật liệu',exact:true}).click();await expect(page.getByLabel('Ghi chú việc đã làm')).toHaveValue('Vật liệu dùng: Dụng cụ 22 (Dụng cụ khám) × 1.');expect(state.requests).toHaveLength(0);
});

test('vật liệu lỗi tải đầu tiên cho thử lại; danh mục trống không đổi ghi chú',async({page})=>{
  const state=await prepare(page,false);
  await page.getByRole('button',{name:'Thêm quá trình điều trị'}).click();
  await page.getByLabel('Ghi chú việc đã làm').fill('Nội dung ban đầu');
  state.failMaterialOnce=true;
  await page.getByRole('button',{name:'Chọn vật liệu dùng',exact:true}).click();
  await expect(page.getByText('Không thể tải danh mục thử nghiệm.',{exact:true})).toBeVisible();
  await page.getByRole('button',{name:'Thử lại danh mục',exact:true}).click();
  await expect.poll(()=>state.materialReads.length).toBe(2);
  await page.getByRole('combobox',{name:'Tên dụng cụ/vật liệu 1',exact:true}).press('ArrowDown');
  await expect(page.locator('.material-note-select-popup:visible').getByText('Gương khám (Dụng cụ khám)',{exact:true})).toBeVisible();
  await page.keyboard.press('Escape');
  await page.getByRole('button',{name:'Hủy',exact:true}).click();
  state.materials=[];
  await page.getByRole('button',{name:'Chọn vật liệu dùng',exact:true}).click();
  await page.getByRole('combobox',{name:'Tên dụng cụ/vật liệu 1',exact:true}).press('ArrowDown');
  await expect(page.getByText('Không có dụng cụ/vật liệu phù hợp.',{exact:true})).toBeVisible();
  await page.keyboard.press('Escape');await page.getByRole('button',{name:'Hủy',exact:true}).click();
  await expect(page.getByLabel('Ghi chú việc đã làm')).toHaveValue('Nội dung ban đầu');
  expect(state.requests).toHaveLength(0);
});




test('ô ảnh chọn file tại phiếu, giữ route và dùng chung tab Hình ảnh', async ({ page }) => {
  await prepare(page);
  const images: any[] = [];
  const uploads: any[] = [];
  await page.route('**/api/clinical-documents**', async route => {
    const r=route.request(), u=new URL(r.url());
    if(u.pathname.endsWith('/content')) return route.fulfill({contentType:'image/png',body:Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZ9sAAAAASUVORK5CYII=','base64')});
    if(r.method()==='POST') {
      const body=r.postDataBuffer()!.toString('utf8'); uploads.push(body);
      const d={documentId:'photo-1',version:1,fileName:'anh.png',contentType:'image/png',sizeBytes:68,documentType:'Other',source:'Clinic',sourceDate:'2026-11-02',context:{dossierId},uploaderName:'Bác sĩ',uploadedAt:new Date().toISOString(),fileState:'Ready',allowedActions:['view','download']};
      images.push(d); return route.fulfill({json:{document:d,documents:[{document:d}]}});
    }
    expect(u.searchParams.get('imagesOnly')).toBe('true');
    return route.fulfill({json:{items:images,total:images.length,page:1,pageSize:20,constraints:{maxFiles:10,maxBytes:20971520},allowedActions:['upload']}});
  });
  const before=page.url();
  const chooserPromise=page.waitForEvent('filechooser');
  await page.getByRole('button',{name:'Thêm ảnh',exact:true}).click();
  const chooser=await chooserPromise;
  await chooser.setFiles({name:'anh.png',mimeType:'image/png',buffer:Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZ9sAAAAASUVORK5CYII=','base64')});
  await expect(page.getByRole('img',{name:'anh.png',exact:true})).toBeVisible();
  expect(page.url()).toBe(before); expect(uploads).toHaveLength(1);
  expect(uploads[0]).toContain('"dossierId":"'+dossierId+'"'); expect(uploads[0]).toContain('"visitId":null');
  await page.getByRole('tab',{name:'Hình ảnh',exact:true}).click();
  await expect(page.locator('[role="tabpanel"]:visible').getByRole('img',{name:'anh.png',exact:true})).toBeVisible();
});


test('ảnh lỗi giữ file và requestKey; không mở nội dung chờ kiểm tra', async ({page})=>{
  await prepare(page); const posts:string[]=[]; let saved=false,contentReads=0;
  await page.route('**/api/clinical-documents**',async route=>{
    const r=route.request(),url=new URL(r.url());
    if(url.pathname.endsWith('/content')){contentReads++;return route.abort();}
    const d={documentId:'pending-1',version:1,fileName:'cho.png',contentType:'image/png',fileState:'PendingScan',context:{dossierId}};
    if(r.method()==='POST') { posts.push(r.postDataBuffer()!.toString()); if(posts.length===1)return route.fulfill({status:503,json:{message:'Lỗi tải ảnh thử nghiệm'}}); saved=true;return route.fulfill({json:{document:d}}); }
    return route.fulfill({json:{items:saved?[d]:[],total:saved?1:0,page:1,pageSize:20,constraints:{maxFiles:10,maxBytes:20971520}}});
  });
  await page.getByLabel('Chọn ảnh từ máy').setInputFiles({name:'cho.png',mimeType:'image/png',buffer:Buffer.from('synthetic')});
  await expect(page.getByText('Lỗi tải ảnh thử nghiệm')).toBeVisible();
  await page.getByRole('button',{name:'Thử lại tải ảnh'}).click();
  await expect(page.getByText('Đang kiểm tra an toàn')).toBeVisible();
  expect(posts).toHaveLength(2);
  const keys=posts.map(body=>body.match(/"requestKey":"([^"]+)"/)?.[1]);expect(keys[0]).toBeTruthy();expect(keys[1]).toBe(keys[0]);expect(contentReads).toBe(0);
});

for(const width of [390,768,1440]) test('ô thêm ảnh dùng bàn phím và bố cục '+width,async({page})=>{
  await page.setViewportSize({width,height:900});await prepare(page);
  const button=page.getByRole('button',{name:'Thêm ảnh',exact:true});await button.focus();
  const choosing=page.waitForEvent('filechooser');await button.press('Enter');const chooser=await choosing;expect(chooser.isMultiple()).toBe(true);
  await chooser.setFiles({name:'wrong.pdf',mimeType:'application/pdf',buffer:Buffer.from('synthetic')});
  await expect(page.getByText('Chỉ chọn ảnh JPEG/PNG không rỗng, tối đa 20 MB mỗi ảnh.')).toBeVisible();
  const box=await button.boundingBox();expect(box?.width).toBeGreaterThanOrEqual(44);expect(box!.x+box!.width).toBeLessThanOrEqual(width);
});



test('Sao chép lưu ngay đủ dữ liệu, không editor/xác nhận và giữ nguyên nguồn',async({page})=>{
  const state=await prepare(page);Object.assign(state.items[0],{estimatedStart:'2026-11-02',estimatedMinutes:35,note:'Ghi chú sao chép',assistantId:8,displayCompleted:true,agreedUnitPriceVnd:275000,
    discount:{type:'Percent',value:10,reason:'Giảm theo ca',status:'Approved',amountVnd:55000}});
  const original=structuredClone(state.items[0]);await page.goto('/patients/123?tab=treatment');
  await expect(page.locator('.treatment-sheet-row-completed')).toHaveCount(1);
  const writes:string[]=[];page.on('request',r=>{if(r.method()!=='GET'&&r.url().includes('/api/'))writes.push(r.url());});
  await page.getByRole('button',{name:'Chức năng Trám răng',exact:true}).click();await page.getByRole('menuitem',{name:'Sao chép',exact:true}).click();
  await expect(page.locator('.treatment-sheet-plan-table tbody > tr')).toHaveCount(2);
  await expect(page.getByRole('row',{name:'Nhập hạng mục kế hoạch',exact:true})).toHaveCount(0);await expect(page.getByRole('button',{name:'Đồng ý',exact:true})).toHaveCount(0);
  expect(state.items[0]).toEqual(original);const copy=state.items[1];expect(copy.id).not.toBe(original.id);
  expect(copy).toMatchObject({estimatedStart:'2026-11-02',estimatedMinutes:35,note:'Ghi chú sao chép',assistantId:8,region:'25, 26',quantity:2,agreedUnitPriceVnd:275000,displayCompleted:false,progress:'NotStarted',decision:'Proposed'});
  expect(copy.discount).toMatchObject(original.discount);expect(state.activities).toHaveLength(0);
  expect(writes.filter(u=>u.endsWith('/confirm-plan'))).toHaveLength(1);expect(writes.some(u=>u.includes('/sheet-items/'))).toBe(false);
  await expect(page.getByText('Tổng dự toán: 1.100.000 ₫',{exact:true})).toBeVisible();await expect(page.getByText('Thành tiền: 990.000 ₫',{exact:true})).toBeVisible();
  await page.reload();await expect(page.locator('.treatment-sheet-plan-table tbody > tr')).toHaveCount(2);await expect(page.locator('.treatment-sheet-row-completed')).toHaveCount(1);
});

test('Sao chép lỗi mạng thử lại cùng ID/key, không mở hàng và không ghi hai bản',async({page})=>{
  const state=await prepare(page);const confirms:any[]=[];let failed=false;
  await page.route('**/sheet-drafts/*/confirm-plan',async route=>{confirms.push({url:route.request().url(),body:route.request().postDataJSON()});if(!failed){failed=true;return route.fulfill({status:503,json:{code:'MSG144',message:'Lỗi sao chép thử nghiệm'}});}return route.fallback();});
  await page.getByRole('button',{name:'Chức năng Trám răng',exact:true}).click();await page.getByRole('menuitem',{name:'Sao chép',exact:true}).click();
  await expect(page.getByText('Lỗi sao chép thử nghiệm')).toBeVisible();expect(state.items).toHaveLength(1);expect(state.drafts.size).toBe(1);
  await expect(page.getByRole('row',{name:'Nhập hạng mục kế hoạch',exact:true})).toHaveCount(0);
  await page.getByRole('button',{name:'Thử lại sao chép',exact:true}).click();await expect(page.locator('.treatment-sheet-plan-table tbody > tr')).toHaveCount(2);
  expect(confirms).toHaveLength(2);expect(confirms[1]).toEqual(confirms[0]);expect(state.items).toHaveLength(2);expect(state.drafts.size).toBe(0);expect(state.activities).toHaveLength(0);
});

test('Sao chép MSG111 đọc version mới rồi thử lại, khóa thao tác khi đang nhập',async({page})=>{
  const state=await prepare(page);state.conflict=true;
  await page.getByRole('button',{name:'Chức năng Trám răng',exact:true}).click();await page.getByRole('menuitem',{name:'Sao chép',exact:true}).click();
  await expect(page.getByText('MSG111 — phiên bản đã thay đổi',{exact:true})).toBeVisible();state.conflict=false;state.version++;
  await page.getByRole('button',{name:'Đọc lại phiên bản sao chép',exact:true}).click();await page.getByRole('button',{name:'Thử lại sao chép',exact:true}).click();
  await expect(page.locator('.treatment-sheet-plan-table tbody > tr')).toHaveCount(2);expect(state.items).toHaveLength(2);
  await page.getByRole('button',{name:'Thêm hạng mục kế hoạch',exact:true}).click();await page.getByRole('button',{name:'Chức năng Trám răng',exact:true}).first().click();
  await expect(page.getByRole('menuitem',{name:'Sao chép',exact:true})).toHaveAttribute('aria-disabled','true');await page.keyboard.press('Escape');
  await page.getByRole('button',{name:'Đóng hàng nhập',exact:true}).click();expect(state.items).toHaveLength(2);
});

test('Sao chép đang gửi khóa bấm lặp, lần sao chép mới sau thành công dùng ID mới',async({page})=>{
  const state=await prepare(page);let release!:()=>void;const gate=new Promise<void>(resolve=>{release=resolve;});let count=0;
  await page.route('**/sheet-drafts/*/confirm-plan',async route=>{count++;if(count===1)await gate;return route.fallback();});
  await page.getByRole('button',{name:'Chức năng Trám răng',exact:true}).click();await page.getByRole('menuitem',{name:'Sao chép',exact:true}).click();
  await expect.poll(()=>count).toBe(1);await page.getByRole('button',{name:'Chức năng Trám răng',exact:true}).click();
  await expect(page.getByRole('menuitem',{name:'Sao chép',exact:true})).toHaveAttribute('aria-disabled','true');release();await page.keyboard.press('Escape');
  await expect(page.locator('.treatment-sheet-plan-table tbody > tr')).toHaveCount(2);
  await page.getByRole('button',{name:'Chức năng Trám răng',exact:true}).first().click();await page.getByRole('menuitem',{name:'Sao chép',exact:true}).click();
  await expect(page.locator('.treatment-sheet-plan-table tbody > tr')).toHaveCount(3);expect(new Set(state.items.map(i=>i.id)).size).toBe(3);expect(state.activities).toHaveLength(0);
});
