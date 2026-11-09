export type FamilyAccess = {
  id: string; familyMemberId: string; code: string; status: string; fullName: string; birthDate: string | null;
  patientCode: string | null; matchingInformation: string | null; requestedAt: string; decidedAt: string | null;
  expiresAt: string | null; scopes: string[]; publicNote: string | null; version: number;
};
export type FamilyAccessOverview = { current: FamilyAccess | null; history: FamilyAccess[]; canRequest: boolean };
export type RecordHeader = { patientCode: string; fullName: string; birthDate: string | null; branchName: string; scopes: string[]; expiresAt: string };
export type RecordPage<T> = { items: T[]; total: number; page: number; pageSize: number };
export const accessLabels: Record<string, string> = { Pending: 'Chờ phòng khám xác minh', Verified: 'Đã được cấp quyền', Rejected: 'Bị từ chối', Revoked: 'Đã thu hồi', Withdrawn: 'Đã rút yêu cầu', Expired: 'Đã hết hạn' };
export const scopeLabels: Record<string, string> = { Examinations: 'Lịch sử khám', Treatment: 'Điều trị', Prescriptions: 'Đơn thuốc', Documents: 'Ảnh và tài liệu' };
export const recordDateTime = (value: string | null) => value ? new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : '—';
export type FamilyBranch = { id: number; name: string };
export type FamilyCandidate = { id: number; patientCode: string; fullName: string; birthDate: string | null; branchIds: number[] };
export type FamilyReview = { access: FamilyAccess; accountId: number; accountUserName: string; accountFullName: string; relationship: string; patient: FamilyCandidate | null; branchId: number | null; verificationNote: string | null; sharingBasis: string | null; decisionReason: string | null; branches: FamilyBranch[] };
