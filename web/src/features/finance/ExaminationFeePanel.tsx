import {Alert,Button,Card,Input,InputNumber,Select,Space,Spin,Tag,Typography} from 'antd';
import {useEffect,useRef,useState} from 'react';
import {Link} from 'react-router-dom';
import {api} from '../../services/api';
import {money} from './types';
import type {ExaminationFee,ExaminationFeeChoice} from './examinationFeeTypes';
export default function ExaminationFeePanel({visitId,completion=false,disabled=false,onChoice,onChanged}:{visitId:string;completion?:boolean;disabled?:boolean;onChoice?:(choice:ExaminationFeeChoice|undefined)=>void;onChanged?:()=>Promise<void>}){
 const [fee,setFee]=useState<ExaminationFee>(),[serviceId,setServiceId]=useState<number>(),[amount,setAmount]=useState<number|null>(null),[reason,setReason]=useState(''),[error,setError]=useState<string>(),[busy,setBusy]=useState(false);
 const pending=useRef<{body:string;key:string}|undefined>(undefined);const path=`/api/finance/visits/${visitId}/examination-fee`;
 async function load(){try{const f=await api<ExaminationFee>(path);setFee(f);const id=f.services.some(x=>x.id===f.serviceId)?f.serviceId!:f.defaultServiceId??undefined;setServiceId(id);setAmount(f.state==='Posted'?f.amountVnd:f.services.find(x=>x.id===id)?.priceVnd??null);setReason(f.reason??'');setError(undefined);}catch(e){setError((e as Error).message);}}
 useEffect(()=>{onChoice?.(undefined);void load();},[visitId]);
 const service=fee?.services.find(x=>x.id===serviceId),canOverride=fee?.allowedActions.includes('override')??false;
 const needsReason=!!fee&&(fee.legacy||amount!=null&&amount!==service?.priceVnd);
 useEffect(()=>{if(completion&&fee)onChoice?.({serviceId:serviceId??null,expectedPriceVersion:service?.version??null,amountVnd:amount,reason:reason.trim()||null});},[completion,fee,serviceId,amount,reason]);
 async function save(){if(!fee||busy)return;if(serviceId==null||amount==null){setError('Chọn dịch vụ và nhập phí khám, hoặc nhập 0 nếu miễn phí.');return;}if(needsReason&&!reason.trim()){setError('Vui lòng ghi lý do chốt phí hoặc miễn phí.');return;}
  const body={patientId:fee.patientId,expectedVersion:fee.version,fee:{serviceId,expectedPriceVersion:service?.version,amountVnd:amount,reason:reason.trim()||null}};const fingerprint=JSON.stringify(body);if(pending.current?.body!==fingerprint)pending.current={body:fingerprint,key:crypto.randomUUID()};setBusy(true);setError(undefined);
  try{setFee(await api<ExaminationFee>(path,{method:'POST',body:JSON.stringify({...body,requestKey:pending.current.key})}));pending.current=undefined;await onChanged?.();}catch(e){setError((e as Error).message);}finally{setBusy(false);}
 }
 return <Card size="small" title="Phí khám" style={{margin:'16px 0'}}>
 {error&&<Alert role="alert" type="error" message={error} action={<Button disabled={busy||disabled} onClick={()=>void load()}>Tải lại phí khám</Button>}/>}
 {!completion&&fee?.state==='Posted'&&<Typography.Paragraph type="secondary">Phí khám đã chốt, không cần nhập lại. Chọn Thu tiền để ghi nhận số tiền thực nhận.</Typography.Paragraph>}
 {!fee?!error&&<Spin/>:!fee.eligible?<Typography.Text type="secondary">Phí khám được ghi khi kết thúc lượt khám đã có phiếu xác nhận.</Typography.Text>:fee.state==='Posted'?<><Space wrap><Typography.Text strong>{fee.serviceName}</Typography.Text><Tag color={fee.amountVnd===0?'green':'blue'}>{fee.amountVnd===0?'Miễn phí':money(fee.amountVnd)}</Tag><Tag>Đã ghi phí</Tag></Space>{fee.reason&&<p>Căn cứ: {fee.reason}</p>}</>:<>
 {!completion&&<Alert type="warning" message={fee.legacy?'Lượt khám cũ chưa có phí khám được ghi nhận. Chỉ bổ sung khi đã kiểm tra căn cứ.':'Phí khám đang chờ xác định, chưa nằm trong tổng phí đã ghi.'}/>}
 {completion&&<Typography.Paragraph type="secondary">Phí sẽ được ghi khi kết thúc khám, chưa phải đã thu tiền. Để trống số tiền nếu cần xác định phí sau tại quầy.</Typography.Paragraph>}
 {(completion||fee.allowedActions.includes('set'))?<>
 {!fee.services.length&&<Alert type="info" message={<>Chưa cấu hình dịch vụ khám. <Link to="/clinical-services">Mở danh mục và giá khám</Link>. Vẫn có thể kết thúc lâm sàng để xác định phí sau.</>}/>}
 {fee.services.length>1&&<Select aria-label="Dịch vụ khám" style={{width:'100%',margin:'12px 0'}} value={serviceId} disabled={busy||disabled} options={fee.services.map(s=>({value:s.id,label:s.name}))} onChange={id=>{setServiceId(id);setAmount(fee.services.find(x=>x.id===id)?.priceVnd??null);}}/>}
 {fee.services.length===1&&<Typography.Paragraph>{service?.name??fee.services[0].name} · Giá bảng: {money(service?.priceVnd)}</Typography.Paragraph>}
 <Space wrap align="start"><div><label htmlFor="examination-fee-amount">Phí khám (₫)</label><br/><InputNumber id="examination-fee-amount" aria-label="Phí khám" value={amount} min={0} max={1e12} precision={0} readOnly={!canOverride} disabled={busy||disabled} onChange={value=>setAmount(value)} style={{width:190}}/></div>
 {(canOverride||fee.legacy)&&<div><label htmlFor="examination-fee-reason">{needsReason?'Lý do / căn cứ (bắt buộc)':'Lý do / căn cứ (nếu có)'}</label><br/><Input.TextArea id="examination-fee-reason" aria-label="Lý do phí khám" rows={2} value={reason} maxLength={1000} disabled={busy||disabled} onChange={e=>setReason(e.target.value)} style={{width:300,maxWidth:'100%'}}/></div>}</Space>
 {amount==null&&<Typography.Paragraph type="secondary">Chưa xác định phí khám. Chưa thể thu khoản này; các phí cận lâm sàng đã ghi vẫn thu được.</Typography.Paragraph>}
 {amount===0&&<Typography.Paragraph>Miễn phí khám{needsReason?' — cần căn cứ.':' theo bảng giá.'}</Typography.Paragraph>}
 {!canOverride&&<Typography.Paragraph type="secondary">Áp dụng đúng giá bảng. Bác sĩ phụ trách hoặc chủ phòng khám có quyền mới được đổi giá / miễn phí khác bảng.</Typography.Paragraph>}
 {!completion&&<Button type="primary" disabled={disabled||amount==null||serviceId==null} loading={busy} style={{marginTop:12}} onClick={()=>void save()}>Xác nhận phí khám</Button>}
 </>:<Typography.Paragraph>Chờ kết thúc khám hoặc người có quyền xác định phí. <Link to="/clinical-services">Xem danh mục giá</Link></Typography.Paragraph>}
 </>}
 </Card>;
}
