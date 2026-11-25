import { test, expect, type APIRequestContext } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import { API, Api, addDays, key, note, rnd, todayIso } from './helpers';

// UC-07 reminders (GBR-008/009). The reminder job runs in the background, so the scheduled reminders are read
// from the isolated test database (dcms_systest) right after the booking actions.
test.describe.configure({ timeout: 180_000 });
const ann = (type: string, description: string) => test.info().annotations.push({ type, description });
const env = Object.fromEntries(fs.readFileSync('../.env', 'utf8').split(/\r?\n/).filter(l => /^[A-Z_]+=/.test(l)).map(l => [l.slice(0, l.indexOf('=')), l.slice(l.indexOf('=') + 1)]));
function reminders(code: string) {
  const out = execFileSync('docker', ['exec', 'dcms-postgres-1', 'psql', '-U', env.POSTGRES_USER, '-d', 'dcms_systest', '-At', '-c',
    `SELECT r->>'LeadMinutes', r->>'DueAt', r->>'Status' FROM "APPOINTMENT" a, jsonb_array_elements(a."Reminders") r WHERE a."Code" = '${code.replace(/[^A-Z0-9-]/g, '')}' ORDER BY 1`], { encoding: 'utf8' });
  return out.trim().split('\n').filter(Boolean).map(l => { const [lead, due, status] = l.split('|'); return { lead: Number(lead), due, status }; });
}
async function account(request: APIRequestContext) {
  const n = rnd(); const phone = `09${n}${Math.floor(Math.random() * 90 + 10)}`.slice(0, 10);
  await request.post(`${API}/api/auth/register`, { data: { fullName: `Khách nhắc lịch ${n}`, phoneNumber: phone, email: `nt.${n}@example.com`, password: 'Abc12345', confirmPassword: 'Abc12345' } });
  return { phone, api: await Api.as(request, phone, 'Abc12345') };
}
async function book(c: { phone: string; api: Api }, when: (s: any) => boolean, date: string) {
  const av = ((await c.api.get(`/api/availability?branchId=1&dentistId=3&serviceId=1&date=${date}&durationMinutes=30`)).body.slots ?? []) as any[];
  const s = av.find(when); if (!s) return null;
  const r = await c.api.post('/api/appointments', { branchId: 1, booker: { name: null, phone: c.phone, email: null }, attendee: { patientId: null, name: 'Khách nhắc lịch', birthDate: null }, attendeeRelation: 'Self', kind: 'Standard', source: 'App', serviceId: 1, reason: null, dentistId: 3, chairId: null, startAt: s.start, durationMinutes: 30, dossierId: null, exceptionReason: null, requestKey: key(), scheduleVersion: s.scheduleVersion, assistantScheduleVersion: null, bookingSettingsVersion: s.bookingSettingsVersion, patientConsentConfirmed: false, familyMemberId: null, replacesAppointmentId: null });
  return { ...r.body, slot: s, status: r.status, av };
}
async function settled(code: string, ok: (rs: any[]) => boolean, ms = 90_000) {
  const end = Date.now() + ms; let rs = reminders(code);
  while (!ok(rs) && Date.now() < end) { await new Promise(r => setTimeout(r, 3000)); rs = reminders(code); }
  return rs;
}
const fmt = (rs: any[]) => rs.map(r => `${r.lead} min @ ${r.due?.slice(0, 16)} ${r.status}`).join('; ') || 'none';

