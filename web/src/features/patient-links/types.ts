import type { SystemMessage } from '../../services/api';

export type LinkStatus = 'PENDING' | 'VERIFIED' | 'REJECTED' | 'REVOKED' | 'WITHDRAWN';
export const statusLabels: Record<LinkStatus, string> = {
  PENDING: 'Chờ xác minh', VERIFIED: 'Đã xác minh', REJECTED: 'Từ chối', REVOKED: 'Đã thu hồi', WITHDRAWN: 'Đã rút',
};
export const statusColors: Record<LinkStatus, string> = {
  PENDING: 'gold', VERIFIED: 'green', REJECTED: 'red', REVOKED: 'volcano', WITHDRAWN: 'default',
};

export type LinkQueueItem = {
  id: number; requestCode: string; accountId: number; accountUserName: string; accountFullName: string;
  declaredFullName: string; requestedAt: string; status: LinkStatus; handlerName: string | null; handledAt: string | null;
};
export type LinkQueuePage = { items: LinkQueueItem[]; total: number; page: number; pageSize: number };
export type PatientSummary = {
  id: number; patientCode: string; fullName: string; birthDate: string | null; gender: string | null; phone: string | null;
};
export type PatientCandidate = { patient: PatientSummary; hasActiveLink: boolean };
export type LinkReviewDetail = {
  id: number; requestCode: string; status: LinkStatus; requestedAt: string;
  account: {
    id: number; userName: string; fullName: string; email: string | null; emailConfirmed: boolean;
    phone: string | null; phoneConfirmed: boolean; isActive: boolean; createdAt: string;
  };
  declaredFullName: string; declaredBirthDate: string | null; declaredPatientCode: string | null; matchingInformation: string | null;
  patient: PatientSummary | null; verificationNote: string | null; decisionReason: string | null; publicNote: string | null;
  reviewerName: string | null; reviewedAt: string | null; revokerName: string | null; revokedAt: string | null;
  withdrawnAt: string | null; conflict: { accountHasActiveLink: boolean; activeRequestCode: string | null }; version: number;
};
export type LinkDecisionResult = { request: LinkReviewDetail; notification: SystemMessage };
export type LinkAudit = {
  id: number; actorAccountId: number; actorName: string; action: string; reason: string | null;
  occurredAt: string; beforeJson: string; afterJson: string;
};

export const auditLabels: Record<string, string> = {
  Submit: 'Gửi yêu cầu', Withdraw: 'Rút yêu cầu', Verify: 'Xác minh', Reject: 'Từ chối', Revoke: 'Thu hồi',
  OwnRecordRead: 'Đọc hồ sơ tự thân',
};

// Same comparison as the server [PROPOSED]: ignore case, Vietnamese diacritics and repeated spaces.
const normalizeName = (value: string) => value.trim().replace(/đ/g, 'd').replace(/Đ/g, 'D')
  .normalize('NFD').replace(/[̀-ͯ]/g, '').toUpperCase().split(/\s+/).filter(Boolean).join(' ');

// Fields where the declared identity differs from a Patient; the server refuses verification (MSG143) if any differ.
export function identityMismatches(detail: LinkReviewDetail, patient: PatientSummary): string[] {
  const result: string[] = [];
  if (normalizeName(detail.declaredFullName) !== normalizeName(patient.fullName)) result.push('Họ tên');
  if (detail.declaredBirthDate && patient.birthDate && detail.declaredBirthDate !== patient.birthDate) result.push('Ngày sinh');
  if (detail.declaredPatientCode && detail.declaredPatientCode.toUpperCase() !== patient.patientCode.toUpperCase()) result.push('Mã hồ sơ');
  return result;
}

export const formatTime =(value: string | null | undefined) =>
  value ? new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : '—';
export const formatDate = (value: string | null | undefined) =>
  value ? value.split('-').reverse().join('/') : '—';
