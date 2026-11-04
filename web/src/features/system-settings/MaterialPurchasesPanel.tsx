import { DeleteOutlined, EditOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import { Alert, AutoComplete, Button, Empty, Form, Input, InputNumber, Modal, Select, Space, Table, Tag } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
import { clinicToday, formatDate } from '../appointments/types';
import { useAuth } from '../authentication/AuthContext';

// Purchase log of materials/instruments. Rules and API: docs/specs/MATERIAL-PURCHASES.md.
type Purchase = { id: string; enteredOn: string; receivedOn: string | null; materialId: string; materialName: string; categoryName: string;
  quantity: number; unit: string; unitPriceVnd: number; totalVnd: number; paidVnd: number; dueVnd: number; version: number };
type Totals = { totalVnd: number; paidVnd: number; dueVnd: number };
type PurchasePage = { items: Purchase[]; total: number; page: number; pageSize: number; totals: Totals };
type Material = { id: string; name: string; categoryName: string };
type Values = { enteredOn: string; receivedOn?: string; materialId?: string; quantity?: number; unit?: string; unitPriceVnd?: number; paidVnd?: number };
const base = '/api/system-settings';
const vnd = (n: number) => `${n.toLocaleString('vi-VN')} ₫`;
const units = ['cái', 'lọ', 'miếng', 'hộp', 'tuýp', 'chai', 'ống', 'bộ', 'gói', 'túi', 'g', 'mL'].map(value => ({ value }));
const paymentOptions = [{ value: 'unpaid', label: 'Chưa thanh toán' }, { value: 'partial', label: 'Còn nợ' }, { value: 'paid', label: 'Đã thanh toán đủ' }];
const paymentTag = (row: Purchase) => row.dueVnd <= 0 ? <Tag color="green">Đã thanh toán đủ</Tag> : row.paidVnd > 0 ? <Tag color="orange">Còn nợ</Tag> : <Tag color="red">Chưa thanh toán</Tag>;

async function allMaterials() {
  const result: Material[] = [];
  for (let page = 1; ; page++) {
    const batch = await api<{ items: Material[]; total: number }>(`${base}/materials?page=${page}&pageSize=100`);
    result.push(...batch.items);
    if (!batch.items.length || result.length >= batch.total) return result;
  }
}

export default function MaterialPurchasesPanel() {
  const { user } = useAuth();
  const canCreate = user?.permissions.includes('catalog.materials.create'), canUpdate = user?.permissions.includes('catalog.materials.update'), canDelete = user?.permissions.includes('catalog.materials.deactivate');
  const [query, setQuery] = useState(''), [search, setSearch] = useState(''), [from, setFrom] = useState(''), [to, setTo] = useState('');
  const [payment, setPayment] = useState<string>(), [page, setPage] = useState(1), [size, setSize] = useState(20), [revision, setRevision] = useState(0);
  const [data, setData] = useState<PurchasePage | null>(null), [loading, setLoading] = useState(false), [error, setError] = useState<string>();
  const [materials, setMaterials] = useState<Material[] | null>(null), [materialError, setMaterialError] = useState<string>();
  const [dialog, setDialog] = useState<{ row?: Purchase } | null>(null), [deleting, setDeleting] = useState<Purchase | null>(null);
  const [formError, setFormError] = useState<string>(), [deleteError, setDeleteError] = useState<string>();
  const [busy, setBusy] = useState(false), guard = useRef(false);
  const pending = useRef<{ body: string; key: string } | null>(null), deletePending = useRef<{ body: string; key: string } | null>(null);
  const [form] = Form.useForm<Values>();
  const watched = Form.useWatch([], form);
  const preview = Math.round((watched?.quantity ?? 0) * (watched?.unitPriceVnd ?? 0)), previewPaid = watched?.paidVnd ?? 0;

  useEffect(() => {
    let live = true; setLoading(true); setError(undefined);
    const params = new URLSearchParams({ q: query, page: String(page), pageSize: String(size) });
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    if (payment) params.set('payment', payment);
    void api<PurchasePage>(`${base}/material-purchases?${params}`).then(v => {
      if (!live) return;
      if (!v.items.length && v.total && page > 1) { setPage(Math.max(1, Math.ceil(v.total / size))); return; }
      setData(v);
    }).catch(e => { if (live) { setError((e as Error).message); setData(null); } }).finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [query, from, to, payment, page, size, revision]);
  useEffect(() => {
    let live = true; setMaterialError(undefined);
    void allMaterials().then(v => { if (live) setMaterials(v); }).catch(e => { if (live) { setMaterialError((e as Error).message); setMaterials(null); } });
    return () => { live = false; };
  }, [revision]);

  function open(row?: Purchase) {
    pending.current = null; setFormError(undefined); form.resetFields();
    form.setFieldsValue(row ? { enteredOn: row.enteredOn, receivedOn: row.receivedOn ?? undefined, materialId: row.materialId, quantity: row.quantity, unit: row.unit,
      unitPriceVnd: row.unitPriceVnd, paidVnd: row.paidVnd } : { enteredOn: clinicToday(), quantity: 1, paidVnd: 0 });
    setDialog({ row });
  }
  async function save(values: Values) {
    if (!dialog || guard.current) return;
    guard.current = true; setBusy(true); setFormError(undefined);
    const body = { enteredOn: values.enteredOn, receivedOn: values.receivedOn || null, materialId: values.materialId, quantity: values.quantity,
      unit: (values.unit ?? '').trim(), unitPriceVnd: values.unitPriceVnd ?? 0, paidVnd: values.paidVnd ?? 0, ...(dialog.row ? { expectedVersion: dialog.row.version } : {}) };
    const fingerprint = JSON.stringify({ id: dialog.row?.id, body });
    if (pending.current?.body !== fingerprint) pending.current = { body: fingerprint, key: crypto.randomUUID() };
    try {
      await api(`${base}/material-purchases${dialog.row ? `/${dialog.row.id}` : ''}`, { method: dialog.row ? 'PUT' : 'POST', body: JSON.stringify({ ...body, requestKey: pending.current.key }) });
      setDialog(null); form.resetFields(); pending.current = null; setRevision(v => v + 1);
    } catch (e) {
      const failure = e as ApiError; setFormError(failure.message);
      if (failure.field) form.setFields([{ name: failure.field as keyof Values, errors: [failure.message] }]);
    } finally { guard.current = false; setBusy(false); }
  }
  async function readVersion(deletion = false) {
    const target = deletion ? deleting : dialog?.row;
    if (!target || guard.current) return;
    guard.current = true; setBusy(true);
    try {
      const latest = await api<Purchase>(`${base}/material-purchases/${target.id}`);
      if (deletion) { setDeleting(latest); deletePending.current = null; setDeleteError(undefined); }
      else { setDialog({ row: latest }); pending.current = null; setFormError(undefined); }
    } catch (e) { if (deletion) setDeleteError((e as Error).message); else setFormError((e as Error).message); }
    finally { guard.current = false; setBusy(false); }
  }
  async function remove() {
    if (!deleting || guard.current) return;
    guard.current = true; setBusy(true); setDeleteError(undefined);
    const body = JSON.stringify({ id: deleting.id, version: deleting.version });
    if (deletePending.current?.body !== body) deletePending.current = { body, key: crypto.randomUUID() };
    try {
      await api(`${base}/material-purchases/${deleting.id}`, { method: 'DELETE', body: JSON.stringify({ expectedVersion: deleting.version, requestKey: deletePending.current.key }) });
      setDeleting(null); deletePending.current = null; setRevision(v => v + 1);
    } catch (e) { setDeleteError((e as Error).message); }
    finally { guard.current = false; setBusy(false); }
  }

  const categories = [...new Set((materials ?? []).map(m => m.categoryName))].sort((a, b) => a.localeCompare(b, 'vi'));
  const options = categories.map(category => ({ label: category, options: (materials ?? []).filter(m => m.categoryName === category)
    .sort((a, b) => a.name.localeCompare(b.name, 'vi')).map(m => ({ value: m.id, label: m.name })) }));
  const totals = data?.totals;
  const flat = { display: 'flex', flexWrap: 'wrap' as const, alignItems: 'center', gap: 8, marginBottom: 12 };
  return <div>
    <div style={flat}>
      <span>Từ</span><Input type="date" style={{ width: 150 }} aria-label="Ngày nhập từ" value={from} max={to || undefined} onChange={e => { setFrom(e.target.value); setPage(1); }} />
      <span>Đến</span><Input type="date" style={{ width: 150 }} aria-label="Ngày nhập đến" value={to} min={from || undefined} onChange={e => { setTo(e.target.value); setPage(1); }} />
      <Input.Search style={{ flex: '1 1 260px', minWidth: 220 }} aria-label="Tìm theo tên dụng cụ/vật liệu" placeholder="Tất cả các loại dụng cụ/vật liệu" maxLength={160} allowClear value={search}
        onChange={e => { setSearch(e.target.value); if (!e.target.value) { setQuery(''); setPage(1); } }} onSearch={v => { setQuery(v.trim()); setPage(1); }} />
      <Select style={{ width: 200 }} aria-label="Lọc thanh toán" placeholder="Mọi trạng thái thanh toán" allowClear value={payment} options={paymentOptions} onChange={v => { setPayment(v); setPage(1); }} />
      <Button type="primary" icon={<ReloadOutlined />} aria-label="Làm mới" onClick={() => setRevision(v => v + 1)} />
    </div>
    <div style={flat}>
      <span><strong>Tổng cộng:</strong> {vnd(totals?.totalVnd ?? 0)}</span>
      <span><strong>Đã trả:</strong> {vnd(totals?.paidVnd ?? 0)}</span>
      <span><strong>Còn lại:</strong> {vnd(totals?.dueVnd ?? 0)}</span>
    </div>
    {materialError && <Alert type="error" showIcon message={materialError} action={<Button onClick={() => setRevision(v => v + 1)}>Thử lại danh mục</Button>} />}
    {materials?.length === 0 && <Alert type="info" showIcon message="Chưa có dụng cụ/vật liệu trong danh mục. Hãy thêm danh mục trước khi ghi nhập hàng." />}
    {error && <Alert type="error" showIcon message={error} action={<Button onClick={() => setRevision(v => v + 1)}>Thử lại</Button>} />}
    <Table<Purchase> rowKey="id" size="small" bordered loading={loading} dataSource={data?.items ?? []} scroll={{ x: 900 }}
      locale={{ emptyText: <Empty description="Chưa có lần nhập hàng nào." /> }}
      pagination={{ current: page, pageSize: size, total: data?.total ?? 0, showSizeChanger: true, pageSizeOptions: [20, 50, 100], showTotal: total => `Có ${total} kết quả.`, onChange: (p, s) => { setPage(s !== size ? 1 : p); setSize(s); } }}
      columns={[
        { title: 'Ngày nhập', dataIndex: 'enteredOn', width: 110, render: v => formatDate(v) },
        { title: 'Ngày nhận', dataIndex: 'receivedOn', width: 110, render: v => v ? formatDate(v) : <span style={{ color: '#667085' }}>Chưa nhận</span> },
        { title: 'Tên dụng cụ/vật liệu', key: 'name', render: (_v, r) => <>{r.materialName} <small style={{ color: '#667085' }}>· {r.categoryName}</small></> },
        { title: 'SL', key: 'quantity', width: 100, align: 'right', render: (_v, r) => `${r.quantity.toLocaleString('vi-VN')} ${r.unit}` },
        { title: 'Đơn giá', dataIndex: 'unitPriceVnd', width: 120, align: 'right', render: v => vnd(v) },
        { title: 'Tổng cộng', dataIndex: 'totalVnd', width: 130, align: 'right', render: v => vnd(v) },
        { title: 'Đã thanh toán', key: 'paid', width: 150, align: 'right', render: (_v, r) => <>{vnd(r.paidVnd)}<br />{paymentTag(r)}</> },
        { title: <Button type="primary" size="small" style={{ background: '#52c41a' }} icon={<PlusOutlined />} aria-label="Thêm mới" disabled={!canCreate || !materials?.length} onClick={() => open()} />, key: 'actions', width: 80, align: 'center',
          render: (_v, r) => <Space size={4}>
            <Button type="text" size="small" icon={<EditOutlined />} disabled={!canUpdate} aria-label={`Sửa nhập hàng ${r.materialName}`}onClick={() => open(r)} />
            <Button type="text" danger size="small" icon={<DeleteOutlined />} disabled={!canDelete} aria-label={`Xóa nhập hàng ${r.materialName}`}onClick={() => { setDeleting(r); setDeleteError(undefined); deletePending.current = null; }} /></Space> },
      ]} />
    <Modal forceRender open={!!dialog} title={`${dialog?.row ? 'Sửa' : 'Thêm'} lần nhập hàng`} okText="Lưu" okButtonProps={{ 'aria-label': 'Lưu' }} cancelText="Hủy" confirmLoading={busy}
      closable={!busy} cancelButtonProps={{ disabled: busy }} onCancel={() => { if (!busy) setDialog(null); }} onOk={() => form.submit()}>
      {formError && <Alert type="error" showIcon message={formError} />}
      {formError && dialog?.row && <Button disabled={busy} onClick={() => void readVersion()}>Đọc lại phiên bản, giữ nội dung</Button>}
      <Form form={form} layout="vertical" disabled={busy} onFinish={v => void save(v)}>
        <Space align="start" wrap>
          <Form.Item name="enteredOn" label="Ngày nhập" rules={[{ required: true, message: 'Vui lòng nhập Ngày nhập.' }]}><Input type="date" aria-label="Ngày nhập" max={clinicToday()} /></Form.Item>
          <Form.Item name="receivedOn" label="Ngày nhận" extra="Để trống nếu chưa nhận hàng."><Input type="date" aria-label="Ngày nhận" max={clinicToday()} /></Form.Item>
        </Space>
        <Form.Item name="materialId" label="Dụng cụ/Vật liệu" rules={[{ required: true, message: 'Vui lòng chọn dụng cụ/vật liệu.' }]}>
          <Select aria-label="Dụng cụ/Vật liệu" showSearch optionFilterProp="label" placeholder="Chọn dụng cụ/vật liệu" loading={!materials} options={options} />
        </Form.Item>
        <Space align="start" wrap>
          <Form.Item name="quantity" label="Số lượng" rules={[{ required: true, message: 'Vui lòng nhập số lượng.' }, { type: 'number', min: 0.001, message: 'Số lượng phải lớn hơn 0.' }]}>
            <InputNumber aria-label="Số lượng" min={0} max={1000000} step={1} precision={3} stringMode={false} decimalSeparator="," />
          </Form.Item>
          <Form.Item name="unit" label="Đơn vị" extra="Theo chứng từ mua: hộp, lọ, tuýp…" rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập đơn vị.' }, { max: 40, message: 'Tối đa 40 ký tự.' }]}>
            <AutoComplete aria-label="Đơn vị" options={units} maxLength={40} filterOption={(input, option) => (option?.value ?? '').toLowerCase().includes(input.toLowerCase())} />
          </Form.Item>
        </Space>
        <Space align="start" wrap>
          <Form.Item name="unitPriceVnd" label="Đơn giá (₫)" rules={[{ required: true, message: 'Vui lòng nhập đơn giá.' }]}>
            <InputNumber aria-label="Đơn giá" min={0} max={1_000_000_000_000} precision={0} step={1000} style={{ width: 180 }} />
          </Form.Item>
          <Form.Item name="paidVnd" label="Đã thanh toán (₫)" rules={[{ required: true, message: 'Vui lòng nhập số đã thanh toán.' }]}>
            <InputNumber aria-label="Đã thanh toán" min={0} max={1_000_000_000_000} precision={0} step={1000} style={{ width: 180 }} />
          </Form.Item>
        </Space>
        <p aria-live="polite">Tổng: <strong>{vnd(preview)}</strong> · Còn nợ: <strong>{vnd(Math.max(preview - previewPaid, 0))}</strong>
          {previewPaid > preview && <span role="alert" style={{ color: '#b42318' }}> · Số đã thanh toán lớn hơn Tổng.</span>}</p>
      </Form>
    </Modal>
    <Modal open={!!deleting} title="Bạn có muốn xóa lần nhập hàng này không?" okText="Đồng ý" okButtonProps={{ 'aria-label': 'Đồng ý' }} cancelText="Không đồng ý" confirmLoading={busy}
      closable={!busy} cancelButtonProps={{ disabled: busy }} onCancel={() => { if (!busy) setDeleting(null); }} onOk={() => void remove()}>
      <p>{deleting && `${deleting.materialName} · ${formatDate(deleting.enteredOn)} · ${vnd(deleting.totalVnd)}`}</p>{deleteError && <Alert type="error" showIcon message={deleteError} />}
      {deleteError && <Button disabled={busy} onClick={() => void readVersion(true)}>Đọc lại phiên bản</Button>}
    </Modal>
  </div>;
}
