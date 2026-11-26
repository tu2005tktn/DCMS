import { test, expect, type APIRequestContext } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { staffLogin, password, suffix, phone } from './helpers';

const api = process.env.E2E_API_BASE_URL ?? 'http://localhost:5080';
test.beforeAll(() => { if (!password) throw new Error('Set E2E_DEMO_PASSWORD'); });

test('SYS1-09 duplicate user name is rejected', async ({ page }) => {
  await staffLogin(page, 'admin.demo');
  await page.getByRole('link', { name: 'Quản lý tài khoản', exact: true }).click();
  await page.getByRole('button', { name: 'Tạo nhân viên' }).click();
  const dlg = page.getByRole('dialog');
  await dlg.getByLabel('Tên đăng nhập').fill('dentist.demo');
  await dlg.getByLabel('Họ tên').fill('Trùng Tên');
  await dlg.getByLabel('Mã nhân viên').fill('DUP' + suffix().slice(0, 6).toUpperCase());
  await dlg.getByLabel('Email').fill(`dup.${suffix()}@example.com`);
  await dlg.getByLabel('Mật khẩu ban đầu').fill('Password123!');
  await dlg.getByLabel('Vai trò được cấp').click();
  await page.locator('.ant-select-item-option').filter({ hasText: 'Trợ lý' }).first().click();
  await dlg.getByRole('button', { name: 'Xác nhận tạo' }).click();
  await page.waitForTimeout(1500);
  await expect(dlg).toBeVisible();
  await expect(dlg.getByText(/đã tồn tại|đã được sử dụng|đã có|trùng/i).first()).toBeVisible();
  await page.screenshot({ path: 'test-results/sys1-09.png' });
});

test('SYS4-03 suspected duplicate patient is flagged', async ({ page }) => {
  const name = `Trùng Hồ Sơ ${suffix()}`; const ph = phone();
  await staffLogin(page, 'assistant.demo');
  const fill = async () => {
    await page.getByRole('link', { name: 'Bệnh nhân', exact: true }).click();
    await page.getByRole('button', { name: 'Thêm hồ sơ', exact: true }).click();
    const form = page.getByRole('dialog');
    await form.getByLabel('Họ tên', { exact: true }).fill(name);
    await form.getByLabel('Ngày sinh', { exact: true }).fill('1990-05-06');
    const phoneField = form.getByLabel('Số điện thoại', { exact: true });
    if (await phoneField.count()) await phoneField.fill(ph);
    await form.getByRole('button', { name: 'Lưu', exact: true }).click();
    return form;
  };
  await fill();
  await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'Đóng hồ sơ khách hàng', exact: true }).click();
  const second = await fill();
  await page.waitForTimeout(1500);
  await page.screenshot({ path: 'test-results/sys4-03.png' });
  await expect(page.getByText(/trùng|tương tự|đã tồn tại/i).first()).toBeVisible();
  void second;
});

async function bookedAppointment(request: APIRequestContext) {
  const r0 = await request.post(`${api}/api/auth/sign-in`, { data: { userName: 'assistant.demo', password } });
  const headers = { Authorization: `Bearer ${(await r0.json()).accessToken}` };
  const options = await (await request.get(`${api}/api/reception/options`, { headers })).json();
  const branchId = options.calendar.branches[0].id;
  const dentistId = options.calendar.dentists.find((x: { name: string }) => x.name === 'Nha sĩ demo').id;
  const serviceId = options.calendar.services[0].id;
  const p = await request.post(`${api}/api/patients`, { headers, data: { fullName: `Tiếp Nhận ${randomUUID().slice(0, 6)}`, birthDate: '1995-01-01', gender: 'MALE', duplicatesReviewed: true } });
  const patient = (await p.json()).patient;
  const av = await (await request.get(`${api}/api/availability?branchId=${branchId}&dentistId=${dentistId}&serviceId=${serviceId}&date=${options.clinicDate}&durationMinutes=1`, { headers })).json();
  const slot = av.slots.find((x: { start: string }) => Date.parse(x.start) > Date.now() + 10000);
  expect(slot, 'a bookable slot must remain today').toBeTruthy();
  const booked = await request.post(`${api}/api/appointments`, { headers, data: { branchId, booker: { name: 'Người đặt', phone: '0912345678' },
    attendee: { patientId: patient.id, name: patient.fullName }, attendeeRelation: 'Relative', kind: 'WalkIn', source: 'InPerson', serviceId, dentistId,
    chairId: slot.chairId, startAt: slot.start, durationMinutes: 1, exceptionReason: 'System test', requestKey: randomUUID(),
    scheduleVersion: slot.scheduleVersion, bookingSettingsVersion: slot.bookingSettingsVersion } });
  expect(booked.ok(), await booked.text()).toBeTruthy();
  return { patient, appointment: await booked.json() };
}

