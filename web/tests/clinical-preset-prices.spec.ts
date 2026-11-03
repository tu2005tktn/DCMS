import { expect, test, type Page } from '@playwright/test';

const visitId='11111111-1111-4111-8111-111111111111',orderId='22222222-2222-4222-8222-222222222222';
async function prepare(page:Page,price:number|null=200000){
  const state={services:[{id:2,branchId:1,name:'SYNTHETIC CBC',kind:'Lab',unit:'Mẫu',priceVnd:price,version:1,active:true},{id:3,branchId:1,name:'SYNTHETIC dịch vụ cũ',kind:'Lab',unit:'Mẫu',priceVnd:null,version:1,active:true}],
    examination:[{id:1,branchId:1,name:'Khám',unit:'Lần',priceVnd:null as number|null,version:1,active:true}],writes:[] as {path:string;body:any}[]};
  const permissions=['services.read','services.manage','finance.read','finance.quotes.manage'];
  const billing={investigationId:orderId,visitId,visitVersion:1,orderVersion:1,mode:null,serviceId:null,serviceName:null,quantity:1,unitPriceVnd:null,amountVnd:null,evidence:null,performedAt:null,providerName:null,allowedActions:['quote']};
  await page.addInitScript(()=>sessionStorage.setItem('dcms.web.access-token','synthetic'));
  await page.route('**/api/**',async route=>{
    const r=route.request(),path=new URL(r.url()).pathname,ok=(json:unknown)=>route.fulfill({json});
    if(path==='/api/auth/me')return ok({id:1,accountType:'Staff',userName:'synthetic',fullName:'SYNTHETIC Owner',roles:['ClinicOwner'],effectiveRoles:['ClinicOwner','Dentist'],permissions});
    if(path==='/api/auth/session/idle')return ok({serverNow:new Date().toISOString(),deadlineAt:new Date(Date.now()+1200000).toISOString(),activityVersion:1});
    if(path==='/api/finance/capabilities')return ok({permissions,branches:[{id:1,name:'CN thử nghiệm'}]});
    if(path==='/api/finance/services'){
      if(r.method()==='GET')return ok(state.services);
      const body=r.postDataJSON();state.writes.push({path,body});const created={...body,id:4,priceVnd:body.priceVnd,version:1};state.services.push(created);return ok(created);
    }
    if(path==='/api/finance/examination-services')return ok(state.examination);
    if(path==='/api/finance/examination-services/1'){
      const body=r.postDataJSON();state.writes.push({path,body});Object.assign(state.examination[0],body,{version:2});return ok(state.examination[0]);
    }
    if(path===`/api/finance/visits/${visitId}`)return ok({visitId,patientId:1,patientName:'SYNTHETIC bệnh nhân',branchId:1,branchName:'CN thử nghiệm',accountVersion:1,totalVnd:0,paidVnd:0,remainingVnd:0,charges:[],payments:[],allowedActions:[],investigations:[{id:orderId,name:'SYNTHETIC CBC',kind:'Lab',state:'Ordered'}]});
    if(path.endsWith(`/investigations/${orderId}/billing`))return ok(billing);
    if(path.endsWith(`/investigations/${orderId}/billing/quote`)){
      const body=r.postDataJSON();state.writes.push({path,body});return ok({...billing,mode:'Clinic',serviceId:2,serviceName:'SYNTHETIC CBC',unit:'Mẫu',unitPriceVnd:body.agreedUnitPriceVnd,amountVnd:body.agreedUnitPriceVnd,allowedActions:['quote','perform']});
    }
    return route.fulfill({status:404,json:{code:'MSG113',message:'Không tìm thấy.'}});
  });
  return state;
}

