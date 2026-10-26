import type { SystemMessage } from '../../services/api';

export type TreatmentItem = {
  id: string; sourceDiagnosisId: number | null; serviceId: number; serviceName: string; region: string | null;
  material: string | null;
  unit: string; quantity: number; plannedOrder: number; estimatedMinutes: number | null;
  estimatedStart: string | null; estimatedEnd: string | null; agreedUnitPriceVnd: number;
  decision: 'Proposed' | 'Agreed' | 'Refused' | 'Deferred'; progress: string;
  counseling: string | null; decisionMaker: string | null; representativeBasis: string | null;
  decidedAt: string | null; consentDocumentId: string | null; consentPlanVersion: number | null;
  decisionPlanVersion: number | null;
  warrantyPolicy: string | null; warrantyPolicyVersion: string | null; warrantyScope: string | null;
  warrantyConfirmedAt: string | null; warrantyEndsOn: string | null;
  estimatedMinMinutes: number | null; estimatedMaxMinutes: number | null; reservedMinutes: number | null;
  estimatedSessions: number | null; returnEarliestOn: string | null; returnTargetOn: string | null;
  returnLatestOn: string | null; shortReservationAcknowledgedBy: number | null;
  shortReservationAcknowledgedAt: string | null;
  referenceUnitPriceVnd: number | null; referencePriceVersion: number | null;
  discount: TreatmentDiscount | null; warrantyComponent: string | null; warrantyExclusions: string | null;
  warrantyActivatedOn: string | null; warrantyExplanation: string | null;
  expectedServiceLifeMonths: number | null;
};
export type TreatmentDiscount = { type: 'Fixed' | 'Percent'; value: number; reason: string;
  qualifyingItemId: string | null; status: 'Proposed' | 'Approved' | 'Rejected';
  decidedBy: number | null; decidedAt: string | null; amountVnd: number };
export type PaymentTerm = { purpose: string; amountVnd: number; dueOn: string; evidence: string | null;
  milestone: string | null; planVersion: number | null };
export type ServicePrice = { serviceId: number; branchId: number; name: string;
  referenceUnitPriceVnd: number | null; priceVersion: number };
export type PricePreview = { dossierId: string; dossierVersion: number; planVersion: number;
  items: { itemId: string; grossVnd: number; approvedDiscountVnd: number; netVnd: number;
    referenceUnitPriceVnd: number | null; decision: TreatmentItem['decision'];
    includedInTotal: boolean }[]; grossVnd: number; approvedDiscountVnd: number;
  netBeforeTaxVnd: number };
export type TreatmentEvent = { id: string; kind: string; dossierVersion: number; actorId: number;
  occurredAt: string; payloadJson: string };
export type TreatmentDossier = {
  id: string; code: string; patientId: number; branchId: number; dentistId: number;
  startedOn: string; diagnosis: string; note: string | null; state: 'Open' | 'Completed' | 'Stopped';
  closureOutcome: string | null; closureReason: string | null; version: number;
  planId: string; planVersion: number; planState: 'None' | 'Draft' | 'Committed'; goals: string | null;
  items: TreatmentItem[]; paymentTerms: PaymentTerm[]; timeline: TreatmentEvent[];
  financialSummaryAvailable: boolean; createdAt: string; updatedAt: string;
  financialSummary?: { available: boolean; plannedVnd: number; postedChargeVnd: number | null;
    receivedVnd: number | null; allocatedVnd: number | null; outstandingVnd: number | null;
    sourceVersion: string | null };
  depositExceptionReason?: string | null; depositExceptionBy?: number | null;
  depositExceptionAt?: string | null;
  displayCompleted: boolean;
};
export type DossierListItem = Pick<TreatmentDossier, 'id' | 'code' | 'patientId' | 'branchId' |
  'dentistId' | 'diagnosis' | 'state' | 'closureOutcome' | 'version' | 'planVersion' | 'updatedAt' | 'displayCompleted'>;
export type DossierPage = { items: DossierListItem[]; total: number; page: number; pageSize: number };
export type TreatmentChange = { dossier: TreatmentDossier; notification: SystemMessage };
export type AppointmentOptions = { branches: { id: number; name: string }[];
  dentists: { id: number; branchId: number; name: string }[];
  services: { id: number; branchId: number; name: string }[] };

export const decisionLabel: Record<TreatmentItem['decision'], string> = {
  Proposed: 'Đề xuất', Agreed: 'Đã đồng ý', Refused: 'Từ chối', Deferred: 'Hoãn quyết định',
};
export const stateLabel: Record<TreatmentDossier['state'], string> = {
  Open: 'Đang điều trị', Completed: 'Đã kết thúc', Stopped: 'Đã dừng',
};
export const money = (amount: number) => `${new Intl.NumberFormat('vi-VN').format(amount)} ₫`;
