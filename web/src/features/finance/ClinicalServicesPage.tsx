import ExaminationServiceCatalog from './ExaminationServiceCatalog';
import './finance.css';
﻿import {Alert,Button,Card,Form,Input,InputNumber,Modal,Select,Space,Table,Tag,Typography} from 'antd';
import {useEffect,useRef,useState} from 'react';
import {Link} from 'react-router-dom';
import {api} from '../../services/api';
import {type Capabilities,type Service,type Provider,kinds,money} from './types';
export default function ClinicalServicesPage(){
 const [cap,setCap]=useState<Capabilities>(),[branch,setBranch]=useState<number>(),[services,setServices]=useState<Service[]>([]),[providers,setProviders]=useState<Provider[]>([]),[error,setError]=useState<string>(),[busy,setBusy]=useState(false),[edit,setEdit]=useState<Service|null|undefined>(),[providerOpen,setProviderOpen]=useState(false);
 const [form]=Form.useForm(),[providerForm]=Form.useForm();const pending=useRef<{body:string;key:string}|undefined>(undefined);
 const can=(p:string)=>cap?.permissions.includes(p);
 useEffect(()=>{api<Capabilities>('/api/finance/capabilities').then(c=>{setCap(c);setBranch(c.branches[0]?.id);if(c.permissions.includes('finance.costs.read'))void api<Provider[]>('/api/finance/providers').then(setProviders).catch(e=>setError((e as Error).message));}).catch(e=>setError((e as Error).message));},[]);
 async function load(){if(branch)setServices(await api<Service[]>(`/api/finance/services?branchId=${branch}&includeInactive=true`));}
 useEffect(()=>{void load().catch(e=>setError((e as Error).message));},[branch]);
 function open(s:Service|null){form.resetFields();form.setFieldsValue(s?{...s,price:s.priceVnd??undefined}:{kind:'XRay',unit:'Lần',active:true});pending.current=undefined;setEdit(s);}
 async function save(provider=false){let v;try{v=await(provider?providerForm:form).validateFields();}catch{return;}if(!provider&&!branch)return;
 const payload=provider?{code:v.code,name:v.name}:{branchId:branch,name:v.name,kind:v.kind,unit:v.unit,priceVnd:v.price??null,expectedVersion:edit?.version??0,active:v.active};const body=JSON.stringify(payload);if(pending.current?.body!==body)pending.current={body,key:crypto.randomUUID()};setBusy(true);setError(undefined);
 try{await api(provider?'/api/finance/providers':`/api/finance/services${edit?`/${edit.id}`:''}`,{method:provider||!edit?'POST':'PUT',body:JSON.stringify({...payload,requestKey:pending.current.key})});pending.current=undefined;if(provider){setProviderOpen(false);setProviders(await api<Provider[]>('/api/finance/providers'));}else{setEdit(undefined);await load();}}
 catch(e){setError((e as Error).message);}finally{setBusy(false);}}
 return <main className="finance-page"><Space wrap><Typography.Title level={2}>Dịch vụ khám & cận lâm sàng</Typography.Title><Link to="/payments">Thanh toán</Link></Space>
 {error&&<Alert role="alert" type="error" message={error}/>}
 <Space style={{marginBottom:16}}><Select aria-label="Chi nhánh dịch vụ" style={{minWidth:220}} disabled={busy||edit!==undefined} value={branch} onChange={setBranch} options={cap?.branches.map(x=>({value:x.id,label:x.name}))}/>{can('services.manage')&&<Button type="primary" disabled={!branch||busy} onClick={()=>open(null)}>Thêm dịch vụ</Button>}<Button disabled={busy} onClick={()=>void load().catch(e=>setError((e as Error).message))}>Tải lại danh mục</Button></Space>
 <ExaminationServiceCatalog branchId={branch} canManage={!!can('services.manage')}/>
 <Alert type="info" message="Giá để trống nghĩa là chưa cấu hình. Giá 0 nghĩa là miễn phí; cần căn cứ khi lập phí cho chỉ định. Dịch vụ này không tự xuất hiện trong đặt lịch công khai."/>
 <Table rowKey="id" dataSource={services} columns={[{title:'Tên dịch vụ',dataIndex:'name'},{title:'Nhóm',dataIndex:'kind',render:v=>kinds[v]},{title:'Đơn vị',dataIndex:'unit'},{title:'Giá tham chiếu',dataIndex:'priceVnd',render:money},{title:'Trạng thái',dataIndex:'active',render:v=><Tag>{v?'Đang dùng':'Ngừng dùng'}</Tag>},{title:'',render:(_,s)=>can('services.manage')&&<Button onClick={()=>open(s)}>Sửa</Button>}]}/>
 {can('finance.costs.read')&&<Card title="Đơn vị thực hiện bên ngoài" extra={can('finance.costs.manage')&&<Button onClick={()=>{providerForm.resetFields();pending.current=undefined;setProviderOpen(true);}}>Thêm đơn vị</Button>}><Table rowKey="id" dataSource={providers} columns={[{title:'Mã',dataIndex:'code'},{title:'Tên đơn vị',dataIndex:'name'}]}/></Card>}
 <Modal open={edit!==undefined} title={edit?'Sửa dịch vụ':'Thêm dịch vụ'} okText="Lưu dịch vụ" cancelText="Đóng" confirmLoading={busy} onCancel={()=>{if(!busy)setEdit(undefined);}} onOk={()=>void save()}>{error&&<Alert type="error" message={error}/>}<Form form={form} layout="vertical" disabled={busy}>
 <Form.Item name="name" label="Tên dịch vụ" rules={[{required:true,whitespace:true,max:160}]}><Input aria-label="Tên dịch vụ" maxLength={160}/></Form.Item><Form.Item name="kind" label="Nhóm dịch vụ" rules={[{required:true}]}><Select options={Object.entries(kinds).map(([value,label])=>({value,label}))}/></Form.Item><Form.Item name="unit" label="Đơn vị tính" rules={[{required:true,whitespace:true,max:40}]}><Input maxLength={40}/></Form.Item><Form.Item name="price" label="Giá tham chiếu (₫)"><InputNumber aria-label="Giá tham chiếu" min={0} max={1e12} precision={0}/></Form.Item><Form.Item name="active" label="Trạng thái" rules={[{required:true}]}><Select options={[{value:true,label:'Đang dùng'},{value:false,label:'Ngừng dùng'}]}/></Form.Item>
 </Form></Modal>
 <Modal open={providerOpen} title="Thêm đơn vị thực hiện" okText="Lưu đơn vị" cancelText="Đóng" confirmLoading={busy} onCancel={()=>{if(!busy)setProviderOpen(false);}} onOk={()=>void save(true)}>{error&&<Alert type="error" message={error}/>}<Form form={providerForm} layout="vertical" disabled={busy}><Form.Item name="code" label="Mã đơn vị" rules={[{required:true,whitespace:true,max:40}]}><Input maxLength={40}/></Form.Item><Form.Item name="name" label="Tên đơn vị" rules={[{required:true,whitespace:true,max:160}]}><Input maxLength={160}/></Form.Item></Form></Modal>
 </main>;
}
