import { Alert, Button, Form, Input, InputNumber, Select, Space, Table, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../services/api';

type Option={id:string;code:string;name:string;strength?:string;dosageForm?:string;baseUnit:string;defaultInstructions?:string};
type Line={id:string;medicineId:string;medicineCode:string;medicineName:string;strength?:string;dosageForm?:string;unit:string;instructions:string;quantity:number;version:number};
export default function PrescriptionPage(){
 const {visitId}=useParams(); const [options,setOptions]=useState<Option[]>([]),[lines,setLines]=useState<Line[]>([]),[editing,setEditing]=useState<Line>(),[error,setError]=useState<string>(),[busy,setBusy]=useState(false); const [form]=Form.useForm();
 async function load(){const bill=await api<{branchId:number}>(`/api/finance/visits/${visitId}`);const [o,l]=await Promise.all([api<Option[]>(`/api/prescriptions/medicine-options?branchId=${bill.branchId}`),api<Line[]>(`/api/prescriptions/visits/${visitId}/lines`)]);setOptions(o);setLines(l);}
 useEffect(()=>{void load().catch(e=>setError((e as Error).message));},[visitId]);
 function choose(id:string){const o=options.find(x=>x.id===id);if(o&&!editing)form.setFieldValue('instructions',o.defaultInstructions??'');}
 async function save(){let v;try{v=await form.validateFields();}catch{return;}setBusy(true);setError(undefined);try{await api(`/api/prescriptions/visits/${visitId}/lines${editing?`/${editing.id}`:''}`,{method:editing?'PUT':'POST',body:JSON.stringify({requestKey:crypto.randomUUID(),expectedVersion:editing?.version??0,medicineId:v.medicineId,quantity:v.quantity,instructions:v.instructions})});setEditing(undefined);form.resetFields();await load();}catch(e){setError((e as Error).message);}finally{setBusy(false);}}
 return <main className="page"><Space><Typography.Title level={2}>Đơn thuốc mua ngoài</Typography.Title><Link to={`/visits/${visitId}`}>Về lượt khám</Link></Space>{error&&<Alert type="error" message={error}/>}<Alert type="info" message="Đơn mua ngoài không cần tồn kho và không tạo xuất kho. Lời dặn sửa tại đây chỉ áp dụng cho dòng đơn."/>
 <Form form={form} layout="vertical" disabled={busy}><Form.Item name="medicineId" label="Thuốc" rules={[{required:true}]}><Select showSearch optionFilterProp="label" onChange={choose} options={options.map(x=>({value:x.id,label:`${x.code} · ${x.name}${x.strength?` · ${x.strength}`:''}`}))}/></Form.Item><Form.Item name="quantity" label="Số lượng" rules={[{required:true}]}><InputNumber min={1} max={10000}/></Form.Item><Form.Item name="instructions" label="Lời dặn trên đơn" rules={[{max:1000}]}><Input.TextArea maxLength={1000}/></Form.Item><Button type="primary" onClick={()=>void save()}>{editing?'Lưu lời dặn':'Thêm vào đơn'}</Button></Form>
 <Table rowKey="id" dataSource={lines} columns={[{title:'Thuốc',render:(_,x)=>`${x.medicineCode} · ${x.medicineName}`},{title:'Số lượng',render:(_,x)=>`${x.quantity} ${x.unit}`},{title:'Lời dặn',dataIndex:'instructions'},{title:'',render:(_,x)=><Button onClick={()=>{setEditing(x);form.setFieldsValue({medicineId:x.medicineId,quantity:x.quantity,instructions:x.instructions});}}>Sửa lời dặn</Button>}]}/></main>;
}
