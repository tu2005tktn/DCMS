import type { VisitResult } from '../reception/types';
export type MedicalFact = { state: 'Unknown' | 'None' | 'Present'; detail?: string | null; verified: boolean };
export type Finding = { tooth?: number | null; region?: string | null; condition: string };
export type ExaminationContent = { symptoms?: string | null; medicalHistory: MedicalFact; allergies: MedicalFact;
  medications: MedicalFact; findings: Finding[]; diagnosis?: string | null; notes?: string | null };
export type Examination = { content: ExaminationContent; confirmed: boolean; updatedBy: number; updatedAt: string };
export type Workspace = { visit: VisitResult; examination: Examination | null; initialContent: ExaminationContent; allowedActions: string[] };
export type History = { visitId: string; arrivalAt: string; examination: Examination };
