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

async function diagnosisField(page: Page) {
  await page.getByRole('tab',{name:'Tổng kết khám',exact:true}).click();
  return page.locator('.ant-form-item').filter({has:page.getByRole('combobox',{name:'Chẩn đoán',exact:true})});
}
async function setDiagnoses(page: Page, values: string[]) {
  const field=await diagnosisField(page); const input=field.getByRole('combobox',{name:'Chẩn đoán',exact:true});
  const remove=field.locator('.ant-select-selection-item-remove');
  for(let count=await remove.count();count>0;count--){await remove.first().click();await expect(remove).toHaveCount(count-1);}
  for(const value of values){await input.fill(value);await page.keyboard.press('Enter');await expect(field.getByTitle(value,{exact:true})).toBeVisible();}
  await page.keyboard.press('Escape');
}

test('real queue to separate examination: draft, documents, conclusion, explicit handoff and patient tab',async({page,request},testInfo)=>{
  const f = await fixture(request); const dentistHeaders = await auth(request,'dentist.demo');
  const dossierResponse = await request.post(`${apiUrl}/api/patients/${f.patient.id}/dossiers`,{headers:dentistHeaders,data:{branchId:f.branchId,dentistId:f.dentistId,
    diagnosis:'SYNTHETIC hồ sơ cũ',requestKey:crypto.randomUUID()}}); expect(dossierResponse.ok(),await dossierResponse.text()).toBeTruthy();
  const dossier = (await dossierResponse.json()).dossier;
  await page.goto('/sign-in'); await page.getByLabel('Tên đăng nhập',{exact:true}).fill('dentist.demo');
  await page.getByLabel('Mật khẩu',{exact:true}).fill(password!); await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();
  await page.getByRole('link',{name:'Tiếp nhận và hàng chờ'}).click(); await page.getByRole('tab',{name:'Hàng chờ và lượt khám'}).click();
  const row = page.getByRole('row').filter({hasText:f.patient.patientCode}); await expect(row).toBeVisible(); await row.getByRole('link',{name:'Mở lượt khám'}).click();
  await expect(page).toHaveURL(new RegExp(`/visits/${f.visit.visitId}$`));
  const patientHeader = page.getByRole('region',{name:'Thông tin bệnh nhân',exact:true});
  await expect(patientHeader.getByRole('heading',{name:`${f.patient.patientCode} · ${f.patient.fullName}`,exact:true})).toBeVisible();
  await expect(patientHeader.getByText(`${Number(f.clinicDate.slice(0,4))-2000} tuổi`,{exact:true})).toBeVisible();
  await expect(patientHeader.getByText('Nữ',{exact:true})).toBeVisible();
  await expect(patientHeader.getByText('01/01/2000',{exact:true})).toBeVisible();
  const chart = page.getByRole('region',{name:'Sơ đồ răng',exact:true});
  await expect(chart.getByRole('button',{name:/^Răng \d+$/})).toHaveCount(32);
  await expect(chart.getByRole('button',{name:'Răng 55',exact:true})).toHaveCount(0);
  await page.getByRole('button',{name:'Bắt đầu khám',exact:true}).click();
  await expect(page.getByLabel('Lý do khám / triệu chứng',{exact:true})).toBeEnabled();
  await page.getByLabel('Lý do khám / triệu chứng',{exact:true}).fill('SYNTHETIC đau răng 16');
  await chart.getByText('Răng sữa',{exact:true}).click();
  await expect(chart.getByRole('button',{name:/^Răng \d+$/})).toHaveCount(20);
  await expect(chart.getByRole('button',{name:'Răng 16',exact:true})).toHaveCount(0);
  await chart.getByRole('button',{name:'Răng 55',exact:true}).click();
  await chart.getByRole('textbox',{name:'Ghi nhanh kết quả',exact:true}).fill('SYNTHETIC kết quả răng sữa được giữ');
  await chart.getByRole('button',{name:'Áp dụng cho 1 răng',exact:true}).click();
  await chart.getByRole('button',{name:'Bỏ chọn',exact:true}).click();
  await chart.getByText('Răng vĩnh viễn',{exact:true}).click();
  await expect(chart.getByText(/Có 1 kết quả thuộc bộ răng đang ẩn/)).toBeVisible();
  await expect(page.getByLabel('Tình trạng 1',{exact:true})).toHaveValue('SYNTHETIC kết quả răng sữa được giữ');
  await page.getByRole('button',{name:'Răng 16',exact:true}).click();
  await chart.getByRole('textbox',{name:'Ghi nhanh kết quả',exact:true}).fill('SYNTHETIC kết quả nháp răng 16');
  await chart.getByRole('button',{name:'Áp dụng cho 1 răng',exact:true}).click();
  await page.getByRole('button',{name:'Lưu nháp',exact:true}).click();
  await expect(page.getByRole('button',{name:'Lưu nháp',exact:true})).toBeEnabled(); await page.reload();
  await expect(page.getByLabel('Lý do khám / triệu chứng',{exact:true})).toHaveValue('SYNTHETIC đau răng 16');
  await expect(chart.getByRole('radio',{name:'Răng hỗn hợp',exact:true})).toBeChecked();
  await expect(chart.getByRole('button',{name:/^Răng \d+$/})).toHaveCount(52);
  await expect(page.getByLabel('Tình trạng 1',{exact:true})).toHaveValue('SYNTHETIC kết quả răng sữa được giữ');
  await page.getByLabel('Tình trạng 2',{exact:true}).fill('SYNTHETIC kết quả buổi này');
  await setDiagnoses(page,['Sâu răng','Viêm lợi, nướu']);
  await page.getByRole('tab',{name:'Khám & sơ đồ răng',exact:true}).click();
  await expect(page.getByRole('checkbox',{name:/Đã xác minh/})).toHaveCount(0);
  await expect(page.getByRole('combobox',{name:/Tình trạng tiền sử|Tình trạng dị ứng|Tình trạng thuốc/})).toHaveCount(0);
  await page.getByLabel('Tiền sử',{exact:true}).fill('SYNTHETIC ghi chú tiền sử');
  await page.getByLabel('Thuốc đang dùng',{exact:true}).fill('SYNTHETIC thuốc đang dùng');
  await page.getByRole('button',{name:'Lưu nháp',exact:true}).click(); await expect(page.getByRole('button',{name:'Lưu nháp',exact:true})).toBeEnabled();
  await page.reload();
  const restoredDiagnoses = await diagnosisField(page);
  await expect(restoredDiagnoses.getByTitle('Sâu răng',{exact:true})).toBeVisible();
  await expect(restoredDiagnoses.getByTitle('Viêm lợi, nướu',{exact:true})).toBeVisible();
  await page.getByRole('tab',{name:'Ảnh & tài liệu',exact:true}).click(); await page.getByRole('button',{name:'Tải tài liệu lên',exact:true}).click();
  const drawer = page.getByRole('dialog');
  await drawer.locator('input[type=file]').setInputFiles({name:'synthetic.pdf',mimeType:'application/pdf',buffer:Buffer.from('%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\n%%EOF')});
  await drawer.getByRole('button',{name:/Tải lên|Bắt đầu tải|Gửi/}).click();
  await expect(drawer.getByText('Đang kiểm tra an toàn',{exact:true})).toBeVisible();
  await drawer.getByRole('button',{name:'Close',exact:true}).click();
  await expect(drawer).not.toBeVisible();
  await expect(page.getByRole('row').filter({hasText:'synthetic.pdf'})).toContainText('Chưa review');
  await expect(page.getByRole('button',{name:'Xác nhận đã xem',exact:true})).toBeDisabled();
  await page.getByRole('tab',{name:'Tổng kết khám',exact:true}).click();
  await expect(page.getByLabel('Kết luận khám',{exact:true})).toHaveCount(0);
  await page.screenshot({path:testInfo.outputPath('multiple-diagnoses-summary.png'),fullPage:true});
  await page.getByLabel('Dặn dò / lời khuyên',{exact:true}).fill('SYNTHETIC hướng dẫn vệ sinh');
  await page.getByRole('combobox',{name:'Hướng xử trí',exact:true}).click(); await page.getByText('Tiếp tục điều trị',{exact:true}).click();
  await page.getByRole('button',{name:'Hoàn tất khám',exact:true}).click();
  await expect(page.getByText('Phiếu khám đã hoàn tất. Kết thúc lượt khám trước khi lập kế hoạch điều trị.',{exact:false})).toBeVisible();
  await expect(page.getByRole('tab',{name:'Tư vấn',exact:true})).toHaveCount(0);
  await expect(page.getByRole('tab',{name:'Điều trị cũ',exact:true})).toHaveCount(0);
  await page.getByRole('button',{name:'Kết thúc lượt khám',exact:true}).click(); const ending=page.getByRole('dialog');await ending.getByRole('checkbox').check();await ending.getByRole('button',{name:'Xác nhận kết thúc lượt',exact:true}).click();await expect(ending).not.toBeVisible();
  const dossiersLoaded = page.waitForResponse(response => response.request().method() === 'GET' && new URL(response.url()).pathname === `/api/patients/${f.patient.id}/dossiers`, { timeout: 20_000 });
  await page.getByRole('link',{name:'Lập kế hoạch điều trị',exact:true}).click();
  expect((await dossiersLoaded).ok()).toBe(true);
  await expect(page).toHaveURL(new RegExp(`/patients/${f.patient.id}\\?tab=treatment&sourceExaminationVisitId=${f.visit.visitId}&examinationVersion=3$`));
  await expect(page.getByText('SYNTHETIC hồ sơ cũ',{exact:true})).toBeVisible();
  await page.getByRole('link',{name:'Mở hồ sơ',exact:true}).click();
  await expect(page).toHaveURL(new RegExp(`/patients/${f.patient.id}/dossiers/${dossier.id}$`));
  const handoff = await request.get(`${apiUrl}/api/visits/${f.visit.visitId}/clinical-handoff`,{headers:dentistHeaders}); expect(handoff.ok()).toBeTruthy();
  const body = await handoff.json(); expect(body.examination.diagnoses).toEqual(['Sâu răng','Viêm lợi, nướu']); expect(body.examination.conclusion).toBe('Sâu răng; Viêm lợi, nướu'); expect(body.teeth.sort((a:number,b:number)=>a-b)).toEqual([16,55]); expect(body.documents[0].fileState).toBe('PendingScan');
  await page.goto(`/visits/${f.visit.visitId}`); await expect(page.getByText('Đã hoàn tất khám',{exact:true}).first()).toBeVisible();
  await diagnosisField(page);
  await expect(page.getByRole('combobox',{name:'Chẩn đoán',exact:true})).toBeDisabled();
  await page.getByRole('tab',{name:'Khám & sơ đồ răng',exact:true}).click();
  await expect(page.getByLabel('Tiền sử',{exact:true})).toHaveValue('SYNTHETIC ghi chú tiền sử');
  await expect(page.getByLabel('Dị ứng',{exact:true})).toHaveValue('');
  await expect(page.getByLabel('Thuốc đang dùng',{exact:true})).toHaveValue('SYNTHETIC thuốc đang dùng');
  await chart.getByText('Răng sữa',{exact:true}).click();
  await expect(chart.getByRole('button',{name:'Răng 55',exact:true})).toBeDisabled();
  await expect(chart.getByRole('button',{name:/^Răng \d+$/})).toHaveCount(20);
  await chart.getByText('Răng vĩnh viễn',{exact:true}).click();
  await expect(chart.getByRole('button',{name:/^Răng \d+$/})).toHaveCount(32);
  await chart.getByRole('button',{name:'Xem cả hai bộ răng',exact:true}).click();
  await expect(chart.getByRole('button',{name:/^Răng \d+$/})).toHaveCount(52);
  await chart.getByText('Răng vĩnh viễn',{exact:true}).click();
  await page.screenshot({path:testInfo.outputPath('s19-confirmed.png'),fullPage:true});
  await page.getByRole('link',{name:'Các phiếu khám',exact:true}).click();
  await expect(page.getByRole('tab',{name:'Khám',exact:true})).toHaveAttribute('aria-selected','true');
  await expect(page.getByRole('region',{name:'Phiếu khám của bệnh nhân'}).getByRole('row').filter({hasText:'Sâu răng'})).toContainText('Đã hoàn tất khám');
});

