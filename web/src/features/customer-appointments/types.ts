export type AppointmentStatus = 'Confirmed' | 'CheckedIn' | 'InProgress' | 'Completed' | 'Cancelled' | 'NoShow';
export type AppointmentCard = {
  code: string; start: string; end: string; durationMinutes: number; dentistName: string; attendeeName: string;
  attendeeKind: 'SELF' | 'RELATIVE'; status: AppointmentStatus; access: 'BOOKED_BY_ME' | 'MY_OWN' | 'BOTH'; version: number;
};
export type AppointmentPage = { items: AppointmentCard[]; total: number; page: number; pageSize: number };
export type AppointmentDetail = AppointmentCard & {
  branchId: number; branchName: string; dentistId: number; assistantId: number | null; serviceId: number | null;
  serviceName: string; bookerName: string; bookerContactMasked: string;
  eligibility: { canSelfService: boolean; hoursRemaining: number; reasonCode: string | null; canRequestSupport: boolean };
  followUp: { id: string; status: string } | null;
  history: { action: 'RESCHEDULE' | 'CANCEL'; actorKind: string; oldStart: string; newStart: string | null; reason: string; occurredAt: string }[];
  clinicContact: { phone: string; hours: string };
};
export type Slot = { start: string; end: string; chairId: number; dentistId: number; assistantId: number | null;
  scheduleVersion: number; assistantScheduleVersion: number | null };
export type ChangeResult = { appointment: AppointmentDetail; notification: { code: string; message: string } };
export const statusLabels: Record<AppointmentStatus, string> = {
  Confirmed: 'Đã xác nhận', CheckedIn: 'Đã tiếp nhận', InProgress: 'Đang khám', Completed: 'Hoàn thành', Cancelled: 'Đã hủy', NoShow: 'Vắng mặt',
};
export const statusColors: Record<AppointmentStatus, string> = {
  Confirmed: 'green', CheckedIn: 'blue', InProgress: 'processing', Completed: 'default', Cancelled: 'red', NoShow: 'orange',
};
export const formatTime = (value: string) => new Date(value).toLocaleString('vi-VN', {
  timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit',
});
export const formatClock = (value: string) => new Date(value).toLocaleTimeString('vi-VN', {
  timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit',
});
export const clinicDate = (value = Date.now()) => new Date(value + 7 * 3600_000).toISOString().slice(0, 10);
export const ineligibleReasons: Record<string, string> = {
  WITHIN_12H: 'Còn dưới 12 giờ trước giờ hẹn nên bạn không thể tự đổi hoặc hủy. Vui lòng liên hệ phòng khám.',
  STARTED: 'Lịch hẹn đã bắt đầu hoặc đã được tiếp nhận nên không thể tự đổi hoặc hủy.',
  NOT_CONFIRMED: 'Lịch hẹn không còn ở trạng thái Đã xác nhận nên không thể tự đổi hoặc hủy.',
  NOT_OWNER: 'Bạn không có quyền tự đổi hoặc hủy lịch này.',
};