test('catalog requires one concrete price for both exam and investigations and labels explicit free',async({page})=>{
  const state=await prepare(page);await page.goto('/clinical-services');await expect(page.getByText('Chưa thiết lập giá',{exact:true})).toHaveCount(2);
  await page.getByRole('button',{name:'Sửa giá khám',exact:true}).click();const exam=page.getByRole('dialog',{name:'Cấu hình phí khám',exact:true});
  await exam.getByRole('button',{name:'Lưu giá khám',exact:true}).click();await expect(exam.getByText('Nhập giá khám.',{exact:true})).toBeVisible();expect(state.writes).toHaveLength(0);
  await exam.getByLabel('Giá khám theo bảng',{exact:true}).fill('0');await exam.getByRole('button',{name:'Lưu giá khám',exact:true}).click();await expect(exam).not.toBeVisible();await expect(page.getByText('Miễn phí (0 ₫)',{exact:true})).toBeVisible();expect(state.writes[0].body.priceVnd).toBe(0);
  await page.getByRole('button',{name:'Thêm dịch vụ',exact:true}).click();const modal=page.getByRole('dialog',{name:'Thêm dịch vụ',exact:true});await modal.getByLabel('Tên dịch vụ',{exact:true}).fill('SYNTHETIC Phim mới');
  await modal.getByRole('button',{name:'Lưu dịch vụ',exact:true}).click();await expect(modal.getByText('Nhập đơn giá cận lâm sàng.',{exact:true})).toBeVisible();expect(state.writes).toHaveLength(1);
  await modal.getByLabel('Đơn giá cận lâm sàng',{exact:true}).fill('175000');await modal.getByRole('button',{name:'Lưu dịch vụ',exact:true}).click();await expect(modal).not.toBeVisible();await expect(page.getByRole('row').filter({hasText:'SYNTHETIC Phim mới'})).toContainText('175.000');expect(state.writes[1].body.priceVnd).toBe(175000);
});

for(const price of [0,200000])test(`sole configured service auto-fills ${price} and legacy missing price is disabled`,async({page})=>{
  const state=await prepare(page,price);await page.goto(`/visits/${visitId}/payments`);await page.getByRole('button',{name:'Dịch vụ và phí',exact:true}).click();const drawer=page.getByRole('dialog');
  await expect(drawer.getByLabel('Đơn giá bệnh nhân',{exact:true})).toHaveValue(String(price));await expect(drawer.getByText('Giá bảng đã thiết lập:',{exact:false})).toBeVisible();
  await drawer.getByRole('combobox',{name:'Dịch vụ tính phí',exact:true}).press('ArrowDown');await expect(page.locator('.ant-select-dropdown:visible .ant-select-item-option').filter({hasText:'SYNTHETIC dịch vụ cũ'})).toHaveClass(/disabled/);await page.keyboard.press('Escape');
  await drawer.getByRole('button',{name:'Lưu hình thức và phí',exact:true}).click();await expect(drawer.getByRole('button',{name:'Xác nhận đã thực hiện — ghi phí',exact:true})).toBeVisible();expect(state.writes).toHaveLength(1);expect(state.writes[0].body).toMatchObject({serviceId:2,agreedUnitPriceVnd:price,evidence:null});
});

test('patient price differing from visible catalog still requires an explicit reason',async({page})=>{
  const state=await prepare(page);await page.goto(`/visits/${visitId}/payments`);await page.getByRole('button',{name:'Dịch vụ và phí',exact:true}).click();const drawer=page.getByRole('dialog');
  await expect(drawer.getByLabel('Đơn giá bệnh nhân',{exact:true})).toHaveValue('200000');await drawer.getByLabel('Đơn giá bệnh nhân',{exact:true}).fill('180000');await drawer.getByRole('button',{name:'Lưu hình thức và phí',exact:true}).click();
  await expect(drawer.getByText('Ghi căn cứ áp dụng giá khác bảng.',{exact:true})).toBeVisible();expect(state.writes).toHaveLength(0);
  await drawer.getByLabel('Căn cứ giá thỏa thuận',{exact:true}).fill('SYNTHETIC căn cứ điều chỉnh');await drawer.getByRole('button',{name:'Lưu hình thức và phí',exact:true}).click();await expect(drawer.getByRole('button',{name:'Xác nhận đã thực hiện — ghi phí',exact:true})).toBeVisible();expect(state.writes[0].body.agreedUnitPriceVnd).toBe(180000);
});
