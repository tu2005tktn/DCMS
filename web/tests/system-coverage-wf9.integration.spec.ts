import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { expect, test, type APIRequestContext } from '@playwright/test';

// System-test coverage for WF9 (Care & Records) cases that had no automated test (5.3 System Test FRs).
// API-level checks of the real backend on the isolated PostgreSQL fixture (see infrastructure/scripts/prepare-clinical-e2e.ps1).
const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5084';
const password = process.env.E2E_DEMO_PASSWORD;
const schema = process.env.DCMS_RECEPTION_E2E_SCHEMA;
test.skip(!password || !schema, 'Requires the isolated PostgreSQL clinical fixture and E2E_DEMO_PASSWORD.');

type Headers = { Authorization: string };
async function staff(request: APIRequestContext, name: string): Promise<Headers> {
  const r = await request.post(`${apiUrl}/api/auth/sign-in`, { data: { userName: name, password } });
  expect(r.ok(), await r.text()).toBeTruthy(); return { Authorization: `Bearer ${(await r.json()).accessToken}` };
}
async function ok<T = any>(r: Awaited<ReturnType<APIRequestContext['get']>>): Promise<T> { expect(r.ok(), `${r.url()} ${r.status()} ${await r.text()}`).toBeTruthy(); return r.json(); }
async function customer(request: APIRequestContext) {
  const suffix = crypto.randomUUID().slice(0, 8);
  const phone = '09' + String(Math.floor(Math.random() * 100000000)).padStart(8, '0');
  const pass = 'Aa1!' + crypto.randomUUID();
  const fullName = `SYNTHETIC khách ${suffix}`;
  await ok(await request.post(`${apiUrl}/api/auth/register`, { data: { fullName, phoneNumber: phone, email: `wf9-${suffix}@example.invalid`, password: pass, confirmPassword: pass } }));
  const r = await request.post(`${apiUrl}/api/auth/sign-in`, { data: { userName: phone, password: pass } });
  const body = await ok(r);
  return { headers: { Authorization: `Bearer ${body.accessToken}` } as Headers, fullName, phone, suffix };
}
async function newPatient(request: APIRequestContext, headers: Headers, fullName: string, birthDate = '1990-05-05') {
  const p = await ok(await request.post(`${apiUrl}/api/patients`, { headers, data: { fullName, birthDate, gender: 'FEMALE', phone: '0912345678', duplicatesReviewed: true, requestKey: crypto.randomUUID() } }));
  return p.patient as { id: number; patientCode: string; fullName: string };
}

test('WF9-01 customer link request is verified, another is rejected with a reason and both are audited', async ({ request }) => {
  const assistant = await staff(request, 'assistant.demo');
  const a = await customer(request), b = await customer(request);
  const patient = await newPatient(request, assistant, a.fullName);
  const submit = (c: typeof a, fullName: string) => request.post(`${apiUrl}/api/patient-links/me/requests`, { headers: c.headers, data: { fullName, birthDate: '1990-05-05', patientCode: null, matchingInformation: 'SYNTHETIC', confirmSelf: true } });
  const ra = (await ok(await submit(a, a.fullName))).request;
  const rb = (await ok(await submit(b, b.fullName))).request;
  expect(ra.status.toUpperCase()).toBe('PENDING'); expect(rb.status.toUpperCase()).toBe('PENDING');
  expect((await ok(await request.get(`${apiUrl}/api/patient-links/me`, { headers: a.headers }))).linkStatus.toUpperCase()).not.toBe('VERIFIED');

  const verified = await ok(await request.post(`${apiUrl}/api/patient-link-reviews/${ra.id}/verify`, { headers: assistant, data: { version: ra.version, patientId: patient.id, verificationNote: 'SYNTHETIC đối chiếu' } }));
  expect(verified.request.status.toUpperCase()).toBe('VERIFIED');
  const rejected = await ok(await request.post(`${apiUrl}/api/patient-link-reviews/${rb.id}/reject`, { headers: assistant, data: { version: rb.version, decisionReason: 'SYNTHETIC không khớp thông tin', verificationNote: null, publicNote: 'SYNTHETIC lý do công khai' } }));
  expect(rejected.request.status.toUpperCase()).toBe('REJECTED');
  expect((await ok(await request.get(`${apiUrl}/api/patient-links/me`, { headers: a.headers }))).linkStatus.toUpperCase()).toBe('VERIFIED');
  const bView = await ok(await request.get(`${apiUrl}/api/patient-links/me`, { headers: b.headers }));
  expect(bView.linkStatus.toUpperCase()).not.toBe('VERIFIED');
  expect(JSON.stringify(bView)).toContain('SYNTHETIC lý do công khai');
  const audit = await ok(await request.get(`${apiUrl}/api/patient-link-reviews/${ra.id}/audit`, { headers: assistant }));
  expect(audit.length).toBeGreaterThan(0);
  const auditB = await ok(await request.get(`${apiUrl}/api/patient-link-reviews/${rb.id}/audit`, { headers: assistant }));
  expect(JSON.stringify(auditB)).toContain('SYNTHETIC không khớp thông tin');

});

