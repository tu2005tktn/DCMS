import type { TreatmentDossier } from './types';

export const clinicalPreview = {
  patientId: 123,
  branchId: 1,
  visitId: '11111111-1111-4111-8111-111111111111',
  dossierId: '22222222-2222-4222-8222-222222222222',
  fillingItemId: '33333333-3333-4333-8333-333333333333',
  rootCanalItemId: '44444444-4444-4444-8444-444444444444',
  fillingStepId: '55555555-5555-4555-8555-555555555555',
  rootCanalStepId: '66666666-6666-4666-8666-666666666666',
};

export const clinicalPreviewHome = '/__preview/clinical';
export const clinicalPreviewTreatment = `${clinicalPreviewHome}/${clinicalPreview.patientId}/${clinicalPreview.visitId}/treatment?dossierId=${clinicalPreview.dossierId}&itemId=${clinicalPreview.fillingItemId}`;
export const clinicalPreviewReview = `${clinicalPreviewHome}/${clinicalPreview.patientId}/${clinicalPreview.visitId}/review?dossierId=${clinicalPreview.dossierId}`;

type MockActivity = {
  id: string; visitId: string; patientId: number; branchId: number; dossierId: string | null;
  itemId: string | null; stepId: string | null; planVersion: number | null;
  serviceId: number; region: string | null; unit: string; quantity: number;
  performedAt: string; elapsedMinutes: number | null; performerId: number;
  outcome: string; nextSteps: string | null; chargingBasis: string;
  chargingBasisEvidence: string | null; priceSnapshotVnd: number; reviewId: string | null;
  state: 'Draft' | 'Posted'; version: number; createdAt: string; postedAt: string | null;
};
type MockStep = { id: string; itemId: string; name: string; version: number;
  readiness: 'Ready' | 'Blocked'; outcome: string };
type MockReview = { id: string; visitId: string; patientId: number; branchId: number;
  sourceActivityId: string | null; sourceItemId: string | null; complaint: string;
  findings: string; coverage: string; outcome: string; coveredComponent: string | null;
  exclusions: string | null; policy: string | null; policyVersion: string | null;
  basis: string | null; nextAction: string | null; dentistAccountId: number;
  recordedAt: string; version: number };

function item(id: string, serviceId: number, serviceName: string, region: string,
  price: number, order: number): TreatmentDossier['items'][number] {
  return {
    id, sourceDiagnosisId: null, serviceId, serviceName, region, material: null,
    unit: 'răng', quantity: 1, plannedOrder: order, estimatedMinutes: 60,
    estimatedStart: null, estimatedEnd: null, agreedUnitPriceVnd: price,
    decision: 'Agreed', progress: 'NotStarted', counseling: 'Đã tư vấn và đồng ý',
    decisionMaker: 'Bệnh nhân giả', representativeBasis: null,
    decidedAt: '2026-10-25T02:00:00Z', consentDocumentId: null,
    consentPlanVersion: 1, decisionPlanVersion: 1,
    warrantyPolicy: null, warrantyPolicyVersion: null, warrantyScope: null,
    warrantyConfirmedAt: null, warrantyEndsOn: null,
    estimatedMinMinutes: null, estimatedMaxMinutes: null, reservedMinutes: null,
    estimatedSessions: null, returnEarliestOn: null, returnTargetOn: null, returnLatestOn: null,
    shortReservationAcknowledgedBy: null, shortReservationAcknowledgedAt: null,
    referenceUnitPriceVnd: null, referencePriceVersion: null, discount: null,
    warrantyComponent: null, warrantyExclusions: null, warrantyActivatedOn: null,
    warrantyExplanation: null, expectedServiceLifeMonths: null,
  };
}

