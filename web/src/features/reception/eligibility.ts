type AppointmentEligibilityInput = { startAt: string; endAt: string; status: string };

export function clinicDateAt(instant: string | number | Date): string {
  return new Date(new Date(instant).getTime() + 7 * 3600_000).toISOString().slice(0, 10);
}

export function checkInBlocker(appointment: AppointmentEligibilityInput, now: string | number | Date = Date.now()): string | null {
  const current = new Date(now).getTime();
  const start = Date.parse(appointment.startAt); const end = Date.parse(appointment.endAt);
  if (![current, start, end].every(Number.isFinite) || end <= start)
    return 'Chưa xác định được khung giờ hợp lệ. Vui lòng đọc lại lịch hẹn.';
  if (appointment.status !== 'Confirmed') return 'Lịch hẹn không còn ở trạng thái cho phép tiếp nhận. Vui lòng mở chi tiết lịch hiện tại.';
  const day = clinicDateAt(start); const today = clinicDateAt(current);
  const format = (value: string) => value.split('-').reverse().join('/');
  if (day !== today) return `Lịch hẹn thuộc ngày ${format(day)}. Chỉ có thể check-in trong ngày hẹn; hôm nay tại phòng khám là ${format(today)} (Asia/Ho_Chi_Minh).`;
  if (end <= current) return 'Khung giờ hẹn đã kết thúc. Vui lòng kiểm tra khả năng tiếp nhận và đặt một ca hợp lệ qua S11 trước khi check-in.';
  return null;
}
