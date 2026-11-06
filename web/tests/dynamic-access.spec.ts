import { expect, test, type Page } from '@playwright/test';
import type { AccessGroup, GrantOption } from '../src/features/system-settings/accessGroups';

const groupId='11111111-1111-4111-8111-111111111111';
const options:GrantOption[]=[
  {key:'patients.read',label:'Xem bệnh nhân',parent:null,requiredRole:null,allowedBaseRoles:['Dentist','ClinicAssistant']},
  ...['create','update','archive'].map((key,index)=>({key:'patients.'+key,label:['Thêm hồ sơ bệnh nhân','Sửa hồ sơ bệnh nhân','Ngừng sử dụng / khôi phục hồ sơ bệnh nhân'][index],parent:'patients.read',requiredRole:'ClinicAssistant',allowedBaseRoles:['ClinicAssistant']})),
  {key:'examinations.read',label:'Xem khám lâm sàng',parent:null,requiredRole:null,allowedBaseRoles:['Dentist','ClinicAssistant']},
  {key:'examinations.manage',label:'Ghi và xác nhận khám',parent:'examinations.read',requiredRole:'Dentist',allowedBaseRoles:['Dentist']},
];
async function prepare(page:Page,readOnly=false){
  const state={groups:[{id:groupId,code:'RECEPTION',name:'SYNTHETIC lễ tân',baseRole:'ClinicAssistant',description:null,active:true,version:3,grants:['patients.read','patients.manage'],assignedCount:1},
    {id:'22222222-2222-4222-8222-222222222222',code:'DENTIST',name:'SYNTHETIC nha sĩ',baseRole:'Dentist',description:null,active:true,version:1,grants:['examinations.read'],assignedCount:0},
    {id:'33333333-3333-4333-8333-333333333333',code:'OLD',name:'SYNTHETIC nhóm ngừng',baseRole:'ClinicAssistant',description:null,active:false,version:1,grants:[],assignedCount:0}] as AccessGroup[],
    staff:[{employeeId:22,accountId:8,name:'SYNTHETIC nhân viên',active:true,accountVersion:4,groupId,assignmentVersion:1,branchIds:[1],roles:['ClinicAssistant'],membershipBranchIds:[1,2]},
      {employeeId:7,accountId:7,name:'SYNTHETIC chủ phòng khám',active:true,accountVersion:1,groupId:null,assignmentVersion:0,branchIds:[1],roles:['ClinicOwner','Dentist'],membershipBranchIds:[1]}],
    requests:[] as {method:string;path:string;body:any}[],failSave:false,
    user:{id:7,accountType:'Staff',fullName:'SYNTHETIC chủ phòng khám',roles:['ClinicOwner'],effectiveRoles:['ClinicOwner','Dentist'],permissions:['settings.read','access-groups.read',...readOnly?[]:['access-groups.manage','staff.access.assign','settings.audit.read']]}};
  const receipts=new Map<string,unknown>();
  await page.addInitScript(()=>sessionStorage.setItem('dcms.web.access-token','synthetic'));
  await page.route('**/api/**',async route=>{
    const request=route.request(),path=new URL(request.url()).pathname,method=request.method();
    const ok=(json:unknown)=>route.fulfill({json});
    if(path==='/api/auth/me')return ok(state.user);
    if(path==='/api/auth/session/idle')return ok({serverNow:new Date().toISOString(),deadlineAt:new Date(Date.now()+1200000).toISOString(),activityVersion:1});
    if(path==='/api/system-settings/settings/capabilities')return ok({sections:[]});
    if(path==='/api/system-settings/access-capabilities')return ok({owner:!readOnly,permissions:state.user.permissions,branches:[{id:1,name:'Chi nhánh 1'},{id:2,name:'Chi nhánh 2'},{id:3,name:'Chi nhánh ngoài membership'}]});
    if(path==='/api/system-settings/settings/audit')return ok({items:[]});
    if(path==='/api/system-settings/access-groups/grant-options')return ok(options);
    if(path==='/api/system-settings/access-groups/staff')return ok(state.staff);
    if(path.startsWith('/api/system-settings/access-groups')){
      const id=path.split('/')[4],group=state.groups.find(row=>row.id===id);
      if(method==='GET')return ok(id?group:state.groups);
      const body=request.postDataJSON();state.requests.push({method,path,body});
      if(path.endsWith('/preview')){
        if(group?.version!==body.expectedVersion)return route.fulfill({status:409,json:{code:'MSG111',message:'SYNTHETIC nhóm đã đổi phiên bản'}});
        return ok({added:body.entry.grants.filter((key:string)=>!group.grants.includes(key)),removed:group.grants.filter(key=>!body.entry.grants.includes(key)),affected:group.assignedCount});
      }
      const receipt=method+path+body.requestKey;if(receipts.has(receipt))return ok(receipts.get(receipt));
      if(group&&group.version!==body.expectedVersion)return route.fulfill({status:409,json:{code:'MSG111',message:'SYNTHETIC nhóm đã đổi phiên bản'}});
      let saved:AccessGroup;
      if(path.endsWith('/state')){group!.active=body.active;group!.version++;saved=group!;}
      else if(group){Object.assign(group,body.entry,{version:group.version+1});saved=group;}
      else{saved={...body.entry,id:'44444444-4444-4444-8444-444444444444',active:true,version:1,assignedCount:0};state.groups.push(saved);}
      receipts.set(receipt,structuredClone(saved));if(state.failSave){state.failSave=false;return route.abort('failed');}return ok(saved);
    }
    if(path==='/api/system-settings/staff/22/access-group'){
      const body=request.postDataJSON();state.requests.push({method,path,body});Object.assign(state.staff[0],{groupId:body.groupId,branchIds:body.branchIds,accountVersion:5});return ok(state.staff[0]);
    }
    if(path==='/api/patients')return ok({items:[],total:0,page:1,pageSize:20});
    if(path==='/api/patients/1')return ok({id:1,patientCode:'BN-SYN',fullName:'SYNTHETIC bệnh nhân',birthDate:null,gender:null,phone:null,link:{},intakeHistory:[],isArchived:false});
    return route.fulfill({status:404,json:{code:'MSG113',message:'Không tìm thấy.'}});
  });
  await page.goto('/system-settings?tab=access-groups');await expect(page.getByRole('button',{name:'Thiết lập SYNTHETIC lễ tân',exact:true})).toBeVisible();return state;
}

