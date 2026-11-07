import { expect, test, type Page } from '@playwright/test';

const id='11111111-1111-4111-8111-111111111111';
const receipt={receiptId:id,preview:false,number:'PT-1-00000012',patientName:'SYNTHETIC bệnh nhân',branchName:'Chi nhánh thử nghiệm',
  method:'Cash',amountVnd:50000,previouslyPaidVnd:0,remainingVnd:150000,payer:'SYNTHETIC người nộp khác bệnh nhân',
  collector:'SYNTHETIC người đã thu',note:'SYNTHETIC ghi chú',issuedAt:'2026-11-02T08:00:00Z',settingsVersion:1,
  settings:{internalUseNote:false,showReceiptNumber:true,patientAddress:false,patientPhone:false,paymentMethod:true,
    dentistAssistant:false,totalFee:true,discount:true,previouslyPaid:true,remaining:true,patientTotalOutstanding:false,
    signatureLines:false,defaultPayerToPatient:false,defaultCollectorToActor:false,paymentNote:true}};

async function prepare(page:Page, initial=structuredClone(receipt)){
  const state={latest:structuredClone(initial),commands:[] as {requestKey:string}[],failNext:false};
  await page.addInitScript(()=>{
    sessionStorage.setItem('dcms.web.access-token','synthetic');
    Object.assign(window,{receiptPrintCalls:[]});
    window.print=()=>{(window as any).receiptPrintCalls.push(document.querySelector('.receipt-print')?.textContent);};
  });
  await page.route('**/api/**',async route=>{
    const path=new URL(route.request().url()).pathname;
    if(path==='/api/auth/me')return route.fulfill({json:{id:1,accountType:'Staff',userName:'synthetic',fullName:'SYNTHETIC người đang xem',roles:['ClinicAssistant'],effectiveRoles:['ClinicAssistant'],permissions:['finance.read','finance.receipts.print']}});
    if(path==='/api/auth/session/idle')return route.fulfill({json:{deadlineAt:new Date(Date.now()+1200000).toISOString(),serverNow:new Date().toISOString(),activityVersion:1}});
    if(path===`/api/finance/payments/${id}/print`){
      if(route.request().method()==='GET')return route.fulfill({json:initial});
      state.commands.push(route.request().postDataJSON());
      if(state.failNext){state.failNext=false;return route.abort('failed');}
      return route.fulfill({json:state.latest});
    }
    return route.fulfill({json:[]});
  });
  await page.goto(`/payments/${id}/print`);
  await expect(page.getByRole('heading',{name:/PHIẾU THU/})).toBeVisible();
  return state;
}

test('hidden receipt fields stay absent on screen and paper without changing receipt facts',async({page})=>{
  const data=structuredClone(receipt);Object.assign(data.settings,{showReceiptNumber:false,paymentMethod:false,previouslyPaid:false,remaining:false,paymentNote:false,defaultPayerToPatient:true,defaultCollectorToActor:true});
  await prepare(page,data);
  await expect(page.getByRole('heading',{name:'PHIẾU THU',exact:true})).toBeVisible();
  for(const label of ['Phương thức','Đã trả trước phiếu này','Còn lại tại thời điểm phiếu','Ghi chú'])await expect(page.getByText(label,{exact:true})).toHaveCount(0);
  for(const text of ['Tiền mặt',receipt.number,receipt.note,'Phiếu thu dùng nội bộ'])await expect(page.getByText(text,{exact:true})).toHaveCount(0);
  await expect(page.getByLabel('Khu vực ký tên')).toHaveCount(0);
  await expect(page.getByText(receipt.payer,{exact:true})).toBeVisible();
  await expect(page.getByText(receipt.collector,{exact:true})).toBeVisible();
  await expect(page.getByText('Số tiền thực nhận',{exact:true})).toBeVisible();
  await page.emulateMedia({media:'print'});
  await expect(page.getByRole('button',{name:'In phiếu',exact:true})).toBeHidden();
  await expect(page.getByRole('link',{name:'Quay lại thanh toán',exact:true})).toBeHidden();
  await expect(page.getByText(receipt.payer,{exact:true})).toBeVisible();
});

test('enabled fields include transfer, zero paid, internal note and signatures',async({page})=>{
  const data=structuredClone(receipt);data.method='Transfer';Object.assign(data.settings,{internalUseNote:true,signatureLines:true});
  await prepare(page,data);
  await expect(page.getByRole('heading',{name:`PHIẾU THU ${receipt.number}`,exact:true})).toBeVisible();
  await expect(page.getByText('Chuyển khoản',{exact:true})).toBeVisible();
  for(const label of ['Phương thức','Đã trả trước phiếu này','Còn lại tại thời điểm phiếu','Ghi chú','Phiếu thu dùng nội bộ'])await expect(page.getByText(label,{exact:true})).toBeVisible();
  await expect(page.getByText(receipt.note,{exact:true})).toBeVisible();
  await expect(page.getByLabel('Khu vực ký tên')).toBeVisible();
  await expect(page.getByLabel('Khu vực ký tên').getByText('Người nộp tiền',{exact:true})).toBeVisible();
});

test('print retry retains request key and commits latest settings before native print',async({page})=>{
  const state=await prepare(page);state.failNext=true;
  Object.assign(state.latest.settings,{paymentMethod:false,paymentNote:false,remaining:false,showReceiptNumber:false});state.latest.settingsVersion=2;
  await page.getByRole('button',{name:'In phiếu',exact:true}).click();
  await expect(page.getByRole('alert')).toBeVisible();
  await expect(page.getByText(receipt.payer,{exact:true})).toBeVisible();
  expect(await page.evaluate(()=>(window as any).receiptPrintCalls)).toEqual([]);
  await page.getByRole('button',{name:'In phiếu',exact:true}).click();
  await expect.poll(()=>page.evaluate(()=>(window as any).receiptPrintCalls.length)).toBe(1);
  expect(state.commands).toHaveLength(2);expect(state.commands[0].requestKey).toBe(state.commands[1].requestKey);
  const printed=(await page.evaluate(()=>(window as any).receiptPrintCalls))[0] as string;
  for(const hidden of ['Phương thức','Tiền mặt',receipt.note,'Còn lại tại thời điểm phiếu',receipt.number])expect(printed).not.toContain(hidden);
  expect(printed).toContain(receipt.payer);expect(printed).toContain(receipt.collector);
  await expect(page.getByRole('alert')).toHaveCount(0);
});