test('WF9-03 / WF9-04 verified customer reads only own record, visits, prescriptions; another patient is refused', async ({ request }) => {
  const assistant = await staff(request, 'assistant.demo'), dentist = await staff(request, 'dentist.demo');
  const a = await customer(request);
  const mine = await newPatient(request, assistant, a.fullName);
  const other = await newPatient(request, assistant, `SYNTHETIC người khác ${a.suffix}`);
  const link = (await ok(await request.post(`${apiUrl}/api/patient-links/me/requests`, { headers: a.headers, data: { fullName: a.fullName, birthDate: '1990-05-05', patientCode: null, matchingInformation: 'SYNTHETIC', confirmSelf: true } }))).request;
  await ok(await request.post(`${apiUrl}/api/patient-link-reviews/${link.id}/verify`, { headers: assistant, data: { version: link.version, patientId: mine.id, verificationNote: 'SYNTHETIC' } }));
  const record = await ok(await request.get(`${apiUrl}/api/patient-links/me/record`, { headers: a.headers }));
  expect(record.patientCode).toBe(mine.patientCode); expect(JSON.stringify(record)).not.toContain(other.patientCode);
  // Another patient's data is refused for the customer account.
  const denied = await request.get(`${apiUrl}/api/patients/${other.id}`, { headers: a.headers });
  expect([401, 403]).toContain(denied.status());
  const deniedMine = await request.get(`${apiUrl}/api/patients/${mine.id}`, { headers: a.headers });
  expect([401, 403]).toContain(deniedMine.status());
  // Visits: an unfinished visit is not listed; a completed one is read-only.
  const options = await ok(await request.get(`${apiUrl}/api/reception/options`, { headers: assistant }));
  const branchId = options.calendar.branches[0].id;
  const dentistId = options.calendar.dentists.find((x: { name: string }) => x.name === 'Nha sĩ demo').id;
  const serviceId = options.calendar.services[0].id;
  const availability = await ok(await request.get(`${apiUrl}/api/availability?branchId=${branchId}&dentistId=${dentistId}&serviceId=${serviceId}&date=${options.clinicDate}&durationMinutes=1`, { headers: assistant }));
  const slot = availability.slots.find((x: { start: string }) => Date.parse(x.start) > Date.now() + 10000); expect(slot).toBeTruthy();
  const booked = await ok(await request.post(`${apiUrl}/api/appointments`, { headers: assistant, data: { branchId, booker: { name: 'SYNTHETIC người đặt', phone: '0912345678' },
    attendee: { patientId: mine.id, name: mine.fullName }, attendeeRelation: 'Self', kind: 'WalkIn', source: 'InPerson', serviceId, dentistId, chairId: slot.chairId,
    startAt: slot.start, durationMinutes: 1, exceptionReason: 'SYNTHETIC', requestKey: crypto.randomUUID(), scheduleVersion: slot.scheduleVersion, bookingSettingsVersion: slot.bookingSettingsVersion } }));
  const checked = await ok(await request.post(`${apiUrl}/api/reception/appointments/${booked.appointmentId}/check-in`, { headers: assistant, data: { patientId: mine.id, expectedVersion: booked.version, requestKey: crypto.randomUUID(), identityConfirmed: true, purpose: 'InitialExamination' } }));
  const started = await ok(await request.post(`${apiUrl}/api/visits/${checked.visitId}/start`, { headers: dentist, data: { patientId: mine.id, expectedVersion: checked.visitVersion, requestKey: crypto.randomUUID() } }));
  const visitsBefore = await ok(await request.get(`${apiUrl}/api/patients/me/visits`, { headers: a.headers }));
  expect(JSON.stringify(visitsBefore)).not.toContain(checked.visitId);
  await ok(await request.put(`${apiUrl}/api/visits/${checked.visitId}/examination`, { headers: dentist, data: {
    patientId: mine.id, expectedVersion: started.visit.visitVersion, requestKey: crypto.randomUUID(), confirm: true,
    content: { ...started.initialContent, symptoms: 'SYNTHETIC', diagnoses: ['Chưa phát hiện bất thường'], conclusion: 'SYNTHETIC kết luận chính thức', advice: 'SYNTHETIC lời dặn', notes: 'SYNTHETIC INTERNAL NOTE', disposition: 'ConsultationOnly' } } }));
  const visitsAfter = await ok(await request.get(`${apiUrl}/api/patients/me/visits`, { headers: a.headers }));
  expect(JSON.stringify(visitsAfter)).toContain(checked.visitId);
  expect(JSON.stringify(visitsAfter)).not.toContain('SYNTHETIC INTERNAL NOTE');
  const detail = await ok(await request.get(`${apiUrl}/api/patients/me/visits/${checked.visitId}`, { headers: a.headers }));
  expect(JSON.stringify(detail)).toContain('SYNTHETIC lời dặn'); expect(JSON.stringify(detail)).not.toContain('SYNTHETIC INTERNAL NOTE');
  const write = await request.put(`${apiUrl}/api/patients/me/visits/${checked.visitId}`, { headers: a.headers, data: {} });
  expect([404, 405, 403]).toContain(write.status());
  const rx = await request.get(`${apiUrl}/api/patients/me/prescriptions`, { headers: a.headers });
  expect(rx.ok(), await rx.text()).toBeTruthy();
});