test('checkbox dependencies survive filtering, clinical role gates apply, lost response reuses one command',async({page})=>{
  const state=await prepare(page);await page.getByRole('button',{name:'Thêm nhóm quyền',exact:true}).click();const dialog=page.getByRole('dialog');
  await dialog.getByLabel('Tên phân quyền',{exact:true}).fill('SYNTHETIC chỉ thêm hồ sơ');await dialog.getByLabel('Mã phân quyền',{exact:true}).fill('SYN-CREATE');
  await dialog.getByRole('checkbox',{name:'Thêm hồ sơ bệnh nhân',exact:true}).check();await expect(dialog.getByRole('checkbox',{name:'Xem bệnh nhân',exact:true})).toBeChecked();
  await expect(dialog.getByRole('checkbox',{name:/Ghi và xác nhận khám/})).toBeDisabled();
  await dialog.getByLabel('Tìm quyền',{exact:true}).fill('Sửa hồ sơ');await dialog.getByRole('checkbox',{name:'Xem bệnh nhân',exact:true}).uncheck();
  await dialog.getByLabel('Tìm quyền',{exact:true}).fill('');await expect(dialog.getByRole('checkbox',{name:'Thêm hồ sơ bệnh nhân',exact:true})).not.toBeChecked();
  await dialog.getByRole('checkbox',{name:'Thêm hồ sơ bệnh nhân',exact:true}).check();state.failSave=true;
  await dialog.getByRole('button',{name:'Lưu phân quyền',exact:true}).click();await expect(dialog.getByRole('alert')).toContainText('Không thể hoàn tất thao tác');
  await expect(dialog.getByLabel('Tên phân quyền',{exact:true})).toHaveValue('SYNTHETIC chỉ thêm hồ sơ');await dialog.getByRole('button',{name:'Lưu phân quyền',exact:true}).click();
  await expect(dialog).not.toBeVisible();const writes=state.requests.filter(request=>request.method==='POST'&&!request.path.endsWith('/preview'));
  expect(writes).toHaveLength(2);expect(writes[0].body.requestKey).toBe(writes[1].body.requestKey);expect(writes[0].body.entry.grants.sort()).toEqual(['patients.create','patients.read']);
  expect(state.groups.filter(group=>group.code==='SYN-CREATE')).toHaveLength(1);
});

