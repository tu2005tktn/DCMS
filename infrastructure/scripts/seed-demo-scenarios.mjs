// Seeds DEMO scenarios into the running dev API (default http://localhost:5080) through public endpoints,
// so receipts, versions and audit rows stay valid. Idempotent: a scenario is skipped when its "DEMO ..." patient exists.
// Reads DEV_SEED_PASSWORD from .env and never prints it. Usage: node infrastructure/scripts/seed-demo-scenarios.mjs
import fs from 'node:fs';
import crypto from 'node:crypto';

const env = Object.fromEntries(fs.readFileSync('.env', 'utf8').split(/\r?\n/)
  .filter(l => l && !l.startsWith('#') && l.includes('='))
  .map(l => { const i = l.indexOf('='); return [l.slice(0, i).trim(), l.slice(i + 1).trim().replace(/^"|"$/g, '')]; }));
const api = process.env.DEMO_API_URL ?? 'http://localhost:5080';
const uuid = () => crypto.randomUUID();
const sleep = ms => new Promise(r => setTimeout(r, ms));
const log = m => console.log(m);

async function login(userName) {
  const r = await fetch(`${api}/api/auth/sign-in`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ userName, password: env.DEV_SEED_PASSWORD }) });
  if (!r.ok) throw new Error(`sign-in ${userName} failed ${r.status}`);
  return { Authorization: `Bearer ${(await r.json()).accessToken}`, 'content-type': 'application/json' };
}
async function call(h, method, path, body) {
  const r = await fetch(api + path, { method, headers: h, body: body ? JSON.stringify(body) : undefined });
  const text = await r.text(); let json; try { json = JSON.parse(text); } catch { json = text; }
  if (!r.ok) throw new Error(`${method} ${path} -> ${r.status} ${text.slice(0, 300)}`);
  return json;
}
// A failing scenario must not abort the others.
async function scenario(name, fn) { try { await fn(); } catch (e) { log(`  ! ${name}: ${e.message}`); } }

const staff = await login('assistant.demo');
const dentist = await login('dentist.demo');
const options = await call(staff, 'GET', '/api/reception/options');
const branchId = options.calendar.branches[0].id;
const dentistId = options.calendar.dentists.find(x => x.name === 'Nha sĩ demo').id;
const serviceId = options.calendar.services[0].id;
const todayClinic = options.clinicDate;

async function ensurePatient(name, birthDate, gender) {
  const found = await call(staff, 'GET', `/api/patients?keyword=${encodeURIComponent(name)}&page=1&pageSize=20`);
  const items = found.items ?? found.patients ?? found;
  const hit = Array.isArray(items) && items.find(p => p.fullName === name);
  if (hit) return { patient: hit, created: false };
  const created = await call(staff, 'POST', '/api/patients', { fullName: name, birthDate, gender, duplicatesReviewed: true });
  return { patient: created.patient, created: true };
}
function addDays(iso, n) { const d = new Date(iso + 'T00:00:00Z'); d.setUTCDate(d.getUTCDate() + n); return d.toISOString().slice(0, 10); }
async function slots(date, minutes, minStartMs) {
  const a = await call(staff, 'GET', `/api/availability?branchId=${branchId}&dentistId=${dentistId}&serviceId=${serviceId}&date=${date}&durationMinutes=${minutes}`);
  return (a.slots ?? []).filter(s => Date.parse(s.start) > Date.now() + minStartMs);
}
async function book(patient, date, minutes, nth, minStartMs = 10000) {
  const list = await slots(date, minutes, minStartMs);
  const slot = list[Math.min(nth, list.length - 1)];
  if (!slot) { log(`  ! no free slot on ${date}`); return null; }
  const r = await call(staff, 'POST', '/api/appointments', {
    branchId, booker: { name: 'DEMO người đặt', phone: '0912345678' },
    attendee: { patientId: patient.id, name: patient.fullName }, attendeeRelation: 'Relative', kind: 'WalkIn', source: 'InPerson',
    serviceId, dentistId, chairId: slot.chairId, startAt: slot.start, durationMinutes: minutes,
    exceptionReason: 'DEMO walk-in', requestKey: uuid(), scheduleVersion: slot.scheduleVersion, bookingSettingsVersion: slot.bookingSettingsVersion });
  return { ...r, startAt: slot.start };
}
const careOwner = 4;
const careTask = (patientId, type, content, dueIso, appointmentId = null) =>
  call(staff, 'POST', '/api/care-tasks', { patientId, branchId, appointmentId, type, content, ownerAccountId: careOwner, dueAt: dueIso, requestKey: uuid() });
const codeOf = b => b.code ?? b.appointmentCode;