const dossier: TreatmentDossier = {
  id: clinicalPreview.dossierId, code: 'HS-MOCK-001', patientId: clinicalPreview.patientId,
  branchId: clinicalPreview.branchId, dentistId: 9, startedOn: '2026-10-25',
  diagnosis: 'Tình huống thử: trám răng 25 và chữa tủy răng 46', note: null,
  state: 'Open', closureOutcome: null, closureReason: null, version: 1,
  planId: '77777777-7777-4777-8777-777777777777', planVersion: 1,
  planState: 'Committed', goals: 'Theo dõi tiến độ từng hạng mục',
  items: [item(clinicalPreview.fillingItemId, 3, 'Trám răng', '25', 300000, 1),
    item(clinicalPreview.rootCanalItemId, 4, 'Chữa tủy', '46', 1200000, 2)],
  paymentTerms: [], timeline: [], financialSummaryAvailable: false,
  createdAt: '2026-10-25T02:00:00Z', updatedAt: '2026-10-25T02:00:00Z',
};
const steps: MockStep[] = [
  { id: clinicalPreview.fillingStepId, itemId: clinicalPreview.fillingItemId,
    name: 'Trám răng 25', version: 1, readiness: 'Ready', outcome: 'NotStarted' },
  { id: clinicalPreview.rootCanalStepId, itemId: clinicalPreview.rootCanalItemId,
    name: 'Bước đầu chữa tủy 46', version: 1, readiness: 'Ready', outcome: 'NotStarted' },
];
const activities: MockActivity[] = [{
  id: '88888888-8888-4888-8888-888888888888', visitId: clinicalPreview.visitId,
  patientId: clinicalPreview.patientId, branchId: clinicalPreview.branchId,
  dossierId: clinicalPreview.dossierId, itemId: clinicalPreview.fillingItemId,
  stepId: null, planVersion: 1, serviceId: 3, region: '25', unit: 'răng',
  quantity: 1, performedAt: '2026-10-20T07:00:00Z', elapsedMinutes: 30,
  performerId: 9, outcome: 'Hoạt động nguồn giả để thử S63', nextSteps: null,
  chargingBasis: 'Included', chargingBasisEvidence: null, priceSnapshotVnd: 300000,
  reviewId: null, state: 'Posted', version: 2,
  createdAt: '2026-10-20T07:00:00Z', postedAt: '2026-10-20T07:30:00Z',
}];
const reviews: MockReview[] = [];
const receipts = new Map<string, { fingerprint: string; result: unknown }>();

function problem(status: number, code: string, message: string): Error {
  return Object.assign(new Error(message), { status, code });
}
function payload(init: RequestInit): Record<string, unknown> {
  return init.body ? JSON.parse(String(init.body)) as Record<string, unknown> : {};
}
function replay(key: string, input: unknown): unknown | undefined {
  const receipt = receipts.get(key);
  if (!receipt) return undefined;
  if (receipt.fingerprint !== JSON.stringify(input))
    throw problem(409, 'MSG111', 'Mã yêu cầu đã dùng cho nội dung khác.');
  return structuredClone(receipt.result);
}
function remember(key: string, input: unknown, result: unknown): void {
  receipts.set(key, { fingerprint: JSON.stringify(input), result: structuredClone(result) });
}

