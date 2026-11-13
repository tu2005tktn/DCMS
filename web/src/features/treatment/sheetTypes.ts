export type SheetContext = {
  services: { id: number; branchId: number; name: string; unit: string; referenceUnitPriceVnd: number | null; priceVersion: number }[];
  members: { id: number; name: string; canDentist: boolean }[];
  visits: { id: string; version: number; arrivalAt: string; dentistId: number }[];
  steps: { id: string; itemId: string; name: string; version: number; outcome: string; held: boolean }[];
  allowedActions: string[];
  consentDocuments?: { id: string; fileName: string; version: number }[];
};
export type SheetActivity = {
  id: string; visitId: string | null; dossierId: string; itemId: string; planVersion: number;
  serviceId: number; region: string | null; unit: string; quantity: number; performedAt: string;
  performerId: number; outcome: string; version: number; priceSnapshotVnd: number;
  sheetVersion?: number; sheetDeleted?: boolean; sheetDiscountType?: string; sheetDiscountValue?: number; sheetDiscountReason?: string; sourceKind?: string; serviceNameSnapshot?: string; labWorkSnapshot?: string; chargingBasis: string; feeStatus: string; discountSnapshotVnd: number; assistantId?: number;
};
export type ActivityPage = { items: SheetActivity[]; total: number; page: number; pageSize: number };
export type SheetDecision = { decision: string; counseling?: string; decisionMaker?: string; representativeBasis?: string };
export type DossierTotals = { dossierCount: number; plannedGrossVnd: number; plannedDiscountVnd: number;
  plannedNetVnd: number; financialAvailable: boolean; postedNetVnd: number | null;
  confirmedAllocatedVnd: number | null; outstandingVnd: number | null; unusedAdvanceVnd: number | null; pendingFeeCount: number };

export function isFdi(value: string) {
  const n = Number(value), quadrant = Math.floor(n / 10), position = n % 10;
  return /^\d{2}$/.test(value) && ((quadrant >= 1 && quadrant <= 4 && position >= 1 && position <= 8)
    || (quadrant >= 5 && quadrant <= 8 && position >= 1 && position <= 5));
}