await scenario('upcoming', async () => {
  const name = 'DEMO Lịch sắp tới';
  const { patient, created } = await ensurePatient(name, '1990-05-12', 'FEMALE');
  if (!created) return log(`skip ${name}`);
  log(name); await book(patient, addDays(todayClinic, 1), 30, 0); await book(patient, addDays(todayClinic, 3), 30, 2);
});
await scenario('cancelled', async () => {
  const name = 'DEMO Lịch đã hủy';
  const { patient, created } = await ensurePatient(name, '1985-03-02', 'MALE');
  if (!created) return log(`skip ${name}`);
  log(name); const b = await book(patient, addDays(todayClinic, 2), 30, 1);
  if (b) await call(staff, 'POST', `/api/appointments/${codeOf(b)}/cancel`, { expectedVersion: b.version, requestKey: uuid(), reason: 'DEMO bệnh nhân báo bận', clinicInitiated: true });
});
await scenario('checked-in', async () => {
  const name = 'DEMO Đang chờ khám';
  const { patient, created } = await ensurePatient(name, '1995-11-20', 'FEMALE');
  if (!created) return log(`skip ${name}`);
  log(name); const b = await book(patient, todayClinic, 1, 0, 5000);
  if (b) await call(staff, 'POST', `/api/reception/appointments/${b.appointmentId}/check-in`, { patientId: patient.id, expectedVersion: b.version, requestKey: uuid(), identityConfirmed: true, purpose: 'InitialExamination', note: 'DEMO triệu chứng đau răng' });
});
await scenario('no-show', async () => {
  const name = 'DEMO Vắng hẹn';
  const { patient, created } = await ensurePatient(name, '1992-07-08', 'MALE');
  // The patient is created first and the no-show later, so a rerun with DEMO_NOSHOW=1 resumes this step only.
  if (!created && process.env.DEMO_NOSHOW !== '1') return log(`skip ${name}`);
  log(name); const b = await book(patient, todayClinic, 1, 0, 5000);
  if (!b) return;
  const wait = Date.parse(b.startAt) + 61000 - Date.now();
  if (wait > 40 * 60000) return log(`  ! next slot is ${Math.round(wait / 60000)} min away; run again later with DEMO_NOSHOW=1`);
  if (wait > 0) { log(`  waiting ${Math.ceil(wait / 1000)}s for the slot to pass`); await sleep(wait); }
  await call(staff, 'POST', `/api/appointments/${codeOf(b)}/no-show`, { expectedVersion: b.version, requestKey: uuid(), reason: 'DEMO khách không đến, không báo trước' });
  await careTask(patient.id, 'MissedAppointment', 'DEMO gọi hỏi lý do vắng hẹn và đặt lại lịch', new Date(Date.now() + 3600e3).toISOString(), b.appointmentId);
});
await scenario('care-tasks', async () => {
  const name = 'DEMO Chăm sóc sau điều trị';
  const { patient, created } = await ensurePatient(name, '1988-01-15', 'FEMALE');
  if (!created) return log(`skip ${name}`);
  log(name);
  await careTask(patient.id, 'FollowUp', 'DEMO hỏi thăm sau nhổ răng khôn (quá hạn)', new Date(Date.now() - 2 * 86400e3).toISOString());
  await careTask(patient.id, 'FollowUp', 'DEMO nhắc tái khám sau 1 tuần', new Date(Date.now() + 4 * 3600e3).toISOString());
  const done = await careTask(patient.id, 'LabBlocked', 'DEMO báo khách mão sứ đã về', new Date(Date.now() + 86400e3).toISOString());
  try { await call(staff, 'POST', `/api/care-tasks/${done.id}/contact-attempts`, { channel: 'Phone', result: 'Reached', nextAction: null, rebookedAppointmentId: null, expectedTaskVersion: done.version, requestKey: uuid() }); }
  catch (e) { log('  ! contact attempt: ' + e.message); }
});
await scenario('prescription', async () => {
  const name = 'DEMO Đơn thuốc';
  const { patient, created } = await ensurePatient(name, '1979-09-30', 'MALE');
  if (!created) return log(`skip ${name}`);
  log(name);
  const meds = await call(dentist, 'GET', `/api/prescriptions/medicine-options?branchId=${branchId}`);
  await call(dentist, 'POST', `/api/prescriptions/patients/${patient.id}`, { requestKey: uuid(), expectedVersion: 0, branchId, prescribedOn: todayClinic,
    diagnosis: 'DEMO viêm tủy răng', otherDiagnosis: null, advice: 'DEMO uống sau ăn, tái khám sau 5 ngày',
    items: meds.slice(0, 2).map(m => ({ medicineId: m.id, quantity: 10, instructions: 'Uống sau ăn, ngày 2 lần', unit: m.baseUnit })) });
});
log('done');
