import { expect, test, type Page } from '@playwright/test';

// S24 (docs/specs/S24-FOLLOW-UP-INSTRUCTION-WEB.md) — AT37: chỉ định được tạo qua API thật của DCMS-97
// (mock tại lớp network như các test treatment khác), trạng thái chỉ đổi từ response/read-back của server,
// handoff S11 truyền followUpId + kind FollowUp, form giữ nguyên khi lỗi, requestKey đổi sau 4xx.

const dossierId = '11111111-1111-4111-8111-111111111111';
const itemId = '22222222-2222-4222-8222-222222222222';
const stepId = '33333333-3333-4333-8333-333333333333';
const instructionId = '44444444-4444-4444-8444-444444444444';
const appointmentId = '55555555-5555-4555-8555-555555555555';

const user = { id: 9, userName: 'synthetic.dentist', fullName: 'SYNTHETIC Dentist',
  accountType: 'Staff', roles: ['Dentist'], effectiveRoles: ['Dentist'], permissions: ['patients.read'] };

const item = { id: itemId, sourceDiagnosisId: null, serviceId: 3, serviceName: 'Trám răng',
  region: '25', unit: 'răng', quantity: 1, plannedOrder: 1, estimatedMinutes: 30,
  estimatedStart: null, estimatedEnd: null, agreedUnitPriceVnd: 300000, decision: 'Agreed',
  progress: 'NotStarted', counseling: 'Đã tư vấn', decisionMaker: 'Bệnh nhân',
  representativeBasis: null, decidedAt: '2026-10-19T00:00:00Z', consentDocumentId: null,
  consentPlanVersion: null, warrantyPolicy: null, warrantyPolicyVersion: null,
  warrantyScope: null, warrantyConfirmedAt: null, warrantyEndsOn: null };

const dossier = { id: dossierId, code: 'HS-SYNTHETIC', patientId: 123, branchId: 1, dentistId: 2,
  startedOn: '2026-10-10', diagnosis: 'SYNTHETIC diagnosis', note: null, state: 'Open',
  closureOutcome: null, closureReason: null, version: 3, planId: crypto.randomUUID(),
  planVersion: 2, planState: 'Committed', goals: 'Điều trị', paymentTerms: [], timeline: [],
  financialSummaryAvailable: false, createdAt: '2026-10-19T00:00:00Z', updatedAt: '2026-10-19T00:00:00Z',
  items: [item] };

const step = { id: stepId, itemId, name: 'Trám răng 25', region: '25', planVersion: 2,
  estimatedMinutes: 20, reservedMinutes: 30, predecessorIds: [], capability: null, labRequirement: null,
  earliestOffsetDays: null, targetOffsetDays: null, latestOffsetDays: null,
  chargingCoverage: 'IncludedInItem', outcome: 'NotStarted', readiness: 'Ready', blockers: [],
  holdReason: null, reviewCondition: null, version: 1 };

const needsBooking = { id: instructionId, patientId: 123, branchId: 1,
  source: { dossierId, itemId, stepId, visitId: null, activityId: null },
  purpose: 'TreatmentContinuation', reason: 'Quay lại làm tiếp bước trám 25', guidance: 'Bác sĩ xem lại trước khi làm',
  earliestOn: '2026-11-01', targetOn: '2026-11-03', latestOn: '2026-11-05',
  durationMinutes: 30, dentistId: 2, capability: null,
  status: 'NeedsBooking', overdue: false, activeAppointmentId: null,
  version: 1, allowedActions: ['reviseWindow', 'cancel', 'supersede', 'findSlots'] };

const booked = { ...needsBooking, status: 'Booked', activeAppointmentId: appointmentId,
  version: 2, allowedActions: ['reviseWindow', 'complete', 'cancel', 'supersede'] };