test('a real concurrent save keeps the browser draft and requires refreshing version',async({page,request})=>{
  const f=await fixture(request,{birthDate:null,gender:null}),headers=await auth(request,'dentist.demo');
  await page.goto('/sign-in');await page.getByLabel('Tên đăng nhập',{exact:true}).fill('dentist.demo');await page.getByLabel('Mật khẩu',{exact:true}).fill(password!);
  await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();await expect(page.getByRole('link',{name:'Tiếp nhận và hàng chờ'})).toBeVisible();await page.goto(`/visits/${f.visit.visitId}`);
  await expect(page.getByRole('region',{name:'Thông tin bệnh nhân',exact:true}).getByText('Chưa rõ',{exact:true})).toHaveCount(3);
  await page.getByRole('button',{name:'Bắt đầu khám',exact:true}).click();
  await setDiagnoses(page,['SYNTHETIC bản nhập cần giữ','Sâu răng']);
  await page.getByLabel('Dặn dò / lời khuyên',{exact:true}).fill('SYNTHETIC lời khuyên cần giữ');
  await page.getByRole('button',{name:'Hoàn tất khám',exact:true}).click();
  await expect(page.getByRole('alert')).toContainText('Ít nhất một kết quả khám răng hoặc vùng');
  await expect(page.getByRole('tab',{name:'Tổng kết khám',exact:true})).toHaveAttribute('aria-selected','true');
  await page.getByRole('button',{name:'Bổ sung: Ít nhất một kết quả khám răng hoặc vùng',exact:true}).click();
  await expect(page.getByRole('tab',{name:'Khám & sơ đồ răng',exact:true})).toHaveAttribute('aria-selected','true');
  await expect(page.getByLabel('Kết quả khám tổng quát',{exact:true})).toBeFocused();
  const field=await diagnosisField(page);await expect(field.getByTitle('SYNTHETIC bản nhập cần giữ',{exact:true})).toBeVisible();
  await expect(page.getByLabel('Dặn dò / lời khuyên',{exact:true})).toHaveValue('SYNTHETIC lời khuyên cần giữ');
  await page.getByRole('tab',{name:'Khám & sơ đồ răng',exact:true}).click();
  const latest=await(await request.get(`${apiUrl}/api/visits/${f.visit.visitId}/examination`,{headers})).json();
  const other=await request.put(`${apiUrl}/api/visits/${f.visit.visitId}/examination`,{headers,data:{patientId:f.patient.id,expectedVersion:latest.visit.visitVersion,
    requestKey:crypto.randomUUID(),content:latest.initialContent,confirm:false}});expect(other.ok(),await other.text()).toBeTruthy();
  await page.getByRole('button',{name:'Lưu nháp',exact:true}).click();await expect(page.getByRole('alert')).toContainText('Bản nhập vẫn còn');
  await page.getByRole('button',{name:'Tải lại phiên bản',exact:true}).click();
  const saved=page.waitForResponse(r=>r.request().method()==='PUT'&&r.url().endsWith(`/visits/${f.visit.visitId}/examination`));
  await page.getByRole('button',{name:'Lưu nháp',exact:true}).click();expect((await saved).ok()).toBeTruthy();await page.reload();
  const restored=await diagnosisField(page);await expect(restored.getByTitle('SYNTHETIC bản nhập cần giữ',{exact:true})).toBeVisible();await expect(restored.getByTitle('Sâu răng',{exact:true})).toBeVisible();
});

