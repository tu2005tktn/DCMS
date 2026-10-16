import { expect, test } from '@playwright/test';
import { validateAvailabilityDuration, validateSchedulePeriods } from '../src/features/working-schedules/validation';
import type { Period } from '../src/features/working-schedules/contracts';

const sample: Period[] = [
  { day: 0, startMinute: 480, endMinute: 1020, kind: 'Work', reason: '' },
  { day: 0, startMinute: 720, endMinute: 780, kind: 'Break', reason: 'Nghỉ trưa' },
  { day: 0, startMinute: 900, endMinute: 930, kind: 'Block', reason: 'Họp nội bộ' },
];
test('staff duration rejects 70, blank and invalid bounds; accepts 75 and boundary values', () => {
  expect(validateAvailabilityDuration(70, false)).toContain('60 hoặc 75');
  for (const value of [null, 0, -15, 495, 30.5, NaN, Infinity]) expect(validateAvailabilityDuration(value, false)).not.toBeNull();
  for (const value of [15, 30, 60, 75, 120, 480]) expect(validateAvailabilityDuration(value, false)).toBeNull();
});
test('public duration must equal the 30/60 minute service catalog value', () => {
  expect(validateAvailabilityDuration(30, true, 30)).toBeNull();
  expect(validateAvailabilityDuration(60, true, 60)).toBeNull();
  for (const value of [15, 70, 75, 120]) expect(validateAvailabilityDuration(value, true, 30)).not.toBeNull();
  expect(validateAvailabilityDuration(60, true, 30)).not.toBeNull();
});
test('reported Work Break Block sample is valid', () => expect(validateSchedulePeriods(sample)).toEqual([]));
test('forgotten Work type identifies overlapping interval numbers and correction', () => {
  const errors = validateSchedulePeriods(sample.map((p, i) => i === 1 ? { ...p, kind: 'Work' } : p));
  expect(errors).toHaveLength(1); expect(errors[0]).toContain('Khoảng 1 (Thứ 2)'); expect(errors[0]).toContain('Khoảng 2 (Thứ 2)'); expect(errors[0]).toContain('đổi Loại khoảng');
});
test('reversed clock explains actual 24 hour values', () => {
  expect(validateSchedulePeriods([{ ...sample[0], endMinute: 300 }])[0]).toContain('kết thúc 05:00 phải sau bắt đầu 08:00');
});
test('exclusions need reasons and must fit one work interval', () => {
  const errors = validateSchedulePeriods([sample[0], { ...sample[1], endMinute: 1080, reason: '' }]);
  expect(errors.some(e => e.includes('nhập lý do'))).toBe(true); expect(errors.some(e => e.includes('nằm hoàn toàn'))).toBe(true);
});
test('all seven days are validated, exclusions cannot overlap and grid is enforced', () => {
  expect(validateSchedulePeriods([...sample, { ...sample[0], day: 1, endMinute: 480 }]).some(e => e.includes('Thứ 3'))).toBe(true);
  expect(validateSchedulePeriods([sample[0], sample[1], { ...sample[2], startMinute: 750, endMinute: 795 }]).some(e => e.includes('chồng nhau'))).toBe(true);
  expect(validateSchedulePeriods([{ ...sample[0], startMinute: 481 }]).some(e => e.includes('15 phút'))).toBe(true);
});