/** Development-only adapter. Never forwards a preview request to the real API. */
export async function clinicalPreviewRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
  const method = (init.method ?? 'GET').toUpperCase();
  const pathname = path.split('?')[0];
  const input = payload(init);
  if (pathname === '/api/auth/me' && method === 'GET') return {
    id: 9, userName: 'synthetic.dentist', fullName: 'Bác sĩ dữ liệu giả',
    accountType: 'Staff', roles: ['Dentist'], effectiveRoles: ['Dentist'],
    permissions: ['patients.read'], hasVerifiedPatientLink: false,
  } as T;
  if (pathname === '/api/clinical-services' && method === 'GET')
    return [{ id: 3, name: 'Trám răng' }, { id: 4, name: 'Chữa tủy' }] as T;
  if (pathname === `/api/dossiers/${clinicalPreview.dossierId}` && method === 'GET') return dossier as T;
  if (pathname === `/api/dossiers/${clinicalPreview.dossierId}/steps` && method === 'GET') return steps as T;
  if (pathname === `/api/visits/${clinicalPreview.visitId}/treatment-context` && method === 'GET')
    return { visit: { available: true, inProgress: true, patientId: clinicalPreview.patientId,
      branchId: clinicalPreview.branchId, version: 1 }, steps,
    activities: activities.filter(row => row.state === 'Posted') } as T;
  if (pathname === `/api/visits/${clinicalPreview.visitId}/reviews` && method === 'GET')
    return [...reviews] as T;
  if (pathname === `/api/visits/${clinicalPreview.visitId}/reviews` && method === 'POST') {
    const key = String(input.requestKey);
    const previous = replay(key, input);
    if (previous) return previous as T;
    const sourceActivityId = input.sourceActivityId as string | null ?? null;
    const sourceItemId = input.sourceItemId as string | null ?? null;
    if (!sourceActivityId && !sourceItemId)
      throw problem(400, 'MSG04', 'Cần chọn hoạt động hoặc hạng mục nguồn.');
    const source = activities.find(row => row.id === sourceActivityId && row.state === 'Posted');
    if (sourceActivityId && !source) throw problem(404, 'MSG113', 'Không tìm thấy hoạt động nguồn.');
    const row: MockReview = { ...input, id: crypto.randomUUID(),
      visitId: clinicalPreview.visitId, patientId: clinicalPreview.patientId,
      branchId: clinicalPreview.branchId, sourceActivityId, sourceItemId,
      complaint: String(input.complaint), findings: String(input.findings),
      coverage: String(input.coverage), outcome: String(input.outcome),
      coveredComponent: input.coveredComponent as string | null ?? null,
      exclusions: input.exclusions as string | null ?? null,
      policy: input.policy as string | null ?? null,
      policyVersion: input.policyVersion as string | null ?? null,
      basis: input.basis as string | null ?? null,
      nextAction: input.nextAction as string | null ?? null,
      dentistAccountId: 9, recordedAt: new Date().toISOString(), version: 1 };
    reviews.unshift(row); remember(key, input, row); return row as T;
  }
  if (pathname === `/api/visits/${clinicalPreview.visitId}/activities/drafts` && method === 'POST') {
    const key = String(input.requestKey);
    const previous = replay(key, input);
    if (previous) return previous as T;
    const activity = input.activity as Record<string, unknown>;
    if (!activity || activity.visitId !== clinicalPreview.visitId || !activity.serviceId)
      throw problem(400, 'MSG04', 'Thông tin hoạt động chưa hợp lệ.');
    const row: MockActivity = { ...activity, id: crypto.randomUUID(),
      visitId: clinicalPreview.visitId, patientId: clinicalPreview.patientId,
      branchId: clinicalPreview.branchId, dossierId: activity.dossierId as string | null ?? null,
      itemId: activity.itemId as string | null ?? null,
      stepId: activity.stepId as string | null ?? null,
      planVersion: activity.planVersion as number | null ?? null,
      serviceId: Number(activity.serviceId), region: activity.region as string | null ?? null,
      unit: String(activity.unit), quantity: Number(activity.quantity),
      performedAt: String(activity.performedAt),
      elapsedMinutes: activity.elapsedMinutes as number | null ?? null, performerId: 9,
      outcome: String(activity.outcome), nextSteps: activity.nextSteps as string | null ?? null,
      chargingBasis: String(activity.chargingBasis),
      chargingBasisEvidence: activity.chargingBasisEvidence as string | null ?? null,
      priceSnapshotVnd: Number(activity.priceSnapshotVnd),
      reviewId: activity.reviewId as string | null ?? null,
      state: 'Draft', version: 1, createdAt: new Date().toISOString(), postedAt: null };
    activities.push(row); remember(key, input, row); return row as T;
  }
  const activityPath = pathname.match(/^\/api\/activities\/([^/]+)\/(draft|post)$/);
  if (activityPath && ((activityPath[2] === 'draft' && method === 'PUT') ||
    (activityPath[2] === 'post' && method === 'POST'))) {
    const key = String(input.requestKey);
    const previous = replay(key, { pathname, input });
    if (previous) return previous as T;
    const row = activities.find(value => value.id === activityPath[1]);
    if (!row) throw problem(404, 'MSG113', 'Không tìm thấy hoạt động giả.');
    if (row.state !== 'Draft' || row.version !== input.expectedVersion)
      throw problem(409, 'MSG111', 'Hoạt động đã thay đổi.');
    if (activityPath[2] === 'draft') {
      const activity = input.activity as Record<string, unknown>;
      Object.assign(row, activity, { version: row.version + 1 });
    } else {
      if (row.chargingBasis === 'Separate')
        throw problem(503, 'MSG144', 'Nguồn tài chính chưa được kết nối trong bản giả lập.');
      if (row.chargingBasis === 'Free' &&
        !reviews.some(review => review.id === row.reviewId && review.coverage === 'Covered'))
        throw problem(409, 'MSG244', 'Cần đánh giá bảo hành Covered trước khi chọn miễn phí.');
      if (row.stepId) {
        const step = steps.find(value => value.id === row.stepId);
        if (!step || step.readiness !== 'Ready' || step.version !== input.expectedStepVersion)
          throw problem(409, 'MSG230', 'Bước chưa sẵn sàng hoặc đã thay đổi.');
        step.outcome = input.stepOutcome === 'Completed' ? 'Completed' : 'Partial';
        step.version++;
      }
      row.state = 'Posted'; row.postedAt = new Date().toISOString(); row.version++;
    }
    const result = { ...row }; remember(key, { pathname, input }, result); return result as T;
  }
  throw problem(404, 'MSG113', 'Chức năng này không thuộc bản xem thử. Không có yêu cầu nào được gửi tới API thật.');
}