test('consultation-only exam can be amended and explicitly ended without a treatment dossier',async({page,request},testInfo)=>{
  const f=await fixture(request); const headers=await auth(request,'dentist.demo');
  const startedResponse=await request.post(`${apiUrl}/api/visits/${f.visit.visitId}/start`,{headers,data:{patientId:f.patient.id,expectedVersion:f.visit.visitVersion,requestKey:crypto.randomUUID()}});
  expect(startedResponse.ok(),await startedResponse.text()).toBeTruthy(); const started=await startedResponse.json();
  const content={...started.initialContent,symptoms:'SYNTHETIC khám tư vấn',diagnosis:'SYNTHETIC tư vấn',findings:[{region:'Toàn hàm',condition:'SYNTHETIC kiểm tra'}],
    medicalHistory:{detail:null},allergies:{detail:null},medications:{detail:null},
    conclusion:'SYNTHETIC kết luận ban đầu',advice:'SYNTHETIC dặn dò',disposition:'ConsultationOnly'};
  const confirmed=await request.put(`${apiUrl}/api/visits/${f.visit.visitId}/examination`,{headers,data:{patientId:f.patient.id,expectedVersion:started.visit.visitVersion,requestKey:crypto.randomUUID(),content,confirm:true}});
  expect(confirmed.ok(),await confirmed.text()).toBeTruthy();
  const treatmentRequests:string[]=[]; page.on('request',r=>{if(/\/api\/(dossiers|patients\/\d+\/dossiers|clinical-activities)/.test(r.url()))treatmentRequests.push(r.url());});
  await page.goto('/sign-in'); await page.getByLabel('Tên đăng nhập',{exact:true}).fill('dentist.demo');await page.getByLabel('Mật khẩu',{exact:true}).fill(password!);
  await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();await expect(page.getByRole('link',{name:'Tiếp nhận và hàng chờ'})).toBeVisible();
  await page.goto(`/visits/${f.visit.visitId}`);await page.getByRole('tab',{name:'Tổng kết khám',exact:true}).click();
  await page.getByRole('button',{name:'Đính chính kết quả',exact:true}).click();await setDiagnoses(page,['SYNTHETIC kết luận đính chính','Viêm nha chu']);
  await page.getByLabel('Lý do đính chính',{exact:true}).fill('SYNTHETIC bổ sung đánh giá');await page.getByRole('button',{name:'Lưu đính chính',exact:true}).click();
  await expect(page.getByRole('button',{name:'Đính chính kết quả',exact:true})).toBeVisible();
  await page.getByRole('tab',{name:'Lịch sử khám',exact:true}).click();await expect(page.getByText('SYNTHETIC kết luận ban đầu',{exact:true})).toBeVisible();
  await expect(page.getByRole('tabpanel',{name:'Lịch sử khám',exact:true}).getByText('SYNTHETIC kết luận đính chính; Viêm nha chu',{exact:true})).toBeVisible();
  await page.getByRole('tab',{name:'Tổng kết khám',exact:true}).click();await page.getByRole('button',{name:'Kết thúc lượt khám',exact:true}).click();
  const dialog=page.getByRole('dialog');await expect(dialog.getByRole('button',{name:'Xác nhận kết thúc lượt',exact:true})).toBeDisabled();
  await dialog.getByRole('checkbox').check();await dialog.getByRole('button',{name:'Xác nhận kết thúc lượt',exact:true}).click();
  await expect(dialog).not.toBeVisible();await expect(page.getByRole('link',{name:'Lập kế hoạch điều trị',exact:true})).toHaveCount(0);
  await expect(page.getByRole('region',{name:'Thông tin bệnh nhân'})).toContainText('Hoàn tất');
  await page.screenshot({path:testInfo.outputPath('s19-consultation-completed.png'),fullPage:true});
  const workspace=await(await request.get(`${apiUrl}/api/visits/${f.visit.visitId}/examination`,{headers})).json();expect(workspace.visit.status).toBe('Completed');
  const dossiers=await(await request.get(`${apiUrl}/api/patients/${f.patient.id}/dossiers`,{headers})).json();expect(dossiers.items).toHaveLength(0);
  expect(treatmentRequests).toHaveLength(0);
});

