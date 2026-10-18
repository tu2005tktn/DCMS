import { AppointmentApiError } from './appointments.errors';
import { addDays, clinicDate, clinicMidnight, formatDateTime, SELF_SERVICE_HOURS } from './appointments.rules';
import type {
  AppointmentCard, AppointmentChange, AppointmentDetail, AppointmentStatus, AvailabilitySlot, SupportRequest,
} from './appointments.types';
import type { AppointmentsApi } from './appointments.api';

// In-memory stand-in for the §4 contract (docs/implementation/UC05-06-S14-S16.md). It applies the same rules the
// server must apply (ownership, 12-hour window, expectedVersion, requestKey, slot recheck) so every UI state can be
// exercised before TV2/TV3 deliver the Appointment API. Data resets when the app reloads. Development only.

type Stored = Omit<AppointmentDetail, 'eligibility' | 'supportRequests' | 'history'> & { history: AppointmentChange[] };

const MESSAGES = {
  MSG31: 'Không thể tự đổi hoặc hủy lịch này. Vui lòng liên hệ phòng khám để được hỗ trợ.',
  MSG33: 'Đã gửi yêu cầu hỗ trợ. Lịch hẹn hiện tại vẫn có hiệu lực cho đến khi phòng khám xử lý thành công.',
  MSG34: 'Không thể đổi sang khung giờ đã chọn. Lịch hẹn cũ của bạn vẫn được giữ nguyên.',
  MSG102: 'Bạn chưa có quyền xem thông tin này. Vui lòng liên hệ phòng khám để được hỗ trợ.',
  MSG111: 'Dữ liệu đã được cập nhật bởi thao tác khác. Vui lòng tải lại và kiểm tra trước khi xác nhận.',
};
const fail = (status: number, code: keyof typeof MESSAGES) => new AppointmentApiError(MESSAGES[code], status, code);
const invalid = (field: string) => new AppointmentApiError(`Vui lòng nhập ${field}.`, 400, 'MSG02');
const delay = () => new Promise(resolve => setTimeout(resolve, 350));
const copy = <T,>(value: T): T => JSON.parse(JSON.stringify(value)) as T;

const HOUR = 3_600_000;
const contact = { phone: '0234 3 888 999', hours: 'Thứ 2 – Thứ 7, 08:00 – 17:00' };
// Slots the mock reports as free but that "another patient" takes before confirmation, to exercise MSG34.
const RACED_LOCAL_TIME = '11:30';

function at(dayOffset: number, hour: number, minute = 0) {
  return new Date(clinicMidnight(addDays(clinicDate(new Date()), dayOffset)).getTime() + (hour * 60 + minute) * 60_000).toISOString();
}
function plusMinutes(iso: string, minutes: number) { return new Date(Date.parse(iso) + minutes * 60_000).toISOString(); }

function seed(): Stored[] {
  const now = Date.now();
  const base = { branchId: 1, branchName: 'DCMS Huế', dentistId: 2, serviceId: 1, serviceName: 'Khám và tư vấn',
    bookerName: 'Chủ tài khoản', bookerContactMasked: '03******25', clinicContact: contact, history: [] as AppointmentChange[] };
  const soon = new Date(Math.ceil((now + 6 * HOUR) / (15 * 60_000)) * 15 * 60_000).toISOString();
  const rows: Stored[] = [
    { ...base, code: 'LH-DEMO-0001', start: at(3, 9), end: at(3, 9, 30), durationMinutes: 30, dentistName: 'BS. Nha sĩ demo',
      attendeeName: 'Bản thân', attendeeKind: 'SELF', status: 'Confirmed', access: 'BOTH', version: 1, followUp: { id: 7, status: 'Booked' } },
    { ...base, code: 'LH-DEMO-0002', start: soon, end: plusMinutes(soon, 60), durationMinutes: 60, dentistName: 'BS. Nha sĩ demo',
      attendeeName: 'Nguyễn Thị Hoa (mẹ)', attendeeKind: 'RELATIVE', status: 'Confirmed', access: 'BOOKED_BY_ME', version: 1, followUp: null },
    { ...base, code: 'LH-DEMO-0003', start: at(8, 14), end: at(8, 14, 30), durationMinutes: 30, dentistName: 'BS. Nha sĩ demo',
      attendeeName: 'Bản thân', attendeeKind: 'SELF', status: 'Confirmed', access: 'MY_OWN', version: 1, followUp: null },
    { ...base, code: 'LH-DEMO-0004', start: at(-10, 8, 30), end: at(-10, 9), durationMinutes: 30, dentistName: 'BS. Nha sĩ demo',
      attendeeName: 'Bản thân', attendeeKind: 'SELF', status: 'Completed', access: 'BOTH', version: 3, followUp: null },
    { ...base, code: 'LH-DEMO-0005', start: at(-4, 15), end: at(-4, 15, 30), durationMinutes: 30, dentistName: 'BS. Nha sĩ demo',
      attendeeName: 'Nguyễn Thị Hoa (mẹ)', attendeeKind: 'RELATIVE', status: 'Cancelled', access: 'BOOKED_BY_ME', version: 2, followUp: null,
      history: [{ action: 'CANCEL', actorKind: 'SELF', oldStart: at(-4, 15), newStart: null, reason: 'Bận việc gia đình', occurredAt: at(-6, 20) }] },
    { ...base, code: 'LH-DEMO-0006', start: at(-20, 10), end: at(-20, 10, 30), durationMinutes: 30, dentistName: 'BS. Nha sĩ demo',
      attendeeName: 'Bản thân', attendeeKind: 'SELF', status: 'NoShow', access: 'MY_OWN', version: 2, followUp: null },
  ];
  return rows;
}