test('WF9-10 / WF9-11 care task is created, assigned, contacted, completed and escalated', async ({ request }) => {
  const assistant = await staff(request, 'assistant.demo'), dentist = await staff(request, 'dentist.demo');
  const options = await ok(await request.get(`${apiUrl}/api/care-tasks/options`, { headers: assistant }));
  const branch = options.branches.find((x: { canCreate: boolean }) => x.canCreate) ?? options.branches[0];
  const patient = await newPatient(request, assistant, `SYNTHETIC chăm sóc ${crypto.randomUUID().slice(0, 8)}`);
  const type = options.types.find((x: { system: boolean }) => !x.system)?.code ?? 'FollowUp';
  const create = (content: string) => request.post(`${apiUrl}/api/care-tasks`, { headers: assistant, data: { patientId: patient.id, branchId: branch.id, instructionId: null, appointmentId: null, reviewId: null, type, content, ownerAccountId: options.actorAccountId, dueAt: new Date(Date.now() + 86_400_000).toISOString(), requestKey: crypto.randomUUID() } });
  const task = await ok(await create('SYNTHETIC gọi hỏi tình trạng'));
  expect(task.status).toBe('Open');
  const contact = await ok(await request.post(`${apiUrl}/api/care-tasks/${task.id}/contact-attempts`, { headers: assistant, data: { channel: 'Phone', result: 'Reached', nextAction: 'SYNTHETIC không cần gọi lại', rebookedAppointmentId: null, expectedTaskVersion: task.version, requestKey: crypto.randomUUID() } }));
  expect(contact.result).toBe('Reached');
  let detail = await ok(await request.get(`${apiUrl}/api/care-tasks/${task.id}`, { headers: assistant }));
  expect(detail.contacts.length).toBe(1);
  const progress = await ok(await request.patch(`${apiUrl}/api/care-tasks/${task.id}`, { headers: assistant, data: { expectedVersion: detail.task.version, status: 'InProgress', ownerAccountId: options.actorAccountId, dueAt: detail.task.dueAt, content: detail.task.content, requestKey: crypto.randomUUID() } }));
  expect(progress.status).toBe('InProgress');
  const done = await ok(await request.patch(`${apiUrl}/api/care-tasks/${task.id}`, { headers: assistant, data: { expectedVersion: progress.version, status: 'Done', ownerAccountId: options.actorAccountId, dueAt: detail.task.dueAt, content: detail.task.content, requestKey: crypto.randomUUID() } }));
  expect(done.status).toBe('Done');
  const open = await ok(await request.get(`${apiUrl}/api/care-tasks/worklist?branchId=${branch.id}&status=Pending&search=${encodeURIComponent(patient.fullName)}`, { headers: assistant }));
  expect(open.items.find((x: { task: { id: string } }) => x.task.id === task.id)).toBeFalsy();
  // Done tasks cannot change again.
  const again = await request.patch(`${apiUrl}/api/care-tasks/${task.id}`, { headers: assistant, data: { expectedVersion: done.version, status: 'Open', ownerAccountId: options.actorAccountId, dueAt: detail.task.dueAt, content: detail.task.content, requestKey: crypto.randomUUID() } });
  expect(again.status()).toBe(409);

  // WF9-11: escalation with a reason, visible to the receiver, permission checked.
  const t2 = await ok(await create('SYNTHETIC quá hạn cần leo thang'));
  const dentistMe = await ok(await request.get(`${apiUrl}/api/auth/me`, { headers: dentist }));
  const dentistId = dentistMe.id ?? dentistMe.accountId;
  const escalated = await ok(await request.post(`${apiUrl}/api/care-tasks/${t2.id}/escalations`, { headers: assistant, data: { targetClinicianAccountId: dentistId, reason: 'SYNTHETIC quá hạn 2 ngày', expectedTaskVersion: t2.version, requestKey: crypto.randomUUID() } }));
  expect(escalated.reason).toBe('SYNTHETIC quá hạn 2 ngày'); expect(escalated.targetClinicianAccountId).toBe(dentistId);
  detail = await ok(await request.get(`${apiUrl}/api/care-tasks/${t2.id}`, { headers: assistant }));
  expect(detail.escalations.length).toBe(1);
  const ack = await ok(await request.post(`${apiUrl}/api/care-escalations/${escalated.id}/acknowledge`, { headers: dentist, data: { expectedVersion: escalated.version, resolution: null, requestKey: crypto.randomUUID() } }));
  expect(ack.status).not.toBe('Open');
  const denied = await request.post(`${apiUrl}/api/care-tasks/${t2.id}/escalations`, { headers: dentist, data: { targetClinicianAccountId: -123, reason: 'SYNTHETIC', expectedTaskVersion: 99, requestKey: crypto.randomUUID() } });
  expect([403, 409]).toContain(denied.status());
});