test('visual odontogram shares batch, surfaces and real 3D selection with persisted findings', async ({page,request},testInfo) => {
  test.setTimeout(120_000);
  const f = await fixture(request); const headers = await auth(request,'dentist.demo');
  await page.goto('/sign-in'); await page.getByLabel('Tên đăng nhập',{exact:true}).fill('dentist.demo');
  await page.getByLabel('Mật khẩu',{exact:true}).fill(password!); await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();
  await expect(page.getByRole('link',{name:'Tiếp nhận và hàng chờ'})).toBeVisible();
  await page.goto(`/visits/${f.visit.visitId}`); await page.getByRole('button',{name:'Bắt đầu khám',exact:true}).click();
  await page.getByLabel('Kết quả khám tổng quát',{exact:true}).fill('SYNTHETIC kết quả toàn hàm');
  const chart = page.getByRole('region',{name:'Sơ đồ răng',exact:true});
  await expect(chart.getByRole('button',{name:/Chọn nhiều răng|Chọn từng răng/})).toHaveCount(0);
  await chart.getByRole('button',{name:'Răng 16',exact:true}).click(); await chart.getByRole('button',{name:'Răng 26',exact:true}).click();
  await expect(chart.getByRole('button',{name:'Răng 16',exact:true})).toHaveAttribute('aria-pressed','true');
  await chart.getByRole('button',{name:'Răng 16',exact:true}).click();
  await expect(chart.getByRole('button',{name:'Răng 16',exact:true})).toHaveAttribute('aria-pressed','false');
  await expect(chart.getByRole('button',{name:'Răng 26',exact:true})).toHaveAttribute('aria-pressed','true');
  await expect(page.getByLabel(/^Tình trạng \d+$/)).toHaveCount(0);
  await chart.getByRole('button',{name:'Răng 16',exact:true}).focus();
  await page.keyboard.press('Space');
  await expect(chart.getByRole('button',{name:'Răng 16',exact:true})).toHaveAttribute('aria-pressed','true');
  await chart.getByRole('textbox',{name:'Ghi nhanh kết quả',exact:true}).fill('Sâu răng');
  await chart.getByRole('combobox',{name:'Bề mặt răng ghi nhanh',exact:true}).click();
  await page.locator('.ant-select-dropdown:visible').getByText('Nhai / cắn',{exact:true}).click(); await page.keyboard.press('Escape');
  await chart.getByRole('button',{name:'Áp dụng cho 2 răng',exact:true}).click();
  await chart.getByRole('textbox',{name:'Ghi nhanh kết quả',exact:true}).fill('Sâu răng');
  await chart.getByRole('button',{name:'Áp dụng cho 2 răng',exact:true}).click();
  await expect(page.getByLabel(/^Tình trạng \d+$/)).toHaveCount(2);
  await chart.getByRole('textbox',{name:'Ghi nhanh kết quả',exact:true}).fill('Răng bị mẻ, vỡ');
  await chart.getByRole('button',{name:'Áp dụng cho 2 răng',exact:true}).click();
  await expect(page.getByLabel(/^Tình trạng \d+$/)).toHaveCount(4);
  await chart.screenshot({path:testInfo.outputPath('odontogram-2d.png')});
  await chart.getByText('Mô hình 3D',{exact:true}).click();
  const canvas = chart.locator('canvas'); await expect(canvas).toBeVisible();
  const hasWebgl = await canvas.evaluate(el => !!((el as HTMLCanvasElement).getContext('webgl2'))); expect(hasWebgl).toBe(true);
  await expect(chart.locator('.tooth-model-loading')).toBeHidden();
  await expect(chart.getByRole('combobox',{name:'Răng xem chi tiết',exact:true})).toHaveCount(0);
  // Tooth clicks toggle selection; the contextual inspect action must preserve that selection.
  for (const [tooth, template] of [[11,21],[14,24],[16,26],[36,36]]) {
    const detailLoaded=page.waitForResponse(response=>response.url().endsWith(`/models/dental/detail/${template}.glb`));
    await chart.getByRole('button',{name:`Răng ${tooth}`,exact:true}).click();
    const label = chart.getByRole('group',{name:`Tùy chọn răng ${tooth}`,exact:true});
    await expect(label).toBeVisible();
    const selection16 = await chart.getByRole('button',{name:'Răng 16',exact:true}).getAttribute('aria-pressed');
    const selection26 = await chart.getByRole('button',{name:'Răng 26',exact:true}).getAttribute('aria-pressed');
    await label.getByRole('button',{name:`Xem riêng răng ${tooth}`,exact:true}).click();
    expect((await detailLoaded).ok()).toBe(true);
    await expect(chart.locator('.tooth-model-loading')).toBeHidden();
    await expect(chart.getByText(`Răng ${tooth} · Đang xem riêng thân và chân răng`,{exact:true})).toBeVisible();
    await expect(chart.getByRole('button',{name:'Răng 16',exact:true})).toHaveAttribute('aria-pressed',selection16!);
    await expect(chart.getByRole('button',{name:'Răng 26',exact:true})).toHaveAttribute('aria-pressed',selection26!);
    await page.mouse.move(0,0);
    await chart.locator('.tooth-model-stage').screenshot({path:testInfo.outputPath(`tooth-detail-${tooth}.png`)});
    if (tooth === 16) {
      await chart.getByRole('button',{name:'Mặt nhai / cắn',exact:true}).click();
      await page.mouse.move(0,0);
      await chart.locator('.tooth-model-stage').screenshot({path:testInfo.outputPath('tooth-detail-16-occlusal.png')});
      await chart.getByRole('button',{name:'Mặt bên',exact:true}).click();
      await chart.getByRole('button',{name:'Chính diện',exact:true}).click();
    }
    await chart.getByRole('button',{name:'Xem toàn hàm',exact:true}).click();
    await expect(chart.locator('.tooth-model-loading')).toBeHidden();
    await chart.getByRole('button',{name:`Răng ${tooth}`,exact:true}).click();
    await chart.getByRole('button',{name:'Đóng nhãn răng',exact:true}).click();
  }
  await chart.getByText('Răng sữa',{exact:true}).click();
  await expect(chart.locator('.tooth-model-loading')).toBeHidden();
  await expect(chart.getByRole('button',{name:/^Răng \d+$/})).toHaveCount(20);
  await chart.getByRole('button',{name:'Răng 55',exact:true}).click();
  await chart.getByRole('button',{name:'Xem riêng răng 55',exact:true}).click();
  await expect(chart.locator('.tooth-model-loading')).toBeHidden();
  await expect(chart.getByText('Răng 55 · Đang xem riêng thân và chân răng',{exact:true})).toBeVisible();
  await chart.locator('.tooth-model-stage').screenshot({path:testInfo.outputPath('tooth-detail-55.png')});
  await chart.getByRole('button',{name:'Xem toàn hàm',exact:true}).click();
  await chart.getByText('Răng hỗn hợp',{exact:true}).click();
  await expect(chart.locator('.tooth-model-loading')).toBeHidden();
  await expect(chart.getByRole('button',{name:/^Răng \d+$/})).toHaveCount(52);
  await chart.getByText('Răng vĩnh viễn',{exact:true}).click();
  await expect(chart.locator('.tooth-model-loading')).toBeHidden();


  await chart.getByRole('button',{name:'Bỏ chọn',exact:true}).click();
  await canvas.scrollIntoViewIfNeeded();
  const box = (await canvas.boundingBox())!;
  // Locate an actual raycast hit through the visible hover status, then click that mesh.
  let hit: {x:number;y:number;tooth:number}|undefined;
  for(let y=.25;y<.8 && !hit;y+=.07) for(let x=.15;x<.9 && !hit;x+=.045) {
    await page.mouse.move(box.x+box.width*x,box.y+box.height*y);
    const text = await chart.locator('.model-controls [role=status]').innerText();
    const match = /^Răng (\d+)$/.exec(text); if(match) hit={x:box.x+box.width*x,y:box.y+box.height*y,tooth:Number(match[1])};
  }
  expect(hit).toBeTruthy(); await page.mouse.click(hit!.x,hit!.y);
  await expect(chart.getByRole('group',{name:`Tùy chọn răng ${hit!.tooth}`,exact:true})).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(chart.getByRole('group',{name:`Tùy chọn răng ${hit!.tooth}`,exact:true})).toHaveCount(0);
  await expect(chart.getByRole('button',{name:`Răng ${hit!.tooth}`,exact:true})).toHaveAttribute('aria-pressed','true');
  await page.mouse.click(hit!.x,hit!.y);
  await expect(chart.getByRole('button',{name:`Răng ${hit!.tooth}`,exact:true})).toHaveAttribute('aria-pressed','false');
  await page.mouse.click(hit!.x,hit!.y);
  await expect(chart.getByRole('button',{name:`Răng ${hit!.tooth}`,exact:true})).toHaveAttribute('aria-pressed','true');
  const contextLabel = chart.getByRole('group',{name:`Tùy chọn răng ${hit!.tooth}`,exact:true});
  await expect(contextLabel).toBeVisible();
  await chart.locator('.tooth-model-stage').screenshot({path:testInfo.outputPath('tooth-context-label.png')});
  await contextLabel.getByRole('button',{name:`Xem riêng răng ${hit!.tooth}`,exact:true}).click();
  await expect(chart.locator('.tooth-model-loading')).toBeHidden();
  await expect(chart.getByRole('button',{name:`Răng ${hit!.tooth}`,exact:true})).toHaveAttribute('aria-pressed','true');
  await chart.getByRole('button',{name:'Xem toàn hàm',exact:true}).click();
  await expect(chart.locator('.tooth-model-loading')).toBeHidden();
  await chart.getByRole('textbox',{name:'Ghi nhanh kết quả',exact:true}).fill('SYNTHETIC chọn mesh 3D');
  await chart.getByRole('button',{name:'Áp dụng cho 1 răng',exact:true}).click();
  await page.mouse.move(box.x+box.width/2,box.y+box.height/2); await page.mouse.down(); await page.mouse.move(box.x+box.width/2+90,box.y+box.height/2+30,{steps:10}); await page.mouse.up();
  await chart.getByRole('button',{name:'Đặt lại góc nhìn',exact:true}).click();
  await chart.screenshot({path:testInfo.outputPath('odontogram-3d.png')});
  await canvas.evaluate(el => { const gl=(el as HTMLCanvasElement).getContext('webgl2'); gl?.getExtension('WEBGL_lose_context')?.loseContext(); });
  await expect(chart.getByText('Mô hình 3D bị gián đoạn. Tiếp tục ghi khám bằng sơ đồ 2D.')).toBeVisible();
  await chart.getByRole('button',{name:'Về sơ đồ 2D',exact:true}).click();
  await expect(canvas).toHaveCount(0);
  await page.getByRole('button',{name:'Lưu nháp',exact:true}).click(); await expect(page.getByRole('button',{name:'Lưu nháp',exact:true})).toBeEnabled();
  await page.reload(); await expect(page.getByLabel('Kết quả khám tổng quát',{exact:true})).toHaveValue('SYNTHETIC kết quả toàn hàm');
  const saved = await (await request.get(`${apiUrl}/api/visits/${f.visit.visitId}/examination`,{headers})).json();
  const findings = saved.examination.content.findings;
  expect(findings.filter((finding:{condition:string})=>finding.condition==='Sâu răng')).toHaveLength(2);
  expect(findings.find((finding:{tooth:number;condition:string})=>finding.tooth===16 && finding.condition==='Sâu răng').surfaces).toEqual(['O']);
  expect(findings.find((finding:{condition:string})=>finding.condition==='SYNTHETIC chọn mesh 3D').tooth).toBe(hit!.tooth);
  await expect(page.getByLabel(/^Tình trạng \d+$/)).toHaveCount(5);
  await page.route('**/models/dental/arch/*.glb',route=>route.abort());
  await page.reload(); await chart.getByText('Mô hình 3D',{exact:true}).click();
  await expect(chart.getByText('Không tải được mô hình răng. Bạn có thể tiếp tục bằng sơ đồ 2D.')).toBeVisible();
  await chart.getByRole('button',{name:'Về sơ đồ 2D',exact:true}).click();
  await page.unroute('**/models/dental/arch/*.glb');
  // Simulate a device without WebGL; clinical APIs still run against PostgreSQL.
  await page.addInitScript(() => {
    HTMLCanvasElement.prototype.getContext = new Proxy(HTMLCanvasElement.prototype.getContext, {
      apply(target, receiver, args) { return String(args[0]).startsWith('webgl') ? null : Reflect.apply(target, receiver, args); },
    });
  });
  await page.reload(); await chart.getByText('Mô hình 3D',{exact:true}).click();
  await expect(chart.getByText('Thiết bị chưa hỗ trợ WebGL. Tiếp tục ghi khám bằng sơ đồ 2D.')).toBeVisible();
  await chart.getByRole('button',{name:'Về sơ đồ 2D',exact:true}).click();
  await expect(page.getByLabel('Kết quả khám tổng quát',{exact:true})).toHaveValue('SYNTHETIC kết quả toàn hàm');
});