let store = seed();
let supports: SupportRequest[] = [];
let nextSupportId = 1;
const receipts = new Map<string, { fingerprint: string; result: unknown }>();

const isUpcoming = (x: Stored) => Date.parse(x.end) > Date.now() && (['Confirmed', 'CheckedIn', 'InProgress'] as AppointmentStatus[]).includes(x.status);

function eligibility(x: Stored): AppointmentDetail['eligibility'] {
  const hoursRemaining = (Date.parse(x.start) - Date.now()) / HOUR;
  const reasonCode = x.status === 'CheckedIn' || x.status === 'InProgress' || hoursRemaining <= 0 ? 'STARTED'
    : x.status !== 'Confirmed' ? 'NOT_CONFIRMED'
    : hoursRemaining < SELF_SERVICE_HOURS ? 'WITHIN_12H' : null;
  return { canSelfService: reasonCode === null, hoursRemaining, reasonCode, canRequestSupport: reasonCode === 'WITHIN_12H' };
}

function detailOf(x: Stored): AppointmentDetail {
  return copy({ ...x, eligibility: eligibility(x), supportRequests: supports.filter(s => s.appointmentCode === x.code).reverse() });
}
const card = (x: Stored): AppointmentCard => copy({ code: x.code, start: x.start, end: x.end, durationMinutes: x.durationMinutes,
  dentistName: x.dentistName, attendeeName: x.attendeeName, attendeeKind: x.attendeeKind, status: x.status, access: x.access, version: x.version });

// Every lookup is by code inside the account's own dataset; anything else is indistinguishable from "not found".
function own(code: string) {
  const found = store.find(x => x.code === code);
  if (!found) throw fail(404, 'MSG102');
  return found;
}

function replay<T>(key: string, fingerprint: string, run: () => T): T {
  const previous = receipts.get(key);
  if (previous) {
    if (previous.fingerprint !== fingerprint) throw fail(409, 'MSG111');
    return copy(previous.result) as T;
  }
  const result = run();
  receipts.set(key, { fingerprint, result: copy(result) });
  return result;
}

function requireSelfService(x: Stored, expectedVersion: number) {
  if (x.version !== expectedVersion) throw fail(409, 'MSG111');
  if (!eligibility(x).canSelfService) throw fail(409, 'MSG31');
}

function freeSlots(x: Stored, date: string): AvailabilitySlot[] {
  const dayStart = clinicMidnight(date).getTime();
  const slots: AvailabilitySlot[] = [];
  for (let minute = 8 * 60; minute + x.durationMinutes <= 17 * 60; minute += 30) {
    if (minute >= 12 * 60 && minute < 13 * 60) continue; // lunch break in the published schedule
    const start = new Date(dayStart + minute * 60_000).toISOString();
    const end = plusMinutes(start, x.durationMinutes);
    if (Date.parse(start) < Date.now() + HOUR) continue; // public lead time ≥ 60 minutes
    const busy = store.some(o => o.code !== x.code && o.dentistId === x.dentistId && (o.status === 'Confirmed' || o.status === 'CheckedIn')
      && Date.parse(o.start) < Date.parse(end) && Date.parse(start) < Date.parse(o.end));
    if (!busy && start !== x.start) slots.push({ start, end, chairId: 1 + (minute / 30) % 2, dentistId: x.dentistId, assistantId: null,
      scheduleVersion: 1, assistantScheduleVersion: null });
  }
  return slots;
}

