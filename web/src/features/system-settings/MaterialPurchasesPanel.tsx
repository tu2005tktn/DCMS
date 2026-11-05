import { DeleteOutlined, EditOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import { Alert, AutoComplete, Button, Empty, Form, Input, InputNumber, Modal, Select, Space, Table, Tag } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
import { clinicToday, formatDate } from '../appointments/types';
import { useAuth } from '../authentication/AuthContext';

// Purchase log of materials/instruments. Rules and API: docs/specs/MATERIAL-PURCHASES.md.
type Purchase = { id: string; enteredOn: string; receivedOn: string | null; expiresOn: string | null; materialId: string; materialName: string; categoryName: string;
  quantity: number; unit: string; unitPriceVnd: number; totalVnd: number; paidVnd: number; dueVnd: number; version: number;
  conversionFactor: number; usageUnit: string | null; usageQuantity: number; usageUnitPriceVnd: number; supplierId: string | null; supplierName: string | null; lotNumber: string | null };
type Supplier = { id: string; name: string };
type Totals = { totalVnd: number; paidVnd: number; dueVnd: number };
type PurchasePage = { items: Purchase[]; total: number; page: number; pageSize: number; totals: Totals };
type Material = { id: string; name: string; categoryName: string; usageUnit?: string | null };
type Values = { enteredOn: string; receivedOn?: string; expiresOn?: string; materialId?: string; quantity?: number; unit?: string; conversionFactor?: number; lotNumber?: string; supplierId?: string; unitPriceVnd?: number; paidVnd?: number };
type SupplierValues = { name: string };
const newSupplierCode = () => `NCC-${crypto.randomUUID().replace(/-/g, '').slice(0, 6).toUpperCase()}`;
const num = (n: number) => n.toLocaleString('vi-VN', { maximumFractionDigits: 3 });
const base = '/api/system-settings';
const vnd = (n: number) => `${n.toLocaleString('vi-VN')} ₫`;
const units = ['cái', 'lọ', 'miếng', 'hộp', 'tuýp', 'chai', 'ống', 'bộ', 'gói', 'túi', 'g', 'mL'].map(value => ({ value }));
const paymentOptions = [{ value: 'unpaid', label: 'Chưa thanh toán' }, { value: 'partial', label: 'Còn nợ' }, { value: 'paid', label: 'Đã thanh toán đủ' }];
const paymentTag = (row: Purchase) => row.dueVnd <= 0 ? <Tag color="green">Đã thanh toán đủ</Tag> : row.paidVnd > 0 ? <Tag color="orange">Còn nợ</Tag> : <Tag color="red">Chưa thanh toán</Tag>;

function expiryTag(expiresOn: string | null) {
  if (!expiresOn) return <span style={{ color: '#667085' }}>—</span>;
  const today = clinicToday(), limit = new Date(`${today}T00:00:00`); limit.setDate(limit.getDate() + 90);
  const soon = limit.toLocaleDateString('sv-SE');
  return <>{formatDate(expiresOn)}{expiresOn < today ? <><br /><Tag color="red">Hết hạn</Tag></> : expiresOn <= soon ? <><br /><Tag color="orange">Sắp hết hạn</Tag></> : null}</>;
}

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
  const [form] = Form.useForm<Values>(), [supplierForm] = Form.useForm<SupplierValues>();
  const canAddSupplier = user?.permissions.includes('catalog.providers.create');
  const [suppliers, setSuppliers] = useState<Supplier[]>([]), [addingSupplier, setAddingSupplier] = useState(false), [supplierError, setSupplierError] = useState<string>();
  const supplierPending = useRef<{ body: string; key: string; code: string } | null>(null);
  const watched = Form.useWatch([], form);
  const preview = Math.round((watched?.quantity ?? 0) * (watched?.unitPriceVnd ?? 0)), previewPaid = watched?.paidVnd ?? 0;
  const usageUnit = materials?.find(m => m.id === watched?.materialId)?.usageUnit ?? undefined;
  const factor = usageUnit ? (watched?.conversionFactor ?? 1) : 1;
  const usagePreview = Math.round((watched?.quantity ?? 0) * factor * 1000) / 1000, usagePrice = factor > 0 ? Math.round((watched?.unitPriceVnd ?? 0) / factor * 100) / 100 : 0;

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

  function loadSuppliers(term = '') {
    void api<Supplier[]>(`${base}/reference-options?kind=suppliers${term ? `&search=${encodeURIComponent(term)}` : ''}`).then(setSuppliers).catch(() => setSuppliers([]));
  }
  async function saveSupplier(values: SupplierValues) {
    if (guard.current) return;
    guard.current = true; setBusy(true); setSupplierError(undefined);
    const entry = { name: values.name.trim(), type: 'Supplier', phone: null, email: null, address: null };
    const fingerprint = JSON.stringify(entry);
    if (supplierPending.current?.body !== fingerprint) supplierPending.current = { body: fingerprint, key: crypto.randomUUID(), code: supplierPending.current?.code ?? newSupplierCode() };
    try {
      const created = await api<Supplier>(`${base}/settings/catalogs/providers`, { method: 'POST', body: JSON.stringify({ entry: { ...entry, code: supplierPending.current.code }, requestKey: supplierPending.current.key }) });
      setSuppliers(list => [...list.filter(x => x.id !== created.id), { id: created.id, name: created.name }]);
      form.setFieldValue('supplierId', created.id);
      setAddingSupplier(false); supplierForm.resetFields(); supplierPending.current = null;
    } catch (e) {
      const failure = e as ApiError; setSupplierError(failure.message);
      if (failure.field === 'name') supplierForm.setFields([{ name: 'name', errors: [failure.message] }]);
      if (failure.field === 'code' && supplierPending.current) supplierPending.current = { ...supplierPending.current, code: newSupplierCode(), key: crypto.randomUUID() };
    } finally { guard.current = false; setBusy(false); }
  }
  function open(row?: Purchase) {
    pending.current = null; setFormError(undefined); form.resetFields(); loadSuppliers();
    form.setFieldsValue(row ? { enteredOn: row.enteredOn, receivedOn: row.receivedOn ?? undefined, expiresOn: row.expiresOn ?? undefined, lotNumber: row.lotNumber ?? undefined, materialId: row.materialId, quantity: row.quantity, unit: row.unit,
      conversionFactor: row.conversionFactor, supplierId: row.supplierId ?? undefined, unitPriceVnd: row.unitPriceVnd, paidVnd: row.paidVnd } : { enteredOn: clinicToday(), quantity: 1, conversionFactor: 1, paidVnd: 0 });
    setDialog({ row });
  }
  async function save(values: Values) {
    if (!dialog || guard.current) return;
    guard.current = true; setBusy(true); setFormError(undefined);
    const body = { enteredOn: values.enteredOn, receivedOn: values.receivedOn || null, expiresOn: values.expiresOn || null, lotNumber: (values.lotNumber ?? '').trim() || null, materialId: values.materialId, quantity: values.quantity,
      unit: (values.unit ?? '').trim(), conversionFactor: usageUnit ? (values.conversionFactor ?? 1) : 1, supplierId: values.supplierId ?? null, unitPriceVnd: values.unitPriceVnd ?? 0, paidVnd: values.paidVnd ?? 0, ...(dialog.row ? { expectedVersion: dialog.row.version } : {}) };
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
    <Table<Purchase> rowKey="id" size="small" bordered loading={loading} dataSource={data?.items ?? []} scroll={{ x: 1020 }}
      locale={{ emptyText: <Empty description="Chưa có lần nhập hàng nào." /> }}
      pagination={{ current: page, pageSize: size, total: data?.total ?? 0, showSizeChanger: true, pageSizeOptions: [20, 50, 100], showTotal: total => `Có ${total} kết quả.`, onChange: (p, s) => { setPage(s !== size ? 1 : p); setSize(s); } }}
      columns={[
        { title: 'Ngày nhập', dataIndex: 'enteredOn', width: 110, render: v => formatDate(v) },
        { title: 'Ngày nhận', dataIndex: 'receivedOn', width: 110, render: v => v ? formatDate(v) : <span style={{ color: '#667085' }}>Chưa nhận</span> },
        { title: 'Hạn dùng', dataIndex: 'expiresOn', width: 120, render: v => expiryTag(v) },
        { title: 'Số lô', dataIndex: 'lotNumber', width: 110, render: v => v ?? <span style={{ color: '#667085' }}>—</span> },
        { title: 'Tên dụng cụ/vật liệu', key: 'name', render: (_v, r) => <>{r.materialName} <small style={{ color: '#667085' }}>· {r.categoryName}</small>{r.supplierName && <><br /><small style={{ color: '#667085' }}>NCC: {r.supplierName}</small></>}</> },
        { title: 'SL', key: 'quantity', width: 100, align: 'right', render: (_v, r) => <>{r.quantity.toLocaleString('vi-VN')} {r.unit}{r.usageUnit && r.conversionFactor !== 1 && <><br /><small style={{ color: '#667085' }}>= {num(r.usageQuantity)} {r.usageUnit}</small></>}</> },
        { title: 'Đơn giá', dataIndex: 'unitPriceVnd', width: 120, align: 'right', render: v => vnd(v) },
        { title: 'Tổng cộng', dataIndex: 'totalVnd', width: 130, align: 'right', render: v => vnd(v) },
        { title: 'Đã thanh toán', key: 'paid', width: 150, align: 'right', render: (_v, r) => <>{vnd(r.paidVnd)}<br />{paymentTag(r)}</> },
        { title: <Button type="primary" size="small" style={{ background: '#52c41a' }} icon={<PlusOutlined />} aria-label="Thêm mới" disabled={!canCreate || !materials?.length} onClick={() => open()} />, key: 'actions', width: 80, align: 'center',
          render: (_v, r) => <Space size={4}>
            <Button type="text" size="small" icon={<EditOutlined />} disabled={!canUpdate} aria-label={`Sửa nhập hàng ${r.materialName}`}onClick={() => open(r)} />
            <Button type="text" danger size="small" icon={<DeleteOutlined />} disabled={!canDelete} aria-label={`Xóa nhập hàng ${r.materialName}`}onClick={() => { setDeleting(r); setDeleteError(undefined); deletePending.current = null; }} /></Space> },
      ]} />
    <Modal forceRender width={640} open={!!dialog} title={`${dialog?.row ? 'Sửa' : 'Thêm'} lần nhập hàng`} okText="Lưu" okButtonProps={{ 'aria-label': 'Lưu' }} cancelText="Hủy" confirmLoading={busy}
      closable={!busy} cancelButtonProps={{ disabled: busy }} onCancel={() => { if (!busy) setDialog(null); }} onOk={() => form.submit()}>
      {formError && <Alert type="error" showIcon message={formError} />}
      {formError && dialog?.row && <Button disabled={busy} onClick={() => void readVersion()}>Đọc lại phiên bản, giữ nội dung</Button>}
      <Form form={form} layout="vertical" disabled={busy} onFinish={v => void save(v)}>
        <Space align="start" wrap>
          <Form.Item name="enteredOn" label="Ngày nhập" rules={[{ required: true, message: 'Vui lòng nhập Ngày nhập.' }]}><Input type="date" aria-label="Ngày nhập" max={clinicToday()} /></Form.Item>
          <Form.Item name="receivedOn" label="Ngày nhận"><Input type="date" aria-label="Ngày nhận" max={clinicToday()} /></Form.Item>
          <Form.Item name="expiresOn" label="Hạn dùng"><Input type="date" aria-label="Hạn dùng" /></Form.Item>
          <Form.Item name="lotNumber" label="Số lô"><Input aria-label="Số lô" maxLength={60} /></Form.Item>
        </Space>
        <Form.Item name="materialId" label="Dụng cụ/Vật liệu" rules={[{ required: true, message: 'Vui lòng chọn dụng cụ/vật liệu.' }]}>
          <Select aria-label="Dụng cụ/Vật liệu" showSearch optionFilterProp="label" placeholder="Chọn dụng cụ/vật liệu" loading={!materials} options={options} />
        </Form.Item>
        <Space align="start" wrap>
          <Form.Item name="quantity" label="Số lượng" rules={[{ required: true, message: 'Vui lòng nhập số lượng.' }, { type: 'number', min: 0.001, message: 'Số lượng phải lớn hơn 0.' }]}>
            <InputNumber aria-label="Số lượng" min={0} max={1000000} step={1} stringMode={false} decimalSeparator="," />
          </Form.Item>
          <Form.Item name="unit" label="Đơn vị" rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập đơn vị.' }, { max: 40, message: 'Tối đa 40 ký tự.' }]}>
            <AutoComplete aria-label="Đơn vị" options={units} maxLength={40} filterOption={(input, option) => (option?.value ?? '').toLowerCase().includes(input.toLowerCase())} />
          </Form.Item>
        </Space>
        {usageUnit && <Form.Item label="Quy đổi sang đơn vị dùng" required>
          <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 8 }}>
            <span>1 {watched?.unit?.trim() || 'đơn vị nhập'} =</span>
            <Form.Item name="conversionFactor" noStyle rules={[{ required: true, message: 'Vui lòng nhập hệ số quy đổi.' }, { type: 'number', min: 0.001, message: 'Hệ số phải lớn hơn 0.' }]}>
              <InputNumber aria-label="Hệ số quy đổi" min={0} max={1000000} step={1} stringMode={false} decimalSeparator="," style={{ width: 120 }} />
            </Form.Item>
          </div>
          <div aria-live="polite" style={{ marginTop: 4 }}>Quy ra: <strong>{num(usagePreview)} {usageUnit}</strong> · Giá mỗi {usageUnit}: <strong>{usagePrice.toLocaleString('vi-VN', { maximumFractionDigits: 2 })} ₫</strong></div>
        </Form.Item>}
        <Form.Item label="Công ty cung cấp">
          <Space.Compact style={{ width: '100%' }}>
            <Form.Item name="supplierId" noStyle>
              <Select aria-label="Công ty cung cấp" showSearch allowClear filterOption={false} placeholder="Không chọn" onSearch={loadSuppliers} style={{ width: '100%' }}
                options={[...suppliers.map(s => ({ value: s.id, label: s.name })),
                  ...(dialog?.row?.supplierId && !suppliers.some(s => s.id === dialog.row?.supplierId) ? [{ value: dialog.row.supplierId, label: dialog.row.supplierName ?? '' }] : [])]} />
            </Form.Item>
            <Button icon={<PlusOutlined />} aria-label="Thêm công ty cung cấp" title={canAddSupplier ? 'Thêm công ty cung cấp mới' : 'Bạn không có quyền thêm công ty cung cấp'} disabled={!canAddSupplier || busy}
              onClick={() => { supplierPending.current = null; setSupplierError(undefined); supplierForm.resetFields(); setAddingSupplier(true); }} />
          </Space.Compact>
        </Form.Item>
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
    <Modal forceRender open={addingSupplier} title="Thêm công ty cung cấp" okText="Lưu" okButtonProps={{ 'aria-label': 'Lưu công ty' }} cancelText="Hủy" confirmLoading={busy}
      closable={!busy} cancelButtonProps={{ disabled: busy }} onCancel={() => { if (!busy) setAddingSupplier(false); }} onOk={() => supplierForm.submit()}>
      {supplierError && <Alert type="error" showIcon message={supplierError} />}
      <Form form={supplierForm} layout="vertical" disabled={busy} onFinish={v => void saveSupplier(v)}>
        <Form.Item name="name" label="Tên công ty" rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập tên công ty.' }, { max: 160, message: 'Tên tối đa 160 ký tự.' }]}>
          <Input aria-label="Tên công ty" maxLength={160} autoComplete="off" /></Form.Item>
      </Form>
    </Modal>
    <Modal open={!!deleting} title="Bạn có muốn xóa lần nhập hàng này không?" okText="Đồng ý" okButtonProps={{ 'aria-label': 'Đồng ý' }} cancelText="Không đồng ý" confirmLoading={busy}
      closable={!busy} cancelButtonProps={{ disabled: busy }} onCancel={() => { if (!busy) setDeleting(null); }} onOk={() => void remove()}>
      <p>{deleting && `${deleting.materialName} · ${formatDate(deleting.enteredOn)} · ${vnd(deleting.totalVnd)}`}</p>{deleteError && <Alert type="error" showIcon message={deleteError} />}
      {deleteError && <Button disabled={busy} onClick={() => void readVersion(true)}>Đọc lại phiên bản</Button>}
    </Modal>
  </div>;
}