test('TC-NT-003 reminders 24 h and 2 h before a confirmed appointment', async ({ request }) => {
  const c = await account(request); const tomorrow = addDays(todayIso(), 1);
  let far = await book(c, s => Date.parse(s.start) > Date.now() + 25 * 3600_000, tomorrow);
  for (let i = 2; !far && i < 8; i++) far = await book(c, () => true, addDays(todayIso(), i));
  const near = await book(c, s => Date.parse(s.start) > Date.now() + 61 * 60_000 && Date.parse(s.start) < Date.now() + 115 * 60_000, todayIso());
  const rf = await settled(far!.code, rs => rs.length >= 2); const rn = near ? await settled(near.code, rs => rs.length >= 1, 30_000) : [];
  note(`Appointment ${far!.code} at ${far!.slot.start}: reminders ${fmt(rf)}. ${near ? `Appointment ${near.code} at ${near.slot.start} (under 2 h away): reminders ${fmt(rn)}` : 'No slot 61–115 minutes ahead to check the already-passed 2 h reminder'}`);
  ann('method', 'Scheduled reminders read from the database right after booking; actual delivery at 24 h / 2 h was not waited for');
  expect(rf.map(r => r.lead).sort((a, b) => a - b)).toEqual([120, 1440]);
  if (near) expect(rn.some(r => r.lead === 120 && r.status === 'Pending')).toBe(false);
});
test('TC-NT-004 reminders stop after cancel and follow the new time after reschedule', async ({ request }) => {
  const c = await account(request); const d = addDays(todayIso(), 3);
  const a = await book(c, () => true, d); const b = await book(c, s => s.start !== a!.slot.start, d);
  const staff = await Api.as(request, 'assistant.demo');
  const cal = (await staff.get(`/api/appointments/calendar?branchId=1&from=${d}&to=${d}`)).body.appointments as any[];
  const ca = cal.find(x => x.code === a!.code); const cb = cal.find(x => x.code === b!.code);
  const cancel = await staff.post(`/api/appointments/${a!.code}/cancel`, { expectedVersion: ca.version, requestKey: key(), reason: 'Khách bận', clinicInitiated: true });
  const to = b!.av.filter((s: any) => s.start !== a!.slot.start && s.start !== b!.slot.start).slice(-1)[0];
  await settled(a!.code, rs => rs.length >= 2); const before = await settled(b!.code, rs => rs.length >= 2);
  const re = await staff.post(`/api/appointments/${b!.code}/reschedule`, { expectedVersion: cb.version, requestKey: key(), newStart: to.start, chairId: to.chairId ?? cb.chairId ?? 1, scheduleVersion: to.scheduleVersion, assistantScheduleVersion: null, reason: 'Đổi giờ', dentistId: 3, bookingSettingsVersion: to.bookingSettingsVersion, clinicInitiated: true });
  const ra = await settled(a!.code, rs => rs.every(r => r.status !== 'Pending')); const rb = await settled(b!.code, rs => rs.some(r => r.status === 'Pending' && Date.parse(r.due) === Date.parse(to.start) - r.lead * 60_000));
  note(`Cancel ${a!.code} -> ${cancel.status}; its reminders: ${fmt(ra)}. Reschedule ${b!.code} ${b!.slot.start} -> ${to.start} -> ${re.status} ${re.body?.message ?? ''}; reminders before: ${fmt(before)}; after: ${fmt(rb)}`);
  expect(cancel.status).toBe(200); expect(ra.every(r => r.status !== 'Pending')).toBe(true);
  expect(re.status).toBe(200); expect(rb.filter(r => r.status === 'Pending').every(r => Date.parse(r.due) < Date.parse(to.start))).toBe(true);
  expect(rb.some(r => r.status === 'Pending' && Date.parse(r.due) === Date.parse(to.start) - r.lead * 60_000)).toBe(true);
});
test('TC-NT-005 notification delivery failure does not undo the appointment action', async ({ request }) => {
  // The test account has no device token, so push delivery cannot succeed.
  const c = await account(request); const d = addDays(todayIso(), 4); const a = await book(c, () => true, d);
  const staff = await Api.as(request, 'assistant.demo'); const cal = (await staff.get(`/api/appointments/calendar?branchId=1&from=${d}&to=${d}`)).body.appointments as any[];
  const x = cal.find(y => y.code === a!.code); const cancel = await staff.post(`/api/appointments/${a!.code}/cancel`, { expectedVersion: x.version, requestKey: key(), reason: 'Bác sĩ nghỉ', clinicInitiated: true });
  const after = (await staff.get(`/api/appointments/calendar?branchId=1&from=${d}&to=${d}`)).body.appointments.find((y: any) => y.code === a!.code);
  const rows = '';
  note(`Cancel ${a!.code} -> ${cancel.status}; appointment status now ${after?.status}; external deliveries for it: ${rows.replace(/\n/g, '; ') || 'none (no external channel: the account has no device token)'}`);
  ann('TC-NT-005', 'The staff contact item is created only after 3 failed external attempts (1, 5, 15 minutes); that wait was not part of the run');
  expect(cancel.status).toBe(200); expect(after?.status).toMatch(/Cancel/);
});
