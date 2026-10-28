import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5084';
const password = process.env.E2E_DEMO_PASSWORD;
test.skip(!password, 'Requires the isolated PostgreSQL clinical fixture and E2E_DEMO_PASSWORD.');

async function auth(request: APIRequestContext, name: string) {
  const r = await request.post(`${apiUrl}/api/auth/sign-in`, { data: { userName: name, password } });
  expect(r.ok(), await r.text()).toBeTruthy(); return { Authorization: `Bearer ${(await r.json()).accessToken}` };
}
async function fixture(request: APIRequestContext, demographics: { birthDate: string | null; gender: string | null } = { birthDate: '2000-01-01', gender: 'FEMALE' }) {
  const headers = await auth(request,'assistant.demo');
  const options = await (await request.get(`${apiUrl}/api/reception/options`,{headers})).json();
  const branchId = options.calendar.branches[0].id;
  const dentistId = options.calendar.dentists.find((x: {name:string}) => x.name === 'Nha sĩ demo').id;
  const serviceId = options.calendar.services[0].id;
  const p = await request.post(`${apiUrl}/api/patients`,{headers,data:{fullName:`SYNTHETIC khám ${crypto.randomUUID().slice(0,8)}`,...demographics,duplicatesReviewed:true}});
  expect(p.ok(),await p.text()).toBeTruthy(); const patient = (await p.json()).patient;
  const availability = await (await request.get(`${apiUrl}/api/availability?branchId=${branchId}&dentistId=${dentistId}&serviceId=${serviceId}&date=${options.clinicDate}&durationMinutes=1`,{headers})).json();
  const slot = availability.slots.find((x: {start:string}) => Date.parse(x.start)>Date.now()+10000); expect(slot).toBeTruthy();
  const booked = await request.post(`${apiUrl}/api/appointments`,{headers,data:{branchId,booker:{name:'SYNTHETIC người đặt',phone:'0912345678'},
    attendee:{patientId:patient.id,name:patient.fullName},attendeeRelation:'Relative',kind:'WalkIn',source:'InPerson',serviceId,dentistId,chairId:slot.chairId,
    startAt:slot.start,durationMinutes:1,exceptionReason:'SYNTHETIC walk-in fixture',requestKey:crypto.randomUUID(),scheduleVersion:slot.scheduleVersion,bookingSettingsVersion:slot.bookingSettingsVersion}});
  expect(booked.ok(),await booked.text()).toBeTruthy(); const appointment = await booked.json();
  const checked = await request.post(`${apiUrl}/api/reception/appointments/${appointment.appointmentId}/check-in`,{headers,data:{patientId:patient.id,
    expectedVersion:appointment.version,requestKey:crypto.randomUUID(),identityConfirmed:true,purpose:'InitialExamination',note:'SYNTHETIC triệu chứng tiếp nhận'}});
  expect(checked.ok(),await checked.text()).toBeTruthy(); return {patient,visit:await checked.json(),branchId,dentistId,headers,clinicDate:options.clinicDate as string};
}

