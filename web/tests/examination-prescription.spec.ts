import { expect, test, type Page } from '@playwright/test';

const visitId='11111111-1111-4111-8111-111111111111';
const medicine={id:'22222222-2222-4222-8222-222222222222',code:'MED-SYN',name:'SYNTHETIC thuốc thử',strength:'SYNTHETIC hàm lượng',dosageForm:'Viên',baseUnit:'Viên',defaultInstructions:'SYNTHETIC lời dặn mặc định'};
const savedLine={id:'33333333-3333-4333-8333-333333333333',visitId,medicineId:medicine.id,medicineCode:medicine.code,medicineName:medicine.name,unit:'Viên',quantity:2,instructions:'SYNTHETIC đơn đã lưu',version:1};

async function prepare(page:Page, setup:{confirmed?:boolean;completed?:boolean;assistant?:boolean;loadFailure?:boolean;existing?:boolean;standalone?:boolean}={}) {
  const content={symptoms:'SYNTHETIC khám tư vấn',medicalHistory:{state:'Unknown',verified:false},allergies:{state:'Unknown',verified:false},medications:{state:'Unknown',verified:false},findings:[],diagnoses:['Chưa phát hiện bất thường'],advice:'SYNTHETIC dặn dò trước kê đơn',disposition:'ConsultationOnly'};
  const state={workspace:{visit:{visitId,patientId:1,patientCode:'BN-SYN',patientName:'SYNTHETIC bệnh nhân',branchId:1,branchName:'CN thử nghiệm',dentistId:7,dentistName:'Bác sĩ thử',appointmentCode:'AP-SYN',arrivalAt:'2026-11-02T08:00:00Z',purpose:'InitialExamination',visitVersion:2,status:setup.completed?'Completed':'InProgress'},
      patient:{id:1,patientCode:'BN-SYN',fullName:'SYNTHETIC bệnh nhân',birthDate:null,gender:null,ageYears:null},initialContent:content,examination:{content,confirmed:!!setup.confirmed,version:1},investigations:[],allowedActions:setup.completed?['amend']:setup.confirmed?['amend','complete','prescribe']:['save','confirm','prescribe']},
    lines:setup.existing?[structuredClone(savedLine)]:[] as typeof savedLine[],requests:[] as {path:string;method:string;body:any}[],queries:[] as string[],failSave:false,loadFailure:!!setup.loadFailure};
  const replay=new Map<string,typeof savedLine>();
  await page.addInitScript(()=>sessionStorage.setItem('dcms.web.access-token','synthetic'));
  await page.route('**/api/**',async route=>{
    const request=route.request(),url=new URL(request.url()),path=url.pathname,method=request.method();
    const ok=(json:unknown)=>route.fulfill({json});
    state.requests.push({path,method,body:method==='GET'?undefined:request.postDataJSON()});
    if(path==='/api/auth/me')return ok({id:7,accountType:'Staff',fullName:'SYNTHETIC bác sĩ',roles:[setup.assistant?'ClinicAssistant':'Dentist'],effectiveRoles:[setup.assistant?'ClinicAssistant':'Dentist'],permissions:setup.assistant?['reception.read','examinations.read']:['reception.read','examinations.read','examinations.manage','prescriptions.read','prescriptions.manage','documents.read']});
    if(path==='/api/auth/session/idle')return ok({serverNow:new Date().toISOString(),deadlineAt:new Date(Date.now()+1200000).toISOString(),activityVersion:1});
    if(path==='/api/system-settings/reference-options')return ok([]);
    if(path.endsWith('/clinical-history')||path.endsWith('/examination/versions'))return ok([]);
    if(path===`/api/visits/${visitId}/examination`){
      if(method==='PUT'){const body=request.postDataJSON();state.workspace.examination={content:body.content,confirmed:body.confirm,version:state.workspace.examination.version+1};state.workspace.allowedActions=body.confirm?['amend','complete','prescribe']:['save','confirm','prescribe'];state.workspace.visit.visitVersion++;}
      return ok(state.workspace);
    }
    if(path===`/api/visits/${visitId}/complete`){state.workspace.visit.status='Completed';state.workspace.allowedActions=['amend'];return ok(state.workspace);}
    if(path==='/api/prescriptions/medicine-options'){state.queries.push(url.searchParams.get('q')??'');return ok([medicine]);}
    if(path.startsWith(`/api/prescriptions/visits/${visitId}/lines`)){
      if(method==='GET')return state.loadFailure?route.fulfill({status:503,json:{code:'MSG144',message:'SYNTHETIC không tải đơn'}}):ok(state.lines);
      const body=request.postDataJSON();if(replay.has(body.requestKey))return ok(replay.get(body.requestKey));
      const old=state.lines.find(line=>path.endsWith('/'+line.id));
      if(method==='PUT'&&old?.version!==body.expectedVersion)return route.fulfill({status:409,json:{code:'MSG111',message:'Dữ liệu thay đổi.'}});
      const line={...savedLine,...body,id:old?.id??savedLine.id,version:old?old.version+1:1};
      state.lines=old?state.lines.map(value=>value.id===old.id?line:value):[...state.lines,line];replay.set(body.requestKey,line);
      if(state.failSave){state.failSave=false;return route.abort('failed');}
      return ok(line);
    }
    if(path.endsWith('/contexts'))return ok({patient:{id:1,fullName:'SYNTHETIC bệnh nhân'},visits:[],dossiers:[],plans:[],investigations:[]});
    return route.fulfill({status:404,json:{code:'MSG113',message:'Không tìm thấy.'}});
  });
  await page.goto(`/visits/${visitId}${setup.standalone?'/prescription':''}`);
  if(!setup.standalone)await page.getByRole('tab',{name:'Tổng kết khám',exact:true}).click();
  return state;
}
const writes=(state:Awaited<ReturnType<typeof prepare>>)=>state.requests.filter(r=>r.path.includes('/api/prescriptions/')&&r.method!=='GET');
async function choose(page:Page){
  const panel=page.getByRole('region',{name:'Đơn thuốc khám'});await panel.getByRole('button',{name:'Kê đơn thuốc',exact:true}).click();
  const input=panel.getByRole('combobox',{name:'Thuốc kê đơn',exact:true});await input.fill('SYNTHETIC');
  await page.locator('.ant-select-dropdown:visible').getByText(`${medicine.code} · ${medicine.name} · ${medicine.strength}`,{exact:true}).click();
  await expect(panel.getByLabel('Lời dặn dùng thuốc',{exact:true})).toHaveValue(medicine.defaultInstructions);
  await expect(panel.getByLabel('Đơn vị thuốc',{exact:true})).toHaveValue('');
  return panel;
}

