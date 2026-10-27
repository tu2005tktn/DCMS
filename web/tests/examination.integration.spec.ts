import { expect, test, type APIRequestContext } from '@playwright/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5084';
const password = process.env.E2E_DEMO_PASSWORD;
test.skip(!password, 'Requires the isolated PostgreSQL clinical fixture and E2E_DEMO_PASSWORD.');

async function auth(request: APIRequestContext, name: string) {
  const r = await request.post(`${apiUrl}/api/auth/sign-in`, { data: { userName: name, password } });
  expect(r.ok(), await r.text()).toBeTruthy(); return { Authorization: `Bearer ${(await r.json()).accessToken}` };
}
async function fixture(request: APIRequestContext) {
  const headers = await auth(request,'assistant.demo');
  const options = await (await request.get(`${apiUrl}/api/reception/options`,{headers})).json();
  const branchId = options.calendar.branches[0].id;
  const dentistId = options.calendar.dentists.find((x: {name:string}) => x.name === 'Nha sĩ demo').id;
  const serviceId = options.calendar.services[0].id;
  const p = await request.post(`${apiUrl}/api/patients`,{headers,data:{fullName:`SYNTHETIC khám ${crypto.randomUUID().slice(0,8)}`,birthDate:'2000-01-01',duplicatesReviewed:true}});
  expect(p.ok(),await p.text()).toBeTruthy(); const patient = (await p.json()).patient;
  const availability = await (await request.get(`${apiUrl}/api/availability?branchId=${branchId}&dentistId=${dentistId}&serviceId=${serviceId}&date=${options.clinicDate}&durationMinutes=1`,{headers})).json();
  const slot = availability.slots.find((x: {start:string}) => Date.parse(x.start)>Date.now()+10000); expect(slot).toBeTruthy();
  const booked = await request.post(`${apiUrl}/api/appointments`,{headers,data:{branchId,booker:{name:'SYNTHETIC người đặt',phone:'0912345678'},
    attendee:{patientId:patient.id,name:patient.fullName},attendeeRelation:'Relative',kind:'WalkIn',source:'InPerson',serviceId,dentistId,chairId:slot.chairId,
    startAt:slot.start,durationMinutes:1,exceptionReason:'SYNTHETIC walk-in fixture',requestKey:crypto.randomUUID(),scheduleVersion:slot.scheduleVersion,bookingSettingsVersion:slot.bookingSettingsVersion}});
  expect(booked.ok(),await booked.text()).toBeTruthy(); const appointment = await booked.json();
  const checked = await request.post(`${apiUrl}/api/reception/appointments/${appointment.appointmentId}/check-in`,{headers,data:{patientId:patient.id,
    expectedVersion:appointment.version,requestKey:crypto.randomUUID(),identityConfirmed:true,purpose:'InitialExamination',note:'SYNTHETIC triệu chứng tiếp nhận'}});
  expect(checked.ok(),await checked.text()).toBeTruthy(); return {patient,visit:await checked.json(),branchId,dentistId,headers};
}

