import type { SystemMessage } from '../../services/api';

export type TreatmentItem = {
  id: string; serviceId: number; serviceName: string; region: string | null;
  unit: string; quantity: number; plannedOrder: number; estimatedMinutes: number | null;
  estimatedStart: string | null; agreedUnitPriceVnd: number;
  discount: TreatmentDiscount | null;
  note?: string | null; responsibleDentistId?: number; assistantId?: number;
  displayCompleted?: boolean;
};
export type TreatmentDiscount = { type: 'Fixed' | 'Percent'; value: number; reason: string; amountVnd: number };
export type ServicePrice = { unit?: string; serviceId: number; branchId: number; name: string;
  referenceUnitPriceVnd: number | null; priceVersion: number };
export type PricePreview = { dossierId: string; dossierVersion: number; planVersion: number;
  items: { itemId: string; grossVnd: number; approvedDiscountVnd: number; netVnd: number }[];
  grossVnd: number; approvedDiscountVnd: number; netBeforeTaxVnd: number };
export type TreatmentEvent = { id: string; kind: string; dossierVersion: number; actorId: number;
  occurredAt: string; payloadJson: string; actorName?: string };
export type TreatmentDossier = {
  sourceExaminationVisitId?: string | null;
  id: string; code: string; patientId: number; branchId: number; dentistId: number;
  startedOn: string; diagnosis: string; note: string | null; state: 'Open' | 'Completed' | 'Stopped' | 'Cancelled';
  version: number; planVersion: number;
  items: TreatmentItem[]; timeline: TreatmentEvent[];
  financialSummaryAvailable: boolean; createdAt: string; updatedAt: string;
  financialSummary?: { available: boolean; plannedVnd: number; postedChargeVnd: number | null;
    receivedVnd: number | null; allocatedVnd: number | null; outstandingVnd: number | null;
    sourceVersion: string | null; unusedAdvanceVnd?: number | null; asOf?: string; pendingFeeCount?: number;
    lines?: { activityId: string; chargeId: string; netVnd: number; confirmedAllocatedVnd: number; outstandingVnd: number; includedCoverage: boolean }[] };
};
export type DossierListItem = Pick<TreatmentDossier, 'id' | 'code' | 'patientId' | 'branchId' |
  'dentistId' | 'diagnosis' | 'state' | 'version' | 'updatedAt'>;
export type DossierPage = { items: DossierListItem[]; total: number; page: number; pageSize: number };
export type TreatmentChange = { dossier: TreatmentDossier; notification: SystemMessage };
export type AppointmentOptions = { branches: { id: number; name: string }[];
  dentists: { id: number; branchId: number; name: string }[];
  services: { id: number; branchId: number; name: string }[] };

export const stateLabel: Record<TreatmentDossier['state'], string> = {
  Open: 'Đang điều trị', Completed: 'Đã kết thúc', Stopped: 'Đã dừng', Cancelled: 'Hủy phiếu',
};
export const money = (amount: number) => `${new Intl.NumberFormat('vi-VN').format(amount)} ₫`;
