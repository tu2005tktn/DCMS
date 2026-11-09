import { Alert, Button, Card, Descriptions, Spin, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../services/api';
import { money } from './types';
import type { ReceiptPrint } from '../system-settings/generalSettings';
import './receiptPrint.css';

type Receipt = { number?:string; patientName:string; branchName:string; method:string; amountVnd:number;
  previouslyPaidVnd:number; remainingVnd:number; payer:string; collector:string; note?:string; issuedAt:string;
  settings:ReceiptPrint };

export default function ReceiptPrintPage() {
  const { id } = useParams(); const [receipt,setReceipt]=useState<Receipt>(); const [error,setError]=useState<string>();
  const [printing,setPrinting]=useState(false);
  const printCommand=useRef<{id:string;key:string}|undefined>(undefined);
  const printInFlight=useRef(false);
  const currentId=useRef(id);
  useEffect(()=>{
    let active=true; currentId.current=id; setReceipt(undefined); setError(undefined); printCommand.current=undefined;
    if(id)void api<Receipt>(`/api/finance/payments/${id}/print`)
      .then(value=>{if(active)setReceipt(value);}).catch(e=>{if(active)setError((e as Error).message);});
    return ()=>{active=false;currentId.current=undefined;};
  },[id]);
  async function print(){
    if(!id||printInFlight.current)return;
    printInFlight.current=true; setPrinting(true); setError(undefined);
    if(printCommand.current?.id!==id)printCommand.current={id,key:crypto.randomUUID()};
    try{
      const latest=await api<Receipt>(`/api/finance/payments/${id}/print`,{method:'POST',body:JSON.stringify({requestKey:printCommand.current.key})});
      printCommand.current=undefined;
      if(currentId.current!==id)return;
      // The POST returns the current print settings. Commit them before the native print snapshot.
      flushSync(()=>setReceipt(latest));
      window.print();
    }catch(e){if(currentId.current===id)setError((e as Error).message);}
    finally{printInFlight.current=false;setPrinting(false);}
  }
  if(!receipt)return error?<main className="finance-page"><Alert type="error" message={error}/></main>:<Spin/>;
  const settings=receipt.settings;
  return <main className="finance-page receipt-print">
    {error&&<Alert className="receipt-print-actions" type="error" message={error}/>}
    <Card><Typography.Title level={2}>PHIẾU THU{settings.showReceiptNumber&&receipt.number?` ${receipt.number}`:''}</Typography.Title>
    <Typography.Title level={4}>{receipt.branchName}</Typography.Title>
    {settings.internalUseNote&&<Typography.Paragraph>Phiếu thu dùng nội bộ</Typography.Paragraph>}
    <Descriptions column={1} items={[{key:'patient',label:'Bệnh nhân',children:receipt.patientName},{key:'payer',label:'Người nộp',children:receipt.payer},
      ...(settings.paymentMethod?[{key:'method',label:'Phương thức',children:receipt.method==='Cash'?'Tiền mặt':'Chuyển khoản'}]:[]),
      {key:'amount',label:'Số tiền thực nhận',children:money(receipt.amountVnd)},
      ...(settings.previouslyPaid?[{key:'paid',label:'Đã trả trước phiếu này',children:money(receipt.previouslyPaidVnd)}]:[]),
      ...(settings.remaining?[{key:'remaining',label:'Còn lại tại thời điểm phiếu',children:money(receipt.remainingVnd)}]:[]),
      {key:'collector',label:'Người thu',children:receipt.collector},{key:'issued',label:'Thời điểm phát hành',children:new Date(receipt.issuedAt).toLocaleString('vi-VN')},
      ...(settings.paymentNote&&receipt.note?[{key:'note',label:'Ghi chú',children:receipt.note}]:[])]}/>
    {settings.signatureLines&&<div className="receipt-print-signatures" aria-label="Khu vực ký tên">
      <div><strong>Người nộp tiền</strong><p>(Ký, ghi rõ họ tên)</p></div>
      <div><strong>Người thu tiền</strong><p>(Ký, ghi rõ họ tên)</p></div>
    </div>}
    <div className="receipt-print-actions"><Button type="primary" aria-label="In phiếu" loading={printing} disabled={printing} onClick={()=>void print()}>In phiếu</Button> <Link to="/patients">Về danh sách bệnh nhân</Link></div>
  </Card></main>;
}