test('SYS4-06 assistant checks in a patient with an appointment', async ({ page, request }) => {
  const f = await bookedAppointment(request);
  await staffLogin(page, 'assistant.demo');
  await page.goto(`/reception/appointments/${f.appointment.appointmentId}`);
  await page.locator('.ant-select').filter({ has: page.locator('#purpose') }).click();
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Enter');
  await page.getByRole('checkbox', { name: 'Đã đối chiếu đúng người đến khám với lịch và hồ sơ đã chọn' }).check();
  const resp = page.waitForResponse(r => /check-in/.test(r.url()) && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Xác nhận Check-in' }).click();
  expect((await resp).status()).toBeLessThan(300);
});

test('SYS4-07 check-in is refused when the appointment has no patient record', async ({ page, request }) => {
  const r0 = await request.post(`${api}/api/auth/sign-in`, { data: { userName: 'assistant.demo', password } });
  const staffHeaders = { Authorization: `Bearer ${(await r0.json()).accessToken}` };
  const options = await (await request.get(`${api}/api/reception/options`, { headers: staffHeaders })).json();
  const branchId = options.calendar.branches[0].id;
  const dentistId = options.calendar.dentists.find((x: { name: string }) => x.name === 'Nha sĩ demo').id;
  const serviceId = options.calendar.services[0].id;
  const av = await (await request.get(`${api}/api/availability?branchId=${branchId}&dentistId=${dentistId}&serviceId=${serviceId}&date=${options.clinicDate}&durationMinutes=1`, { headers: staffHeaders })).json();
  const slot = av.slots.find((x: { start: string }) => Date.parse(x.start) > Date.now() + 10000);
  test.skip(!slot, 'No slot left today');
  const pass = `Aa1!${randomUUID()}`; const p = phone();
  await request.post(`${api}/api/auth/register`, { data: { fullName: 'Khách Chưa Có Hồ Sơ', phoneNumber: p, email: `${randomUUID()}@example.invalid`, password: pass, confirmPassword: pass } });
  const session = await (await request.post(`${api}/api/auth/sign-in`, { data: { userName: p, password: pass } })).json();
  const b = await request.post(`${api}/api/appointments`, { headers: { Authorization: `Bearer ${session.accessToken}` }, data: { branchId, booker: { name: 'Khách Chưa Có Hồ Sơ', phone: p },
    attendee: { name: 'Khách Chưa Có Hồ Sơ' }, attendeeRelation: 'Self', kind: 'Standard', serviceId, dentistId, startAt: slot.start, durationMinutes: 30,
    scheduleVersion: slot.scheduleVersion, requestKey: randomUUID() } });
  test.skip(b.status() !== 201, 'Customer booking not possible for the remaining slot');
  const body = await b.json();
  await staffLogin(page, 'assistant.demo');
  await page.goto(`/reception/appointments/${body.appointmentId}`);
  await page.waitForTimeout(3000);
  await page.screenshot({ path: 'test-results/sys4-07.png', fullPage: true });
  const confirm = page.getByRole('button', { name: 'Xác nhận Check-in' });
  const blocked = (await confirm.count()) === 0 || (await confirm.isDisabled());
  expect(blocked).toBeTruthy();
});
