export type Account = {
  id: number; userName: string; fullName: string; email: string | null; phone: string | null;
  accountType: string; isActive: boolean; version: number; roles: string[]; employeeCode: string | null;
};
export type AccountPage = { items: Account[]; total: number; page: number; pageSize: number };
export const roleLabels: Record<string, string> = {
  Admin: 'Quản trị viên', Dentist: 'Nha sĩ', ClinicOwner: 'Chủ phòng khám',
  ClinicAssistant: 'Trợ lý phòng khám', Guest: 'Guest (tài khoản)', Patient: 'Patient',
};