async function signIn(page:Page,name:string){await page.goto('/sign-in');await page.getByLabel('Tên đăng nhập',{exact:true}).fill(name);await page.getByLabel('Mật khẩu',{exact:true}).fill(password!);await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();await expect(page.getByRole('link',{name:'Thanh toán',exact:true})).toBeVisible();}
test('configured examination fee → completion → partial cash → transfer confirmation',async({page,request},testInfo)=>{
 const f=await fixture(request),dentist=await auth(request,'dentist.demo');
 await signIn(page,'dentist.demo');await page.goto('/clinical-services');await page.getByRole('button',{name:'Sửa giá khám',exact:true}).click();let modal=page.getByRole('dialog');await modal.getByLabel('Giá khám theo bảng',{exact:true}).fill('100000');await modal.getByRole('button',{name:'Lưu giá khám',exact:true}).click();await expect(modal).not.toBeVisible();
 let r=await request.post(`${apiUrl}/api/visits/${f.visit.visitId}/start`,{headers:dentist,data:{patientId:f.patient.id,expectedVersion:f.visit.visitVersion,requestKey:crypto.randomUUID()}});expect(r.ok(),await r.text()).toBeTruthy();const w=await r.json();
 r=await request.put(`${apiUrl}/api/visits/${f.visit.visitId}/examination`,{headers:dentist,data:{patientId:f.patient.id,expectedVersion:w.visit.visitVersion,requestKey:crypto.randomUUID(),confirm:true,content:{symptoms:'SYNTHETIC khám định kỳ',medicalHistory:{},allergies:{},medications:{},findings:[],diagnoses:['Chưa phát hiện bất thường'],advice:'SYNTHETIC vệ sinh',disposition:'ProceedToTreatment'}}});expect(r.ok(),await r.text()).toBeTruthy();
 await page.goto(`/visits/${f.visit.visitId}`);await page.getByRole('tab',{name:'Tổng kết khám',exact:true}).click();await page.getByRole('button',{name:'Kết thúc lượt khám',exact:true}).click();modal=page.getByRole('dialog');await expect(modal.getByLabel('Phí khám',{exact:true})).toHaveValue('100000');await modal.getByRole('checkbox').check();await modal.getByRole('button',{name:'Xác nhận kết thúc lượt',exact:true}).click();await expect(modal).not.toBeVisible();await expect(page.getByRole('link',{name:'Lập kế hoạch điều trị',exact:true})).toBeVisible();await page.getByRole('link',{name:'Chuyển thu ngân',exact:true}).click();await expect(page.getByText('Đã ghi phí',{exact:true})).toBeVisible();
 const cashier=await page.context().browser()!.newContext();const cp=await cashier.newPage();await signIn(cp,'assistant.demo');await cp.goto('/payments');await cp.getByRole('row').filter({hasText:f.patient.fullName}).getByRole('link',{name:'Mở thanh toán',exact:true}).click();
 const headers=await auth(request,'assistant.demo');let bill=await(await request.get(`${apiUrl}/api/finance/visits/${f.visit.visitId}`,{headers})).json();expect(bill.charges).toHaveLength(1);expect(bill.payments).toHaveLength(0);const name=bill.charges[0].name;
 await cp.getByRole('button',{name:'Thu tiền',exact:true}).click();const pay=cp.getByRole('dialog');await pay.getByLabel('Người nộp tiền',{exact:true}).fill('SYNTHETIC payer');await pay.getByLabel(`Thu ${name}`,{exact:true}).fill('30000');await pay.getByRole('button',{name:'Xác nhận đã nhận tiền mặt',exact:true}).click();await expect(pay).not.toBeVisible();
 await cp.getByRole('button',{name:'Thu tiền',exact:true}).click();await pay.getByLabel('Người nộp tiền',{exact:true}).fill('SYNTHETIC payer');await pay.getByRole('combobox',{name:'Phương thức thanh toán',exact:true}).press('ArrowDown');await cp.locator('.ant-select-dropdown:visible').getByText('Chuyển khoản',{exact:true}).click();await pay.getByRole('button',{name:'Lưu chuyển khoản chờ',exact:true}).click();await expect(pay).not.toBeVisible();
 bill=await(await request.get(`${apiUrl}/api/finance/visits/${f.visit.visitId}`,{headers})).json();expect(bill.paidVnd).toBe(30000);expect(bill.remainingVnd).toBe(70000);
 await cp.getByRole('button',{name:'Xác nhận tiền vào',exact:true}).click();await pay.getByLabel('Mã giao dịch ngân hàng',{exact:true}).fill(`EXAM-${crypto.randomUUID()}`);await pay.getByLabel('Nội dung thanh toán',{exact:true}).fill('SYNTHETIC đã đối chiếu');await pay.getByRole('button',{name:'Xác nhận tiền vào',exact:true}).click();await expect(pay).not.toBeVisible();bill=await(await request.get(`${apiUrl}/api/finance/visits/${f.visit.visitId}`,{headers})).json();expect(bill.remainingVnd).toBe(0);await cp.screenshot({path:testInfo.outputPath('examination-paid.png'),fullPage:true});await cashier.close();
});
