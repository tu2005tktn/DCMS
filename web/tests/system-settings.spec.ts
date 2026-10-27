import { test, expect, type Page } from '@playwright/test';

type Row = {id:string;name:string;version:number;isDeleted?:boolean;categoryId?:string};
async function prepare(page: Page, initial = true, role = 'Dentist', count = 1) {
  const user = {id:7,userName:'synthetic',fullName:'SYNTHETIC Dentist',accountType:'Staff',roles:[role],effectiveRoles:[role],permissions:[]};
  const state = { categories: (initial ? Array.from({length:count},(_,i)=>({id:`cat-${i+1}`,name:`Nhóm ${String(i+1).padStart(3,'0')}`,version:1})) : []) as Row[],
    materials: [] as Row[], requests: [] as any[], reads:0, failNetwork:false };
  await page.route('**/api/**',async route=>{
    const r=route.request(),url=new URL(r.url()),path=url.pathname,method=r.method();
    const ok=(json:unknown)=>route.fulfill({json});
    if(path==='/api/auth/sign-in')return ok({accessToken:'synthetic',user});
    if(path==='/api/auth/me')return ok(user);
    if(!path.startsWith('/api/system-settings/'))return route.fulfill({status:404,json:{code:'NOT_MOCKED',message:path}});
    const kind=path.split('/')[3], id=path.split('/')[4], rows=kind==='materials'?state.materials:state.categories;
    const dto=(row:Row)=>({...row,...(kind==='materials'?{categoryName:state.categories.find(c=>c.id===row.categoryId)?.name}:{itemCount:state.materials.filter(i=>!i.isDeleted&&i.categoryId===row.id).length})});
    if(method==='GET'){
      state.reads++;
      if(id){const row=rows.find(x=>x.id===id&&!x.isDeleted);return row?ok(dto(row)):route.fulfill({status:404,json:{code:'MSG113',message:'Không tìm thấy.'}});}
      const q=(url.searchParams.get('q')??'').toLowerCase(), group=url.searchParams.get('categoryId'),p=Number(url.searchParams.get('page')??1),size=Number(url.searchParams.get('pageSize')??20);
      const filtered=rows.filter(x=>!x.isDeleted&&x.name.toLowerCase().includes(q)&&(!group||x.categoryId===group));
      return ok({items:filtered.slice((p-1)*size,p*size).map(dto),total:filtered.length,page:p,pageSize:size});
    }
    const body=r.postDataJSON(); state.requests.push({kind,id,method,...body});
    if(state.failNetwork){state.failNetwork=false;return route.abort('failed');}
    const row=rows.find(x=>x.id===id&&!x.isDeleted);
    if(method!=='POST'&&row?.version!==body.expectedVersion)return route.fulfill({status:409,json:{code:'MSG111',message:'MSG111 — Dữ liệu đã thay đổi.'}});
    if(method==='DELETE'){
      if(kind==='material-categories'&&state.materials.some(i=>!i.isDeleted&&i.categoryId===id))return route.fulfill({status:409,json:{code:'CATALOG_CATEGORY_IN_USE',message:'Danh mục đang có dụng cụ/vật liệu. Vui lòng chuyển hoặc xóa các mục trước.'}});
      row!.isDeleted=true;row!.version++;return ok(dto(row!));
    }
    if(rows.some(x=>!x.isDeleted&&x.id!==id&&x.name.trim().toUpperCase()===body.name.trim().toUpperCase()&&(kind!=='materials'||x.categoryId===body.categoryId)))return route.fulfill({status:409,json:{code:'CATALOG_NAME_DUPLICATE',message:'Tên đã tồn tại. Vui lòng nhập tên khác.',field:'name'}});
    if(method==='POST'){
      const created={id:crypto.randomUUID(),name:body.name,categoryId:body.categoryId,version:1};rows.push(created);return ok(dto(created));
    }
    Object.assign(row!,{name:body.name,categoryId:body.categoryId,version:row!.version+1});return ok(dto(row!));
  });
  await page.goto('/sign-in'); await page.getByLabel('Tên đăng nhập',{exact:true}).fill('synthetic');await page.getByLabel('Mật khẩu',{exact:true}).fill('synthetic');
  await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();await page.goto('/system-settings');
  return state;
}
async function choose(page:Page,label:string,name:string){const select=page.getByRole('combobox',{name:label,exact:true});await select.click();await select.fill(name);await page.locator('.ant-select-dropdown:visible').getByText(name,{exact:true}).click();}

