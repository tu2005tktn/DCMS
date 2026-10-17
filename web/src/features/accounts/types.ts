export type Account = {
  id: number; userName: string; fullName: string; email: string | null; phone: string | null;
  accountType: string; isActive: boolean; version: number; roles: string[]; employeeCode: string | null;
  createdAt: string; updatedAt: string;
};
export type AccountPage = { items: Account[]; total: number; page: number; pageSize: number };
export type AccountCatalog = { roles: string[]; assignableRoles: string[]; statuses: string[] };
export const formatAccountTime = (value: string) => {
  const date = new Date(value);
  const datePart = new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric' }).format(date);
  const timePart = new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit', hour12: false }).format(date);
  return `${datePart} ${timePart}`;
};
export const roleLabels: Record<string, string> = {
  Admin: 'Quản trị viên', Dentist: 'Nha sĩ', ClinicOwner: 'Chủ phòng khám',
  ClinicAssistant: 'Trợ lý phòng khám', Guest: 'Guest (tài khoản)', Patient: 'Patient',
};
