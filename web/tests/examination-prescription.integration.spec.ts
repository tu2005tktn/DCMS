import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiUrl=process.env.E2E_API_URL,password=process.env.E2E_DEMO_PASSWORD;
test.skip(!apiUrl||!password,'Requires isolated clinical PostgreSQL fixture, E2E_API_URL and E2E_DEMO_PASSWORD.');
async function auth(request:APIRequestContext,name:string){
  const response=await request.post(`${apiUrl}/api/auth/sign-in`,{data:{userName:name,password}});
  expect(response.ok(),await response.text()).toBeTruthy();return {Authorization:`Bearer ${(await response.json()).accessToken}`};
}
async function ok(response:Awaited<ReturnType<APIRequestContext['post']>>){expect(response.ok(),await response.text()).toBeTruthy();return response.json();}
async function fixture(request:APIRequestContext){
  const assistant=await auth(request,'assistant.demo'),dentist=await auth(request,'dentist.demo');
  const options=await ok(await request.get(`${apiUrl}/api/reception/options`,{headers:assistant}));
  const branchId=options.calendar.branches[0].id,dentistId=options.calendar.dentists.find((x:{name:string})=>x.name==='Nha sĩ demo').id,serviceId=options.calendar.services[0].id;
  const patient=(await ok(await request.post(`${apiUrl}/api/patients`,{headers:assistant,data:{fullName:'SYNTHETIC khám kê đơn '+crypto.randomUUID().slice(0,8),duplicatesReviewed:true,requestKey:crypto.randomUUID()}}))).patient;
  const availability=await ok(await request.get(`${apiUrl}/api/availability?branchId=${branchId}&dentistId=${dentistId}&serviceId=${serviceId}&date=${options.clinicDate}&durationMinutes=1`,{headers:assistant}));
  const slot=availability.slots.find((x:{start:string})=>Date.parse(x.start)>Date.now()+10000);expect(slot).toBeTruthy();
  const appointment=await ok(await request.post(`${apiUrl}/api/appointments`,{headers:assistant,data:{branchId,booker:{name:'SYNTHETIC người đặt',phone:'0912345678'},attendee:{patientId:patient.id,name:patient.fullName},attendeeRelation:'Relative',kind:'WalkIn',source:'InPerson',serviceId,dentistId,chairId:slot.chairId,startAt:slot.start,durationMinutes:1,exceptionReason:'SYNTHETIC isolated prescription fixture',requestKey:crypto.randomUUID(),scheduleVersion:slot.scheduleVersion,bookingSettingsVersion:slot.bookingSettingsVersion}}));
  const checked=await ok(await request.post(`${apiUrl}/api/reception/appointments/${appointment.appointmentId}/check-in`,{headers:assistant,data:{patientId:patient.id,expectedVersion:appointment.version,requestKey:crypto.randomUUID(),identityConfirmed:true,purpose:'InitialExamination'}}));
  const started=await ok(await request.post(`${apiUrl}/api/visits/${checked.visitId}/start`,{headers:dentist,data:{patientId:patient.id,expectedVersion:checked.visitVersion,requestKey:crypto.randomUUID()}}));
  const content={...started.initialContent,symptoms:'SYNTHETIC kiểm tra kết luận và đơn thuốc',medicalHistory:{},allergies:{},medications:{},findings:[],diagnoses:['Chưa phát hiện bất thường'],advice:'SYNTHETIC dặn dò đã lưu',disposition:'ConsultationOnly'};
  await ok(await request.put(`${apiUrl}/api/visits/${checked.visitId}/examination`,{headers:dentist,data:{patientId:patient.id,expectedVersion:started.visit.visitVersion,requestKey:crypto.randomUUID(),content,confirm:false}}));
  return {visitId:checked.visitId as string,patient,dentist,branchId};
}
async function signIn(page:Page,visitId:string){
  await page.goto('/sign-in');await page.getByLabel('Tên đăng nhập',{exact:true}).fill('dentist.demo');await page.getByLabel('Mật khẩu',{exact:true}).fill(password!);await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();
  await expect(page.getByRole('heading',{name:'Tài khoản của tôi',exact:true})).toBeVisible();await page.goto(`/visits/${visitId}`);await page.getByRole('tab',{name:'Tổng kết khám',exact:true}).click();
}
async function confirm(page:Page,visitId:string){
  const saved=page.waitForResponse(response=>response.url().endsWith(`/api/visits/${visitId}/examination`)&&response.request().method()==='PUT');
  await page.getByRole('button',{name:'Hoàn tất khám',exact:true}).click();expect((await saved).ok()).toBeTruthy();await expect(page.getByRole('button',{name:'Kết thúc lượt khám',exact:true})).toBeVisible();
}
async function complete(page:Page,visitId:string){
  await page.getByRole('button',{name:'Kết thúc lượt khám',exact:true}).click();const dialog=page.getByRole('dialog',{name:'Kết thúc lượt khám',exact:true});
  await dialog.getByRole('checkbox').check();const saved=page.waitForResponse(response=>response.url().endsWith(`/api/visits/${visitId}/complete`)&&response.request().method()==='POST');
  await dialog.getByRole('button',{name:'Xác nhận kết thúc lượt',exact:true}).click();expect((await saved).ok()).toBeTruthy();await expect(dialog).not.toBeVisible();
}