async function signIn(page: Page) {
  await page.goto('http://127.0.0.1:5173/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill('synthetic.dentist');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('synthetic');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
}

async function openDossier(page: Page) {
  await page.evaluate(path => { history.pushState(null, '', path); window.dispatchEvent(new PopStateEvent('popstate')); },
    `/patients/123/dossiers/${dossierId}`);
  await expect(page.getByText('Chỉ định quay lại (S24)')).toBeVisible();
}

async function mockCommon(page: Page) {
  // Catch-all đăng ký trước để các route cụ thể đăng ký sau thắng (Playwright kiểm tra từ dưới lên).
  await page.route('**/api/**', route => route.fulfill({ status: 404, json: { code: 'MSG144', message: 'not mocked' } }));
  await page.route('**/api/auth/me', route => route.fulfill({ json: user }));
  await page.route('**/api/auth/sign-in', route => route.fulfill({ json: { accessToken: 'synthetic-token', user } }));
  await page.route(`**/api/dossiers/${dossierId}`, route => route.fulfill({ json: dossier }));
  await page.route(`**/api/dossiers/${dossierId}/steps`, route => route.fulfill({ json: [step] }));
  await page.route('**/api/appointments/options', route => route.fulfill({ json: {
    branches: [{ id: 1, name: 'Chi nhánh thử nghiệm' }],
    dentists: [{ id: 2, branchId: 1, name: 'Bác sĩ thử nghiệm' }],
    chairs: [{ id: 7, branchId: 1, name: 'Ghế 1' }],
    services: [{ id: 3, branchId: 1, name: 'Trám răng', publicDurationMinutes: 30 }],
    ownScheduleOnly: false,
  } }));
  await page.route('**/api/patients/123', route => route.fulfill({ json: { id: 123, fullName: 'Nguyễn Văn Thử' } }));
}

test('S24 find slots mở S11 với followUpId; trạng thái Booked chỉ sau server commit', async ({ page }) => {
  let list = [needsBooking];

  await mockCommon(page);
  await page.route('**/api/follow-up-instructions*', route => {
    void route.fulfill({ json: list });
  });
  await page.route('**/api/availability*', route => route.fulfill({ json: { slots: [{
    start: '2026-11-03T02:00:00.000Z', end: '2026-11-03T02:30:00.000Z',
    chairId: 7, dentistId: 2, assistantId: null, scheduleVersion: 1, assistantScheduleVersion: null,
  }] } }));
  const appointmentBodies: Record<string, unknown>[] = [];
  let appointmentCaptured: (() => void) | undefined;
  const appointmentCapturedPromise = new Promise<void>(resolve => { appointmentCaptured = resolve; });
  await page.route('**/api/appointments', async route => {
    const body = route.request().postDataJSON() as Record<string, unknown>;
    appointmentBodies.push(body);
    appointmentCaptured?.();
    // Delay để khẳng định UI KHÔNG hiển thị Booked trước khi server commit.
    await new Promise(resolve => setTimeout(resolve, 700));
    await route.fulfill({ json: {
      appointmentId, code: 'AP-TEST', version: 1, status: 'Confirmed',
      startAt: '2026-11-03T02:00:00.000Z', endAt: '2026-11-03T02:30:00.000Z',
      notification: { code: 'MSG29', message: 'MSG29: Đã xác nhận lịch hẹn.' },
    } });
    list = [booked];
  });
  await page.route(`**/api/appointments/${appointmentId}`, route => route.fulfill({ json: {
    appointment: { id: appointmentId, code: 'AP-TEST', branchId: 1, branchName: 'Chi nhánh thử nghiệm',
      startAt: '2026-11-03T02:00:00.000Z', endAt: '2026-11-03T02:30:00.000Z', durationMinutes: 30,
      dentistId: 2, dentistName: 'Bác sĩ thử nghiệm', chairId: 7, chairName: 'Ghế 1',
      serviceName: 'Trám răng', attendeeName: 'Nguyễn Văn Thử', attendeeRelation: 'Self',
      kind: 'FollowUp', source: 'Phone', status: 'Confirmed', version: 1 },
    bookerName: 'SYNTHETIC Dentist', bookerPhoneMasked: '0900***000', bookerEmailMasked: null,
    bookedFromApp: false, reason: null, exceptionReason: null, createdAt: '2026-11-03T00:00:00Z',
  } }));

  await signIn(page);
  await openDossier(page);
  await expect(page.getByText('Chưa đặt lịch')).toBeVisible();

  await page.locator('.ant-collapse-header').filter({ hasText: 'Tiếp tục điều trị' }).click();
  await page.getByRole('button', { name: 'Tìm khung giờ (S11)' }).click();

  // Handoff: kind FollowUp, bệnh nhân/khóa theo chỉ định, cửa sổ hiển thị rõ.
  await expect(page.getByText('Đặt lịch tại quầy (S11)')).toBeVisible();
  await expect(page.getByText('Tái khám', { exact: true })).toBeVisible();
  await expect(page.getByText('Nguyễn Văn Thử')).toBeVisible();
  await expect(page.getByText('30 phút (khóa theo chỉ định)')).toBeVisible();
  await expect(page.getByText('Trong cửa sổ 01/11/2026 – 05/11/2026 của chỉ định.')).toBeVisible();

  await page.getByLabel('SĐT (10 số) hoặc email').fill('0900000000');
  await page.getByLabel('Dịch vụ', { exact: true }).click();
  await page.locator('.ant-select-item-option').filter({ hasText: 'Trám răng' }).click();
  await expect(page.getByRole('button', { name: '09:00' })).toBeVisible();
  await page.getByRole('button', { name: '09:00' }).click();
  await page.getByRole('button', { name: 'Xem lại', exact: true }).click();
  await expect(page.getByText(`${instructionId.slice(0, 8)} · v1 · 30 phút · cửa sổ 01/11/2026–05/11/2026`)).toBeVisible();

  await page.getByRole('button', { name: 'Xác nhận đặt lịch' }).click();
  await appointmentCapturedPromise;

  const body = appointmentBodies[0];
  expect(body.kind).toBe('FollowUp');
  expect(body.followUpId).toBe(instructionId);
  expect(body.durationMinutes).toBe(30);
  expect(body.dentistId).toBe(2);
  expect((body.attendee as { patientId: number }).patientId).toBe(123);
  expect(typeof body.requestKey).toBe('string');

  // Server chưa trả lời → trạng thái hiển thị vẫn là của bản đọc trước (không Booked local).
  await expect(page.getByText('Đã đặt lịch')).toHaveCount(0);

  await expect(page.getByText('Đã đặt lịch')).toBeVisible({ timeout: 5000 });
  await expect(page.getByText('MSG29: Đã xác nhận lịch hẹn.')).toBeVisible();
  await expect(page.getByText('Lịch hẹn AP-TEST')).toBeVisible({ timeout: 5000 });
});

test('S24 tạo chỉ định: validation chặn window sai, lỗi server giữ form, requestKey đổi sau 4xx', async ({ page }) => {
  let failNextCreate = true;
  const createBodies: Record<string, unknown>[] = [];

  await mockCommon(page);
  await page.route('**/api/follow-up-instructions*', async route => {
    if (route.request().method() === 'POST') {
      const body = route.request().postDataJSON() as Record<string, unknown>;
      createBodies.push(body);
      if (failNextCreate) {
        await route.fulfill({ status: 400, json: { code: 'MSG229', message: 'MSG229: Cửa sổ hoặc thời lượng không hợp lệ.' } });
        return;
      }
      await route.fulfill({ status: 201, json: {
        instruction: { ...needsBooking, id: '66666666-6666-4666-8666-666666666666',
          reason: (body.reason as string), version: 1 },
        notification: { code: 'MSG231', message: 'MSG231: Đã lưu chỉ định quay lại. Chưa có lịch hẹn được đặt.' },
      } });
      return;
    }
    await route.fulfill({ json: createBodies.length >= 2 && !failNextCreate
      ? [{ ...needsBooking, id: '66666666-6666-4666-8666-666666666666', reason: createBodies.at(-1)!.reason as string }]
      : [] });
  });

  await signIn(page);
  await openDossier(page);
  await expect(page.getByText('Chưa có chỉ định quay lại cho bệnh nhân này.')).toBeVisible();

  await page.getByRole('button', { name: 'Tạo chỉ định' }).click();
  await expect(page.getByText('Tạo chỉ định quay lại (S24)')).toBeVisible();

  await page.getByLabel('Mục đích (purpose)', { exact: true }).click();
  await page.locator('.ant-select-item-option').filter({ hasText: 'Tái khám sau điều trị' }).click();
  await page.getByLabel('Hạng mục kế hoạch đã đồng ý', { exact: true }).click();
  await page.locator('.ant-select-item-option').filter({ hasText: 'Trám răng · 25' }).click();
  await page.getByLabel('Bước điều trị (step)', { exact: true }).click();
  await page.locator('.ant-select-item-option').filter({ hasText: 'Trám răng 25 · giữ chỗ 30 phút' }).click();

  // Window sai: target < earliest → chặn phía client, không gọi API.
  await page.getByLabel('Ngày sớm nhất (earliestOn)').fill('2026-11-05');
  await page.getByLabel('Ngày mục tiêu (targetOn)').fill('2026-11-04');
  await page.getByLabel('Ngày muộn nhất (latestOn)').fill('2026-11-06');
  await page.getByLabel('Lý do (reason)').fill('SYNTHETIC lý do tái khám');
  await page.getByRole('button', { name: 'Lưu chỉ định' }).click();
  await expect(page.getByText('Ngày mục tiêu phải từ ngày sớm nhất trở đi.')).toBeVisible();
  expect(createBodies).toHaveLength(0);

  // Sửa window hợp lệ → server trả lỗi 400 (MSG229) → giữ nguyên dữ liệu form.
  await page.getByLabel('Ngày mục tiêu (targetOn)').fill('2026-11-05');
  await page.getByRole('button', { name: 'Lưu chỉ định' }).click();
  await expect(page.getByText('MSG229: Cửa sổ hoặc thời lượng không hợp lệ.')).toBeVisible();
  await expect(page.getByText('Tạo chỉ định quay lại (S24)')).toBeVisible();
  await expect(page.getByLabel('Lý do (reason)')).toHaveValue('SYNTHETIC lý do tái khám');
  expect(createBodies).toHaveLength(1);

  const first = createBodies[0];
  expect(first.purpose).toBe('PostoperativeReview');
  expect(first.patientId).toBe(123);
  expect(first.branchId).toBe(1);
  expect(first.durationMinutes).toBe(30);
  expect(first.earliestOn).toBe('2026-11-05');
  expect(first.latestOn).toBe('2026-11-06');
  const source = first.source as { dossierId: string; itemId: string; stepId: string; visitId: null; activityId: null };
  expect(source).toEqual({ dossierId, itemId, stepId, visitId: null, activityId: null });

  // Server nhận lần thứ hai → drawer đóng, trạng thái từ response của server.
  failNextCreate = false;
  await page.getByRole('button', { name: 'Lưu chỉ định' }).click();
  await expect(page.getByText('Tạo chỉ định quay lại (S24)')).toHaveCount(0);
  await expect(page.getByText('MSG231: Đã lưu chỉ định quay lại. Chưa có lịch hẹn được đặt.')).toBeVisible();
  await expect(page.getByText('Chưa đặt lịch')).toBeVisible();
  expect(createBodies).toHaveLength(2);
  // 4xx làm đổi requestKey để server không trộn hai payload khác nhau vào cùng một command.
  expect(createBodies[0].requestKey).not.toBe(createBodies[1].requestKey);
});
