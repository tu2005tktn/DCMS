import type { SystemMessage } from '../../services/api';

export type StepDefinition = {
  logicalStepId: string; itemId: string | null; name: string; region: string | null;
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
  planVersion: number; estimatedMinutes: number; reservedMinutes: number; predecessorIds: string[];
  capability: string | null; labRequirement: string | null; earliestOffsetDays: number | null;
  targetOffsetDays: number | null; latestOffsetDays: number | null; chargingCoverage: string;
  outcome: string; readiness: 'Ready' | 'Blocked'; blockers: string[];
  holdReason: string | null; reviewCondition: string | null; version: number };
export type StepChange = { steps: PlanStep[]; instructionIds: string[]; notification: SystemMessage };
