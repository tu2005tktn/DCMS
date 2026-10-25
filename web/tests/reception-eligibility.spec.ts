import { expect, test } from '@playwright/test';
import { checkInBlocker, clinicDateAt } from '../src/features/reception/eligibility';
import { receptionErrorPrefix } from '../src/features/reception/errors';

const now = '2026-10-25T08:00:00Z';
const appointment = { status: 'Confirmed', startAt: '2026-10-25T09:00:00Z', endAt: '2026-10-25T09:30:00Z' };

test('same-day future slot can receive an early actual arrival', () => expect(checkInBlocker(appointment, now)).toBeNull());
test('future-day reception explains planned date and current clinic day', () => {
  const reason = checkInBlocker({ ...appointment, startAt: '2026-10-26T01:00:00Z', endAt: '2026-10-26T01:30:00Z' }, now);
  expect(reason).toContain('26/10/2026'); expect(reason).toContain('25/10/2026');
});
test('clinic day uses Ho Chi Minh midnight rather than UTC or browser timezone', () => {
  expect(clinicDateAt('2026-10-25T17:00:00Z')).toBe('2026-10-26');
  expect(checkInBlocker({ ...appointment, startAt: '2026-10-25T17:00:00Z', endAt: '2026-10-25T17:30:00Z' }, now)).toContain('26/10/2026');
});
test('past-day, elapsed and cancelled appointments do not offer reception', () => {
  expect(checkInBlocker(appointment, '2026-10-26T08:00:00Z')).not.toBeNull();
  expect(checkInBlocker(appointment, appointment.endAt)).toContain('đã kết thúc');
  expect(checkInBlocker({ ...appointment, status: 'Cancelled' }, now)).toContain('trạng thái');
});
test('business 409 is distinct from stale-version conflict', () => {
  expect(receptionErrorPrefix(409, 'MSG111')).toContain('Dữ liệu đã thay đổi');
  expect(receptionErrorPrefix(409, 'MSG42')).not.toContain('Dữ liệu đã thay đổi');
  expect(receptionErrorPrefix(409, 'MSG43')).toContain('Cần đối chiếu');
});
