import { execFileSync } from 'node:child_process';
import { expect, test, type APIRequestContext } from '@playwright/test';

// System-test coverage for cases that had no automated test (5.3 System Test FRs).
// API-level checks of the real backend on the isolated PostgreSQL fixture (see infrastructure/scripts/prepare-clinical-e2e.ps1).
const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5084';
const password = process.env.E2E_DEMO_PASSWORD;
const schema = process.env.DCMS_RECEPTION_E2E_SCHEMA;
test.skip(!password || !schema, 'Requires the isolated PostgreSQL clinical fixture and E2E_DEMO_PASSWORD.');

async function auth(request: APIRequestContext, name: string) {
  const r = await request.post(`${apiUrl}/api/auth/sign-in`, { data: { userName: name, password } });
  expect(r.ok(), await r.text()).toBeTruthy(); return { Authorization: `Bearer ${(await r.json()).accessToken}` };
}
function sql(statement: string) {
  expect(schema).toMatch(/^dcms_reception_e2e_[a-z0-9]+$/);
  return execFileSync('docker', ['exec', '-i', 'dcms-postgres-1', 'sh', '-c', 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -tA'],
    { encoding: 'utf8', input: `SET search_path TO ${schema};
${statement};
` });
}
async function newBooking(request: APIRequestContext) {
  const headers = await auth(request, 'assistant.demo');
  const options = await (await request.get(`${apiUrl}/api/reception/options`, { headers })).json();
  const branchId = options.calendar.branches[0].id;
  const dentistId = options.calendar.dentists.find((x: { name: string }) => x.name === 'Nha sĩ demo').id;
  const serviceId = options.calendar.services[0].id;
  const p = await request.post(`${apiUrl}/api/patients`, { headers, data: { fullName: `SYNTHETIC hồi cứu ${crypto.randomUUID().slice(0, 8)}`, birthDate: '2000-01-01', gender: 'FEMALE', duplicatesReviewed: true } });
  expect(p.ok(), await p.text()).toBeTruthy(); const patient = (await p.json()).patient;
  const availability = await (await request.get(`${apiUrl}/api/availability?branchId=${branchId}&dentistId=${dentistId}&serviceId=${serviceId}&date=${options.clinicDate}&durationMinutes=1`, { headers })).json();
  const slot = availability.slots.find((x: { start: string }) => Date.parse(x.start) > Date.now() + 10000); expect(slot).toBeTruthy();
  const booked = await request.post(`${apiUrl}/api/appointments`, { headers, data: { branchId, booker: { name: 'SYNTHETIC người đặt', phone: '0912345678' },
    attendee: { patientId: patient.id, name: patient.fullName }, attendeeRelation: 'Relative', kind: 'WalkIn', source: 'InPerson', serviceId, dentistId, chairId: slot.chairId,
    startAt: slot.start, durationMinutes: 1, exceptionReason: 'SYNTHETIC backdate fixture', requestKey: crypto.randomUUID(), scheduleVersion: slot.scheduleVersion, bookingSettingsVersion: slot.bookingSettingsVersion } });
  expect(booked.ok(), await booked.text()).toBeTruthy();
  return { headers, patient, branchId, appointment: await booked.json() };
}

test('WF4-07 backdated check-in needs the reception.backdate right and a reason', async ({ request }) => {
  const f = await newBooking(request);
  const arrival = new Date(Date.now() - 60_000).toISOString();
  const body = (reason: string | null) => ({ patientId: f.patient.id, expectedVersion: f.appointment.version, requestKey: crypto.randomUUID(), identityConfirmed: true,
    purpose: 'InitialExamination', arrivalAt: arrival, arrivalReason: reason, note: 'SYNTHETIC hồi cứu' });
  const url = `${apiUrl}/api/reception/appointments/${f.appointment.appointmentId}/check-in`;
  const refused = await request.post(url, { headers: f.headers, data: body('SYNTHETIC đến sớm') });
  expect(refused.status(), await refused.text()).toBe(403);
  const assistantId = sql(`SELECT "Id" FROM "USER_ACCOUNT" WHERE "UserName" = 'assistant.demo'`).trim().split(/\r?\n/).pop()!;
  expect(assistantId).toMatch(/^\d+$/);
  sql(`INSERT INTO "AspNetUserClaims" ("UserId","ClaimType","ClaimValue") VALUES (${assistantId},'permission','reception.backdate')`);
  try {
    const noReason = await request.post(url, { headers: f.headers, data: body(null) });
    expect(noReason.status(), await noReason.text()).toBe(400);
    const ok = await request.post(url, { headers: f.headers, data: body('SYNTHETIC đến sớm') });
    expect(ok.ok(), await ok.text()).toBeTruthy();
    const visit = await ok.json();
    expect(JSON.stringify(visit)).toContain('SYNTHETIC đến sớm');
  } finally {
    sql(`DELETE FROM "AspNetUserClaims" WHERE "UserId" = ${assistantId} AND "ClaimValue" = 'reception.backdate'`);
  }
});

test('WF7-05 advance and deposit received before treatment are listed with balances', async ({ request }) => {
  const f = await newBooking(request);
  const deposit = await request.post(`${apiUrl}/api/finance/advances`, { headers: f.headers, data: { requestKey: crypto.randomUUID(), patientId: f.patient.id,
    branchId: f.branchId, dossierId: null, kind: 'Deposit', method: 'Cash', amountVnd: 500000, payer: 'SYNTHETIC người nộp', note: 'SYNTHETIC đặt cọc' } });
  expect(deposit.ok(), await deposit.text()).toBeTruthy();
  const created = await deposit.json();
  expect(created.amountVnd).toBe(500000); expect(created.availableVnd).toBe(500000); expect(created.allocatedVnd).toBe(0);
  const list = await request.get(`${apiUrl}/api/finance/advances?branchId=${f.branchId}&patientId=${f.patient.id}`, { headers: f.headers });
  expect(list.ok(), await list.text()).toBeTruthy();
  const rows = await list.json();
  const row = (Array.isArray(rows) ? rows : rows.items).find((x: { id: string }) => x.id === created.id);
  expect(row, JSON.stringify(rows)).toBeTruthy(); expect(row.availableVnd).toBe(500000);
});

test('WF7-09 revenue report matches the recorded cash advances', async ({ request }) => {
  const f = await newBooking(request);
  const day = new Date().toISOString().slice(0, 10);
  const report = async () => (await (await request.get(`${apiUrl}/api/finance/revenue?from=${day}&to=${day}&branchId=${f.branchId}`, { headers: f.headers })).json());
  const before = await report();
  const paid = await request.post(`${apiUrl}/api/finance/advances`, { headers: f.headers, data: { requestKey: crypto.randomUUID(), patientId: f.patient.id,
    branchId: f.branchId, dossierId: null, kind: 'Deposit', method: 'Cash', amountVnd: 250000, payer: 'SYNTHETIC người nộp', note: 'SYNTHETIC doanh thu' } });
  expect(paid.ok(), await paid.text()).toBeTruthy();
  const after = await report();
  expect(after.totals.cashVnd - before.totals.cashVnd).toBe(250000);
  expect(after.totals.receivedVnd - before.totals.receivedVnd).toBe(250000);
});