test('whole-mouth examination completes without any selected tooth and remains read-only',async({page,request})=>{
  const f=await fixture(request);
  await page.goto('/sign-in'); await page.getByLabel('Tên đăng nhập',{exact:true}).fill('dentist.demo'); await page.getByLabel('Mật khẩu',{exact:true}).fill(password!);
  await page.getByRole('button',{name:'Đăng nhập',exact:true}).click(); await expect(page.getByRole('link',{name:'Tiếp nhận và hàng chờ'})).toBeVisible();
  await page.goto(`/visits/${f.visit.visitId}`); await page.getByRole('button',{name:'Bắt đầu khám',exact:true}).click();
  await page.getByLabel('Lý do khám / triệu chứng',{exact:true}).fill('SYNTHETIC khám định kỳ');
  await setDiagnoses(page,['Chưa phát hiện bất thường']);
  await page.getByRole('tab',{name:'Tổng kết khám',exact:true}).click();
  await expect(page.getByLabel('Kết luận khám',{exact:true})).toHaveCount(0); await page.getByLabel('Dặn dò / lời khuyên',{exact:true}).fill('SYNTHETIC hướng dẫn chăm sóc');
  await page.getByRole('combobox',{name:'Hướng xử trí',exact:true}).click(); await page.locator('.ant-select-dropdown:visible').getByText('Tư vấn, chưa điều trị',{exact:true}).click();
  await page.getByRole('button',{name:'Hoàn tất khám',exact:true}).click(); await expect(page.getByText('Phiếu khám đã hoàn tất. Kết thúc lượt khám trước khi lập kế hoạch điều trị.',{exact:false})).toBeVisible();
  await page.getByRole('tab',{name:'Khám & sơ đồ răng',exact:true}).click();
  await expect(page.getByLabel('Kết quả khám tổng quát',{exact:true})).toBeDisabled();
  const chart=page.getByRole('region',{name:'Sơ đồ răng',exact:true});
  await expect(chart.getByRole('button',{name:'Răng 16',exact:true})).toBeDisabled();
  await chart.getByText('Mô hình 3D',{exact:true}).click(); await expect(chart.locator('canvas')).toBeVisible();
  await expect(chart.locator('.tooth-model-loading')).toBeHidden();
  await expect(chart.getByRole('combobox',{name:'Răng xem chi tiết',exact:true})).toHaveCount(0);
  await chart.locator('canvas').focus(); await page.keyboard.press('ArrowRight');
  const label = chart.getByRole('group',{name:'Tùy chọn răng 17',exact:true});
  await expect(label).toBeVisible();
  await label.getByRole('button',{name:'Xem riêng răng 17',exact:true}).click();
  await expect(chart.locator('.tooth-model-loading')).toBeHidden();
  await expect(chart.getByText('Răng 17 · Đang xem riêng thân và chân răng',{exact:true})).toBeVisible();
  await expect(chart.getByRole('button',{name:'Đặt lại góc nhìn',exact:true})).toBeEnabled();
  await expect(chart.getByRole('textbox',{name:'Ghi nhanh kết quả',exact:true})).toHaveCount(0);
  const headers=await auth(request,'dentist.demo');
  const reopened=await(await request.get(`${apiUrl}/api/visits/${f.visit.visitId}/examination`,{headers})).json();
  expect(reopened.examination.content.diagnoses).toEqual(['Chưa phát hiện bất thường']);
  expect(reopened.examination.content.conclusion).toBe('Chưa phát hiện bất thường');
  expect(reopened.examination.content.findings).toEqual([]);
});


test('diagnosis choices can be removed and a blank final diagnosis stays on summary',async({page,request})=>{
  const f=await fixture(request),headers=await auth(request,'dentist.demo');
  const start=await request.post(`${apiUrl}/api/visits/${f.visit.visitId}/start`,{headers,data:{patientId:f.patient.id,expectedVersion:f.visit.visitVersion,requestKey:crypto.randomUUID()}});
  expect(start.ok(),await start.text()).toBeTruthy(); const workspace=await start.json();
  const content={...workspace.initialContent,symptoms:'SYNTHETIC khám kiểm tra',findings:[{region:'Toàn hàm',condition:'SYNTHETIC kết quả'}],
    diagnoses:[],diagnosis:'SYNTHETIC không được phục hồi',conclusion:'SYNTHETIC không được phục hồi',advice:'SYNTHETIC hướng dẫn',disposition:'ConsultationOnly'};
  const response=await request.put(`${apiUrl}/api/visits/${f.visit.visitId}/examination`,{headers,data:{patientId:f.patient.id,expectedVersion:workspace.visit.visitVersion,requestKey:crypto.randomUUID(),content,confirm:false}});
  expect(response.ok(),await response.text()).toBeTruthy();const draft=await response.json();
  await page.goto('/sign-in');await page.getByLabel('Tên đăng nhập',{exact:true}).fill('dentist.demo');await page.getByLabel('Mật khẩu',{exact:true}).fill(password!);
  await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();await expect(page.getByRole('link',{name:'Tiếp nhận và hàng chờ'})).toBeVisible();await page.goto(`/visits/${f.visit.visitId}`);
  await setDiagnoses(page,['Sâu răng','Viêm nha chu']);
  const field=await diagnosisField(page);
  await field.getByTitle('Viêm nha chu',{exact:true}).locator('.ant-select-selection-item-remove').click();
  await expect(field.getByTitle('Sâu răng',{exact:true})).toBeVisible();await expect(field.getByTitle('Viêm nha chu',{exact:true})).toHaveCount(0);
  await setDiagnoses(page,[]);await page.getByRole('button',{name:'Hoàn tất khám',exact:true}).click();
  await expect(page.getByRole('alert')).toContainText('Chẩn đoán / Kết luận cuối cùng');
  await expect(page.getByRole('tab',{name:'Tổng kết khám',exact:true})).toHaveAttribute('aria-selected','true');
  await expect(field.locator('.ant-select-selection-item')).toHaveCount(0);await expect(page.getByLabel('Kết luận khám',{exact:true})).toHaveCount(0);
  const reopened=await(await request.get(`${apiUrl}/api/visits/${f.visit.visitId}/examination`,{headers})).json();expect(reopened.visit.visitVersion).toBe(draft.visit.visitVersion);
});


