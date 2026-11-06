import {Alert,Button,Descriptions,Drawer,Form,Input,InputNumber,Select,Space,Spin,Typography} from 'antd';
import {useEffect,useRef,useState} from 'react';
import {Link} from 'react-router-dom';
import {api} from '../../services/api';
import type {Workspace} from '../examinations/types';
import {type Billing,type Service,type Provider,type Capabilities,money,modes} from './types';
export default function InvestigationBillingDrawer({workspace,order,onClose,onChanged}:{workspace:{visit:Pick<Workspace['visit'],'visitId'|'patientId'|'branchId'>};order:{id:string;name:string;kind:string};onClose:()=>void;onChanged:()=>Promise<void>}){
 const [billing,setBilling]=useState<Billing>(),[services,setServices]=useState<Service[]>([]),[providers,setProviders]=useState<Provider[]>([]),[cap,setCap]=useState<Capabilities>(),[error,setError]=useState<string>(),[busy,setBusy]=useState(false),[dirty,setDirty]=useState(false);
 const [form]=Form.useForm();const pending=useRef<{fingerprint:string;key:string}|undefined>(undefined);
 const base=`/api/visits/${workspace.visit.visitId}/investigations/${order.id}/billing`;
 async function load(){const [b,s,c]=await Promise.all([api<Billing>(base),api<Service[]>(`/api/finance/services?branchId=${workspace.visit.branchId}`),api<Capabilities>('/api/finance/capabilities')]);const matches=s.filter(x=>x.kind===order.kind),configured=matches.filter(x=>x.priceVnd!=null);const selected=matches.find(x=>x.id===b.serviceId)??(b.serviceId==null&&configured.length===1?configured[0]:undefined);setBilling(b);setDirty(false);setServices(matches);setCap(c);if(c.permissions.includes('finance.costs.read'))setProviders(await api<Provider[]>('/api/finance/providers'));form.setFieldsValue({mode:b.mode??'Clinic',serviceId:selected?.id,quantity:b.quantity,price:b.unitPriceVnd??selected?.priceVnd??undefined,evidence:b.evidence??'',providerId:b.providerId??undefined,cost:b.providerCostVnd==null?undefined:b.providerCostVnd/b.quantity});}
 useEffect(()=>{void load().catch(e=>setError((e as Error).message));},[order.id]);
 const mode=Form.useWatch('mode',form)??'Clinic',qty=Form.useWatch('quantity',form)??1,price=Form.useWatch('price',form);
 const selectedServiceId=Form.useWatch('serviceId',form),selectedService=services.find(service=>service.id===selectedServiceId);
 const billable=mode==='Clinic'||mode==='OutsourcedClinic';
 async function submit(action:'quote'|'perform'){
  if(!billing||busy)return;let values;try{values=action==='quote'?await form.validateFields():null;}catch{return;}
  const service=services.find(x=>x.id===values?.serviceId),provider=providers.find(x=>x.id===values?.providerId);
  const body={patientId:workspace.visit.patientId,expectedVisitVersion:billing.visitVersion,expectedVersion:billing.orderVersion,...(values?{mode:values.mode,...(billable?{serviceId:values.serviceId,expectedPriceVersion:service?.version,quantity:values.quantity,agreedUnitPriceVnd:values.price,evidence:values.evidence||null,...(values.mode==='OutsourcedClinic'?{providerId:provider?.id,providerVersion:provider?.version,providerUnitCostVnd:values.cost}:{})}:{})}:{})};
  const fingerprint=JSON.stringify({action,body});if(pending.current?.fingerprint!==fingerprint)pending.current={fingerprint,key:crypto.randomUUID()};setBusy(true);setError(undefined);
  try{const result=await api<Billing>(`${base}/${action}`,{method:'POST',body:JSON.stringify({...body,requestKey:pending.current.key})});setBilling(result);setDirty(false);pending.current=undefined;await onChanged();}
  catch(e){setError((e as Error).message);}finally{setBusy(false);}
 }
 return <Drawer open width={580} title={`Dịch vụ và phí · ${order.name}`} onClose={()=>{if(!busy)onClose();}} maskClosable={!busy}>
 {error&&<Alert type="error" role="alert" message={error} action={<Button disabled={busy} onClick={()=>void load().catch(e=>setError((e as Error).message))}>Tải lại phí</Button>}/>}
 {!billing?<Spin/>:<>
 <Descriptions column={1} size="small" items={[{key:'m',label:'Hình thức đã lưu',children:billing.mode?modes[billing.mode]:'Chưa phân loại thu phí'},{key:'s',label:'Dịch vụ',children:billing.serviceName??'—'},{key:'a',label:'Phí bệnh nhân',children:billing.amountVnd==null?'Không có khoản phí đã lập':money(billing.amountVnd)},{key:'p',label:'Thực hiện',children:billing.performedAt?`Đã xác nhận · ${new Date(billing.performedAt).toLocaleString('vi-VN')}`:'Chưa xác nhận'}]}/>
 {billing.performedAt&&<Alert type="success" message="Đã ghi phí vào thanh toán. Hủy chỉ định lâm sàng không tự hoàn tiền hoặc xóa phí."/>}
 {billing.providerName&&<Typography.Paragraph>Phải trả {billing.providerName}: {money(billing.providerCostVnd)}</Typography.Paragraph>}
 {billing.allowedActions.includes('quote')&&<Form form={form} layout="vertical" disabled={busy} onValuesChange={()=>setDirty(true)}>
 <Form.Item name="mode" label="Nơi thực hiện và thu tiền" rules={[{required:true}]}><Select aria-label="Nơi thực hiện và thu tiền" options={Object.entries(modes).filter(([v])=>v!=='OutsourcedClinic'||cap?.permissions.includes('finance.costs.manage')).map(([value,label])=>({value,label}))}/></Form.Item>
 {billable?<>
 {!services.length&&<Alert type="info" message={<>Chưa có dịch vụ phù hợp. <Link to="/clinical-services">Mở danh mục dịch vụ</Link></>}/>}
 {services.some(service=>service.priceVnd==null)&&<Alert type="warning" message={<>Có dịch vụ chưa thiết lập giá. <Link to="/clinical-services">Thiết lập giá dịch vụ</Link> trước khi lập phí.</>}/>}
 <Form.Item name="serviceId" label="Dịch vụ" rules={[{required:true}]}><Select aria-label="Dịch vụ tính phí" showSearch optionFilterProp="label" options={services.map(x=>({value:x.id,label:`${x.name} · ${x.priceVnd==null?'Chưa thiết lập giá':x.priceVnd===0?'Miễn phí (0 ₫)':money(x.priceVnd)} / ${x.unit}`,disabled:x.priceVnd==null}))} onChange={id=>{const s=services.find(x=>x.id===id);form.setFieldsValue({price:s?.priceVnd??undefined});}}/></Form.Item>
 {selectedService&&<Typography.Paragraph>Giá bảng đã thiết lập: <strong>{selectedService.priceVnd===0?'Miễn phí (0 ₫)':money(selectedService.priceVnd)} / {selectedService.unit}</strong></Typography.Paragraph>}
 <Space wrap align="start"><Form.Item name="quantity" label="Số lượng" rules={[{required:true}]}><InputNumber aria-label="Số lượng dịch vụ" min={1} max={100} precision={0}/></Form.Item><Form.Item name="price" label="Đơn giá áp dụng (₫)" extra="Tự điền từ bảng giá. Giá khác bảng cần căn cứ." rules={[{required:true}]}><InputNumber aria-label="Đơn giá bệnh nhân" min={0} max={1e12} precision={0}/></Form.Item></Space>
 <Typography.Paragraph>Thành tiền: {money(price==null?null:qty*price)}</Typography.Paragraph>
 <Form.Item name="evidence" label="Căn cứ điều chỉnh giá" extra="Bắt buộc khi áp dụng giá khác bảng; miễn phí đúng bảng không cần nhập lại căn cứ." rules={[{max:1000},{required:selectedService?.priceVnd!=null&&price!=null&&price!==selectedService.priceVnd,whitespace:true,message:'Ghi căn cứ áp dụng giá khác bảng.'}]}><Input.TextArea aria-label="Căn cứ giá thỏa thuận" maxLength={1000}/></Form.Item>
 {mode==='OutsourcedClinic'&&<><Form.Item name="providerId" label="Đơn vị thực hiện" rules={[{required:true}]}><Select aria-label="Đơn vị thực hiện" options={providers.map(x=>({value:x.id,label:x.name}))}/></Form.Item><Form.Item name="cost" label="Chi phí trả đơn vị / đơn vị dịch vụ (₫)" rules={[{required:true}]}><InputNumber aria-label="Chi phí trả đơn vị" min={0} max={1e12} precision={0}/></Form.Item><Link to="/clinical-services">Quản lý đơn vị thực hiện</Link></>}
 </>:<Alert type="info" message="Chỉ lưu chỉ định và kết quả; không phát sinh phí tại phòng khám."/>}
 <Button type="primary" loading={busy} onClick={()=>void submit('quote')}>Lưu hình thức và phí</Button>
 </Form>}
 {dirty&&<Typography.Paragraph type="secondary">Lưu hình thức và phí trước khi xác nhận thực hiện.</Typography.Paragraph>}
 {billing.allowedActions.includes('perform')&&<Button style={{marginTop:16}} type="primary" disabled={dirty} loading={busy} onClick={()=>void submit('perform')}>Xác nhận đã thực hiện — ghi phí</Button>}
 <Typography.Paragraph style={{marginTop:24}}><Link to={`/visits/${workspace.visit.visitId}/payments`}>Xem thanh toán của lượt khám</Link></Typography.Paragraph>
 </>}
 </Drawer>;
}
