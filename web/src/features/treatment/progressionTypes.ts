import type { SystemMessage } from '../../services/api';

export const durationOptions = Array.from({ length: 32 }, (_, index) => ({
  label: `${(index + 1) * 15} phút`, value: (index + 1) * 15,
}));
export const durationOptionsFor = (value: number | null | undefined) => value != null &&
  !durationOptions.some(option => option.value === value)
  ? [{ label: `${value} phút (đã lưu)`, value }, ...durationOptions] : durationOptions;
export function durationRangeValid(min: number | null | undefined, max: number | null | undefined,
  selected: number | null | undefined, reserved: number | null | undefined): boolean {
  if (!selected || selected < 1 || selected > 480 || !reserved || reserved < selected || reserved > 480 || reserved % 15 !== 0) return false;
  if (min == null && max == null) return true;
  return min != null && max != null && min > 0 && min <= selected && selected <= max && max <= 480;
}

export type StepDefinition = {
  logicalStepId: string; itemId: string | null; name: string; region: string | null;
  estimatedMinMinutes: number | null; estimatedMaxMinutes: number | null;
  estimatedMinutes: number; reservedMinutes: number; predecessorIds: string[];
  capability: string | null; labRequirement: string | null;
  earliestOffsetDays: number | null; targetOffsetDays: number | null; latestOffsetDays: number | null;
  chargingCoverage: 'IncludedInItem' | 'FinancialReview';
};
export type TemplateVersion = { number: number; state: 'Draft' | 'Active' | 'Retired';
  steps: StepDefinition[]; createdAt: string; activatedAt: string | null; retiredAt: string | null };
export type TreatmentTemplate = { id: string; branchId: number; name: string; version: number;
  versions: TemplateVersion[] };
export type TemplateListItem = { id: string; branchId: number; name: string; version: number;
  activeVersion: number | null; createdAt: string };
export type PlanStep = { id: string; itemId: string; name: string; region: string | null;
  planVersion: number; estimatedMinMinutes: number | null; estimatedMaxMinutes: number | null;
  estimatedMinutes: number; reservedMinutes: number; predecessorIds: string[];
  capability: string | null; labRequirement: string | null; earliestOffsetDays: number | null;
  targetOffsetDays: number | null; latestOffsetDays: number | null; chargingCoverage: string;
  outcome: string; readiness: 'Ready' | 'Blocked'; blockers: string[];
  holdReason: string | null; reviewCondition: string | null; version: number;
  shortReservationAcknowledgedBy: number | null; shortReservationAcknowledgedAt: string | null };
export type StepChange = { steps: PlanStep[]; instructionIds: string[]; notification: SystemMessage };