test('incomplete tooth finding stays on summary until explicitly opening the missing field',async({page,request},testInfo)=>{
  const f=await fixture(request),headers=await auth(request,'dentist.demo');
  const start=await request.post(`${apiUrl}/api/visits/${f.visit.visitId}/start`,{headers,data:{patientId:f.patient.id,expectedVersion:f.visit.visitVersion,requestKey:crypto.randomUUID()}});
  expect(start.ok(),await start.text()).toBeTruthy();const started=await start.json();
  const content={...started.initialContent,symptoms:'SYNTHETIC đau răng',findings:[{tooth:16,condition:''}],diagnoses:['Sâu răng'],advice:'SYNTHETIC hướng dẫn',disposition:'ConsultationOnly'};
  const response=await request.put(`${apiUrl}/api/visits/${f.visit.visitId}/examination`,{headers,data:{patientId:f.patient.id,expectedVersion:started.visit.visitVersion,requestKey:crypto.randomUUID(),content,confirm:false}});
  expect(response.ok(),await response.text()).toBeTruthy();const draft=await response.json();
  await page.goto('/sign-in');await page.getByLabel('Tên đăng nhập',{exact:true}).fill('dentist.demo');await page.getByLabel('Mật khẩu',{exact:true}).fill(password!);
  await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();await expect(page.getByRole('link',{name:'Tiếp nhận và hàng chờ'})).toBeVisible();await page.goto(`/visits/${f.visit.visitId}`);
  const diagnosis=await diagnosisField(page);await expect(diagnosis.getByTitle('Sâu răng',{exact:true})).toBeVisible();
  await page.getByRole('button',{name:'Hoàn tất khám',exact:true}).click();
  await expect(page.getByRole('tab',{name:'Tổng kết khám',exact:true})).toHaveAttribute('aria-selected','true');
  await expect(page.getByRole('alert')).toContainText('Chưa thể hoàn tất khám');
  await expect(page.getByRole('button',{name:'Bổ sung: Kết quả khám răng 16',exact:true})).toBeInViewport();
  await expect(diagnosis.getByTitle('Sâu răng',{exact:true})).toBeVisible();
  const unchanged=await(await request.get(`${apiUrl}/api/visits/${f.visit.visitId}/examination`,{headers})).json();expect(unchanged.visit.visitVersion).toBe(draft.visit.visitVersion);
  await page.screenshot({path:testInfo.outputPath('completion-missing-tooth.png'),fullPage:true,animations:'disabled'});
  await page.getByRole('button',{name:'Bổ sung: Kết quả khám răng 16',exact:true}).click();
  await expect(page.getByRole('tab',{name:'Khám & sơ đồ răng',exact:true})).toHaveAttribute('aria-selected','true');
  await expect(page.getByLabel('Tình trạng 1',{exact:true})).toBeFocused();
  await page.getByLabel('Tình trạng 1',{exact:true}).fill('SYNTHETIC kết quả răng 16');
  await page.getByRole('tab',{name:'Tổng kết khám',exact:true}).click();await expect(page.getByRole('button',{name:'Bổ sung: Kết quả khám răng 16',exact:true})).toHaveCount(0);
  await page.getByRole('button',{name:'Hoàn tất khám',exact:true}).click();
  await expect(page.getByText('Phiếu khám đã hoàn tất. Kết thúc lượt khám trước khi lập kế hoạch điều trị.',{exact:false})).toBeVisible();
  await expect(page.getByRole('tab',{name:'Tổng kết khám',exact:true})).toHaveAttribute('aria-selected','true');await expect(page.getByRole('alert').filter({hasText:'Chưa thể hoàn tất khám'})).toHaveCount(0);
});

