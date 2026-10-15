import type { SystemMessage } from '../../services/api';

// Contract: docs/implementation/UC08-09-38-S05-S06.md.
export type Gender = 'MALE' | 'FEMALE' | 'OTHER';
export const genderLabels: Record<Gender, string> = { MALE: 'Nam', FEMALE: 'Nữ', OTHER: 'Khác' };

export type PatientListItem = {
  id: number; patientCode: string; fullName: string; birthDate: string | null; gender: Gender | null; phone: string | null;
  isArchived: boolean; lastVisitAt: string | null; updatedAt: string;
};
export type PatientPage = { items: PatientListItem[]; total: number; page: number; pageSize: number };
export type PatientDetail = {
  id: number; patientCode: string; fullName: string; birthDate: string | null; gender: Gender | null; phone: string | null;
  address: string | null; guardianName: string | null; guardianRelationship: string | null; guardianPhone: string | null;
  note: string | null; isArchived: boolean; archivedAt: string | null; archiveReason: string | null;
  createdAt: string; updatedAt: string; version: number;
  link: { verifiedAccountUserName: string | null; verifiedAt: string | null };
};
export type PatientChange = { patient: PatientDetail; notification: SystemMessage };
export type DuplicateCandidate = { patient: PatientListItem; reasons: string[] };
export type PatientAuditEntry = {
  id: number; action: string; actorName: string; reason: string | null; occurredAt: string;
  changes: { field: string; before: string | null; after: string | null }[];
};

export const auditActionLabels: Record<string, string> = {
  CREATE: 'Tạo hồ sơ', UPDATE: 'Cập nhật hành chính', ARCHIVE: 'Ngừng sử dụng', RESTORE: 'Khôi phục',
};

// MSG115: search succeeded with no match (a failed load is never shown as "no results").
export const noPatientsMessage = 'Không tìm thấy hồ sơ bệnh nhân phù hợp.';
