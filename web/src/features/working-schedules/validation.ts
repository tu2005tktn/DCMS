import type { Period } from './contracts';

export const formatScheduleTime = (value: number) => `${String(Math.floor(value / 60)).padStart(2, '0')}:${String(value % 60).padStart(2, '0')}`;
const dayLabel = (day: number) => day === 6 ? 'Chủ nhật' : `Thứ ${day + 2}`;
const label = (p: Period, index: number) => `Khoảng ${index + 1} (${dayLabel(p.day)})`;
const overlap = (a: Period, b: Period) => a.startMinute < b.endMinute && b.startMinute < a.endMinute;

export function validateAvailabilityDuration(minutes: number | null, isPublic: boolean, catalogDuration?: number): string | null {
  if (minutes === null || !Number.isFinite(minutes) || !Number.isInteger(minutes))
    return 'Nhập thời lượng bằng số nguyên phút. Nhân viên dùng 15–480 phút, bội số của 15.';
  if (isPublic)
    return (minutes === 30 || minutes === 60) && minutes === catalogDuration
      ? null : 'Thời lượng công khai phải theo dịch vụ đã chọn: 30 hoặc 60 phút.';
  if (minutes < 15 || minutes > 480) return 'Thời lượng của nhân viên phải từ 15 đến 480 phút, bội số của 15.';
  if (minutes % 15 !== 0)
    return `Thời lượng ${minutes} phút chưa hợp lệ. Chọn bội số của 15 phút, ví dụ ${Math.floor(minutes / 15) * 15} hoặc ${Math.ceil(minutes / 15) * 15} phút.`;
  return null;
}

export function validateSchedulePeriods(periods: Period[]): string[] {
  const errors: string[] = [];
  if (periods.length > 140) errors.push('Một tuần không được vượt quá 140 khoảng.');
  periods.forEach((p, i) => {
    const name = label(p, i);
    if (!Number.isInteger(p.day) || p.day < 0 || p.day > 6) errors.push(`${name}: ngày phải nằm trong tuần đang chọn.`);
    if (!Number.isInteger(p.startMinute) || !Number.isInteger(p.endMinute) || p.startMinute < 0 || p.endMinute > 1440 || p.startMinute % 15 !== 0 || p.endMinute % 15 !== 0)
      errors.push(`${name}: giờ phải theo bước 15 phút, trong khoảng 00:00–24:00.`);
    if (p.startMinute >= p.endMinute)
      errors.push(`${name}: kết thúc ${formatScheduleTime(p.endMinute)} phải sau bắt đầu ${formatScheduleTime(p.startMinute)}. Kiểm tra SA/CH nếu ô giờ dùng định dạng 12 giờ.`);
    if (!['Work', 'Break', 'Block'].includes(p.kind)) errors.push(`${name}: chọn loại Làm việc, Nghỉ hoặc Block.`);
    if ((p.reason ?? '').length > 500) errors.push(`${name}: lý do không được vượt quá 500 ký tự.`);
    if (p.kind !== 'Work') {
      if (!p.reason?.trim()) errors.push(`${name}: nhập lý do nghỉ / block.`);
      if (!periods.some(w => w.day === p.day && w.kind === 'Work' && w.startMinute <= p.startMinute && w.endMinute >= p.endMinute))
        errors.push(`${name}: giờ nghỉ / block phải nằm hoàn toàn trong một khoảng Làm việc cùng ngày.`);
    }
    for (let j = 0; j < i; j++) {
      const other = periods[j];
      if (other.day !== p.day || (other.kind === 'Work') !== (p.kind === 'Work') || !overlap(other, p)) continue;
      errors.push(p.kind === 'Work'
        ? `${label(other, j)} và ${name} trùng giờ Làm việc. Nếu một khoảng là giờ nghỉ hoặc họp, hãy đổi Loại khoảng thành Nghỉ hoặc Block.`
        : `${label(other, j)} và ${name} có giờ nghỉ / block chồng nhau.`);
    }
  });
  return errors;
}
