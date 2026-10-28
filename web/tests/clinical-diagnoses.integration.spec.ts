import { test, expect, type Page, type APIRequestContext } from '@playwright/test';
const apiUrl=process.env.E2E_API_URL??'http://localhost:5084',password=process.env.E2E_DEMO_PASSWORD;
test.skip(!password,'Requires isolated PostgreSQL fixture.');
async function auth(request:APIRequestContext,name:string){const r=await request.post(`${apiUrl}/api/auth/sign-in`,{data:{userName:name,password}});expect(r.ok(),await r.text()).toBeTruthy();return{Authorization:`Bearer ${(await r.json()).accessToken}`};}
async function signIn(page:Page,name:string){await page.goto('/sign-in');await page.getByLabel('Tên đăng nhập',{exact:true}).fill(name);await page.getByLabel('Mật khẩu',{exact:true}).fill(password!);await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();await expect(page.getByRole('button',{name:'Đăng xuất',exact:true})).toBeVisible();}
async function ok(response:Awaited<ReturnType<APIRequestContext['post']>>){expect(response.ok(),await response.text()).toBeTruthy();return response.json();}

test('examination selects a live catalog item and stores its snapshot without requiring settings access',async({page,request},testInfo)=>{
  const owner=await auth(request,'owner.demo'),assistant=await auth(request,'assistant.demo'),dentist=await auth(request,'dentist.demo');
  const name='SYNTHETIC bệnh răng '+crypto.randomUUID().slice(0,8);
  const entry=await ok(await request.post(`${apiUrl}/api/settings/catalogs/diagnoses`,{headers:owner,data:{requestKey:crypto.randomUUID(),entry:{name,code:'C-'+crypto.randomUUID().slice(0,8)}}}));
  const options=await(await request.get(`${apiUrl}/api/reception/options`,{headers:assistant})).json(),branchId=options.calendar.branches[0].id,dentistId=options.calendar.dentists.find((x:{name:string})=>x.name==='Nha sĩ demo').id,serviceId=options.calendar.services[0].id;
  const patient=(await ok(await request.post(`${apiUrl}/api/patients`,{headers:assistant,data:{fullName:'SYNTHETIC catalog exam',duplicatesReviewed:true}}))).patient;
  const availability=await(await request.get(`${apiUrl}/api/availability?branchId=${branchId}&dentistId=${dentistId}&serviceId=${serviceId}&date=${options.clinicDate}&durationMinutes=1`,{headers:assistant})).json();const slot=availability.slots.find((x:{start:string})=>Date.parse(x.start)>Date.now()+10000);expect(slot).toBeTruthy();
  const appointment=await ok(await request.post(`${apiUrl}/api/appointments`,{headers:assistant,data:{branchId,booker:{name:'SYNTHETIC người đặt',phone:'0912345678'},attendee:{patientId:patient.id,name:patient.fullName},attendeeRelation:'Relative',kind:'WalkIn',source:'InPerson',serviceId,dentistId,chairId:slot.chairId,startAt:slot.start,durationMinutes:1,exceptionReason:'SYNTHETIC test',requestKey:crypto.randomUUID(),scheduleVersion:slot.scheduleVersion,bookingSettingsVersion:slot.bookingSettingsVersion}}));
  const visit=await ok(await request.post(`${apiUrl}/api/reception/appointments/${appointment.appointmentId}/check-in`,{headers:assistant,data:{patientId:patient.id,expectedVersion:appointment.version,requestKey:crypto.randomUUID(),identityConfirmed:true,purpose:'InitialExamination'}}));
  await ok(await request.post(`${apiUrl}/api/visits/${visit.visitId}/start`,{headers:dentist,data:{patientId:patient.id,expectedVersion:visit.visitVersion,requestKey:crypto.randomUUID()}}));
  await signIn(page,'dentist.demo');await page.goto(`/visits/${visit.visitId}`);const chart=page.getByRole('region',{name:'Sơ đồ răng',exact:true});await chart.getByRole('button',{name:'Răng 16',exact:true}).click();await chart.getByRole('combobox',{name:'Chẩn đoán cho răng đang chọn',exact:true}).fill(name);
  await page.locator('.ant-select-dropdown:visible .ant-select-item-option-content').filter({hasText:name}).click();await chart.getByRole('button',{name:'Áp dụng cho 1 răng',exact:true}).click();
  const saving=page.waitForResponse(r=>r.url().endsWith(`/api/visits/${visit.visitId}/examination`)&&r.request().method()==='PUT');
  await page.getByRole('button',{name:'Lưu nháp',exact:true}).click();const savedResponse=await saving;expect(savedResponse.ok(),await savedResponse.text()).toBeTruthy();await expect(page.getByText('Phiếu nháp',{exact:true})).toBeVisible();await page.reload();await expect(page.getByRole('combobox',{name:'Chẩn đoán răng 16',exact:true})).toBeVisible();
  const saved=await(await request.get(`${apiUrl}/api/visits/${visit.visitId}/examination`,{headers:dentist})).json();expect(saved.examination.content.diagnosisReferences).toEqual([{id:entry.id,version:entry.version,name,code:entry.code}]);
  expect((await request.get(`${apiUrl}/api/settings/catalogs/diagnoses`,{headers:dentist})).status()).toBe(403);await page.screenshot({path:testInfo.outputPath('catalog-tooth-snapshot.png'),fullPage:true,animations:'disabled'});
});
