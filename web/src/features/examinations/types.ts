import type { ReferenceSnapshot } from './diagnosisReferences';
import type { DocumentItem } from '../clinical-documents/types';
import type { VisitResult } from '../reception/types';
import type { Gender } from '../patients/types';
export type MedicalFact = { state: 'Unknown' | 'None' | 'Present'; detail?: string | null; verified: boolean };
export type Finding = { tooth?: number | null; region?: string | null; condition: string; diagnoses?: string[] | null; surfaces?: string[] | null };
export type ExaminationContent = { symptoms?: string | null; medicalHistory: MedicalFact; allergies: MedicalFact;
  medications: MedicalFact; findings: Finding[]; unscopedDiagnoses?: string[] | null; diagnosisReferences?: ReferenceSnapshot[] | null; diagnoses?: string[] | null; diagnosis?: string | null; notes?: string | null;
  conclusion?: string | null; advice?: string | null; disposition?: string | null; returnNeed?: string | null; referralDestination?: string | null };
export type Examination = { content: ExaminationContent; confirmed: boolean; updatedBy: number; updatedAt: string; id?: string; version?: number };
export type ExaminationPatient = { id: number; patientCode: string; fullName: string; birthDate: string | null; gender: Gender | null; ageYears: number | null };
export type Workspace = { visit: VisitResult; examination: Examination | null; initialContent: ExaminationContent; allowedActions: string[]; patient: ExaminationPatient; investigations?: Investigation[] };
export type History = { visitId: string; arrivalAt: string; examination: Examination };
export type ExaminationVersion = { version: number; content: ExaminationContent; confirmed: boolean; actorId: number; recordedAt: string; reason?: string | null };
export type PatientExamination = { visitId: string; appointmentCode: string; branchId: number; branchName: string; dentistName: string; arrivalAt: string; visitStatus: string; examination: Examination | null };
export const dispositionLabels: Record<string, string> = { ConsultationOnly: 'Tư vấn, chưa điều trị', FollowUp: 'Theo dõi / cần quay lại', Referral: 'Chuyển khám', DeferredDecision: 'Hoãn quyết định điều trị', ProceedToTreatment: 'Tiếp tục điều trị' };

export const unscopedDiagnoses = (content: ExaminationContent): string[] => content.unscopedDiagnoses ?? content.diagnoses ?? (content.diagnosis?.trim() ? [content.diagnosis] : []);
export const uniqueDiagnoses = (values: string[]): string[] => [...new Map(values.map(value => [value.trim().toLocaleLowerCase('vi'), value.trim()])).values()].filter(Boolean);
export const selectedDiagnoses = (content: ExaminationContent): string[] => uniqueDiagnoses([...unscopedDiagnoses(content), ...content.findings.flatMap(f => f.diagnoses ?? [])]);
export const findingText = (finding: Finding): string => [...(finding.diagnoses ?? []), finding.condition].filter(Boolean).join(' · ');
export const diagnosisText = (content: ExaminationContent): string => selectedDiagnoses(content).join("; ");

export type Investigation = { id: string; visitId: string; kind: 'XRay' | 'Imaging' | 'Lab' | 'Pathology' | 'Other'; name: string; region?: string | null; reason: string;
  requiredForDiagnosis: boolean; state: 'Ordered' | 'ResultAvailable' | 'Reviewed' | 'Cancelled'; version: number;
  createdByName: string; createdAt: string; resultText?: string | null; resultByName?: string | null; resultAt?: string | null;
  interpretation?: string | null; reviewedByName?: string | null; reviewedAt?: string | null; cancellationReason?: string | null; cancelledAt?: string | null;
  resultDocumentIds: string[]; documents: DocumentItem[]; allowedActions: string[] };
export type InvestigationPage = { visitId: string; visitVersion: number; canOrder: boolean; items: Investigation[] };