test('tạo nhóm trước, thêm/sửa/chuyển/xóa vật liệu và chặn nhóm còn mục',async({page})=>{
  const state=await prepare(page,false);
  await expect(page.getByText('Chưa có danh mục. Hãy tạo danh mục trước khi thêm dụng cụ/vật liệu.')).toBeVisible();
  await page.getByRole('button',{name:'Tạo danh mục',exact:true}).click();
  await page.getByRole('button',{name:'Thêm mới',exact:true}).click();await page.getByLabel('Tên danh mục',{exact:true}).fill('Vật liệu trám');
  expect(state.requests).toHaveLength(0);await page.getByRole('button',{name:'Lưu',exact:true}).click();await expect(page.getByRole('button',{name:'Sửa Vật liệu trám'})).toBeVisible();
  await page.getByRole('tab',{name:'Dụng cụ / Vật liệu',exact:true}).click();await page.getByRole('button',{name:'Thêm mới',exact:true}).click();
  await page.getByLabel('Tên',{exact:true}).fill('Composite');await choose(page,'Thuộc danh mục','Vật liệu trám');await page.getByRole('button',{name:'Lưu',exact:true}).click();
  await expect(page.getByRole('button',{name:'Sửa Composite'})).toBeVisible();
  await page.getByRole('button',{name:'Sửa Composite'}).click();await page.getByLabel('Tên',{exact:true}).fill('Composite A2');await page.getByRole('button',{name:'Lưu',exact:true}).click();
  await expect(page.getByRole('button',{name:'Sửa Composite A2'})).toBeVisible();
  await page.getByRole('tab',{name:'Danh mục',exact:true}).click();await page.getByRole('button',{name:'Xóa Vật liệu trám'}).click();await page.getByRole('button',{name:'Đồng ý',exact:true}).click();
  await expect(page.getByText('Danh mục đang có dụng cụ/vật liệu. Vui lòng chuyển hoặc xóa các mục trước.')).toBeVisible();await page.getByRole('button',{name:'Không đồng ý',exact:true}).click();
  await page.getByRole('tab',{name:'Dụng cụ / Vật liệu',exact:true}).click();await page.getByRole('button',{name:'Xóa Composite A2'}).click();await page.getByRole('button',{name:'Không đồng ý',exact:true}).click();
  await expect(page.getByRole('button',{name:'Sửa Composite A2'})).toBeVisible();await page.getByRole('button',{name:'Xóa Composite A2'}).click();await page.getByRole('button',{name:'Đồng ý',exact:true}).click();
  await expect(page.getByRole('button',{name:'Sửa Composite A2'})).toHaveCount(0);expect(state.materials[0].isDeleted).toBe(true);
  await page.getByRole('tab',{name:'Danh mục',exact:true}).click();await page.getByRole('button',{name:'Xóa Vật liệu trám'}).click();await page.getByRole('button',{name:'Đồng ý',exact:true}).click();await expect(page.getByRole('button',{name:'Sửa Vật liệu trám'})).toHaveCount(0);
});