test('legacy group becomes granular, stale preview preserves input, changes show affected staff',async({page})=>{
  const state=await prepare(page);await expect(page.getByRole('button',{name:'Ngừng nhóm SYNTHETIC lễ tân'})).toBeDisabled();
  await page.getByRole('button',{name:'Thiết lập SYNTHETIC lễ tân',exact:true}).click();const dialog=page.getByRole('dialog');
  await expect(dialog.getByRole('checkbox',{name:'Thêm hồ sơ bệnh nhân',exact:true})).toBeChecked();
  await dialog.getByRole('checkbox',{name:'Sửa hồ sơ bệnh nhân',exact:true}).uncheck();await dialog.getByLabel('Tên phân quyền',{exact:true}).fill('SYNTHETIC quyền đã sửa');
  await dialog.getByLabel('Lý do thay đổi phân quyền',{exact:true}).fill('SYNTHETIC thu hồi sửa');state.groups[0].version=4;
  await dialog.getByRole('button',{name:'Xem trước thay đổi',exact:true}).click();await expect(dialog.getByRole('alert')).toContainText('SYNTHETIC nhóm đã đổi phiên bản');
  await dialog.getByRole('button',{name:'Đọc lại phiên bản nhóm, giữ bản nhập',exact:true}).click();await expect(dialog.getByLabel('Tên phân quyền',{exact:true})).toHaveValue('SYNTHETIC quyền đã sửa');
  await dialog.getByRole('button',{name:'Xem trước thay đổi',exact:true}).click();await expect(dialog.getByText('Ảnh hưởng 1 nhân viên',{exact:true})).toBeVisible();
  await dialog.getByRole('button',{name:'Lưu phân quyền',exact:true}).click();await expect(dialog).not.toBeVisible();
  const saved=state.requests.find(request=>request.method==='PUT')!;expect(saved.body.expectedVersion).toBe(4);expect(saved.body.entry.grants.sort()).toEqual(['patients.archive','patients.create','patients.read']);
});

test('assignment offers active role-compatible groups and existing branch memberships only',async({page})=>{
  const state=await prepare(page);await page.getByRole('tab',{name:'Gán cho nhân viên',exact:true}).click();await expect(page.getByRole('button',{name:'Gán quyền SYNTHETIC chủ phòng khám',exact:true})).toBeDisabled();
  await page.getByRole('button',{name:'Gán quyền SYNTHETIC nhân viên',exact:true}).click();const dialog=page.getByRole('dialog');
  await dialog.getByRole('combobox',{name:'Nhóm quyền nhân viên',exact:true}).press('ArrowDown');await expect(page.locator('.ant-select-dropdown:visible').getByText('SYNTHETIC nha sĩ',{exact:true})).toHaveCount(0);
  await expect(page.locator('.ant-select-dropdown:visible').getByText('SYNTHETIC nhóm ngừng',{exact:true})).toHaveCount(0);await dialog.getByRole('combobox',{name:'Nhóm quyền nhân viên',exact:true}).press('Escape');
  await dialog.getByRole('combobox',{name:'Chi nhánh nghiệp vụ',exact:true}).press('ArrowDown');await expect(page.locator('.ant-select-dropdown:visible').getByText('Chi nhánh ngoài membership',{exact:true})).toHaveCount(0);
  await page.locator('.ant-select-dropdown:visible').getByText('Chi nhánh 2',{exact:true}).click();await dialog.getByRole('combobox',{name:'Chi nhánh nghiệp vụ',exact:true}).press('Escape');
  await dialog.getByLabel('Lý do gán nhóm',{exact:true}).fill('SYNTHETIC phân công');await dialog.getByRole('button',{name:'Lưu nhóm cho nhân viên',exact:true}).click();await expect(dialog).not.toBeVisible();
  const saved=state.requests.find(request=>request.path.endsWith('/22/access-group'))!;expect(saved.body).toMatchObject({groupId,expectedVersion:4,groupVersion:3,branchIds:[1,2],reason:'SYNTHETIC phân công'});
});

test('read-only manager can inspect groups without saving or assigning',async({page})=>{
  const state=await prepare(page,true);state.user.roles=['Admin'];state.user.effectiveRoles=['Admin'];await page.goto('/system-settings?tab=access-groups');
  await expect(page.locator('.staff-sidebar').getByRole('link',{name:'Thiết lập hệ thống',exact:true})).toBeVisible();
  await expect(page.getByRole('button',{name:'Thêm nhóm quyền',exact:true})).toHaveCount(0);await page.getByRole('button',{name:'Thiết lập SYNTHETIC lễ tân',exact:true}).click();
  const dialog=page.getByRole('dialog');await expect(dialog.getByRole('checkbox',{name:'Thêm hồ sơ bệnh nhân',exact:true})).toBeDisabled();await expect(dialog.getByRole('button',{name:'Lưu phân quyền',exact:true})).toHaveCount(0);
});

