import {Alert,Button,Card,Form,Input,InputNumber,Modal,Table,Tag} from 'antd';
import {useEffect,useRef,useState} from 'react';
import {api} from '../../services/api';
import {money} from './types';
import type {ExaminationService} from './examinationFeeTypes';
export default function ExaminationServiceCatalog({branchId,canManage}:{branchId?:number;canManage:boolean}){
 const [items,setItems]=useState<ExaminationService[]>([]),[edit,setEdit]=useState<ExaminationService|null|undefined>(),[error,setError]=useState<string>(),[busy,setBusy]=useState(false);const [form]=Form.useForm();const pending=useRef<{body:string;key:string}|undefined>(undefined);
 async function load(){if(branchId)setItems(await api<ExaminationService[]>(`/api/finance/examination-services?branchId=${branchId}`));}
 useEffect(()=>{setItems([]);void load().catch(e=>setError((e as Error).message));},[branchId]);
 function open(value:ExaminationService|null){form.resetFields();form.setFieldsValue({name:value?.name??'Khám',price:value?.priceVnd??undefined});pending.current=undefined;setError(undefined);setEdit(value);}
 async function save(){let v;try{v=await form.validateFields();}catch{return;}const body={branchId,name:v.name,priceVnd:v.price??null,expectedVersion:edit?.version??0};const fingerprint=JSON.stringify(body);if(pending.current?.body!==fingerprint)pending.current={body:fingerprint,key:crypto.randomUUID()};setBusy(true);setError(undefined);try{await api(`/api/finance/examination-services${edit?`/${edit.id}`:''}`,{method:edit?'PUT':'POST',body:JSON.stringify({...body,requestKey:pending.current.key})});setEdit(undefined);pending.current=undefined;await load();}catch(e){setError((e as Error).message);}finally{setBusy(false);}}
 return <Card title="Dịch vụ và giá khám" style={{margin:'20px 0'}} extra={canManage&&!items.length&&<Button disabled={!branchId||busy} onClick={()=>open(null)}>Thêm dịch vụ khám</Button>}>
 {error&&<Alert role="alert" type="error" message={error}/>}
 <Alert type="info" message="Giá khám dùng khi kết thúc lượt khám. Để trống là chưa cấu hình; nhập 0 là miễn phí theo bảng giá. Không tự đổi phí các lượt đã ghi."/>
 <Table rowKey="id" pagination={false} dataSource={items} locale={{emptyText:'Chi nhánh chưa có dịch vụ khám được cấu hình.'}} columns={[{title:'Dịch vụ khám',dataIndex:'name'},{title:'Giá khám',dataIndex:'priceVnd',render:v=>v===0?<Tag color="green">Miễn phí (0 ₫)</Tag>:money(v)},{title:'',render:(_,s)=>canManage&&<Button disabled={busy} onClick={()=>open(s)}>Sửa giá khám</Button>}]}/>
 <Modal open={edit!==undefined} title="Cấu hình phí khám" okText="Lưu giá khám" cancelText="Đóng" confirmLoading={busy} onCancel={()=>{if(!busy)setEdit(undefined);}} onOk={()=>void save()}>{error&&<Alert type="error" message={error}/>}<Form form={form} layout="vertical" disabled={busy}><Form.Item name="name" label="Tên dịch vụ khám" rules={[{required:true,whitespace:true,max:160}]}><Input aria-label="Tên dịch vụ khám"/></Form.Item><Form.Item name="price" label="Giá khám (₫)" extra="Để trống nếu chưa có giá; 0 nghĩa là miễn phí."><InputNumber aria-label="Giá khám theo bảng" min={0} max={1e12} precision={0}/></Form.Item></Form></Modal>
 </Card>;
}
