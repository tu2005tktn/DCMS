// Contract: docs/specs/S41-MOBILE-MEDICAL-RECORDS.md — DTO lấy từ máy chủ, không suy diễn.
export type SystemMessage = { code: string; message: string };
export type { LinkStatus } from '../patient-link/patient-link.types';

// GET /api/patient-links/me/record (điểm kiểm quyền, 403 khi link không còn Verified).
export type OwnRecordSummary = { patientCode: string; fullName: string; birthDate: string | null; linkedAt: string };

// GET /api/patients/me/treatment-summary — OwnTreatmentSummaryItem (projection chính thức của Vinh/DCMS-97).
export type TreatmentItem = {
  id: string; serviceId: number; serviceName: string; region: string | null; unit: string;
  quantity: number; plannedOrder: number; estimatedMinutes: number | null;
  estimatedStart: string | null; estimatedEnd: string | null; agreedUnitPriceVnd: number;
  decision: 'Proposed' | 'Agreed' | 'Refused' | 'Deferred'; progress: string;
  counseling: string | null; decisionMaker: string | null; representativeBasis: string | null;
  decidedAt: string | null; consentDocumentId: string | null; consentPlanVersion: number | null;
  warrantyPolicy: string | null; warrantyPolicyVersion: string | null; warrantyScope: string | null;
  warrantyConfirmedAt: string | null; warrantyEndsOn: string | null;
};
export type OwnTreatmentSummaryItem = {
  dossierId: string; code: string; state: 'Open' | 'Completed' | 'Stopped';
  startedOn: string; diagnosis: string; planVersion: number; agreedItems: TreatmentItem[];
};

// GET /api/me/appointments — MyAppointmentCard của máy chủ (không có branchName: chỉ có ở chi tiết).
export type VisitCard = {
  code: string; start: string; end: string; durationMinutes: number; dentistName: string;
  attendeeName: string; attendeeKind: 'SELF' | 'RELATIVE'; status: string; access: string; version: number;
};
export type VisitPage = { items: VisitCard[]; total: number; page: number; pageSize: number };
export type VisitScope = 'UPCOMING' | 'HISTORY';

export const dossierStateLabels: Record<OwnTreatmentSummaryItem['state'], string> = {
  Open: 'Đang điều trị', Completed: 'Đã kết thúc', Stopped: 'Đã dừng',
};
export const decisionLabels: Record<TreatmentItem['decision'], string> = {
  Proposed: 'Đề xuất', Agreed: 'Đã đồng ý', Refused: 'Từ chối', Deferred: 'Hoãn quyết định',
};
// Giá trị progress do máy chủ đặt; nhãn tiếng Việt chỉ cho các trạng thái biết trước, còn lại hiển thị đúng DTO.
const progressLabels: Record<string, string> = {
  NotStarted: 'Chưa thực hiện', InProgress: 'Đang thực hiện', Completed: 'Đã hoàn thành',
  Partial: 'Một phần', Stopped: 'Đã dừng', Cancelled: 'Đã hủy', Skipped: 'Bỏ qua',
};
export const progressLabel = (value: string) => progressLabels[value] ?? value;
export const money = (amount: number) => `${new Intl.NumberFormat('vi-VN').format(amount)} ₫`;