test('real queue to examination: draft/reopen, S20 pending upload, confirmation, counseling and old treatment',async({page,request},testInfo)=>{
  const f = await fixture(request); const dentistHeaders = await auth(request,'dentist.demo');
  const dossierResponse = await request.post(`${apiUrl}/api/patients/${f.patient.id}/dossiers`,{headers:dentistHeaders,data:{branchId:f.branchId,dentistId:f.dentistId,
    diagnosis:'SYNTHETIC hồ sơ cũ',requestKey:crypto.randomUUID()}}); expect(dossierResponse.ok(),await dossierResponse.text()).toBeTruthy();
  const dossier = (await dossierResponse.json()).dossier;
  await page.goto('/sign-in'); await page.getByLabel('Tên đăng nhập',{exact:true}).fill('dentist.demo');
  await page.getByLabel('Mật khẩu',{exact:true}).fill(password!); await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();
  await page.getByRole('link',{name:'Tiếp nhận và hàng chờ'}).click(); await page.getByRole('tab',{name:'Hàng chờ và lượt khám'}).click();
  const row = page.getByRole('row').filter({hasText:f.patient.patientCode}); await expect(row).toBeVisible(); await row.getByRole('link',{name:'Mở lượt khám'}).click();
  await expect(page).toHaveURL(new RegExp(`/visits/${f.visit.visitId}$`));
  await page.getByRole('button',{name:'Bắt đầu khám',exact:true}).click();
  await expect(page.getByLabel('Lý do khám / triệu chứng',{exact:true})).toBeEnabled();
  await page.getByLabel('Lý do khám / triệu chứng',{exact:true}).fill('SYNTHETIC đau răng 16');
  await page.getByRole('button',{name:'Răng 16',exact:true}).click();
  await page.getByRole('button',{name:'Lưu nháp',exact:true}).click();
  await expect(page.getByRole('button',{name:'Lưu nháp',exact:true})).toBeEnabled(); await page.reload();
  await expect(page.getByLabel('Lý do khám / triệu chứng',{exact:true})).toHaveValue('SYNTHETIC đau răng 16');
  await page.getByLabel('Tình trạng 1',{exact:true}).fill('SYNTHETIC kết quả buổi này');
  await page.getByLabel('Chẩn đoán',{exact:true}).fill('SYNTHETIC chẩn đoán 16');
  for(const label of ['Đã xác minh tiền sử','Đã xác minh dị ứng','Đã xác minh thuốc']) await page.getByRole('checkbox',{name:label,exact:true}).check();
  await page.getByRole('button',{name:'Lưu nháp',exact:true}).click(); await expect(page.getByRole('button',{name:'Lưu nháp',exact:true})).toBeEnabled();
  await page.getByRole('tab',{name:'Ảnh & tài liệu',exact:true}).click(); await page.getByRole('button',{name:'Tải tài liệu lên',exact:true}).click();
  const drawer = page.getByRole('dialog');
  await drawer.locator('input[type=file]').setInputFiles({name:'synthetic.pdf',mimeType:'application/pdf',buffer:Buffer.from('%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\n%%EOF')});
  await drawer.getByRole('button',{name:/Tải lên|Bắt đầu tải|Gửi/}).click();
  await expect(drawer.getByText('Đang kiểm tra an toàn',{exact:true})).toBeVisible();
  await drawer.getByRole('button',{name:'Close',exact:true}).click();
  await expect(drawer).not.toBeVisible();
  await expect(page.getByRole('row').filter({hasText:'synthetic.pdf'})).toContainText('Chưa review');
  await expect(page.getByRole('button',{name:'Xác nhận đã xem',exact:true})).toBeDisabled();
  await page.getByRole('tab',{name:'Khám & sơ đồ răng',exact:true}).click(); await page.getByRole('button',{name:'Xác nhận kết quả khám',exact:true}).click();
  await expect(page.getByText('Đã xác nhận kết quả khám',{exact:true})).toBeVisible();
  await page.getByRole('button',{name:'Chuyển sang tư vấn',exact:true}).click(); await expect(page.getByText('SYNTHETIC hồ sơ cũ',{exact:true})).toBeVisible();
  await page.getByRole('tab',{name:'Điều trị cũ',exact:true}).click(); await page.getByRole('link',{name:'Điều trị trong lượt này',exact:true}).click();
  await expect(page).toHaveURL(new RegExp(`/patients/${f.patient.id}/visits/${f.visit.visitId}/treatment\\?dossierId=${dossier.id}$`));
  await expect(page.getByText('Nguồn lượt khám hoặc xác minh liên quan chưa được kết nối.',{exact:false})).toHaveCount(0);
  const handoff = await request.get(`${apiUrl}/api/visits/${f.visit.visitId}/clinical-handoff`,{headers:dentistHeaders}); expect(handoff.ok()).toBeTruthy();
  const body = await handoff.json(); expect(body.teeth).toEqual([16]); expect(body.documents[0].fileState).toBe('PendingScan');
  await page.goto(`/visits/${f.visit.visitId}`); await expect(page.getByText('Đã xác nhận kết quả khám',{exact:true})).toBeVisible();
  await expect(page.getByLabel('Chẩn đoán',{exact:true})).toBeDisabled();
  await page.screenshot({path:testInfo.outputPath('s19-confirmed.png'),fullPage:true});
});

test('a real concurrent save keeps the browser draft and requires refreshing version',async({page,request})=>{
  const f = await fixture(request); const headers = await auth(request,'dentist.demo');
  await page.goto('/sign-in'); await page.getByLabel('Tên đăng nhập',{exact:true}).fill('dentist.demo'); await page.getByLabel('Mật khẩu',{exact:true}).fill(password!);
  await page.getByRole('button',{name:'Đăng nhập',exact:true}).click(); await expect(page.getByRole('link',{name:'Tiếp nhận và hàng chờ'})).toBeVisible(); await page.goto(`/visits/${f.visit.visitId}`);
  await page.getByRole('button',{name:'Bắt đầu khám',exact:true}).click(); await expect(page.getByLabel('Chẩn đoán',{exact:true})).toBeEnabled();
  await page.getByLabel('Chẩn đoán',{exact:true}).fill('SYNTHETIC bản nhập cần giữ');
  const latest = await (await request.get(`${apiUrl}/api/visits/${f.visit.visitId}/examination`,{headers})).json();
  const other = await request.put(`${apiUrl}/api/visits/${f.visit.visitId}/examination`,{headers,data:{patientId:f.patient.id,expectedVersion:latest.visit.visitVersion,
    requestKey:crypto.randomUUID(),content:latest.initialContent,confirm:false}}); expect(other.ok(),await other.text()).toBeTruthy();
  await page.getByRole('button',{name:'Lưu nháp',exact:true}).click(); await expect(page.getByRole('alert')).toContainText('Bản nhập vẫn còn');
  await expect(page.getByLabel('Chẩn đoán',{exact:true})).toHaveValue('SYNTHETIC bản nhập cần giữ'); await page.getByRole('button',{name:'Tải lại phiên bản',exact:true}).click();
  const saved = page.waitForResponse(r=>r.request().method()==='PUT' && r.url().endsWith(`/visits/${f.visit.visitId}/examination`));
  await page.getByRole('button',{name:'Lưu nháp',exact:true}).click(); expect((await saved).ok()).toBeTruthy(); await page.reload(); await expect(page.getByLabel('Chẩn đoán',{exact:true})).toHaveValue('SYNTHETIC bản nhập cần giữ');
});