test('real examination confirms and completes without prescribing any medicine',async({page,request},testInfo)=>{
  const f=await fixture(request),prescriptionWrites:string[]=[];
  page.on('request',r=>{if(r.url().includes('/api/prescriptions/')&&r.method()!=='GET')prescriptionWrites.push(r.url());});
  await signIn(page,f.visitId);const panel=page.getByRole('region',{name:'Đơn thuốc khám'});
  await expect(panel.getByRole('button',{name:'Kê đơn thuốc',exact:true})).toBeVisible();await expect(panel.getByRole('combobox')).toHaveCount(0);
  await confirm(page,f.visitId);await complete(page,f.visitId);
  expect(await ok(await request.get(`${apiUrl}/api/prescriptions/visits/${f.visitId}/lines`,{headers:f.dentist}))).toEqual([]);
  expect((await ok(await request.get(`${apiUrl}/api/visits/${f.visitId}/examination`,{headers:f.dentist}))).visit.status).toBe('Completed');expect(prescriptionWrites).toHaveLength(0);
  await page.screenshot({path:testInfo.outputPath('optional-no-prescription.png'),fullPage:true});
});

test('real inline prescription saves and edits without losing conclusion then stays on the completed visit',async({page,request},testInfo)=>{
  const f=await fixture(request),owner=await auth(request,'owner.demo'),suffix=crypto.randomUUID().slice(0,8);
  const medicine=await ok(await request.post(`${apiUrl}/api/system-settings/settings/catalogs/medicines`,{headers:owner,data:{requestKey:crypto.randomUUID(),entry:{code:'RX-'+suffix,name:'SYNTHETIC thuốc '+suffix,baseUnit:'Viên',instructions:'SYNTHETIC lời dặn mẫu',prescriptionEligible:true,internalStockTracked:false}}}));
  await signIn(page,f.visitId);await page.getByLabel('Dặn dò / lời khuyên',{exact:true}).fill('SYNTHETIC kết luận đang nhập');
  const panel=page.getByRole('region',{name:'Đơn thuốc khám'});await panel.getByRole('button',{name:'Kê đơn thuốc',exact:true}).click();await panel.getByRole('combobox',{name:'Thuốc kê đơn',exact:true}).fill(suffix);
  await page.locator('.ant-select-dropdown:visible').getByText(`${medicine.code} · ${medicine.name}`,{exact:true}).click();await expect(panel.getByLabel('Lời dặn dùng thuốc',{exact:true})).toHaveValue('SYNTHETIC lời dặn mẫu');
  await panel.getByLabel('Số lượng thuốc',{exact:true}).fill('2');await panel.getByLabel('Lời dặn dùng thuốc',{exact:true}).fill('SYNTHETIC lời dặn theo lượt');
  await panel.getByRole('button',{name:'Thêm vào đơn',exact:true}).click();await expect(panel.getByText('2 Viên',{exact:true})).toBeVisible();await expect(page.getByLabel('Dặn dò / lời khuyên',{exact:true})).toHaveValue('SYNTHETIC kết luận đang nhập');
  const draft=await ok(await request.get(`${apiUrl}/api/visits/${f.visitId}/examination`,{headers:f.dentist}));expect(draft.examination.content.advice).toBe('SYNTHETIC dặn dò đã lưu');expect(draft.examination.confirmed).toBe(false);
  await panel.getByRole('button',{name:`Sửa thuốc ${medicine.name}`,exact:true}).click();await panel.getByLabel('Lời dặn dùng thuốc',{exact:true}).fill('SYNTHETIC lời dặn đã sửa');
  const updated=page.waitForResponse(response=>response.url().includes(`/api/prescriptions/visits/${f.visitId}/lines/`)&&response.request().method()==='PUT');
  await panel.getByRole('button',{name:'Lưu thay đổi thuốc',exact:true}).click();expect((await updated).ok()).toBeTruthy();await expect(panel.getByRole('cell',{name:'SYNTHETIC lời dặn đã sửa',exact:true})).toBeVisible();
  const lines=await ok(await request.get(`${apiUrl}/api/prescriptions/visits/${f.visitId}/lines`,{headers:f.dentist}));expect(lines).toHaveLength(1);expect(lines[0]).toMatchObject({visitId:f.visitId,medicineId:medicine.id,quantity:2,instructions:'SYNTHETIC lời dặn đã sửa',version:2});
  expect((await ok(await request.get(`${apiUrl}/api/system-settings/settings/catalogs/medicines/${medicine.id}`,{headers:owner}))).instructions).toBe('SYNTHETIC lời dặn mẫu');
  await confirm(page,f.visitId);await page.reload();await page.getByRole('tab',{name:'Tổng kết khám',exact:true}).click();await expect(panel.getByText('SYNTHETIC lời dặn đã sửa',{exact:true})).toBeVisible();
  await complete(page,f.visitId);await expect(panel.getByRole('combobox')).toHaveCount(0);await expect(panel.getByText('SYNTHETIC lời dặn đã sửa',{exact:true})).toBeVisible();
  for(const width of [1440,390]){await page.setViewportSize({width,height:1000});await expect.poll(()=>page.evaluate(()=>document.documentElement.scrollWidth)).toBeLessThanOrEqual(width+1);await page.screenshot({path:testInfo.outputPath(`optional-prescription-${width}.png`),fullPage:true});}
  const done=await ok(await request.get(`${apiUrl}/api/visits/${f.visitId}/examination`,{headers:f.dentist}));expect(done.visit.status).toBe('Completed');expect(done.examination.content.advice).toBe('SYNTHETIC kết luận đang nhập');
});
