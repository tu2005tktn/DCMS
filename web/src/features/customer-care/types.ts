// S60 Chăm sóc khách hàng. Contract: docs/specs/S60-CUSTOMER-CARE-WEB.vi.md §6.
export type CareStatus = 'Open' | 'InProgress' | 'Done' | 'Cancelled';
export type StatusFilter = CareStatus | 'Pending';
export type CareTask = { id: string; patientId: number; branchId: number; instructionId: string | null; appointmentId: string | null;
  reviewId: string | null; type: string; content: string; ownerAccountId: number | null; dueAt: string; status: CareStatus; version: number;
  allowedActions: string[] };
export type CareWorklistItem = { task: CareTask; branchName: string; patientCode: string; patientName: string; phone: string | null; typeName: string;
  ownerName: string | null; lastContactResult: string | null; lastContactAt: string | null };
export type CareWorklistPage = { items: CareWorklistItem[]; total: number; page: number; pageSize: number };
export type CareStaff = { accountId: number; name: string; roles: string[] };
export type CareBranch = { id: number; name: string; canCreate: boolean; staff: CareStaff[] };
export type CareType = { code: string; name: string; system: boolean };
// allBranches: the owner reads every branch and may pick one; other staff read their assigned branches (spec §2).
export type CareOptions = { clinicDate: string; actorAccountId: number; allBranches: boolean; branches: CareBranch[]; types: CareType[] };
export type CarePatient = { patientId: number; patientCode: string; fullName: string; phone: string | null; lastVisitAt: string };
export type ContactAttempt = { id: string; taskId: string; channel: string; result: string; nextAction: string | null;
  rebookedAppointmentId: string | null; actorId: number; occurredAt: string };
export type CareTaskDetail = { task: CareTask; contacts: ContactAttempt[]; escalations: unknown[] };
export type WorklistFilter = { branchId?: number; from: string; to: string; ownerAccountId?: number; type?: string; status?: StatusFilter; search?: string };

export const statusLabels: Record<CareStatus, string> = { Open: 'Mở', InProgress: 'Đang xử lý', Done: 'Đã xong', Cancelled: 'Đã hủy' };
export const statusColors: Record<CareStatus, string> = { Open: 'blue', InProgress: 'gold', Done: 'green', Cancelled: 'default' };
export const statusFilters: { value: StatusFilter; label: string }[] = [
  { value: 'Pending', label: 'Cần xử lý (Mở + Đang xử lý)' }, { value: 'Open', label: 'Mở' }, { value: 'InProgress', label: 'Đang xử lý' },
  { value: 'Done', label: 'Đã xong' }, { value: 'Cancelled', label: 'Đã hủy' }];
// "Rebooked" needs a committed Appointment; this screen does not pick one (spec §1), so it is shown but not offered.
export const contactResultLabels: Record<string, string> = { Reached: 'Đã liên lạc được', NoAnswer: 'Không nghe máy', Rebooked: 'Đã đặt lại lịch',
  Declined: 'Khách từ chối', Other: 'Khác' };
export const contactResultChoices = ['Reached', 'NoAnswer', 'Declined', 'Other'];
export const channelLabels: Record<string, string> = { Phone: 'Điện thoại', InPerson: 'Trực tiếp', Other: 'Khác' };

const clinicOffset = 7 * 60 * 60 * 1000;
/** Clinic wall time (UTC+7) of an instant, as `YYYY-MM-DD HH:mm`. */
export function clinicTime(iso: string) {
  const d = new Date(new Date(iso).getTime() + clinicOffset).toISOString();
  return `${d.slice(8, 10)}/${d.slice(5, 7)}/${d.slice(0, 4)} ${d.slice(11, 16)}`;
}
/** Clinic date and time parts of an instant. */
export function clinicParts(iso: string) {
  const d = new Date(new Date(iso).getTime() + clinicOffset).toISOString();
  return { date: d.slice(0, 10), hour: Number(d.slice(11, 13)), minute: Number(d.slice(14, 16)) };
}
/** Instant for a clinic date + time, sent with the clinic offset. */
export const toDueAt = (date: string, hour: number, minute: number) => `${date}T${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}:00+07:00`;
