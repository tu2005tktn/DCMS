import { test, expect, type Page } from '@playwright/test';

async function mock(page: Page, permissions = ['patients.read','patients.manage','reception.read','finance.read','services.read','accounts.manage']) {
  const user = { id:1,fullName:'Nhân viên mẫu',userName:'synthetic',accountType:'Staff',roles:['ClinicOwner'],effectiveRoles:['ClinicOwner','Dentist'],permissions };
  const queries: string[] = [];
  await page.addInitScript(()=>sessionStorage.setItem('dcms.web.access-token','synthetic'));
  await page.route('**/api/**', async route=> {
    const url=new URL(route.request().url());
    if(url.pathname==='/api/auth/me') return route.fulfill({json:user});
    if(url.pathname==='/api/auth/session/idle') return route.fulfill({json:{deadlineAt:new Date(Date.now()+600000).toISOString(),serverNow:new Date().toISOString(),activityVersion:1}});
    if(url.pathname==='/api/auth/sign-out') return route.fulfill({status:503,json:{code:'MSG144',message:'Không thể đăng xuất lúc này.'}});
    if(url.pathname==='/api/patients') {
      queries.push(url.search);
      return route.fulfill({json:{total:6,page:Number(url.searchParams.get('page')??1),pageSize:20,items:Array.from({length:6},(_,i)=>({id:i+1,patientCode:`BN-M00${i+1}`,fullName:`Hồ sơ mẫu 0${i+1}`,birthDate:i===4?null:'1995-03-12',gender:'FEMALE',phone:null,lastVisitAt:null,isArchived:i===5,version:1}))}});
    }
    return route.fulfill({json:[]});
  });
  return queries;
}

test('staff UI giữ quyền, filter và phân trang; desktop/mobile không tràn',async({page})=>{
  const queries=await mock(page);await page.goto('/patients');
  await expect(page.getByRole('heading',{name:'Bệnh nhân',exact:true})).toBeVisible();
  await expect(page.getByRole('button',{name:'Thêm hồ sơ',exact:true})).toBeVisible();
  await expect(page.getByRole('row').filter({hasText:'BN-M001'})).toContainText('Hồ sơ mẫu 01');
  await page.getByPlaceholder('Mã bệnh nhân, họ tên (có hoặc không dấu), SĐT').fill('mẫu');
  await page.getByRole('button',{name:'Tìm kiếm',exact:true}).click();
  await expect.poll(()=>queries.some(q=>new URLSearchParams(q).get('keyword')==='mẫu')).toBe(true);
  for(const width of [1440,768,390]) {
    await page.setViewportSize({width,height:1000});
    await expect(page.locator('.staff-sidebar')).toBeVisible();
    const sidebar = await page.locator('.staff-sidebar').boundingBox();
    const content = await page.locator('.staff-main').boundingBox();
    expect(content!.x).toBeGreaterThanOrEqual(sidebar!.width);
    await expect(page.locator('.staff-sidebar').getByRole('link',{name:'Bệnh nhân',exact:true})).toBeVisible();
    await expect.poll(()=>page.evaluate(()=>document.documentElement.scrollWidth)).toBeLessThanOrEqual(width+1);
    await page.screenshot({path:`test-results/staff-workspace-${width}.png`,fullPage:true});
    if(width<992) {
      await page.getByRole('button',{name:'Mở menu nhân viên'}).click();
      await expect(page.getByRole('dialog')).toBeVisible();
      await expect(page.getByRole('dialog').getByRole('link',{name:'Bệnh nhân',exact:true})).toBeVisible();
      await page.keyboard.press('Escape');await expect(page.getByRole('dialog')).not.toBeVisible();
    }
  }
  await page.getByRole('button',{name:'Mở menu nhân viên'}).click();
  await page.getByRole('dialog').getByRole('link',{name:'Tài khoản của tôi',exact:true}).click();
  await expect(page.getByRole('heading',{name:'Tài khoản của tôi',exact:true})).toBeVisible();
  await expect(page.getByRole('dialog')).not.toBeVisible();
  await page.evaluate(()=>window.scrollTo(0,document.body.scrollHeight));
  expect((await page.locator('.staff-sidebar').boundingBox())!.y).toBe(0);
});

test('quyền đọc không mở quyền thêm, menu không lộ chức năng và URL bị chặn',async({page})=>{
  await mock(page,['patients.read']);await page.goto('/patients');
  await expect(page.getByRole('heading',{name:'Bệnh nhân',exact:true})).toBeVisible();
  await expect(page.getByRole('button',{name:'Thêm hồ sơ',exact:true})).toHaveCount(0);
  await expect(page.getByRole('link',{name:'Quản lý tài khoản',exact:true})).toHaveCount(0);
  await page.goto('/accounts');await expect(page.getByText('Bạn không có quyền thực hiện thao tác này.',{exact:true})).toBeVisible();
});

test('deep link chọn đúng menu, lỗi logout giữ trang và reduced motion',async({page})=>{
  await mock(page);await page.goto('/patients');
  await expect(page.locator('.staff-sidebar .ant-menu-item-selected')).toContainText('Bệnh nhân');
  await page.emulateMedia({reducedMotion:'reduce'});
  await page.getByRole('button',{name:'Đăng xuất',exact:true}).click();
  await expect(page.getByText('Không thể đăng xuất lúc này.',{exact:true})).toBeVisible();
  await expect(page.getByRole('heading',{name:'Bệnh nhân',exact:true})).toBeVisible();
});

test('sign-in không có staff shell',async({page})=>{
  await page.goto('/sign-in');await expect(page.getByLabel('Tên đăng nhập',{exact:true})).toBeVisible();
  await expect(page.locator('.staff-sidebar')).toHaveCount(0);
});

test('portal bệnh nhân giữ giao diện riêng tại 390/768/1440',async({page})=>{
  await page.route('**/api/**',route=>route.fulfill({json:{enabled:false}}));
  for(const width of [390,768,1440]) {
    await page.setViewportSize({width,height:950});await page.goto('/customer/sign-in');
    await expect(page.getByRole('heading',{name:'Đăng nhập',exact:true})).toBeVisible();
    await expect(page.locator('.staff-sidebar,.staff-header')).toHaveCount(0);
    await expect.poll(()=>page.evaluate(()=>document.documentElement.scrollWidth)).toBeLessThanOrEqual(width+1);
  }
});