function sql(statement: string) {
  expect(schema).toMatch(/^dcms_reception_e2e_[a-z0-9]+$/);
  return execFileSync('docker', ['exec', '-i', 'dcms-postgres-1', 'sh', '-c', 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -tA'],
    { encoding: 'utf8', input: `SET search_path TO ${schema};\n${statement};\n` });
}
async function poll<T>(fn: () => Promise<T | undefined>, seconds = 60): Promise<T | undefined> {
  const end = Date.now() + seconds * 1000;
  while (Date.now() < end) { const v = await fn(); if (v) return v; await new Promise(r => setTimeout(r, 2000)); }
  return undefined;
}
async function customerBooking(request: APIRequestContext) {
  const c = await customer(request);
  const options = await ok(await request.get(`${apiUrl}/api/availability/options`, { headers: c.headers }));
  const branch = options.branches[0];
  const dentist = options.employees.find((x: { branchId: number; name: string }) => x.branchId === branch.id && x.name === 'Nha sĩ demo');
  const service = options.services.find((x: { branchId: number }) => x.branchId === branch.id);
  for (let d = 2; d < 12; d++) {
    const date = new Date(Date.now() + d * 86_400_000).toISOString().slice(0, 10);
    const availability = await (await request.get(`${apiUrl}/api/availability?branchId=${branch.id}&dentistId=${dentist.id}&serviceId=${service.id}&date=${date}&durationMinutes=${service.publicDurationMinutes ?? 30}`, { headers: c.headers })).json();
    const slot = availability.slots?.[0]; if (!slot) continue;
    const booked = await request.post(`${apiUrl}/api/appointments`, { headers: c.headers, data: {
      branchId: branch.id, booker: { name: c.fullName, phone: c.phone, email: null }, attendee: { patientId: null, name: c.fullName, birthDate: null }, attendeeRelation: 'Self',
      kind: 'Standard', source: 'App', serviceId: service.id, reason: 'SYNTHETIC', dentistId: dentist.id, assistantId: null, chairId: null, startAt: slot.start,
      durationMinutes: service.publicDurationMinutes ?? 30, followUpId: null, exceptionReason: null, bookingSettingsVersion: slot.bookingSettingsVersion,
      scheduleVersion: slot.scheduleVersion, assistantScheduleVersion: null, requestKey: crypto.randomUUID() } });
    if (booked.ok()) return { c, appointment: await booked.json() };
  }
  throw new Error('no bookable slot in the next 10 days');
}

test('WF9-12 / WF9-13 / WF9-09 notifications: booked and cancelled alerts, unread count, mark read and a single reminder', async ({ request }) => {
  test.setTimeout(240_000);
  const assistant = await staff(request, 'assistant.demo');
  const { c, appointment } = await customerBooking(request);
  const accountId = (await ok(await request.get(`${apiUrl}/api/patient-links/me`, { headers: c.headers }))).account.id;
  const unread = async () => JSON.stringify(await ok(await request.get(`${apiUrl}/api/notifications/unread-count`, { headers: c.headers })));
  const all = async () => (await ok(await request.get(`${apiUrl}/api/notifications?limit=50`, { headers: c.headers }))).items as { id: string; kind: string | number }[];
  const kinds = ['AppointmentBooked', 'AppointmentRescheduled', 'AppointmentCancelled', 'ReminderUpcoming', 'SupportRequestUpdate', 'NoShow', 'ScheduleReduced'];
  const is = (x: { kind: string | number }, kind: string) => x.kind === kind || x.kind === kinds.indexOf(kind);
  const find = (kind: string) => poll(async () => (await all()).find(x => is(x, kind)));
  const booked = await find('AppointmentBooked'); expect(booked, 'booked notification').toBeTruthy();
  const countAfterBooked = await unread();
  const marked = await ok(await request.post(`${apiUrl}/api/notifications/mark-read`, { headers: c.headers, data: { notificationIds: [booked!.id], expectedAccountId: accountId } }));
  expect(marked.marked).toContain(booked!.id);
  expect(await unread()).not.toBe(countAfterBooked);
  // WF9-13: the worker sends a reminder for a due reminder row and does not repeat it.
  const apptId = appointment.appointmentId;
  sql(`UPDATE "APPOINTMENT_REMINDER" SET "DueAt" = now() - interval '1 minute' WHERE "AppointmentId" = '${apptId}' AND "Status" = 'Pending'`);
  expect(await find('ReminderUpcoming'), 'reminder notification').toBeTruthy();
  await new Promise(r => setTimeout(r, 20_000));
  const sent = Number(sql(`SELECT count(*) FROM "APPOINTMENT_REMINDER" WHERE "AppointmentId" = '${apptId}' AND "Status" = 'Sent'`).trim().split(/\r?\n/).pop());
  expect((await all()).filter(x => is(x, 'ReminderUpcoming')).length).toBe(sent);
  // WF9-09: the unread count changes without any reload when staff cancel the appointment.
  const beforeCancel = await unread();
  const detail = await ok(await request.get(`${apiUrl}/api/appointments/${apptId}`, { headers: assistant }));
  await ok(await request.post(`${apiUrl}/api/appointments/${appointment.code ?? detail.code}/cancel`, { headers: assistant, data: { expectedVersion: detail.version ?? appointment.version, requestKey: crypto.randomUUID(), reason: 'SYNTHETIC phòng khám hủy', clinicInitiated: true } }));
  expect(await find('AppointmentCancelled'), 'cancel notification').toBeTruthy();
  expect(await unread()).not.toBe(beforeCancel);
});

async function completedVisit(request: APIRequestContext, assistant: Headers, dentist: Headers, patient: { id: number; fullName: string }, finish: boolean) {
  const options = await ok(await request.get(`${apiUrl}/api/reception/options`, { headers: assistant }));
  const branchId = options.calendar.branches[0].id;
  const dentistId = options.calendar.dentists.find((x: { name: string }) => x.name === 'Nha sĩ demo').id;
  const serviceId = options.calendar.services[0].id;
  const availability = await ok(await request.get(`${apiUrl}/api/availability?branchId=${branchId}&dentistId=${dentistId}&serviceId=${serviceId}&date=${options.clinicDate}&durationMinutes=1`, { headers: assistant }));
  const slot = availability.slots.find((x: { start: string }) => Date.parse(x.start) > Date.now() + 10000); expect(slot).toBeTruthy();
  const booked = await ok(await request.post(`${apiUrl}/api/appointments`, { headers: assistant, data: { branchId, booker: { name: 'SYNTHETIC người đặt', phone: '0912345678' },
    attendee: { patientId: patient.id, name: patient.fullName }, attendeeRelation: 'Self', kind: 'WalkIn', source: 'InPerson', serviceId, dentistId, chairId: slot.chairId,
    startAt: slot.start, durationMinutes: 1, exceptionReason: 'SYNTHETIC', requestKey: crypto.randomUUID(), scheduleVersion: slot.scheduleVersion, bookingSettingsVersion: slot.bookingSettingsVersion } }));
  const checked = await ok(await request.post(`${apiUrl}/api/reception/appointments/${booked.appointmentId}/check-in`, { headers: assistant, data: { patientId: patient.id, expectedVersion: booked.version, requestKey: crypto.randomUUID(), identityConfirmed: true, purpose: 'InitialExamination' } }));
  const started = await ok(await request.post(`${apiUrl}/api/visits/${checked.visitId}/start`, { headers: dentist, data: { patientId: patient.id, expectedVersion: checked.visitVersion, requestKey: crypto.randomUUID() } }));
  if (finish) await ok(await request.put(`${apiUrl}/api/visits/${checked.visitId}/examination`, { headers: dentist, data: {
    patientId: patient.id, expectedVersion: started.visit.visitVersion, requestKey: crypto.randomUUID(), confirm: true,
    content: { ...started.initialContent, symptoms: 'SYNTHETIC', diagnoses: ['Chưa phát hiện bất thường'], conclusion: 'SYNTHETIC kết luận', advice: 'SYNTHETIC lời dặn', notes: 'SYNTHETIC', disposition: 'ConsultationOnly' } } }));
  return { branchId, visitId: checked.visitId as string, visitVersion: started.visit.visitVersion as number, appointment: booked };
}

test('WF9-05 family record access: request, approve, reject and revoke', async ({ request }) => {
  const assistant = await staff(request, 'assistant.demo');
  const owner = await customer(request);
  const member = async (name: string) => { const r = await ok(await request.post(`${apiUrl}/api/me/family-members`, { headers: owner.headers, data: { fullName: name, relationship: 'Child', birthDate: '2015-03-12', gender: 'MALE', contactPhone: '0912345678', requestKey: crypto.randomUUID() } })); return r.member ?? r.Member ?? r; };
  const m1 = await member(`SYNTHETIC con A ${owner.suffix}`), m2 = await member(`SYNTHETIC con B ${owner.suffix}`);
  const p1 = await newPatient(request, assistant, m1.fullName, '2015-03-12'), p2 = await newPatient(request, assistant, m2.fullName, '2015-03-12');
  const dentistH = await staff(request, 'dentist.demo');
  await completedVisit(request, assistant, dentistH, p1, false);
  const submit = async (m: { id: string }, p: { patientCode: string }) => (await ok(await request.post(`${apiUrl}/api/me/family-members/${m.id}/record-access/requests`, { headers: owner.headers, data: { patientCode: p.patientCode, matchingInformation: 'SYNTHETIC', requestKey: crypto.randomUUID() } })));
  const queue = async () => (await ok(await request.get(`${apiUrl}/api/family-record-reviews?status=All&keyword=${owner.suffix}`, { headers: assistant }))).items as { id: string; version: number; fullName: string; status: string }[];
  const r1 = await submit(m1, p1); await submit(m2, p2);
  const row1 = (await queue()).find(x => x.fullName === m1.fullName)!, row2 = (await queue()).find(x => x.fullName === m2.fullName)!;
  expect(row1, JSON.stringify(r1)).toBeTruthy(); expect(row2).toBeTruthy();
  expect((await request.get(`${apiUrl}/api/me/family-members/${m1.id}/record/examinations`, { headers: owner.headers })).status()).toBe(403);
  const branchId = (await ok(await request.get(`${apiUrl}/api/reception/options`, { headers: assistant }))).calendar.branches[0].id;
  const verified = await ok(await request.post(`${apiUrl}/api/family-record-reviews/${row1.id}/verify`, { headers: assistant, data: { patientId: p1.id, branchId, scopes: ['Examinations', 'Prescriptions'], expiresAt: new Date(Date.now() + 30 * 86_400_000).toISOString(), sharingBasis: 'PatientConsent', verificationNote: 'SYNTHETIC đối chiếu', consentConfirmed: true, expectedVersion: row1.version, requestKey: crypto.randomUUID() } }));
  expect(JSON.stringify(verified)).toMatch(/verified|granted|approved|active/i);
  expect((await request.get(`${apiUrl}/api/me/family-members/${m1.id}/record/examinations`, { headers: owner.headers })).ok()).toBeTruthy();
  expect((await request.get(`${apiUrl}/api/me/family-members/${m1.id}/record/documents`, { headers: owner.headers })).status()).toBe(403);
  const rejected = await ok(await request.post(`${apiUrl}/api/family-record-reviews/${row2.id}/reject`, { headers: assistant, data: { decisionReason: 'SYNTHETIC không đủ căn cứ', publicNote: 'SYNTHETIC', expectedVersion: row2.version, requestKey: crypto.randomUUID() } }));
  expect(JSON.stringify(rejected)).toMatch(/reject/i);
  expect((await request.get(`${apiUrl}/api/me/family-members/${m2.id}/record/examinations`, { headers: owner.headers })).status()).toBe(403);
  const current = (await ok(await request.get(`${apiUrl}/api/family-record-reviews/${row1.id}`, { headers: assistant }))).access;
  await ok(await request.post(`${apiUrl}/api/family-record-reviews/${row1.id}/revoke`, { headers: assistant, data: { decisionReason: 'SYNTHETIC thu hồi', publicNote: null, expectedVersion: current.version, requestKey: crypto.randomUUID() } }));
  expect((await request.get(`${apiUrl}/api/me/family-members/${m1.id}/record/examinations`, { headers: owner.headers })).status()).toBe(403);
  const audit = JSON.stringify(await ok(await request.get(`${apiUrl}/api/family-record-reviews/${row1.id}/audit`, { headers: assistant })));
  expect(audit).toMatch(/Verified/); expect(audit).toMatch(/Revoked/);
});

function png(): Buffer {
  const crc = (b: Buffer) => { let x = 0xffffffff; for (const v of b) { let c = (x ^ v) & 0xff; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; x = (x >>> 8) ^ c; } return (x ^ 0xffffffff) >>> 0; };
  const chunk = (t: string, d: Buffer) => { const l = Buffer.alloc(4); l.writeUInt32BE(d.length); const td = Buffer.concat([Buffer.from(t), d]); const c = Buffer.alloc(4); c.writeUInt32BE(crc(td)); return Buffer.concat([l, td, c]); };
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(1, 0); ihdr.writeUInt32BE(1, 4); ihdr[8] = 8; ihdr[9] = 0;
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ihdr), chunk('IDAT', Buffer.from([0x78, 0x9c, 0x63, 0x60, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01])), chunk('IEND', Buffer.alloc(0))]);
}

