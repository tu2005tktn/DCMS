import type { APIRequestContext } from '@playwright/test';
import { Api, addDays, key, rnd, todayIso } from './helpers';

// Preconditions created through the API (the UI under test is exercised by the specs themselves).
export const nowVn = () => new Date(Date.now() + 7 * 3600_000);

export async function newPatient(api: Api, over: Record<string, unknown> = {}) {
  const n = rnd();
  const res = await api.post('/api/patients', {
    patientCode: null, fullName: `Bệnh nhân ST ${n}`, birthDate: '1990-05-05', gender: 'MALE', phone: `03${n}11`.slice(0, 10),
    address: 'Huế', guardianName: null, guardianRelationship: null, guardianPhone: null, note: null, duplicatesReviewed: true, requestKey: key(), ...over,
  });
  if (res.status !== 201) throw new Error('patient ' + JSON.stringify(res.body));
  return res.body.patient as { id: number; patientCode: string; fullName: string; birthDate: string; version: number };
}

export async function slots(api: Api, dentistId: number, date = todayIso(), dur = 30) {
  const r = await api.get(`/api/availability?branchId=1&dentistId=${dentistId}&serviceId=1&date=${date}&durationMinutes=${dur}`);
  return (r.body?.slots ?? []) as any[];
}

/** Next free slot today (dentists 3, 4, 6 have an evening exception during the test run). */
export async function nextSlotToday(api: Api, from = Date.now() - 10 * 60_000) {
  for (const d of [4, 3]) {
    const s = (await slots(api, d, todayIso(), 15)).find(x => Date.parse(x.start) >= from);
    if (s) return s;
  }
  throw new Error('no slot left today');
}

export async function staffBook(api: Api, slot: any, patient: { id?: number | null; fullName: string } | null, extra: Record<string, unknown> = {}) {
  const res = await api.post('/api/appointments', {
    branchId: 1, booker: { name: 'Lễ tân ghi nhận', phone: '0987654321', email: null },
    attendee: { patientId: patient?.id ?? null, name: patient?.fullName ?? 'Khách mới', birthDate: null }, attendeeRelation: 'Self',
    kind: 'Standard', source: 'Phone', serviceId: 1, reason: 'Kiểm thử hệ thống', dentistId: slot.dentistId, chairId: null,
    startAt: slot.start, durationMinutes: Math.round((Date.parse(slot.end) - Date.parse(slot.start)) / 60000) || 30, dossierId: null, exceptionReason: 'Kiểm thử hệ thống', requestKey: key(),
    scheduleVersion: slot.scheduleVersion, assistantScheduleVersion: null, bookingSettingsVersion: slot.bookingSettingsVersion,
    patientConsentConfirmed: false, familyMemberId: null, replacesAppointmentId: null, ...extra,
  });
  if (res.status !== 201) throw new Error('booking ' + JSON.stringify(res.body));
  return res.body as { appointmentId: string; code: string; version: number; status: string };
}

export async function checkIn(api: Api, apptId: string, patientId: number, version: number, purpose = 'InitialExamination') {
  const r = await api.post(`/api/reception/appointments/${apptId}/check-in`, { patientId, expectedVersion: version, requestKey: key(), identityConfirmed: true, arrivalAt: null, arrivalReason: null, purpose, note: null });
  if (r.status !== 200) throw new Error('check-in ' + JSON.stringify(r.body));
  return r.body as { visitId: string; visitVersion: number; dentistName: string };
}

export const dentistUser = (name: string) => ({ 'Nha sĩ demo': 'dentist.demo', 'Chủ phòng khám demo': 'owner.demo', 'Nhân viên đa vai trò demo': 'dual.demo' } as Record<string, string>)[name];
export { addDays };
export async function staff(request: APIRequestContext, user = 'assistant.demo') { return Api.as(request, user); }

/** A queued examination visit for the tests: a fresh patient checked in today, or (when today has no free
 *  slot left) an already queued initial-examination visit of dentist.demo / owner.demo. */
export async function freshVisit(request: APIRequestContext) {
  const api = await Api.as(request, 'assistant.demo');
  try {
    const p = await newPatient(api);
    const a = await staffBook(api, await nextSlotToday(api), p);
    const v = await checkIn(api, a.appointmentId, p.id, a.version);
    return { visitId: v.visitId, patientId: p.id, code: p.patientCode, name: p.fullName, user: dentistUser(v.dentistName) };
  } catch {
    const q = (await api.get(`/api/reception/queue?branchId=1&date=${todayIso()}&pageSize=100`)).body.items as any[];
    const v = q.find(x => (x.status === 'Queued' || x.status === 'InProgress') && x.purpose === 'InitialExamination' && dentistUser(x.dentistName) !== 'dual.demo');
    if (!v) throw new Error('no visit available');
    return { visitId: v.visitId, patientId: v.patientId, code: v.patientCode, name: v.patientName, user: dentistUser(v.dentistName) };
  }
}

/** A patient with a sheet that has one confirmed work line (precondition created through the API). */
export async function sheetWithWork(request: any, serviceId = 32, teeth = ['36'], withActual = true, existing?: any) {
  const api = await Api.as(request, 'owner.demo'); const p = existing ?? await newPatient(await Api.as(request, 'assistant.demo'));
  const d = (await api.post(`/api/patients/${p.id}/dossiers`, { branchId: 1, dentistId: 4, diagnosis: 'Mất răng / sâu răng', note: null, startedOn: null, requestKey: key(), sourceVisitId: null, expectedSourceVisitVersion: null })).body.dossier;
  const price = (await api.get('/api/service-prices/?branchId=1')).body.find((x: any) => x.serviceId === serviceId).referenceUnitPriceVnd;
  const row = { serviceId, dentistId: 4, regions: teeth, quantity: 1, estimatedMinutes: 30, unitPriceVnd: price, costVnd: price, note: '', discountType: 'VND', discountValue: 0, discountNote: null };
  const draft = async (kind: string, r: any) => { const id = key(); const s = (await api.put(`/api/dossiers/${d.id}/sheet-drafts/${id}`, { kind, visitId: null, row: r, expectedVersion: 0, requestKey: key() })).body;
    const cur = (await api.get(`/api/dossiers/${d.id}/`)).body; return api.post(`/api/dossiers/${d.id}/sheet-drafts/${id}/confirm-${kind === 'Plan' ? 'plan' : 'actual'}`, { expectedVersion: (cur.dossier ?? cur).version, expectedDraftVersion: s.version, requestKey: key() }); };
  await draft('Plan', row); const items = ((await api.get(`/api/dossiers/${d.id}/`)).body.dossier ?? (await api.get(`/api/dossiers/${d.id}/`)).body).items;
  if (withActual) await draft('Actual', { ...row, sourceItemId: items[0].id, note: 'Đã làm' });
  return { patient: p, dossier: d, price };
}

