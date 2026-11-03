import { readFileSync } from 'node:fs';
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const base = process.env.E2E_API_URL, password = process.env.E2E_DEMO_PASSWORD, fixtureFile = process.env.E2E_SCREEN_STAFF_FIXTURE;
test.skip(!base || !password || !fixtureFile, 'Requires dedicated synthetic staff and an isolated API runtime.');
async function ok(response: Awaited<ReturnType<APIRequestContext['post']>>) { expect(response.ok(), await response.text()).toBeTruthy(); return response.json(); }
async function login(request: APIRequestContext, userName: string) { return { Authorization: `Bearer ${(await ok(await request.post(`${base}/api/auth/sign-in`, { data: { userName, password } }))).accessToken}` }; }
async function signIn(page: Page, userName: string) {
  await page.goto('/sign-in'); await page.getByLabel('Tên đăng nhập', { exact: true }).fill(userName); await page.getByLabel('Mật khẩu', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click(); await expect(page.getByRole('heading', { name: 'Tài khoản của tôi', exact: true })).toBeVisible();
}
async function setup(request: APIRequestContext, role: string, grants: string[]) {
  const fixtures = JSON.parse(readFileSync(fixtureFile!, 'utf8').replace(/^\uFEFF/, ''));
  const staff = fixtures.find((x: { role: string }) => x.role === role); expect(staff.schema).toMatch(/^dcms_reception_e2e_[a-z0-9]+$/);
  const owner = await login(request, 'owner.demo');
  const options = await ok(await request.get(`${base}/api/system-settings/access-groups/grant-options`, { headers: owner }));
  function expand(keys: string[]) { const result = new Set(keys); for (const key of keys) { let parent = options.find((x: { key: string }) => x.key === key)?.parent; while (parent) { result.add(parent); parent = options.find((x: { key: string }) => x.key === parent)?.parent; } } return [...result]; }
  const code = 'SYN-SCREEN-' + crypto.randomUUID().slice(0, 8);
  let group = await ok(await request.post(`${base}/api/system-settings/access-groups/`, { headers: owner, data: { requestKey: crypto.randomUUID(), entry: { code, name: code, baseRole: role, grants: expand(grants) } } }));
  const list = await ok(await request.get(`${base}/api/system-settings/access-groups/staff`, { headers: owner })); const row = list.find((x: { accountId: number }) => x.accountId === staff.accountId);
  await ok(await request.put(`${base}/api/system-settings/staff/${staff.employeeId}/access-group`, { headers: owner, data: { groupId: group.id, expectedVersion: row.accountVersion, groupVersion: group.version, requestKey: crypto.randomUUID(), branchIds: [staff.branchId], reason: 'SYNTHETIC screen verification' } }));
  group = (await ok(await request.get(`${base}/api/system-settings/access-groups`, { headers: owner }))).find((x: { id: string }) => x.id === group.id);
  return { staff, headers: await login(request, staff.userName), async change(keys: string[]) {
    group = await ok(await request.put(`${base}/api/system-settings/access-groups/${group.id}`, { headers: owner, data: { expectedVersion: group.version, requestKey: crypto.randomUUID(), reason: 'SYNTHETIC screen grant update', entry: { code, name: code, baseRole: role, grants: expand(keys) } } }));
  } };
}

test('real doctor: schedule and treatment read-only, create allowed after grant, same-token revocation', async ({ page, request }, testInfo) => {
  test.setTimeout(120000);
  const reads = ['patients.read', 'treatment.read', 'treatment.templates.read', 'booking-settings.read', 'working-schedules.read'];
  const { staff, headers, change } = await setup(request, 'Dentist', reads);
  await signIn(page, staff.userName);
  const bookingResponse = page.waitForResponse(response => response.url().endsWith(`/api/booking-settings/${staff.branchId}`));
  await page.goto('/booking-settings'); expect((await bookingResponse).ok()).toBeTruthy(); await expect(page.getByRole('spinbutton').first()).toBeDisabled();
  await page.goto('/working-schedules'); await expect(page.getByRole('heading', { name: /Lịch làm việc/ })).toBeVisible();
  await page.goto('/treatment-templates'); await expect(page.getByRole('button', { name: 'Tạo mẫu', exact: true })).toBeDisabled();
  const templateInput = { branchId: staff.branchId, name: 'SYNTHETIC template ' + crypto.randomUUID().slice(0, 8), requestKey: crypto.randomUUID(), steps: [{ logicalStepId: crypto.randomUUID(), itemId: null, name: 'SYNTHETIC khám kiểm tra', estimatedMinutes: 30, reservedMinutes: 30, predecessorIds: [], chargingCoverage: 'IncludedInItem' }] };
  expect((await request.post(`${base}/api/treatment-templates/`, { headers, data: templateInput })).status()).toBe(403);
  await change([...reads, 'treatment.templates.manage', 'treatment.dossiers.create']);
  const template = await ok(await request.post(`${base}/api/treatment-templates/`, { headers, data: templateInput })); expect(template.name).toBe(templateInput.name);
  const assistant = await login(request, 'assistant.demo');
  const patient = (await ok(await request.post(`${base}/api/patients`, { headers: assistant, data: { fullName: 'SYNTHETIC permission treatment ' + crypto.randomUUID().slice(0, 8), duplicatesReviewed: true } }))).patient;
  const created = await ok(await request.post(`${base}/api/patients/${patient.id}/dossiers`, { headers, data: { branchId: staff.branchId, dentistId: staff.employeeId, diagnosis: 'SYNTHETIC chẩn đoán thử', requestKey: crypto.randomUUID() } }));
  await page.reload(); await expect(page.getByRole('button', { name: 'Tạo mẫu', exact: true })).toBeEnabled(); await expect(page.getByText(templateInput.name, { exact: true })).toBeVisible();
  await page.goto(`/patients/${patient.id}/dossiers/${created.dossier.id}`); await expect(page.getByText(created.dossier.code, { exact: false }).first()).toBeVisible();
  expect((await request.post(`${base}/api/dossiers/${created.dossier.id}/close`, { headers, data: {} })).status()).toBe(403);
  await page.screenshot({ path: testInfo.outputPath('real-treatment-read-only.png'), fullPage: true });
  await change(reads.filter(x => x !== 'treatment.templates.read'));
  expect((await request.get(`${base}/api/treatment-templates?branchId=${staff.branchId}`, { headers })).status()).toBe(403);
  await page.goto('/treatment-templates'); await expect(page.getByText('Bạn không có quyền thực hiện thao tác này.', { exact: true })).toBeVisible();
});

test('real delegated admin: general configuration read, write preview and revocation', async ({ page, request }, testInfo) => {
  test.setTimeout(90000);
  const { staff, headers, change } = await setup(request, 'Admin', ['settings.general.read']);
  await signIn(page, staff.userName); await page.goto('/system-settings?tab=general');
  const prefix = page.getByRole('textbox', { name: 'Tiền tố', exact: true }); await expect(prefix).toBeDisabled();
  const settings = await ok(await request.get(`${base}/api/system-settings/settings/general`, { headers }));
  expect((await request.post(`${base}/api/system-settings/settings/general/preview`, { headers, data: { expectedVersion: settings.version, settings: settings.settings } })).status()).toBe(403);
  await change(['settings.general.manage']); await page.reload(); await expect(prefix).toBeEnabled();
  await prefix.fill('SYNTEST'); await expect(page.getByRole('button', { name: 'Xem trước và lưu', exact: true })).toBeEnabled();
  const previewResponse = page.waitForResponse(response => response.url().endsWith('/settings/general/preview'));
  await page.getByRole('button', { name: 'Xem trước và lưu', exact: true }).click(); expect((await previewResponse).ok()).toBeTruthy();
  await page.screenshot({ path: testInfo.outputPath('real-delegated-settings-preview.png'), fullPage: true });
  await change(['settings.general.read']);
  expect((await request.post(`${base}/api/system-settings/settings/general/preview`, { headers, data: {} })).status()).toBe(403);
  await page.reload(); await expect(prefix).toBeDisabled();
});

test('real permission editor exposes every screen and nested treatment price rights', async ({ page }, testInfo) => {
  await signIn(page, 'owner.demo'); await page.goto('/system-settings?tab=access-groups');
  await page.getByRole('button', { name: 'Thêm nhóm quyền', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Thiết lập phân quyền', exact: true });
  await dialog.getByRole('combobox', { name: 'Vai trò nền', exact: true }).press('ArrowDown');
  await page.locator('.ant-select-dropdown:visible').getByText('Nha sĩ', { exact: true }).click();
  await expect(dialog.getByRole('checkbox', { name: 'Xem lịch làm việc', exact: true })).toBeEnabled();
  await expect(dialog.getByRole('checkbox', { name: 'Xem cài đặt đặt lịch', exact: true })).toBeEnabled();
  await expect(dialog.getByRole('checkbox', { name: 'Xem mẫu điều trị', exact: true })).toBeEnabled();
  await dialog.getByRole('checkbox', { name: 'Sửa đơn giá dòng công việc điều trị', exact: true }).check();
  await expect(dialog.getByRole('checkbox', { name: 'Sửa dòng công việc trong phiếu điều trị', exact: true })).toBeChecked();
  await expect(dialog.getByRole('checkbox', { name: 'Xem hồ sơ điều trị', exact: true })).toBeChecked();
  await expect(dialog.getByRole('checkbox', { name: /^Xem tài khoản nhân viên/ })).toBeDisabled();
  await dialog.getByLabel('Tìm quyền', { exact: true }).fill('điều trị');
  await page.screenshot({ path: testInfo.outputPath('all-screens-permission-editor.png'), fullPage: true });
  await page.setViewportSize({ width: 390, height: 900 });
  await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(391);
  await page.screenshot({ path: testInfo.outputPath('all-screens-permission-editor-mobile.png'), fullPage: true });
});