test('WF9-06 / WF9-07 clinical documents: upload rules, scan state, view and download; no delete endpoint exists', async ({ request }) => {
  test.setTimeout(150_000);
  const assistant = await staff(request, 'assistant.demo'), dentist = await staff(request, 'dentist.demo');
  const patient = await newPatient(request, assistant, `SYNTHETIC tài liệu ${crypto.randomUUID().slice(0, 8)}`);
  const visit = await completedVisit(request, assistant, dentist, patient, false);
  const meta = () => JSON.stringify({ patientId: patient.id, visitId: visit.visitId, dossierId: null, planId: null, investigationId: null, consentVersion: null, documentType: 'Other', source: 'Clinic', sourceDate: new Date().toISOString().slice(0, 10), description: 'SYNTHETIC', requestKey: crypto.randomUUID() });
  const upload = (name: string, mime: string, buffer: Buffer) => request.post(`${apiUrl}/api/clinical-documents`, { headers: dentist, multipart: { metadata: meta(), file: { name, mimeType: mime, buffer } } });
  const good = await upload('synthetic.png', 'image/png', png());
  expect(good.status(), await good.text()).toBe(201);
  const doc = (await good.json()).document;
  expect(['PendingScan', 'Ready']).toContain(doc.fileState);
  const bad = await upload('synthetic.exe', 'application/x-msdownload', Buffer.from('MZ-synthetic'));
  expect(bad.status()).toBe(400);
  const big = await upload('big.png', 'image/png', Buffer.concat([png(), Buffer.alloc(21 * 1024 * 1024)]));
  expect([400, 413]).toContain(big.status());
  // Until the antivirus check ends the content is refused. The fixture runs without ClamAV, so the scan pass is applied in SQL.
  const early = await request.get(`${apiUrl}/api/clinical-documents/${doc.documentId}/content`, { headers: dentist });
  expect(early.status()).toBe(409);
  sql(`UPDATE "CLINICAL_DOCUMENT" SET "FileState" = 'Ready', "Version" = "Version" + 1 WHERE "Id" = '${doc.documentId}'`);
  {
    const content = await request.get(`${apiUrl}/api/clinical-documents/${doc.documentId}/content`, { headers: dentist });
    expect(content.ok()).toBeTruthy(); expect((await content.body()).length).toBeGreaterThan(0);
    const download = await request.get(`${apiUrl}/api/clinical-documents/${doc.documentId}/content?disposition=attachment`, { headers: dentist });
    expect(download.headers()['content-disposition'] ?? '').toMatch(/attachment/i);
  }
  const list = await ok(await request.get(`${apiUrl}/api/clinical-documents?patientId=${patient.id}`, { headers: dentist }));
  expect(list.items.some((x: { documentId: string }) => x.documentId === doc.documentId)).toBeTruthy();
  const del = await request.delete(`${apiUrl}/api/clinical-documents/${doc.documentId}`, { headers: dentist });
  expect(del.status(), 'document delete is not available in the API').not.toBe(200);
});