test('unused groups require a reason to deactivate and can be restored',async({page})=>{
  const state=await prepare(page);await page.getByRole('button',{name:'Ngừng nhóm SYNTHETIC nha sĩ',exact:true}).click();const dialog=page.getByRole('dialog');
  await dialog.getByRole('button',{name:'Xác nhận trạng thái',exact:true}).click();await expect(dialog.getByRole('alert')).toContainText('Nhập lý do');expect(state.requests).toHaveLength(0);
  await dialog.getByLabel('Lý do đổi trạng thái',{exact:true}).fill('SYNTHETIC ngừng nhóm');await dialog.getByRole('button',{name:'Xác nhận trạng thái',exact:true}).click();await expect(dialog).not.toBeVisible();
  await page.getByRole('button',{name:'Khôi phục nhóm SYNTHETIC nha sĩ',exact:true}).click();await dialog.getByLabel('Lý do đổi trạng thái',{exact:true}).fill('SYNTHETIC dùng lại');
  await dialog.getByRole('button',{name:'Xác nhận trạng thái',exact:true}).click();await expect(dialog).not.toBeVisible();expect(state.groups[1].active).toBe(true);
  expect(state.requests.map(request=>request.body.expectedVersion)).toEqual([1,2]);
});

test('revoked management permission disables the editor and preserves typed values',async({page})=>{
  const state=await prepare(page);await page.getByRole('button',{name:'Thêm nhóm quyền',exact:true}).click();const dialog=page.getByRole('dialog');
  await dialog.getByLabel('Tên phân quyền',{exact:true}).fill('SYNTHETIC bản đang nhập');state.user.permissions=['settings.read','access-groups.read'];
  const refreshed=page.waitForResponse(response=>response.url().endsWith('/api/auth/me'));
  await page.evaluate(()=>window.dispatchEvent(new Event('focus')));await refreshed;
  await expect(dialog.getByLabel('Tên phân quyền',{exact:true})).toHaveValue('SYNTHETIC bản đang nhập');await expect(dialog.getByLabel('Tên phân quyền',{exact:true})).toBeDisabled();
  await expect(dialog.getByRole('button',{name:'Lưu phân quyền',exact:true})).toHaveCount(0);expect(state.requests).toHaveLength(0);
});

for(const action of ['create','update','archive'])test(`patient interface exposes only the assigned ${action} action`,async({page})=>{
  const state=await prepare(page);state.user.roles=['ClinicAssistant'];state.user.effectiveRoles=['ClinicAssistant'];state.user.permissions=['patients.read','patients.'+action];
  await page.goto('/patients');await expect(page.getByRole('heading',{name:'Bệnh nhân',exact:true})).toBeVisible();
  await expect(page.getByRole('button',{name:'Thêm hồ sơ',exact:true})).toHaveCount(action==='create'?1:0);
  await page.goto('/patients/1');await expect(page.getByRole('heading',{name:'SYNTHETIC bệnh nhân',exact:true})).toBeVisible();
  await expect(page.getByRole('button',{name:'Sửa thông tin',exact:true})).toHaveCount(action==='update'?1:0);await expect(page.getByRole('button',{name:'Ngừng sử dụng',exact:true})).toHaveCount(action==='archive'?1:0);
  await expect(page.getByRole('tab',{name:'Khám',exact:true})).toHaveCount(0);
  await page.goto('/patients/1?tab=examinations');await expect(page.getByRole('tab',{name:'Thông tin cơ bản',exact:true})).toHaveAttribute('aria-selected','true');
});

test('delegated reference catalogs expose only granted catalogs and actions',async({page})=>{
  const state=await prepare(page);state.user.roles=['ClinicAssistant'];state.user.effectiveRoles=['ClinicAssistant'];state.user.permissions=['settings.read','catalog.diagnoses.read'];
  await page.route('**/api/system-settings/settings/catalogs/**',route=>route.fulfill({json:{items:[{id:'synthetic-diagnosis',name:'SYNTHETIC chẩn đoán',kind:'diagnoses',code:null,description:null,active:true,reserved:false,version:1}],total:1,page:1,pageSize:20}}));
  await page.goto('/system-settings?tab=catalogs');await expect(page.getByRole('cell',{name:'SYNTHETIC chẩn đoán',exact:true})).toBeVisible();
  await expect(page.getByRole('menuitem',{name:'Nguồn khách hàng',exact:true})).toHaveCount(0);await expect(page.getByRole('button',{name:'Thêm mới',exact:true})).toHaveCount(0);
  await expect(page.getByRole('button',{name:'Sửa SYNTHETIC chẩn đoán',exact:true})).toHaveCount(0);await expect(page.getByRole('button',{name:'Ngừng dùng',exact:true})).toHaveCount(0);
  state.user.permissions.push('catalog.diagnoses.create');await page.evaluate(()=>window.dispatchEvent(new Event('focus')));
  await expect(page.getByRole('button',{name:'Thêm mới',exact:true})).toBeVisible();await expect(page.getByRole('button',{name:'Sửa SYNTHETIC chẩn đoán',exact:true})).toHaveCount(0);
});
