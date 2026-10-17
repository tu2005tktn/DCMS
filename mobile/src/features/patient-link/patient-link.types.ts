// Contract: docs/implementation/UC36-S07-S08.md (GET/POST /api/patient-links/me).
export type SystemMessage = { code: string; message: string };
export type LinkStatus = 'NOT_LINKED' | 'PENDING' | 'VERIFIED' | 'REJECTED' | 'REVOKED' | 'WITHDRAWN';

export type SelfLinkRequest = {
  id: number; requestCode: string; status: Exclude<LinkStatus, 'NOT_LINKED'>; requestedAt: string;
  declaredFullName: string; declaredBirthDate: string | null; declaredPatientCode: string | null;
  matchingInformation: string | null; decidedAt: string | null; result: SystemMessage | null;
  publicNote: string | null; version: number;
};
export type SelfLinkOverview = {
  account: { id: number; userName: string; fullName: string; verifiedEmail: string | null; verifiedPhone: string | null };
  linkStatus: LinkStatus; current: SelfLinkRequest | null; history: SelfLinkRequest[]; canSubmit: boolean;
};
export type SubmitLinkRequest = {
  fullName: string; birthDate: string | null; patientCode: string | null; matchingInformation: string | null; confirmSelf: boolean;
};
export type SelfLinkChange = { request: SelfLinkRequest; notification: SystemMessage; created: boolean };
export type OwnRecordSummary = { patientCode: string; fullName: string; birthDate: string | null; linkedAt: string };

// "Hồ sơ của tôi" (docs/implementation/UC08-09-38-S05-S06.md §10). The clinic's internal note is never returned.
export type Gender = 'MALE' | 'FEMALE' | 'OTHER';
export type OwnPatientRecord = {
  patientCode: string; fullName: string; birthDate: string | null; gender: Gender | null; phone: string | null;
  address: string | null; guardianName: string | null; guardianRelationship: string | null; guardianPhone: string | null;
  isArchived: boolean; linkedAt: string; updatedAt: string; version: number;
};
export type UpdateOwnRecord = Omit<OwnPatientRecord, 'patientCode' | 'isArchived' | 'linkedAt' | 'updatedAt'>;
export type OwnRecordChange = { record: OwnPatientRecord; notification: SystemMessage };