test('prescription loading failure does not require a prescription to confirm or complete examination',async({page})=>{
  const state=await prepare(page,{loadFailure:true});await expect(page.getByText('Không tải được đơn thuốc',{exact:true})).toBeVisible();
  await page.getByRole('button',{name:'Hoàn tất khám',exact:true}).click();await expect(page.getByText('Đã hoàn tất khám',{exact:true}).first()).toBeVisible();
  await page.getByRole('button',{name:'Kết thúc lượt khám',exact:true}).click();const dialog=page.getByRole('dialog',{name:'Kết thúc lượt khám',exact:true});await dialog.getByRole('checkbox').check();await dialog.getByRole('button',{name:'Xác nhận kết thúc lượt',exact:true}).click();
  await expect(dialog).not.toBeVisible();expect(state.workspace.visit.status).toBe('Completed');expect(writes(state)).toHaveLength(0);
});

test('inline add and edit preserve unsaved conclusion; saved lines survive reload and standalone route',async({page})=>{
  const state=await prepare(page);await page.getByLabel('Dặn dò / lời khuyên',{exact:true}).fill('SYNTHETIC kết luận cần giữ');const panel=await choose(page);
  await panel.getByLabel('Số lượng thuốc',{exact:true}).fill('3');await panel.getByLabel('Đơn vị thuốc',{exact:true}).fill('Tuýp');await panel.getByLabel('Lời dặn dùng thuốc',{exact:true}).fill('SYNTHETIC chỉ dẫn riêng');await panel.getByRole('button',{name:'Thêm vào đơn',exact:true}).click();
  expect(writes(state)[0].body.unit).toBe('Tuýp');
  await expect(panel.getByText('3 Tuýp',{exact:true})).toBeVisible();await expect(page.getByLabel('Dặn dò / lời khuyên',{exact:true})).toHaveValue('SYNTHETIC kết luận cần giữ');
  expect(state.requests.filter(r=>r.path.endsWith('/examination')&&r.method==='PUT')).toHaveLength(0);
  await panel.getByRole('button',{name:`Sửa thuốc ${medicine.name}`,exact:true}).click();await panel.getByLabel('Đơn vị thuốc',{exact:true}).fill('');await panel.getByLabel('Lời dặn dùng thuốc',{exact:true}).fill('SYNTHETIC lời dặn đã sửa');await panel.getByRole('button',{name:'Lưu thay đổi thuốc',exact:true}).click();
  await expect(panel.getByText('Nhập đơn vị thuốc.',{exact:true})).toBeVisible();expect(writes(state)).toHaveLength(1);
  await panel.getByLabel('Đơn vị thuốc',{exact:true}).fill('Viên');await panel.getByRole('button',{name:'Lưu thay đổi thuốc',exact:true}).click();
  expect(writes(state)[1].body.unit).toBe('Viên');
  await expect(panel.getByRole('cell',{name:'SYNTHETIC lời dặn đã sửa',exact:true})).toBeVisible();await page.getByRole('button',{name:'Hoàn tất khám',exact:true}).click();
  await expect(page.getByRole('button',{name:'Kết thúc lượt khám',exact:true})).toBeVisible();await page.reload();await page.getByRole('tab',{name:'Tổng kết khám',exact:true}).click();await expect(panel.getByText('SYNTHETIC lời dặn đã sửa',{exact:true})).toBeVisible();
  await page.goto(`/visits/${visitId}/prescription`);await expect(panel.getByText('SYNTHETIC lời dặn đã sửa',{exact:true})).toBeVisible();
  expect(state.queries).toContain('SYNTHETIC');expect(state.requests.some(r=>r.path.startsWith('/api/finance'))).toBe(false);expect(state.lines).toHaveLength(1);
});