test('WF9-14 follow-up instruction is created by the dentist, listed with its window and booked from it', async ({ request }) => {
  const assistant = await staff(request, 'assistant.demo'), dentist = await staff(request, 'dentist.demo');
  const patient = await newPatient(request, assistant, `SYNTHETIC tái khám ${crypto.randomUUID().slice(0, 8)}`);
  const visit = await completedVisit(request, assistant, dentist, patient, false);
  const day = (n: number) => new Date(Date.now() + n * 86_400_000).toISOString().slice(0, 10);
  const services = await ok(await request.get(`${apiUrl}/api/clinical-services?branchId=${visit.branchId}`, { headers: dentist }));
  const serviceId0 = (services.items ?? services)[1]?.id ?? (services.items ?? services)[0].id;
  const dentistAcc = (await ok(await request.get(`${apiUrl}/api/reception/options`, { headers: assistant }))).calendar.dentists.find((x: { name: string }) => x.name === 'Nha sĩ demo').id;
  const d0 = await ok(await request.post(`${apiUrl}/api/patients/${patient.id}/dossiers`, { headers: dentist, data: { branchId: visit.branchId, dentistId: dentistAcc, diagnosis: 'SYNTHETIC', note: null, startedOn: null, requestKey: crypto.randomUUID() } }));
  const item = { id: null, sourceDiagnosisId: null, serviceId: serviceId0, region: '25', unit: 'răng', quantity: 1, plannedOrder: 1, estimatedMinutes: 30, estimatedStart: null, estimatedEnd: null, agreedUnitPriceVnd: 300000, decision: 'Agreed', counseling: 'Đã tư vấn', decisionMaker: 'Bệnh nhân', representativeBasis: null, consentDocumentId: null };
  const d1 = await ok(await request.post(`${apiUrl}/api/dossiers/${d0.dossier.id}/plan/draft`, { headers: dentist, data: { expectedVersion: d0.dossier.version, goals: 'SYNTHETIC', items: [item], paymentTerms: [], requestKey: crypto.randomUUID() } }));
  const d2 = await ok(await request.post(`${apiUrl}/api/dossiers/${d0.dossier.id}/plan/commit`, { headers: dentist, data: { expectedVersion: d1.dossier.version, requestKey: crypto.randomUUID() } }));
  const posted = { dossierId: d2.dossier.id as string, itemId: d2.dossier.items[0].id as string };
  const created = await ok(await request.post(`${apiUrl}/api/follow-up-instructions`, { headers: dentist, data: { patientId: patient.id, branchId: visit.branchId,
    source: { dossierId: posted.dossierId, itemId: posted.itemId, stepId: null, visitId: null, activityId: null }, purpose: 'TreatmentContinuation', reason: 'SYNTHETIC tái khám sau 3 ngày', guidance: 'SYNTHETIC',
    earliestOn: day(2), targetOn: day(3), latestOn: day(5), durationMinutes: 15, dentistId: null, capability: null, requestKey: crypto.randomUUID() } }));
  const instruction = created.instruction;
  expect(instruction.earliestOn).toBe(day(2)); expect(instruction.latestOn).toBe(day(5));
  const list = await ok(await request.get(`${apiUrl}/api/follow-up-instructions?patientId=${patient.id}`, { headers: dentist }));
  expect(list.some((x: { id: string }) => x.id === instruction.id)).toBeTruthy();
  const options = await ok(await request.get(`${apiUrl}/api/reception/options`, { headers: assistant }));
  const dentistId = options.calendar.dentists.find((x: { name: string }) => x.name === 'Nha sĩ demo').id, serviceId = options.calendar.services[0].id;
  let bookedId: string | undefined;
  for (let d = 2; d <= 5 && !bookedId; d++) {
    const availability = await (await request.get(`${apiUrl}/api/availability?branchId=${visit.branchId}&dentistId=${dentistId}&serviceId=${serviceId}&date=${day(d)}&durationMinutes=15`, { headers: assistant })).json();
    const slot = availability.slots?.[0]; if (!slot) continue;
    const booked = await request.post(`${apiUrl}/api/appointments`, { headers: assistant, data: { branchId: visit.branchId, booker: { name: 'SYNTHETIC người đặt', phone: '0912345678' },
      attendee: { patientId: patient.id, name: patient.fullName }, attendeeRelation: 'Self', kind: 'FollowUp', source: 'InPerson', serviceId, dentistId, chairId: slot.chairId, startAt: slot.start,
      durationMinutes: 15, patientConsentConfirmed: true, followUpId: instruction.id, exceptionReason: null, requestKey: crypto.randomUUID(), scheduleVersion: slot.scheduleVersion, bookingSettingsVersion: slot.bookingSettingsVersion } });
    if (booked.ok()) bookedId = (await booked.json()).appointmentId; else console.log('follow-up booking attempt', d, booked.status(), (await booked.text()).slice(0, 200));
  }
  expect(bookedId, 'follow-up booking').toBeTruthy();
  const after = await ok(await request.get(`${apiUrl}/api/follow-up-instructions/${instruction.id}`, { headers: dentist }));
  expect(after.activeAppointmentId ?? after.instruction?.activeAppointmentId).toBe(bookedId);
});

