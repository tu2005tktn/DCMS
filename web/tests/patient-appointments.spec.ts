import { expect, test, type Page } from '@playwright/test';

const appointmentId = '00000000-0000-0000-0000-000000000001';
const appointment = { id: appointmentId, code: 'AP-PATIENT', branchId: 1, branchName: 'Synthetic branch',
  startAt: '2026-11-01T02:00:00Z', endAt: '2026-11-01T02:30:00Z', durationMinutes: 30,
  dentistId: 1, dentistName: 'Synthetic dentist', chairId: 1, chairName: 'Chair', serviceName: 'Exam',
  attendeeName: 'Synthetic patient', attendeeRelation: 'Self', kind: 'Standard', source: 'Phone', status: 'Confirmed', version: 1 };

async function setup(page: Page) {
  const queries: URL[] = [];
  let fail = false;
  await page.addInitScript(() => sessionStorage.setItem('dcms.web.access-token', 'fixture'));
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url());
    let body: unknown = {};
    if (url.pathname === '/api/auth/me') body = { id: 2, userName: 'fixture', fullName: 'Fixture', accountType: 'Staff',
      roles: ['Dentist'], effectiveRoles: ['Dentist'], permissions: ['patients.read'], hasVerifiedPatientLink: false };
    else if (url.pathname === '/api/patients/7') body = { id: 7, patientCode: 'P-FIXTURE', fullName: 'Synthetic patient',
      isArchived: false, createdAt: '2026-10-19T00:00:00Z', updatedAt: '2026-10-19T00:00:00Z', version: 1, link: {} };
    else if (url.pathname === '/api/patients/7/appointments') {
      queries.push(url);
      if (fail) return route.fulfill({ status: 503, contentType: 'application/json', body: JSON.stringify({ code: 'MSG144', message: 'Synthetic connection failure' }) });
      const items = url.searchParams.get('scope') === 'HISTORY' ? [] : [appointment];
      body = { items, total: items.length, page: 1, pageSize: 20 };
    } else if (url.pathname === `/api/appointments/${appointmentId}`) body = { appointment, bookerName: 'Booker', bookerPhoneMasked: '***' };
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
  });
  await page.goto('/patients/7');
  await expect(page.getByRole('heading', { name: 'Synthetic patient', exact: true })).toBeVisible();
  return { queries, fail: (value: boolean) => { fail = value; } };
}

test('patient appointment tab loads lazily, filters history and opens the exact appointment detail', async ({ page }) => {
  const state = await setup(page);
  expect(state.queries).toEqual([]);
  await page.getByRole('tab', { name: 'Lịch hẹn', exact: true }).click();
  const link = page.getByRole('link', { name: 'AP-PATIENT', exact: true });
  await expect(link).toHaveAttribute('href', `/appointments/${appointmentId}`);
  await expect(page.getByRole('row').filter({ hasText: 'AP-PATIENT' })).toContainText('01/11/2026 · 09:00–09:30');
  expect(state.queries[0].pathname).toBe('/api/patients/7/appointments');
  const scopeSelect = page.locator('.ant-select').filter({ has: page.getByRole('combobox', { name: 'Nhóm lịch hẹn' }) });
  await scopeSelect.locator('.ant-select-selector').click();
  await page.locator('.ant-select-dropdown:visible').getByText('Lịch sử', { exact: true }).click();
  await expect(page.getByText('Không có lịch hẹn phù hợp của bệnh nhân trong phạm vi của bạn.', { exact: true })).toBeVisible();
  expect(state.queries.at(-1)?.searchParams.get('scope')).toBe('HISTORY');
  expect(state.queries.at(-1)?.searchParams.get('page')).toBe('1');
  await scopeSelect.locator('.ant-select-selector').click();
  await page.locator('.ant-select-dropdown:visible').getByText('Tất cả', { exact: true }).click();
  await link.click();
  await expect(page).toHaveURL(new RegExp(`/appointments/${appointmentId}$`));
  await expect(page.getByRole('heading', { name: 'Lịch hẹn AP-PATIENT', exact: true })).toBeVisible();
});

test('read error is distinct from empty data and can be retried', async ({ page }) => {
  const state = await setup(page); state.fail(true);
  await page.getByRole('tab', { name: 'Lịch hẹn', exact: true }).click();
  await expect(page.getByText('Synthetic connection failure', { exact: true })).toBeVisible();
  await expect(page.getByText('Không có lịch hẹn phù hợp của bệnh nhân trong phạm vi của bạn.', { exact: true })).toHaveCount(0);
  state.fail(false);
  await page.getByRole('button', { name: 'Thử lại', exact: true }).click();
  await expect(page.getByRole('link', { name: 'AP-PATIENT', exact: true })).toBeVisible();
});