test('investigations preserve the examination draft through order, result, physician review and finalization',async({page,request},testInfo)=>{
  test.setTimeout(120_000);
  const f=await fixture(request),headers=await auth(request,'dentist.demo');
  await page.goto('/sign-in');await page.getByLabel('Tên đăng nhập',{exact:true}).fill('dentist.demo');await page.getByLabel('Mật khẩu',{exact:true}).fill(password!);
  await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();await expect(page.getByRole('link',{name:'Tiếp nhận và hàng chờ'})).toBeVisible();await page.goto(`/visits/${f.visit.visitId}`);
  await page.getByRole('button',{name:'Bắt đầu khám',exact:true}).click();
  await page.getByLabel('Lý do khám / triệu chứng',{exact:true}).fill('SYNTHETIC chảy máu lợi, bản nhập chưa lưu');
  await page.getByLabel('Kết quả khám tổng quát',{exact:true}).fill('SYNTHETIC nướu đỏ, kết quả đã khám');
  await setDiagnoses(page,['Viêm lợi, nướu']);await page.getByLabel('Dặn dò / lời khuyên',{exact:true}).fill('SYNTHETIC hướng dẫn');
  await page.getByRole('combobox',{name:'Hướng xử trí',exact:true}).click();await page.locator('.ant-select-dropdown:visible').getByText('Tư vấn, chưa điều trị',{exact:true}).click();
  await page.getByRole('tab',{name:/^Cận lâm sàng/}).click();await page.getByRole('button',{name:'Thêm chỉ định',exact:true}).click();
  let dialog=page.getByRole('dialog');
  // Specific techniques are discoverable; switching groups must not retain a mismatched name.
  await dialog.getByRole('combobox',{name:'Tên chỉ định',exact:true}).click();
  await expect(page.locator('.ant-select-dropdown:visible .ant-select-item-option-content').filter({hasText:'Phim toàn cảnh (Panorama / OPG)'})).toBeVisible();
  await dialog.getByRole('combobox',{name:'Nhóm chỉ định',exact:true}).press('ArrowDown');
  await page.locator('.ant-select-dropdown:visible .ant-select-item-option-content').filter({hasText:'Xét nghiệm'}).click();
  await dialog.getByRole('combobox',{name:'Tên chỉ định',exact:true}).click();
  await page.locator('.ant-select-dropdown:visible .ant-select-item-option-content').filter({hasText:'Công thức máu'}).click();
  await expect(dialog.getByRole('combobox',{name:'Tên chỉ định',exact:true})).toHaveValue('Công thức máu');
  await dialog.getByRole('combobox',{name:'Nhóm chỉ định',exact:true}).press('ArrowDown');
  await page.locator('.ant-select-dropdown:visible .ant-select-item-option-content').filter({hasText:'Chụp phim răng hàm mặt'}).click();
  await expect(dialog.getByRole('combobox',{name:'Tên chỉ định',exact:true})).toHaveValue('');
  await dialog.getByRole('combobox',{name:'Tên chỉ định',exact:true}).fill('Phim cận chóp');
  await dialog.getByLabel('Răng / vùng chỉ định',{exact:true}).fill('31–33');await dialog.getByLabel('Lý do chỉ định',{exact:true}).fill('SYNTHETIC đánh giá xương theo khám');
  await expect(dialog.getByRole('checkbox')).toBeChecked();await dialog.getByRole('button',{name:'Tạo chỉ định',exact:true}).click();await expect(dialog).not.toBeVisible();
  let order=page.locator('.investigation-card').filter({hasText:'Phim cận chóp'});await expect(order).toContainText('Chờ kết quả');
  await page.getByRole('tab',{name:'Khám & sơ đồ răng',exact:true}).click();await expect(page.getByLabel('Lý do khám / triệu chứng',{exact:true})).toHaveValue('SYNTHETIC chảy máu lợi, bản nhập chưa lưu');
  await page.getByRole('tab',{name:'Tổng kết khám',exact:true}).click();await expect(page.getByRole('button',{name:'Hoàn tất khám',exact:true})).toBeEnabled();await page.getByRole('button',{name:'Hoàn tất khám',exact:true}).click();
  await expect(page.getByRole('alert')).toContainText('Cận lâm sàng cần nhận và đọc kết quả');
  await page.getByRole('button',{name:/^Bổ sung: Cận lâm sàng/}).click();await expect(page.getByRole('region',{name:'Chỉ định cận lâm sàng',exact:true})).toBeFocused();
  // A cancelled accidental order remains in history and does not block finalization.
  await page.getByRole('button',{name:'Thêm chỉ định',exact:true}).click();dialog=page.getByRole('dialog');
  await dialog.getByRole('combobox',{name:'Tên chỉ định',exact:true}).fill('SYNTHETIC chỉ định nhầm');await dialog.getByLabel('Lý do chỉ định',{exact:true}).fill('SYNTHETIC kiểm tra');
  await dialog.getByRole('button',{name:'Tạo chỉ định',exact:true}).click();await expect(dialog).not.toBeVisible();
  const accidental=page.locator('.investigation-card').filter({hasText:'SYNTHETIC chỉ định nhầm'});await accidental.getByRole('button',{name:'Hủy chỉ định',exact:true}).click();dialog=page.getByRole('dialog');
  await dialog.getByLabel('Lý do hủy chỉ định',{exact:true}).fill('SYNTHETIC đã kiểm tra, không còn cần');await dialog.getByRole('button',{name:'Xác nhận hủy chỉ định',exact:true}).click();await expect(dialog).not.toBeVisible();await expect(accidental).toContainText('Đã hủy');
  await order.getByRole('button',{name:'Tải tệp kết quả',exact:true}).click();const upload=page.getByRole('dialog');
  await upload.locator('input[type=file]').setInputFiles({name:'synthetic-investigation.pdf',mimeType:'application/pdf',buffer:Buffer.from('%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\n%%EOF')});
  await upload.getByRole('button',{name:/Tải lên|Bắt đầu tải|Gửi/}).click();await expect(upload.getByText('Đang kiểm tra an toàn',{exact:true})).toBeVisible();
  await upload.getByRole('button',{name:'Close',exact:true}).click();await expect(upload).not.toBeVisible();
  await expect(order).toContainText('synthetic-investigation.pdf');
  await order.getByRole('button',{name:'Nhận kết quả',exact:true}).click();dialog=page.getByRole('dialog');
  await dialog.getByLabel('Nội dung kết quả',{exact:true}).fill('SYNTHETIC báo cáo văn bản đã nhận; file đính kèm còn kiểm tra');
  await dialog.getByRole('button',{name:'Lưu kết quả',exact:true}).click();await expect(dialog).not.toBeVisible();await expect(order).toContainText('Chờ bác sĩ đọc');
  await page.getByRole('tab',{name:'Tổng kết khám',exact:true}).click();await page.getByRole('button',{name:'Hoàn tất khám',exact:true}).click();await expect(page.getByRole('alert')).toContainText('Cận lâm sàng cần nhận và đọc kết quả');
  await page.getByRole('button',{name:/^Bổ sung: Cận lâm sàng/}).click();await order.getByRole('button',{name:'Đọc kết quả',exact:true}).click();dialog=page.getByRole('dialog');
  await expect(dialog).toContainText('SYNTHETIC báo cáo văn bản đã nhận');await dialog.getByLabel('Nhận định của bác sĩ',{exact:true}).fill('SYNTHETIC đã đánh giá phần văn bản');
  await dialog.getByRole('button',{name:'Xác nhận đã đọc kết quả',exact:true}).click();await expect(dialog).not.toBeVisible();await expect(order).toContainText('Bác sĩ đã đọc');
  await page.screenshot({path:testInfo.outputPath('investigation-reviewed.png'),fullPage:true,animations:'disabled'});
  await page.getByRole('tab',{name:'Tổng kết khám',exact:true}).click();await expect(page.getByRole('alert').filter({hasText:'Chưa thể hoàn tất khám'})).toHaveCount(0);
  await page.getByRole('button',{name:'Hoàn tất khám',exact:true}).click();await expect(page.getByText('Phiếu khám đã hoàn tất. Kết thúc lượt khám trước khi lập kế hoạch điều trị.',{exact:false})).toBeVisible();
  const latest=await(await request.get(`${apiUrl}/api/visits/${f.visit.visitId}/examination`,{headers})).json();expect(latest.examination.confirmed).toBe(true);
  expect(latest.examination.content.symptoms).toBe('SYNTHETIC chảy máu lợi, bản nhập chưa lưu');
  const result=latest.investigations.find((item:{name:string})=>item.name==='Phim cận chóp');expect(result.state).toBe('Reviewed');expect(result.resultDocumentIds).toEqual([]);
  expect(result.documents[0].context.investigationId).toBe(result.id);expect(result.documents[0].fileState).toBe('PendingScan');expect(result.documents[0].reviews).toEqual([]);
});


test('scoped diagnoses are entered once and follow their teeth and region through save and finalization',async({page,request},testInfo)=>{
  const f=await fixture(request),headers=await auth(request,'dentist.demo');
  await request.post(`${apiUrl}/api/visits/${f.visit.visitId}/start`,{headers,data:{patientId:f.patient.id,expectedVersion:f.visit.visitVersion,requestKey:crypto.randomUUID()}});
  await page.goto('/sign-in');await page.getByLabel('Tên đăng nhập',{exact:true}).fill('dentist.demo');await page.getByLabel('Mật khẩu',{exact:true}).fill(password!);
  await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();await expect(page.getByRole('link',{name:'Tiếp nhận và hàng chờ'})).toBeVisible();await page.goto(`/visits/${f.visit.visitId}`);
  await page.getByLabel('Lý do khám / triệu chứng',{exact:true}).fill('SYNTHETIC kiểm tra răng');
  const chart=page.getByRole('region',{name:'Sơ đồ răng',exact:true});
  await chart.getByRole('button',{name:'Răng 16',exact:true}).click();await chart.getByRole('button',{name:'Răng 26',exact:true}).click();
  const quick=chart.getByRole('combobox',{name:'Chẩn đoán cho răng đang chọn',exact:true});await quick.fill('Sâu răng');await quick.press('Enter');await quick.press('Escape');
  await chart.getByRole('button',{name:'Áp dụng cho 2 răng',exact:true}).click();
  await page.getByRole('tab',{name:'Tổng kết khám',exact:true}).click();
  const summary=page.getByRole('region',{name:'Chẩn đoán đã ghi tại răng và vùng',exact:true});await expect(summary.getByText('Sâu răng',{exact:false})).toHaveCount(2);
  await expect((await diagnosisField(page)).locator('.ant-select-selection-item')).toHaveCount(0);
  const scopedSave=page.waitForResponse(r=>r.url().endsWith(`/api/visits/${f.visit.visitId}/examination`)&&r.request().method()==='PUT');
  await page.getByRole('button',{name:'Lưu nháp',exact:true}).click();const scopedSaved=await scopedSave;expect(scopedSaved.ok(),await scopedSaved.text()).toBeTruthy();await page.reload();
  await expect(page.getByLabel('Kết quả khám tổng quát',{exact:true})).toHaveValue('');
  const selectFor=(name:string)=>page.locator('.ant-select').filter({has:page.getByRole('combobox',{name,exact:true})});
  await selectFor('Chẩn đoán răng 16').locator('.ant-select-selection-item-remove').click();
  await page.getByLabel('Tình trạng 1',{exact:true}).fill('SYNTHETIC phát hiện, chưa kết luận');
  await selectFor('Chẩn đoán răng 26').locator('.ant-select-selection-item-remove').click();
  const tooth26=page.getByRole('combobox',{name:'Chẩn đoán răng 26',exact:true});await tooth26.fill('Viêm tủy răng');await tooth26.press('Enter');await tooth26.press('Escape');
  const general=page.getByRole('combobox',{name:'Chẩn đoán toàn hàm',exact:true});await general.fill('Viêm lợi, nướu');await general.press('Enter');await general.press('Escape');
  await page.getByRole('tab',{name:'Tổng kết khám',exact:true}).click();await expect(summary).not.toContainText('Sâu răng');await expect(summary).toContainText('Viêm tủy răng');await expect(summary).toContainText('Viêm lợi, nướu');
  await page.getByLabel('Dặn dò / lời khuyên',{exact:true}).fill('SYNTHETIC hướng dẫn');await page.getByRole('combobox',{name:'Hướng xử trí',exact:true}).press('ArrowDown');await page.locator('.ant-select-dropdown:visible .ant-select-item-option-content').filter({hasText:'Tư vấn, chưa điều trị'}).click();
  await page.screenshot({path:testInfo.outputPath('scoped-diagnoses-summary.png'),fullPage:true,animations:'disabled'});
  await page.getByRole('button',{name:'Hoàn tất khám',exact:true}).click();await expect(page.getByText('Phiếu khám đã hoàn tất. Kết thúc lượt khám trước khi lập kế hoạch điều trị.',{exact:false})).toBeVisible();
  const latest=await(await request.get(`${apiUrl}/api/visits/${f.visit.visitId}/examination`,{headers})).json();
  expect(latest.examination.content.diagnoses).toEqual(['Viêm tủy răng','Viêm lợi, nướu']);expect(latest.examination.content.unscopedDiagnoses).toEqual([]);
  expect(latest.examination.content.findings.find((x:{tooth:number})=>x.tooth===26).diagnoses).toEqual(['Viêm tủy răng']);
});


