// Contracts: docs/specs/CUSTOMER-FAMILY-MEMBERS.md and FAMILY-DELEGATED-RECORD-ACCESS.vi.md. Mobile screens: MOBILE-FAMILY-MEMBERS.vi.md.
export type Gender = 'MALE' | 'FEMALE' | 'OTHER';
export type Relationship = 'Father' | 'Mother' | 'Spouse' | 'Child' | 'Sibling' | 'Grandparent' | 'Other';
export type FamilyMember = {
  id: string; fullName: string; relationship: Relationship; birthDate: string | null; version: number;
  gender: Gender | null; contactPhone: string | null;
};
export type Page<T> = { items: T[]; total: number; page: number; pageSize: number };
export type SaveFamilyMember = {
  fullName: string; relationship: Relationship; birthDate: string | null; gender: Gender; contactPhone: string;
};

export type AccessStatus = 'Pending' | 'Verified' | 'Rejected' | 'Revoked' | 'Withdrawn' | 'Expired';
export type Scope = 'Examinations' | 'Treatment' | 'Prescriptions' | 'Documents';
export type FamilyAccess = {
  id: string; familyMemberId: string; code: string; status: AccessStatus; fullName: string; birthDate: string | null;
  patientCode: string | null; matchingInformation: string | null; requestedAt: string; decidedAt: string | null;
  expiresAt: string | null; scopes: Scope[]; publicNote: string | null; version: number;
};
export type AccessOverview = { current: FamilyAccess | null; history: FamilyAccess[]; canRequest: boolean };
export type RecordHeader = { patientCode: string; fullName: string; birthDate: string | null; branchName: string; scopes: Scope[]; expiresAt: string };

export type FamilyAppointment = { code: string; startAt: string; endAt: string; status: string; branchName: string; dentistName: string };
export type FamilyExamination = { visitId: string; arrivalAt: string; dentistName: string; branchName: string; symptoms: string | null; diagnoses: string[]; conclusion: string | null; advice: string | null };
export type FamilyTreatment = { id: string; code: string; diagnosis: string; state: string; startedOn: string;
  items: { serviceName: string; region: string | null; progress: string; estimatedStart: string | null; estimatedEnd: string | null }[] };
export type FamilyMedicine = { medicineName: string; strength: string | null; unit: string; quantity: number; route: string;
  morningDose: string; noonDose: string; afternoonDose: string; eveningDose: string; mealTiming: string; days: number; instructions: string };
export type FamilyPrescription = { id: string; visitId: string; finalizedAt: string | null; diagnosis: string | null; generalAdvice: string; lines: FamilyMedicine[] };
export type FamilyDocument = { id: string; fileName: string; contentType: string; sizeBytes: number; documentType: string; sourceDate: string };

export const relationships: { value: Relationship; label: string }[] = [
  { value: 'Father', label: 'Bố' }, { value: 'Mother', label: 'Mẹ' }, { value: 'Spouse', label: 'Vợ/Chồng' },
  { value: 'Child', label: 'Con' }, { value: 'Sibling', label: 'Anh/Chị/Em' }, { value: 'Grandparent', label: 'Ông/Bà' }, { value: 'Other', label: 'Khác' },
];
export const relationshipLabel = (value: string) => relationships.find(x => x.value === value)?.label ?? value;
export const genderLabels: Record<Gender, string> = { MALE: 'Nam', FEMALE: 'Nữ', OTHER: 'Khác' };
export const notProvided = 'Chưa cung cấp';
export const displayDate = (iso: string | null) => iso ? iso.split('-').reverse().join('/') : notProvided;

export const accessLabels: Record<AccessStatus, string> = {
  Pending: 'Chờ phòng khám xác minh', Verified: 'Đã được cấp quyền', Rejected: 'Bị từ chối',
  Revoked: 'Đã thu hồi', Withdrawn: 'Đã rút yêu cầu', Expired: 'Đã hết hạn',
};
export const scopeLabels: Record<Scope, string> = { Examinations: 'Lịch sử khám', Treatment: 'Điều trị', Prescriptions: 'Đơn thuốc', Documents: 'Ảnh và tài liệu' };
export const progressLabels: Record<string, string> = { NotStarted: 'Chưa thực hiện', InProgress: 'Đang thực hiện', Completed: 'Đã hoàn thành', Partial: 'Một phần', Cancelled: 'Đã hủy', Stopped: 'Đã dừng' };
export const appointmentLabels: Record<string, string> = { Confirmed: 'Đã đặt', CheckedIn: 'Đã đến khám', Queued: 'Chờ khám', ExaminationConfirmed: 'Đã xác nhận phiếu khám', InProgress: 'Đang khám', Completed: 'Đã hoàn thành', Cancelled: 'Đã hủy', NoShow: 'Không đến' };
export const mealLabels: Record<string, string> = { BeforeMeal: 'Trước ăn', AfterMeal: 'Sau ăn', WithMeal: 'Cùng bữa ăn' };