export const mockAppointmentsApi: AppointmentsApi = {
  async list(_token, q) {
    await delay();
    let rows = store.filter(x => (q.scope === 'UPCOMING') === isUpcoming(x));
    if (q.status !== 'ALL') rows = rows.filter(x => x.status === q.status);
    if (q.from) rows = rows.filter(x => clinicDate(new Date(x.start)) >= q.from!);
    if (q.to) rows = rows.filter(x => clinicDate(new Date(x.start)) <= q.to!);
    rows.sort((a, b) => (q.scope === 'UPCOMING' ? 1 : -1) * (Date.parse(a.start) - Date.parse(b.start)));
    const page = rows.slice((q.page - 1) * q.pageSize, q.page * q.pageSize);
    return { items: page.map(card), total: rows.length, page: q.page, pageSize: q.pageSize };
  },
  async detail(_token, code) { await delay(); return detailOf(own(code)); },
  async availability(_token, detail, date) {
    await delay();
    return freeSlots(own(detail.code), date);
  },
  async reschedule(_token, code, c) {
    await delay();
    const x = own(code);
    return replay(c.requestKey, JSON.stringify(['reschedule', code, c.expectedVersion, c.newStart, c.chairId, c.reason]), () => {
      if (!c.reason.trim()) throw invalid('Lý do');
      requireSelfService(x, c.expectedVersion);
      // Recheck the chosen slot as the server would; failure keeps the original interval untouched.
      const date = clinicDate(new Date(c.newStart));
      const local = formatDateTime(c.newStart).slice(0, 5);
      if (local === RACED_LOCAL_TIME || !freeSlots(x, date).some(s => s.start === c.newStart && s.chairId === c.chairId)) throw fail(409, 'MSG34');
      x.history.unshift({ action: 'RESCHEDULE', actorKind: 'SELF', oldStart: x.start, newStart: c.newStart, reason: c.reason.trim(), occurredAt: new Date().toISOString() });
      x.start = c.newStart; x.end = plusMinutes(c.newStart, x.durationMinutes); x.version += 1;
      // The follow-up link is kept (GBR-076).
      return { appointment: detailOf(x), notification: { code: 'MSG35', message: `Đã đổi lịch hẹn sang ${formatDateTime(x.start)}. Mọi thông báo nhắc lịch sẽ theo giờ hẹn mới.` } };
    });
  },
  async cancel(_token, code, c) {
    await delay();
    const x = own(code);
    return replay(c.requestKey, JSON.stringify(['cancel', code, c.expectedVersion, c.reason]), () => {
      if (!c.reason.trim()) throw invalid('Lý do');
      requireSelfService(x, c.expectedVersion);
      x.history.unshift({ action: 'CANCEL', actorKind: 'SELF', oldStart: x.start, newStart: null, reason: c.reason.trim(), occurredAt: new Date().toISOString() });
      x.status = 'Cancelled'; x.version += 1;
      // An active follow-up returns to NeedsBooking (GBR-025).
      if (x.followUp) x.followUp = { ...x.followUp, status: 'NeedsBooking' };
      return { appointment: detailOf(x), notification: { code: 'MSG37', message: `Đã hủy lịch hẹn ${x.code}.` } };
    });
  },
  async requestSupport(_token, code, c) {
    await delay();
    const x = own(code);
    return replay(c.requestKey, JSON.stringify(['support', code, c.kind, c.reason]), () => {
      if (!c.reason.trim()) throw invalid('Lý do');
      if (supports.some(s => s.appointmentCode === code && s.status === 'Pending')) throw fail(409, 'MSG111');
      if (!eligibility(x).canRequestSupport) throw fail(409, 'MSG31');
      const request: SupportRequest = { id: nextSupportId++, appointmentCode: code, kind: c.kind, reason: c.reason.trim(), status: 'Pending',
        createdAt: new Date().toISOString(), decidedAt: null, publicNote: null, version: 1 };
      supports.push(request);
      // The appointment is untouched: still Confirmed, no slot is held.
      return { request: copy(request), notification: { code: 'MSG33', message: MESSAGES.MSG33 } };
    });
  },
  async withdrawSupport(_token, id, expectedVersion) {
    await delay();
    const request = supports.find(s => s.id === id);
    if (!request) throw fail(404, 'MSG102');
    if (request.version !== expectedVersion || request.status !== 'Pending') throw fail(409, 'MSG111');
    request.status = 'Withdrawn'; request.decidedAt = new Date().toISOString(); request.version += 1;
    return copy(request);
  },
};

// Development helper for demos: restore the seeded data.
export function resetMockAppointments() { store = seed(); supports = []; nextSupportId = 1; receipts.clear(); }