test('lost response retries the same command and stale edit keeps inputs while reading latest line version',async({page})=>{
  const state=await prepare(page);const panel=await choose(page);state.failSave=true;
  await panel.getByRole('button',{name:'Thêm vào đơn',exact:true}).click();await expect(panel.getByRole('alert')).toBeVisible();await expect(panel.getByLabel('Lời dặn dùng thuốc',{exact:true})).toHaveValue(medicine.defaultInstructions);
  await panel.getByRole('button',{name:'Thêm vào đơn',exact:true}).click();await expect(panel.getByText('1 Viên',{exact:true})).toBeVisible();expect(writes(state)[0].body.requestKey).toBe(writes(state)[1].body.requestKey);expect(state.lines).toHaveLength(1);
  await panel.getByRole('button',{name:`Sửa thuốc ${medicine.name}`,exact:true}).click();await panel.getByLabel('Lời dặn dùng thuốc',{exact:true}).fill('SYNTHETIC bản nhập sau xung đột');state.lines[0].version++;
  await panel.getByRole('button',{name:'Lưu thay đổi thuốc',exact:true}).click();await expect(panel.getByText(/Đơn thuốc đã thay đổi/)).toBeVisible();await panel.getByRole('button',{name:'Đọc lại dòng thuốc',exact:true}).click();
  await expect(panel.getByLabel('Lời dặn dùng thuốc',{exact:true})).toHaveValue('SYNTHETIC bản nhập sau xung đột');await panel.getByRole('button',{name:'Lưu thay đổi thuốc',exact:true}).click();await expect(panel.getByRole('cell',{name:'SYNTHETIC bản nhập sau xung đột',exact:true})).toBeVisible();expect(state.lines[0].version).toBe(3);
});

test('closed visit shows saved prescription without edit or medicine options request',async({page})=>{
  const state=await prepare(page,{confirmed:true,completed:true,existing:true});const panel=page.getByRole('region',{name:'Đơn thuốc khám'});
  await expect(panel.getByText(savedLine.instructions,{exact:true})).toBeVisible();await expect(panel.getByRole('combobox')).toHaveCount(0);await expect(panel.getByRole('button',{name:`Sửa thuốc ${medicine.name}`,exact:true})).toHaveCount(0);expect(state.queries).toHaveLength(0);
});

test('assistant does not request or show prescription editing',async({page})=>{
  const state=await prepare(page,{assistant:true});await expect(page.getByRole('region',{name:'Đơn thuốc khám'})).toHaveCount(0);expect(state.requests.filter(r=>r.path.includes('/api/prescriptions/'))).toHaveLength(0);
});

test('unfinished prescription is optional on completion with explicit discard choice',async({page})=>{
  const state=await prepare(page);const panel=await choose(page);await panel.getByLabel('Lời dặn dùng thuốc',{exact:true}).fill('SYNTHETIC chưa lưu');
  page.once('dialog',dialog=>void dialog.dismiss());await page.getByRole('link',{name:'Về hàng chờ',exact:true}).click();await expect(page).toHaveURL(new RegExp(`/visits/${visitId}$`));
  await page.getByRole('button',{name:'Hoàn tất khám',exact:true}).click();await expect(page.getByRole('button',{name:'Kết thúc lượt khám',exact:true})).toBeVisible();await expect(panel.getByLabel('Lời dặn dùng thuốc',{exact:true})).toHaveValue('SYNTHETIC chưa lưu');expect(writes(state)).toHaveLength(0);
  await page.getByRole('button',{name:'Kết thúc lượt khám',exact:true}).click();const ending=page.getByRole('dialog',{name:'Kết thúc lượt khám',exact:true});await ending.getByRole('checkbox').check();await ending.getByRole('button',{name:'Xác nhận kết thúc lượt',exact:true}).click();
  const warning=page.getByRole('dialog').filter({has:page.getByText('Có thuốc chưa lưu vào đơn',{exact:true})});await expect(warning).toBeVisible();await warning.getByRole('button',{name:'Quay lại kê đơn',exact:true}).click();await expect(warning).not.toBeVisible();await ending.getByRole('button',{name:'Cancel',exact:true}).click();
  await expect(panel.getByLabel('Lời dặn dùng thuốc',{exact:true})).toHaveValue('SYNTHETIC chưa lưu');expect(state.workspace.visit.status).toBe('InProgress');
  await page.getByRole('button',{name:'Kết thúc lượt khám',exact:true}).click();await ending.getByRole('checkbox').check();await ending.getByRole('button',{name:'Xác nhận kết thúc lượt',exact:true}).click();await warning.getByRole('button',{name:'Kết thúc, bỏ phần thuốc chưa lưu',exact:true}).click();
  await expect(ending).not.toBeVisible();expect(state.workspace.visit.status).toBe('Completed');expect(writes(state)).toHaveLength(0);
});
