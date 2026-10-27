import { Alert, Button, Card, Empty, Form, Input, Modal, Result, Select, Space, Table, Tabs, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, ApiError } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import './systemSettings.css';

type Kind = 'materials' | 'material-categories';
type Row = { id: string; name: string; version: number; categoryId?: string; categoryName?: string; itemCount?: number };
type Page = { items: Row[]; total: number; page: number; pageSize: number };
type Values = { name: string; categoryId?: string };
const base = '/api/system-settings';

async function allCategories() {
  const result: Row[] = [];
  for (let page = 1; ; page++) {
    const batch = await api<Page>(`${base}/material-categories?page=${page}&pageSize=100`);
    result.push(...batch.items);
    if (!batch.items.length || result.length >= batch.total) return result;
  }
}
export default function SystemSettingsPage() {
  const { user } = useAuth();
  const allowed = user?.accountType === 'Staff' && user.effectiveRoles.includes('Dentist');
  const [kind, setKind] = useState<Kind>('materials'), [query, setQuery] = useState(''), [search, setSearch] = useState('');
  const [categoryId, setCategoryId] = useState<string>(), [page, setPage] = useState(1), [size, setSize] = useState(20);
  const [data, setData] = useState<Page | null>(null), [loading, setLoading] = useState(false), [error, setError] = useState<string>();
  const [categories, setCategories] = useState<Row[] | null>(null), [categoryError, setCategoryError] = useState<string>();
  const [revision, setRevision] = useState(0);
  const [dialog, setDialog] = useState<{ kind: Kind; row?: Row } | null>(null), [deleting, setDeleting] = useState<{kind: Kind; row: Row} | null>(null);
  const [formError, setFormError] = useState<string>(), [deleteError, setDeleteError] = useState<string>();
  const [busy, setBusy] = useState(false), guard = useRef(false);
  const pending = useRef<{ body: string; key: string } | null>(null), deletePending = useRef<{body: string; key: string} | null>(null);
  const [form] = Form.useForm<Values>();
  useEffect(() => {
    if (!allowed) return;
    let live = true; setLoading(true); setError(undefined);
    const params = new URLSearchParams({ q: query, page: String(page), pageSize: String(size) });
    if (kind === 'materials' && categoryId) params.set('categoryId', categoryId);
    void api<Page>(`${base}/${kind}?${params}`).then(v => {
      if (!live) return;
      if (!v.items.length && v.total && page > 1) { setPage(Math.max(1, Math.ceil(v.total/size))); return; }
      setData(v);
    }).catch(e => { if (live) { setError(e.message); setData(null); } }).finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [allowed, kind, query, categoryId, page, size, revision]);
  useEffect(() => {
    if (!allowed) return;
    let live = true; setCategoryError(undefined);
    void allCategories().then(v => { if (live) setCategories(v); }).catch(e => { if (live) { setCategoryError(e.message); setCategories(null); } });
    return () => { live = false; };
  }, [allowed, revision]);
  function tab(value: string) { setKind(value as Kind); setPage(1); setQuery(''); setSearch(''); setCategoryId(undefined); setData(null); }
  function open(row?: Row) {
    pending.current = null; setFormError(undefined); form.resetFields();
    form.setFieldsValue({ name: row?.name ?? '', categoryId: row?.categoryId }); setDialog({kind, row});
  }
  async function save(values: Values) {
    if (!dialog || guard.current) return;
    guard.current = true; setBusy(true); setFormError(undefined);
    const body = { name: values.name.trim(), ...(dialog.kind === 'materials' ? { categoryId: values.categoryId } : {}), ...(dialog.row ? { expectedVersion: dialog.row.version } : {}) };
    const fingerprint = JSON.stringify({kind:dialog.kind,id:dialog.row?.id,body});
    if (pending.current?.body !== fingerprint) pending.current = {body:fingerprint,key:crypto.randomUUID()};
    try {
      await api(`${base}/${dialog.kind}${dialog.row ? `/${dialog.row.id}` : ''}`, { method: dialog.row ? 'PUT' : 'POST', body: JSON.stringify({...body,requestKey:pending.current.key}) });
      setDialog(null); form.resetFields(); pending.current = null; setRevision(v => v+1);
    } catch (e) {
      const failure = e as ApiError; setFormError(failure.message);
      if (failure.field === 'name') form.setFields([{name:'name',errors:[failure.message]}]);
    } finally { guard.current = false; setBusy(false); }
  }
  async function readVersion(deletion = false) {
    const target = deletion ? deleting : dialog;
    if (!target?.row || guard.current) return;
    guard.current = true; setBusy(true);
    try {
      const latest = await api<Row>(`${base}/${target.kind}/${target.row.id}`);
      if (deletion) { setDeleting({kind:target.kind,row:latest}); deletePending.current = null; setDeleteError(undefined); }
      else { setDialog({kind:target.kind,row:latest}); pending.current = null; setFormError(undefined); }
    } catch (e) { if (deletion) setDeleteError((e as Error).message); else setFormError((e as Error).message); }
    finally { guard.current = false; setBusy(false); }
  }
  async function remove() {
    if (!deleting || guard.current) return;
    guard.current = true; setBusy(true); setDeleteError(undefined);
    const body = JSON.stringify({kind:deleting.kind,id:deleting.row.id,version:deleting.row.version});
    if (deletePending.current?.body !== body) deletePending.current = {body,key:crypto.randomUUID()};
    try {
      await api(`${base}/${deleting.kind}/${deleting.row.id}`, {method:'DELETE',body:JSON.stringify({expectedVersion:deleting.row.version,requestKey:deletePending.current.key})});
      setDeleting(null); deletePending.current = null; setRevision(v => v+1);
    } catch (e) { setDeleteError((e as Error).message); }
    finally { guard.current = false; setBusy(false); }
  }
  if (!allowed) return <Result status="403" title="Bạn không có quyền thực hiện thao tác này." extra={<Link to="/">Về trang chủ</Link>} />;
  const options = categories?.map(c => ({value:c.id,label:c.name})) ?? [];
  const itemMode = kind === 'materials';
  return <div className="system-settings-page">
    <Typography.Title level={2}>Thiết lập hệ thống</Typography.Title>
    <Typography.Paragraph type="secondary">Danh mục dụng cụ và vật liệu dùng chung cho các chi nhánh.</Typography.Paragraph>
    <Card><Tabs activeKey={kind} onChange={tab} items={[{key:'materials',label:'Dụng cụ / Vật liệu'},{key:'material-categories',label:'Danh mục'}]} />
      <div className="system-settings-toolbar">
        <Input.Search aria-label="Tìm kiếm theo tên" placeholder="Tìm kiếm theo tên" maxLength={160} allowClear value={search}
          onChange={e => { setSearch(e.target.value); if (!e.target.value) {setQuery('');setPage(1);} }} onSearch={v => {setQuery(v.trim());setPage(1);}} />
        {itemMode && <Select aria-label="Lọc danh mục" placeholder="Tất cả danh mục" allowClear showSearch optionFilterProp="label" value={categoryId} loading={!categories && !categoryError} options={options} onChange={v => {setCategoryId(v);setPage(1);}} />}
        <Button type="primary" onClick={() => open()} disabled={itemMode && (!categories || !categories.length)}>Thêm mới</Button>
      </div>
      {categoryError && <Alert type="error" showIcon message={categoryError} action={<Button onClick={() => setRevision(v=>v+1)}>Thử lại danh mục</Button>} />}
      {itemMode && categories?.length === 0 && <Alert type="info" showIcon message="Chưa có danh mục. Hãy tạo danh mục trước khi thêm dụng cụ/vật liệu." action={<Button onClick={() => tab('material-categories')}>Tạo danh mục</Button>} />}
      {error && <Alert type="error" showIcon message={error} action={<Button onClick={() => setRevision(v=>v+1)}>Thử lại</Button>} />}
      <Table<Row> rowKey="id" loading={loading} dataSource={data?.items ?? []} scroll={{x:650}}
        locale={{emptyText:<Empty description={itemMode ? 'Chưa có dụng cụ/vật liệu.' : 'Chưa có danh mục.'} />}}
        pagination={{current:page,pageSize:size,total:data?.total ?? 0,showSizeChanger:true,pageSizeOptions:[20,50,100],onChange:(p,s)=>{setPage(s!==size ? 1 : p);setSize(s);}}}
        columns={[{title:'STT',key:'ordinal',width:65,render:(_v,_r,index)=>(page-1)*size+index+1},
          {title:itemMode ? 'Tên' : 'Tên danh mục',dataIndex:'name'},
          itemMode ? {title:'Thuộc danh mục',dataIndex:'categoryName'} : {title:'Số mục đang thuộc nhóm',dataIndex:'itemCount',width:200},
          {title:'Thao tác',key:'actions',width:150,render:(_v,row)=><Space><Button size="small" aria-label={`Sửa ${row.name}`} onClick={()=>open(row)}>Sửa</Button><Button danger size="small" aria-label={`Xóa ${row.name}`} onClick={()=>{setDeleting({kind,row});setDeleteError(undefined);deletePending.current=null;}}>Xóa</Button></Space>}]} />
    </Card>
    <Modal forceRender open={!!dialog} title={`${dialog?.row ? 'Sửa' : 'Thêm'} ${dialog?.kind === 'materials' ? 'dụng cụ/vật liệu' : 'danh mục'}`} okText="Lưu" okButtonProps={{'aria-label':'Lưu'}} cancelText="Hủy" confirmLoading={busy}
      closable={!busy} cancelButtonProps={{disabled:busy}} onCancel={()=>{if(!busy)setDialog(null);}} onOk={()=>form.submit()}>
      {formError && <Alert type="error" showIcon message={formError} />}
      {formError && dialog?.row && <Button disabled={busy} onClick={()=>void readVersion()}>Đọc lại phiên bản, giữ nội dung</Button>}
      <Form form={form} layout="vertical" disabled={busy} onFinish={v=>void save(v)}>
        <Form.Item name="name" label={dialog?.kind === 'materials' ? 'Tên' : 'Tên danh mục'} rules={[{required:true,whitespace:true,message:'Vui lòng nhập tên.'},{max:160,message:'Tên tối đa 160 ký tự.'}]}><Input aria-label={dialog?.kind === 'materials' ? 'Tên' : 'Tên danh mục'} maxLength={160} autoComplete="off" /></Form.Item>
        {dialog?.kind === 'materials' && <Form.Item name="categoryId" label="Thuộc danh mục" rules={[{required:true,message:'Vui lòng chọn danh mục.'}]}><Select aria-label="Thuộc danh mục" showSearch optionFilterProp="label" placeholder="Chọn danh mục" loading={!categories} options={options} /></Form.Item>}
      </Form>
    </Modal>
    <Modal open={!!deleting} title={deleting?.kind === 'materials' ? 'Bạn có muốn xóa dụng cụ/vật liệu này không?' : 'Bạn có muốn xóa danh mục này không?'} okText="Đồng ý" okButtonProps={{'aria-label':'Đồng ý'}} cancelText="Không đồng ý" confirmLoading={busy}
      closable={!busy} cancelButtonProps={{disabled:busy}} onCancel={()=>{if(!busy)setDeleting(null);}} onOk={()=>void remove()}>
      <p>{deleting?.row.name}</p>{deleteError && <Alert type="error" showIcon message={deleteError} />}
      {deleteError && <Button disabled={busy} onClick={()=>void readVersion(true)}>Đọc lại phiên bản</Button>}
    </Modal>
  </div>;
}