test('WF9-08 support request: resolve is blocked until the appointment is changed, then closes with actor and time', async ({ request }) => {
  const assistant = await staff(request, 'assistant.demo');
  const { c, appointment } = await customerBooking(request);
  const detail = await ok(await request.get(`${apiUrl}/api/appointments/${appointment.appointmentId}`, { headers: assistant }));
  const code = appointment.code ?? detail.code;
  const start = new Date(Date.parse(appointment.startAt ?? detail.startAt) + 3_600_000).toISOString();
  const created = await ok(await request.post(`${apiUrl}/api/appointments/${code}/support-requests`, { headers: c.headers, data: { requestKey: crypto.randomUUID(), kind: 'Change', reason: 'SYNTHETIC xin dời lịch', newStartAt: start, dentistId: null, chairId: null } }));
  const id = created.request.id as string;
  let req = await ok(await request.get(`${apiUrl}/api/support-requests/${id}`, { headers: assistant }));
  expect(req.status).toBe('Pending');
  const verified = await ok(await request.post(`${apiUrl}/api/support-requests/${id}/verify-requester`, { headers: assistant, data: { requestKey: crypto.randomUUID(), expectedVersion: req.expectedVersion, notes: 'SYNTHETIC đã xác minh qua điện thoại' } }));
  const afterVerify = verified.expectedVersion ?? (await ok(await request.get(`${apiUrl}/api/support-requests/${id}`, { headers: assistant }))).expectedVersion;
  const blocked = await request.post(`${apiUrl}/api/support-requests/${id}/resolve`, { headers: assistant, data: { requestKey: crypto.randomUUID(), expectedVersion: afterVerify, decision: 'Resolve', notes: null, decisionReason: null } });
  expect(blocked.ok(), await blocked.text()).toBeFalsy();
  const changed = await ok(await request.post(`${apiUrl}/api/support-requests/${id}/change-appointment`, { headers: assistant, data: { requestKey: crypto.randomUUID(), expectedVersion: afterVerify, reason: 'SYNTHETIC khách xin dời', notes: null, newStartAt: start } }));
  expect(changed.ok ?? changed.Ok ?? true).toBeTruthy();
  req = await ok(await request.get(`${apiUrl}/api/support-requests/${id}`, { headers: assistant }));
  const resolved = await ok(await request.post(`${apiUrl}/api/support-requests/${id}/resolve`, { headers: assistant, data: { requestKey: crypto.randomUUID(), expectedVersion: req.expectedVersion, decision: 'Resolve', notes: null, decisionReason: null } }));
  const final = await ok(await request.get(`${apiUrl}/api/support-requests/${id}`, { headers: assistant }));
  expect(final.status).toBe('Resolved'); expect(final.deciderName).toBeTruthy(); expect(final.decidedAt).toBeTruthy();
  expect(JSON.stringify(resolved)).toMatch(/Resolve/);
});