test('amendment of a confirmed open visit can create and review a supplemental investigation',async({page,request},testInfo)=>{
  const f=await fixture(request),headers=await auth(request,'dentist.demo');
  const start=await request.post(`${apiUrl}/api/visits/${f.visit.visitId}/start`,{headers,data:{patientId:f.patient.id,expectedVersion:f.visit.visitVersion,requestKey:crypto.randomUUID()}});expect(start.ok()).toBeTruthy();const started=await start.json();
  const content={...started.initialContent,symptoms:'SYNTHETIC khám tổng quát',findings:[],diagnoses:['Chưa phát hiện bất thường'],advice:'SYNTHETIC dặn dò',disposition:'ConsultationOnly'};
  const finish=await request.put(`${apiUrl}/api/visits/${f.visit.visitId}/examination`,{headers,data:{patientId:f.patient.id,expectedVersion:started.visit.visitVersion,requestKey:crypto.randomUUID(),content,confirm:true}});expect(finish.ok(),await finish.text()).toBeTruthy();const confirmed=await finish.json();
  await page.goto('/sign-in');await page.getByLabel('Tên đăng nhập',{exact:true}).fill('dentist.demo');await page.getByLabel('Mật khẩu',{exact:true}).fill(password!);await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();await expect(page.getByRole('link',{name:'Tiếp nhận và hàng chờ'})).toBeVisible();await page.goto(`/visits/${f.visit.visitId}`);
  await page.getByRole('tab',{name:/^Cận lâm sàng/}).click();await expect(page.getByRole('button',{name:'Thêm chỉ định',exact:true})).toHaveCount(0);await expect(page.getByText('Để bổ sung chỉ định, vào Tổng kết khám và bấm Đính chính kết quả.',{exact:true})).toBeVisible();
  await page.getByRole('tab',{name:'Tổng kết khám',exact:true}).click();await page.getByRole('button',{name:'Đính chính kết quả',exact:true}).click();
  await page.getByRole('tab',{name:'Khám & sơ đồ răng',exact:true}).click();await page.getByLabel('Ghi chú khám',{exact:true}).fill('SYNTHETIC bản đính chính chưa lưu');
  await page.getByRole('tab',{name:/^Cận lâm sàng/}).click();await page.getByRole('button',{name:'Thêm chỉ định',exact:true}).click();let dialog=page.getByRole('dialog');
  await dialog.getByRole('combobox',{name:'Tên chỉ định',exact:true}).fill('Phim quanh chóp bổ sung');await dialog.getByLabel('Lý do chỉ định',{exact:true}).fill('SYNTHETIC đánh giá bổ sung');await dialog.getByRole('button',{name:'Tạo chỉ định',exact:true}).click();await expect(dialog).toBeVisible();
  await dialog.getByLabel('Lý do bổ sung sau xác nhận khám',{exact:true}).fill('SYNTHETIC bổ sung đánh giá sau kết luận');await dialog.getByRole('button',{name:'Tạo chỉ định',exact:true}).click();await expect(dialog).not.toBeVisible();
  const order=page.locator('.investigation-card').filter({hasText:'Phim quanh chóp bổ sung'});await expect(order).toContainText('Chờ kết quả');
  await page.getByRole('tab',{name:'Tổng kết khám',exact:true}).click();await expect(page.getByLabel('Lý do đính chính',{exact:true})).toHaveValue('SYNTHETIC bổ sung đánh giá sau kết luận');await page.getByRole('button',{name:'Lưu đính chính',exact:true}).click();await expect(page.getByRole('alert')).toContainText('Cận lâm sàng cần nhận và đọc kết quả');
  await page.getByRole('tab',{name:/^Cận lâm sàng/}).click();await page.screenshot({path:testInfo.outputPath('amendment-investigation-created.png'),fullPage:true,animations:'disabled'});
  await order.getByRole('button',{name:'Nhận kết quả',exact:true}).click();dialog=page.getByRole('dialog');await dialog.getByLabel('Nội dung kết quả',{exact:true}).fill('SYNTHETIC kết quả bổ sung');await dialog.getByRole('button',{name:'Lưu kết quả',exact:true}).click();await expect(dialog).not.toBeVisible();
  await order.getByRole('button',{name:'Đọc kết quả',exact:true}).click();await page.getByRole('dialog').getByRole('button',{name:'Xác nhận đã đọc kết quả',exact:true}).click();await expect(page.getByRole('dialog')).not.toBeVisible();
  await page.getByRole('tab',{name:'Tổng kết khám',exact:true}).click();await page.getByRole('button',{name:'Lưu đính chính',exact:true}).click();await expect(page.getByRole('button',{name:'Đính chính kết quả',exact:true})).toBeVisible();
  const saved=await(await request.get(`${apiUrl}/api/visits/${f.visit.visitId}/examination`,{headers})).json();expect(saved.examination.version).toBe(confirmed.examination.version+1);expect(saved.examination.content.notes).toBe('SYNTHETIC bản đính chính chưa lưu');
  const closed=await request.post(`${apiUrl}/api/visits/${f.visit.visitId}/complete`,{headers,data:{patientId:f.patient.id,expectedVersion:saved.visit.visitVersion,requestKey:crypto.randomUUID(),workReviewed:true}});expect(closed.ok(),await closed.text()).toBeTruthy();await page.reload();await page.getByRole('tab',{name:'Tổng kết khám',exact:true}).click();await page.getByRole('button',{name:'Đính chính kết quả',exact:true}).click();await page.getByRole('tab',{name:/^Cận lâm sàng/}).click();await expect(page.getByRole('button',{name:'Thêm chỉ định',exact:true})).toHaveCount(0);await expect(page.getByText('Lượt khám đã kết thúc. Có thể đính chính hồ sơ cũ; cần tạo lượt khám mới để lập chỉ định mới.',{exact:true})).toBeVisible();
});