test('tên trùng và conflict giữ form; đọc phiên bản mới không xóa tên đã nhập',async({page})=>{
  const state=await prepare(page);await page.getByRole('tab',{name:'Danh mục',exact:true}).click();await page.getByRole('button',{name:'Thêm mới',exact:true}).click();
  await page.getByLabel('Tên danh mục',{exact:true}).fill('Nhóm 001');await page.getByRole('button',{name:'Lưu',exact:true}).click();await expect(page.getByRole('alert').filter({hasText:'Tên đã tồn tại.'})).toBeVisible();
  await expect(page.getByLabel('Tên danh mục',{exact:true})).toHaveValue('Nhóm 001');await page.getByRole('button',{name:'Hủy',exact:true}).click();
  await page.getByRole('button',{name:'Sửa Nhóm 001'}).click();await page.getByLabel('Tên danh mục',{exact:true}).fill('Nhóm đã sửa');state.categories[0].version++;
  await page.getByRole('button',{name:'Lưu',exact:true}).click();await expect(page.getByText('MSG111 — Dữ liệu đã thay đổi.')).toBeVisible();
  await page.getByRole('button',{name:'Đọc lại phiên bản, giữ nội dung',exact:true}).click();await expect(page.getByLabel('Tên danh mục',{exact:true})).toHaveValue('Nhóm đã sửa');
  await page.getByRole('button',{name:'Lưu',exact:true}).click();await expect(page.getByRole('button',{name:'Sửa Nhóm đã sửa'})).toBeVisible();
  const puts=state.requests.filter(r=>r.method==='PUT');expect(puts[0].requestKey).not.toBe(puts[1].requestKey);
});

test('mất mạng giữ form và retry cùng key; trường bắt buộc không gửi command',async({page})=>{
  const state=await prepare(page);await page.getByRole('button',{name:'Thêm mới',exact:true}).click();await page.getByRole('button',{name:'Lưu',exact:true}).click();
  await expect(page.getByText('Vui lòng nhập tên.')).toBeVisible();expect(state.requests).toHaveLength(0);
  await page.getByLabel('Tên',{exact:true}).fill('Gương khám');await choose(page,'Thuộc danh mục','Nhóm 001');state.failNetwork=true;
  await page.getByRole('button',{name:'Lưu',exact:true}).click();await expect(page.getByRole('alert')).toBeVisible();await expect(page.getByLabel('Tên',{exact:true})).toHaveValue('Gương khám');
  await page.getByRole('button',{name:'Lưu',exact:true}).click();await expect(page.getByRole('button',{name:'Sửa Gương khám'})).toBeVisible();
  expect(state.requests[0].requestKey).toBe(state.requests[1].requestKey);expect(state.materials).toHaveLength(1);
});

test('dropdown tải hết nhóm >100, tìm/lọc/phân trang và responsive',async({page})=>{
  const state=await prepare(page,true,'Dentist',105);await page.getByRole('button',{name:'Thêm mới',exact:true}).click();await page.getByLabel('Tên',{exact:true}).fill('Dụng cụ cuối danh mục');
  await choose(page,'Thuộc danh mục','Nhóm 105');await page.getByRole('button',{name:'Lưu',exact:true}).click();await expect(page.getByRole('button',{name:'Sửa Dụng cụ cuối danh mục'})).toBeVisible();
  expect(state.materials[0].categoryId).toBe('cat-105');await choose(page,'Lọc danh mục','Nhóm 105');
  await page.getByLabel('Tìm kiếm theo tên',{exact:true}).fill('cuối');await page.getByLabel('Tìm kiếm theo tên',{exact:true}).press('Enter');await expect(page.getByRole('button',{name:'Sửa Dụng cụ cuối danh mục'})).toBeVisible();
  await page.getByRole('tab',{name:'Danh mục',exact:true}).click();await page.getByTitle('2',{exact:true}).click();await expect(page.getByRole('button',{name:'Sửa Nhóm 021'})).toBeVisible();
  for(const width of [390,768,1440]){await page.setViewportSize({width,height:950});expect(await page.evaluate(()=>document.documentElement.scrollWidth)).toBeLessThanOrEqual(width+1);await page.screenshot({path:`test-results/system-settings-${width}.png`,fullPage:true});}
});

test('role khác bị chặn URL trực tiếp và không tải API danh mục',async({page})=>{
  const state=await prepare(page,true,'ClinicAssistant');await expect(page.getByText('Bạn không có quyền thực hiện thao tác này.')).toBeVisible();
  await expect(page.getByRole('link',{name:'Thiết lập hệ thống',exact:true})).toHaveCount(0);expect(state.reads).toBe(0);
});