// WF8-09 / WF8-10: the SQL seed of the clinic catalog is loaded into the isolated fixture schema (API-level / database-level check).
test('WF8-09 / WF8-10 clinic catalog seed provides labs, work types, 49 services, 17 templates and is idempotent', async () => {
  test.setTimeout(120_000);
  const seed = readFileSync(resolve(process.cwd(), '..', 'infrastructure', 'seed', 'clinic-catalog.seed.sql'), 'utf8');
  const run = (text: string) => execFileSync('docker', ['exec', '-i', 'dcms-postgres-1', 'sh', '-c', 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -tA'],
    { encoding: 'utf8', input: `SET search_path TO ${schema};\n${text}\n` });
  const count = (table: string, where = 'true') => Number(run(`SELECT count(*) FROM "${table}" WHERE ${where};`).trim().split(/\r?\n/).pop());
  const snapshot = () => ({ providers: count('CATALOG_PROVIDER', `"Code" IN ('LAB-01','LAB-02')`), workTypes: count('LAB_WORK_TYPE'), services: count('SCHEDULING_SERVICE'),
    templates: count('TREATMENT_TEMPLATE'), items: count('TREATMENT_TEMPLATE_ITEM'), materials: count('MATERIAL_CATALOG_ITEM'), categories: count('MATERIAL_CATEGORY') });
  const before = snapshot();
  run(seed);
  const first = snapshot();
  expect(first.providers).toBe(2);
  expect(before.providers === 0 || before.providers === 2).toBeTruthy();
  expect(first.workTypes).toBeGreaterThanOrEqual(7);
  expect(first.templates).toBeGreaterThanOrEqual(17);
  const seededServices = count('SCHEDULING_SERVICE', `"BranchId" = (SELECT "Id" FROM "SCHEDULING_BRANCH" WHERE "Name" = 'S09 - Chi nhánh thử nghiệm' LIMIT 1)`);
  expect(seededServices).toBeGreaterThanOrEqual(49);
  run(seed);
  expect(snapshot()).toEqual(first);
});

test('WF9-09 the support inbox shows a new customer request without reloading the page', async ({ page, request }) => {
  test.setTimeout(120_000);
  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill('assistant.demo');
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await page.getByRole('link', { name: 'Hộp thư hỗ trợ', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Hộp thư hỗ trợ', exact: true })).toBeVisible();
  const rows = page.locator('table tbody tr.ant-table-row');
  await expect(rows.first()).toBeVisible({ timeout: 15_000 }).catch(() => undefined);
  const before = await rows.count();
  const { c, appointment } = await customerBooking(request);
  const detail = await ok(await request.get(`${apiUrl}/api/appointments/${appointment.appointmentId}`, { headers: await staff(request, 'assistant.demo') }));
  const code = appointment.code ?? detail.code;
  await ok(await request.post(`${apiUrl}/api/appointments/${code}/support-requests`, { headers: c.headers, data: { requestKey: crypto.randomUUID(), kind: 'Change', reason: 'SYNTHETIC thời gian thực', newStartAt: null, dentistId: null, chairId: null } }));
  await expect.poll(() => rows.count(), { timeout: 60_000, message: 'new request row appears without reload' }).toBeGreaterThan(before);
});
