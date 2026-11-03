import { Alert, Button, Card, Empty, Form, Input, InputNumber, Select, Space, Spin, Table, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
import { usePermissions } from '../authentication/usePermissions';

type MedicineOption = { id:string; code:string; name:string; strength?:string; dosageForm?:string; baseUnit:string; defaultInstructions?:string };
type PrescriptionLine = { id:string; medicineId:string; medicineCode:string; medicineName:string; strength?:string; dosageForm?:string; unit:string; instructions:string; quantity:number; version:number };
type Values = { medicineId:string; quantity:number; instructions?:string };
type Props = { visitId:string; branchId:number; readOnly?:boolean; disabled?:boolean; initiallyExpanded?:boolean;
  onDirtyChange?:(dirty:boolean)=>void; onBusyChange?:(busy:boolean)=>void };

export default function PrescriptionPanel({ visitId, branchId, readOnly=false, disabled=false, initiallyExpanded=false, onDirtyChange, onBusyChange }:Props) {
  const has = usePermissions();
  readOnly = readOnly || !has('prescriptions.manage');
  const [lines,setLines]=useState<PrescriptionLine[]|null>(null),[loadError,setLoadError]=useState<string>();
  const [expanded,setExpanded]=useState(initiallyExpanded),[options,setOptions]=useState<MedicineOption[]>([]),[query,setQuery]=useState('');
  const [optionsLoading,setOptionsLoading]=useState(false),[optionsError,setOptionsError]=useState<string>(),[optionRevision,setOptionRevision]=useState(0);
  const [editing,setEditing]=useState<PrescriptionLine>(),[saveError,setSaveError]=useState<ApiError>(),[busy,setBusy]=useState(false),[dirty,setDirty]=useState(false);
  const [form]=Form.useForm<Values>();
  const generation=useRef(0),saving=useRef(false),pending=useRef<{fingerprint:string;key:string}|undefined>(undefined);
  const path=`/api/prescriptions/visits/${visitId}/lines`;

  useEffect(()=>{onDirtyChange?.(dirty);},[dirty,onDirtyChange]);
  useEffect(()=>()=>{onDirtyChange?.(false);},[onDirtyChange]);
  useEffect(()=>{onBusyChange?.(busy);},[busy,onBusyChange]);
  useEffect(()=>()=>{onBusyChange?.(false);},[onBusyChange]);
  async function loadLines(token=generation.current) {
    setLoadError(undefined);
    try {
      const rows=await api<PrescriptionLine[]>(path);
      if(token!==generation.current)return;
      setLines(rows);setExpanded(value=>value||rows.length>0);return rows;
    } catch(e) {if(token===generation.current)setLoadError((e as Error).message);}
  }
  useEffect(()=>{
    const token=++generation.current;setLines(null);setEditing(undefined);setDirty(false);setBusy(false);saving.current=false;
    pending.current=undefined;setSaveError(undefined);setExpanded(initiallyExpanded);setQuery('');setOptions([]);form.resetFields();
    void loadLines(token);return ()=>{generation.current++;};
  },[visitId,branchId]);
  useEffect(()=>{if(readOnly){setEditing(undefined);setDirty(false);pending.current=undefined;form.resetFields();}},[readOnly,form]);
  useEffect(()=>{
    if(!expanded||readOnly)return;
    let active=true;setOptionsLoading(true);setOptionsError(undefined);
    const timer=window.setTimeout(()=>{
      void api<MedicineOption[]>(`/api/prescriptions/medicine-options?${new URLSearchParams({branchId:String(branchId),q:query})}`)
        .then(rows=>{if(active)setOptions(rows);})
        .catch(e=>{if(active){setOptions([]);setOptionsError((e as Error).message);}})
        .finally(()=>{if(active)setOptionsLoading(false);});
    },250);
    return ()=>{active=false;window.clearTimeout(timer);};
  },[expanded,readOnly,branchId,visitId,query,optionRevision]);
  function reset() {form.resetFields();setEditing(undefined);setDirty(false);setSaveError(undefined);pending.current=undefined;}
  function edit(line:PrescriptionLine) {
    if(dirty&&!window.confirm('Bỏ phần kê đơn chưa lưu để sửa dòng thuốc này?'))return;
    setExpanded(true);setEditing(line);setSaveError(undefined);setDirty(false);pending.current=undefined;
    form.setFieldsValue({medicineId:line.medicineId,quantity:line.quantity,instructions:line.instructions});
  }
  async function readLatest() {
    const rows=await loadLines();
    if(rows&&editing){const latest=rows.find(line=>line.id===editing.id);if(latest){setEditing(latest);setSaveError(undefined);pending.current=undefined;}}
  }
  async function save(values:Values) {
    if(readOnly||disabled||saving.current||lines==null||loadError)return;
    const token=generation.current;
    const body={expectedVersion:editing?.version??0,medicineId:values.medicineId,quantity:values.quantity,instructions:values.instructions??''};
    const target=editing?`${path}/${editing.id}`:path,fingerprint=JSON.stringify({target,...body});
    if(pending.current?.fingerprint!==fingerprint)pending.current={fingerprint,key:crypto.randomUUID()};
    saving.current=true;setBusy(true);setSaveError(undefined);
    try {
      const saved=await api<PrescriptionLine>(target,{method:editing?'PUT':'POST',body:JSON.stringify({...body,requestKey:pending.current.key})});
      if(token!==generation.current)return;
      setLines(current=>current?.some(line=>line.id===saved.id)?current.map(line=>line.id===saved.id?saved:line):[...(current??[]),saved]);reset();
    } catch(e) {if(token===generation.current)setSaveError(e as ApiError);}
    finally {if(token===generation.current){saving.current=false;setBusy(false);}}
  }
  const selectedOptions=[...options];
  if(editing&&!selectedOptions.some(option=>option.id===editing.medicineId))selectedOptions.push({id:editing.medicineId,code:editing.medicineCode,name:editing.medicineName,strength:editing.strength,dosageForm:editing.dosageForm,baseUnit:editing.unit});
  return <section aria-label="Đơn thuốc khám">
    <Card size="small" title="Đơn thuốc (không bắt buộc)" style={{margin:'16px 0'}} extra={lines&&!loadError&&(lines.length>0||!readOnly)?
      <Button type="link" disabled={busy} aria-expanded={expanded} onClick={()=>setExpanded(value=>!value)}>{expanded?'Thu gọn':lines.length?'Xem đơn thuốc':'Kê đơn thuốc'}</Button>:undefined}>
      <Typography.Paragraph type="secondary">Kê đơn mua ngoài khi cần; có thể hoàn tất khám mà không kê đơn.</Typography.Paragraph>
      {loadError?<Alert type="error" showIcon message="Không tải được đơn thuốc" description={loadError} action={<Button onClick={()=>void loadLines()}>Thử tải đơn thuốc</Button>}/>:lines==null?<Spin/>:
        !expanded?<Typography.Text type="secondary">{lines.length?`Đã kê ${lines.length} dòng thuốc.`:'Chưa kê thuốc cho lượt khám này.'}</Typography.Text>:<>
          {lines.length>0?<Table size="small" rowKey="id" pagination={false} dataSource={lines} scroll={{x:520}} columns={[
            {title:'Thuốc',render:(_,line:PrescriptionLine)=><><strong>{line.medicineName}</strong><div>{[line.medicineCode,line.strength,line.dosageForm].filter(Boolean).join(' · ')}</div></>},
            {title:'Số lượng',render:(_,line:PrescriptionLine)=>`${line.quantity} ${line.unit}`},{title:'Lời dặn',dataIndex:'instructions'},
            ...(!readOnly?[{title:'',render:(_:unknown,line:PrescriptionLine)=><Button disabled={busy||disabled} aria-label={`Sửa thuốc ${line.medicineName}`} onClick={()=>edit(line)}>Sửa</Button>}]:[]),
          ]}/>:<Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chưa có thuốc trong đơn"/>}
          {!readOnly&&<>
            {optionsError&&<Alert type="error" message="Không tải được danh mục thuốc" description={optionsError} action={<Button onClick={()=>setOptionRevision(v=>v+1)}>Thử tải thuốc</Button>}/>}
            {!optionsLoading&&!optionsError&&options.length===0&&<Typography.Paragraph type="secondary">Chưa tìm thấy thuốc phù hợp. Có thể tìm tên khác hoặc hoàn tất khám không kê đơn.</Typography.Paragraph>}
            {saveError&&<Alert role="alert" type="error" showIcon message={saveError.code==='MSG111'?'Đơn thuốc đã thay đổi. Phần nhập được giữ; đọc lại phiên bản dòng thuốc trước khi lưu tiếp.':saveError.message}
              action={saveError.code==='MSG111'?<Button disabled={busy||disabled} onClick={()=>void readLatest()}>Đọc lại dòng thuốc</Button>:undefined}/>}
            <Form form={form} name={`prescription-${visitId}`} layout="vertical" initialValues={{quantity:1}} disabled={busy||disabled} onValuesChange={()=>setDirty(true)} onFinish={values=>void save(values)}>
              <Form.Item name="medicineId" label="Thuốc kê đơn" rules={[{required:true,message:'Chọn thuốc cần kê.'}]}>
                <Select aria-label="Thuốc kê đơn" showSearch filterOption={false} loading={optionsLoading} onSearch={setQuery} placeholder="Tìm mã hoặc tên thuốc" options={selectedOptions.map(option=>({value:option.id,label:[option.code,option.name,option.strength,option.dosageForm].filter(Boolean).join(' · ')}))}
                  onChange={medicineId=>{const option=options.find(item=>item.id===medicineId);if(option&&!editing)form.setFieldValue('instructions',option.defaultInstructions??'');}}/>
              </Form.Item>
              <Form.Item name="quantity" label="Số lượng thuốc" rules={[{required:true,message:'Nhập số lượng.'},{type:'integer',min:1,max:10000,message:'Số lượng là số nguyên từ 1 đến 10.000.'}]}><InputNumber aria-label="Số lượng thuốc" min={1} max={10000} precision={0}/></Form.Item>
              <Form.Item name="instructions" label="Lời dặn dùng thuốc" rules={[{max:1000,message:'Lời dặn tối đa 1.000 ký tự.'}]}><Input.TextArea aria-label="Lời dặn dùng thuốc" rows={2} maxLength={1000}/></Form.Item>
              <Space wrap><Button type="primary" htmlType="submit" aria-label={editing?'Lưu thay đổi thuốc':'Thêm vào đơn'} loading={busy} disabled={busy||disabled||!!optionsError}>{editing?'Lưu thay đổi thuốc':'Thêm vào đơn'}</Button>
                <Button disabled={busy||disabled} onClick={reset}>Bỏ phần nhập thuốc</Button></Space>
            </Form>
            <Typography.Paragraph type="secondary" style={{marginTop:8}}>Lưu từng thuốc vào đơn bằng nút trên. Hoàn tất khám không tự lưu phần thuốc đang nhập.</Typography.Paragraph>
          </>}
        </>}
    </Card>
  </section>;
}
