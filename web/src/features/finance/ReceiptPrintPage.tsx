import { Alert, Button, Card, Descriptions, Spin, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../services/api';
import { money } from './types';

type Receipt = { number?:string; patientName:string; branchName:string; method:string; amountVnd:number;
  previouslyPaidVnd:number; remainingVnd:number; payer:string; collector:string; note?:string; issuedAt:string };

export default function ReceiptPrintPage() {
  const { id } = useParams(); const [receipt,setReceipt]=useState<Receipt>(); const [error,setError]=useState<string>();
  useEffect(()=>{ if(!id)return; api<Receipt>(`/api/finance/payments/${id}/print`).then(setReceipt).catch(e=>setError((e as Error).message)); },[id]);
  async function print(){ if(!id)return; try { await api(`/api/finance/payments/${id}/print`,{method:'POST',body:JSON.stringify({requestKey:crypto.randomUUID()})}); window.print(); } catch(e){setError((e as Error).message);} }
  if(error)return <main className="finance-page"><Alert type="error" message={error}/></main>;
  if(!receipt)return <Spin/>;
  return <main className="finance-page receipt-print"><Card><Typography.Title level={2}>PHIẾU THU {receipt.number??''}</Typography.Title>
    <Typography.Title level={4}>{receipt.branchName}</Typography.Title>
    <Descriptions column={1} items={[{key:'patient',label:'Bệnh nhân',children:receipt.patientName},{key:'payer',label:'Người nộp',children:receipt.payer},
      {key:'method',label:'Phương thức',children:receipt.method==='Cash'?'Tiền mặt':'Chuyển khoản'}, {key:'amount',label:'Số tiền thực nhận',children:money(receipt.amountVnd)},
      {key:'paid',label:'Đã trả trước phiếu này',children:money(receipt.previouslyPaidVnd)},{key:'remaining',label:'Còn lại tại thời điểm phiếu',children:money(receipt.remainingVnd)},
      {key:'collector',label:'Người thu',children:receipt.collector},{key:'issued',label:'Thời điểm phát hành',children:new Date(receipt.issuedAt).toLocaleString('vi-VN')},
      ...(receipt.note?[{key:'note',label:'Ghi chú',children:receipt.note}]:[])]}/>
    <Button type="primary" onClick={()=>void print()}>In phiếu</Button> <Link to="/payments">Quay lại thanh toán</Link>
  </Card></main>;
}
