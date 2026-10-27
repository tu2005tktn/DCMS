import { Alert, Button, Checkbox, Drawer, Empty, Form, Input, InputNumber, Menu, Modal, Segmented, Select, Space, Table, Tag, Timeline, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useEffect, useRef, useState, type ReactNode } from 'react';
import { api, ApiError, readAccessToken } from '../../services/api';

// S58 đợt 4: companies, lab work types and medicines. Contract: docs/specs/S58-SUPPLY-CATALOGS.md.
type Row = { id: string; code: string; name: string; active: boolean; version: number; createdByName: string; updatedAt: string; updatedByName: string } & Record<string, unknown>;
type Page = { items: Row[]; total: number; page: number; pageSize: number };
type StatePreview = { entry: Row; allowed: boolean; blockers: string[]; impact: string };
type HistoryItem = { occurredAt: string; actorName: string; command: string; reason: string | null; before: Row | null; after: Row };
type Status = 'Active' | 'Inactive' | 'All';
type Option = { id: string; code: string | null; name: string };
type SectionKey = 'suppliers' | 'labs' | 'lab-work-types' | 'medicines';

const vnd = (value: unknown) => typeof value === 'number' ? `${value.toLocaleString('vi-VN')} ₫` : 'Chưa xác định';
const providerTypes = [{ value: 'Supplier', label: 'Cung cấp vật liệu' }, { value: 'Lab', label: 'Labo' }, { value: 'Both', label: 'Cả hai' }];
const units = [{ value: 'Jaw', label: 'Hàm' }, { value: 'HalfJaw', label: 'Bán hàm' }, { value: 'Tooth', label: 'Răng' }, { value: 'Piece', label: 'Cái' }, { value: 'Set', label: 'Bộ' }];
const label = (list: { value: string; label: string }[], value: unknown) => list.find(x => x.value === value)?.label ?? String(value ?? '—');
const text = (value: unknown) => (value as string | null) || '—';
const priceRule = { validator: (_: unknown, v?: number | null) => v == null || (Number.isInteger(v) && v >= 0) ? Promise.resolve() : Promise.reject(new Error('Giá là số nguyên không âm (VND).')) };

type Section = {
  key: SectionKey; title: string; resource: string; type?: string; help?: string; nameMax: number; exportable?: boolean;
  defaults: Record<string, unknown>; columns: ColumnsType<Row>; fields: (providers: Option[], row?: Row) => ReactNode;
};
const providerFields = () => <>
  <Form.Item name="type" label="Loại công ty" rules={[{ required: true, message: 'Vui lòng chọn Loại công ty.' }]}><Select options={providerTypes} /></Form.Item>
  <Form.Item name="address" label="Địa chỉ" rules={[{ max: 1000, message: 'Địa chỉ tối đa 1000 ký tự.' }]}><Input.TextArea rows={2} maxLength={1000} /></Form.Item>
  <Form.Item name="phone" label="Số điện thoại" rules={[{ max: 20, message: 'Số điện thoại tối đa 20 ký tự.' }, { pattern: /^\+?[0-9 ().-]*$/, message: 'Số điện thoại không đúng định dạng.' }]}>
    <Input maxLength={20} /></Form.Item>
  <Form.Item name="email" label="Email" rules={[{ type: 'email', message: 'Email không đúng định dạng.' }, { max: 254, message: 'Email tối đa 254 ký tự.' }]}><Input maxLength={254} /></Form.Item>
</>;
const providerColumns: ColumnsType<Row> = [
  { title: 'Loại', dataIndex: 'type', width: 150, render: v => label(providerTypes, v) },
  { title: 'Điện thoại', dataIndex: 'phone', width: 140, render: text },
  { title: 'Email', dataIndex: 'email', ellipsis: true, render: text },
];
const sections: Section[] = [
  { key: 'suppliers', title: 'Công ty cung cấp vật liệu', resource: 'providers', type: 'Supplier', nameMax: 160, defaults: { type: 'Supplier' },
    help: 'Công ty loại "Cả hai" hiện ở cả tab này và tab Công ty labo; đó là cùng một công ty.', columns: providerColumns, fields: providerFields },
  { key: 'labs', title: 'Công ty labo', resource: 'providers', type: 'Lab', nameMax: 160, defaults: { type: 'Lab' }, columns: providerColumns, fields: providerFields },
  { key: 'lab-work-types', title: 'Loại công việc labo', resource: 'lab-work-types', nameMax: 160, defaults: { unit: 'Tooth' },
    help: 'Giá, đơn vị và bảo hành là mặc định tham khảo; yêu cầu labo sẽ lưu lại giá trị tại thời điểm tạo.',
    columns: [
      { title: 'Công ty mặc định', dataIndex: 'providerName', render: (v, row) => v ? <>{String(v)}{row.providerActive === false && <Tag className="general-settings-tag">Ngừng dùng</Tag>}</> : '—' },
      { title: 'Đơn vị', dataIndex: 'unit', width: 100, render: v => label(units, v) },
      { title: 'Giá tham khảo', dataIndex: 'referencePrice', width: 150, align: 'right', render: vnd },
    ],
    fields: (providers, row) => <>
      <Form.Item name="providerId" label="Công ty labo mặc định">
        <Select allowClear showSearch optionFilterProp="label" placeholder="Không chọn" options={[...providers.map(x => ({ value: x.id, label: x.name })),
          ...(row?.providerId && !providers.some(x => x.id === row.providerId) ? [{ value: row.providerId as string, label: `${String(row.providerName)} (ngừng dùng)` }] : [])]} />
      </Form.Item>
      <Form.Item name="unit" label="Đơn vị" rules={[{ required: true, message: 'Vui lòng chọn Đơn vị.' }]}><Select options={units} /></Form.Item>
      <Form.Item name="referencePrice" label="Giá tham khảo (VND)" extra="Để trống nếu chưa xác định; 0 là giá tham khảo bằng 0." rules={[priceRule]}>
        <InputNumber style={{ width: '100%' }} min={0} step={1000} precision={0} /></Form.Item>
      <Form.Item name="warranty" label="Bảo hành" rules={[{ max: 1000, message: 'Bảo hành tối đa 1000 ký tự.' }]}><Input.TextArea rows={2} maxLength={1000} /></Form.Item>
    </> },
  { key: 'medicines', title: 'Thuốc', resource: 'medicines', nameMax: 200, exportable: true, defaults: { prescriptionEligible: true, internalStockTracked: false },
    help: 'Thuốc kê mua ngoài không cần theo dõi tồn và không trừ kho. Lời dặn là mặc định cho dòng đơn mới; nha sĩ sửa theo từng bệnh nhân.',
    columns: [
      { title: 'Hoạt chất', dataIndex: 'activeIngredient', ellipsis: true, render: text },
      { title: 'Đơn vị', dataIndex: 'baseUnit', width: 90 },
      { title: 'Giá tham khảo', dataIndex: 'referencePrice', width: 140, align: 'right', render: vnd },
      { title: 'Kê đơn', dataIndex: 'prescriptionEligible', width: 90, render: v => v ? <Tag color="blue">Có</Tag> : '—' },
    ],
    fields: () => <>
      <Form.Item name="activeIngredient" label="Hoạt chất / biệt dược" rules={[{ max: 200, message: 'Tối đa 200 ký tự.' }]}><Input maxLength={200} /></Form.Item>
      <Space.Compact block>
        <Form.Item name="strength" label="Hàm lượng" style={{ flex: 1 }} rules={[{ max: 200, message: 'Tối đa 200 ký tự.' }]}><Input maxLength={200} /></Form.Item>
        <Form.Item name="dosageForm" label="Dạng bào chế" style={{ flex: 1 }} rules={[{ max: 200, message: 'Tối đa 200 ký tự.' }]}><Input maxLength={200} /></Form.Item>
      </Space.Compact>
      <Form.Item name="baseUnit" label="Đơn vị gốc" extra="Ví dụ: viên, gói, ống." rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập Đơn vị gốc.' }, { max: 40, message: 'Tối đa 40 ký tự.' }]}>
        <Input maxLength={40} /></Form.Item>
      <Form.Item name="packaging" label="Quy cách đóng gói" rules={[{ max: 160, message: 'Tối đa 160 ký tự.' }]}><Input maxLength={160} placeholder="Hộp 10 vỉ x 10 viên" /></Form.Item>
      <Form.Item name="instructions" label="Hướng dẫn sử dụng mặc định" rules={[{ max: 1000, message: 'Tối đa 1000 ký tự.' }]}><Input.TextArea rows={2} maxLength={1000} showCount /></Form.Item>
      <Form.Item name="referencePrice" label="Giá tham khảo (VND)" extra="Để trống nếu chưa xác định." rules={[priceRule]}>
        <InputNumber style={{ width: '100%' }} min={0} step={1000} precision={0} /></Form.Item>
      <Form.Item name="prescriptionEligible" valuePropName="checked"><Checkbox>Dùng để kê đơn</Checkbox></Form.Item>
      <Form.Item name="internalStockTracked" valuePropName="checked"><Checkbox>Theo dõi tồn kho nội bộ</Checkbox></Form.Item>
    </> },
];
const base = '/api/settings/catalogs';
const commandLabels: Record<string, string> = { create: 'Tạo mới', update: 'Sửa', state: 'Đổi trạng thái' };

export default function SupplyCatalogPanel() {
  const [key, setKey] = useState<SectionKey>('suppliers'), [status, setStatus] = useState<Status>('Active');
  const [search, setSearch] = useState(''), [query, setQuery] = useState(''), [page, setPage] = useState(1), [size, setSize] = useState(20);
  const [data, setData] = useState<Page | null>(null), [loading, setLoading] = useState(false), [error, setError] = useState<string>(), [revision, setRevision] = useState(0);
  const [editing, setEditing] = useState<{ row?: Row } | null>(null), [formError, setFormError] = useState<ApiError>();
  const [stateTarget, setStateTarget] = useState<StatePreview | null>(null), [stateReason, setStateReason] = useState(''), [stateError, setStateError] = useState<string>();
  const [history, setHistory] = useState<{ row: Row; items: HistoryItem[] | null; error?: string } | null>(null);
  const [labProviders, setLabProviders] = useState<Option[]>([]);
  const [busy, setBusy] = useState(false), guard = useRef(false), requestKey = useRef<{ fingerprint: string; key: string } | null>(null);
  const [form] = Form.useForm<Record<string, unknown>>();
  const section = sections.find(x => x.key === key)!;

  function keyFor(fingerprint: string) {
    if (requestKey.current?.fingerprint !== fingerprint) requestKey.current = { fingerprint, key: crypto.randomUUID() };
    return requestKey.current.key;
  }
  useEffect(() => {
    let live = true; setLoading(true); setError(undefined);
    const params = new URLSearchParams({ q: query, status, page: String(page), pageSize: String(size), ...(section.type ? { type: section.type } : {}) });
    void api<Page>(`${base}/${section.resource}?${params}`).then(v => {
      if (!live) return;
      if (!v.items.length && v.total && page > 1) { setPage(Math.max(1, Math.ceil(v.total / size))); return; }
      setData(v);
    }).catch(e => { if (live) { setError((e as Error).message); setData(null); } }).finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [key, status, query, page, size, revision]);

  function select(next: SectionKey) { setKey(next); setPage(1); setSearch(''); setQuery(''); setData(null); }
  async function run<T>(action: () => Promise<T>, onError: (e: ApiError) => void) {
    if (guard.current) return undefined;
    guard.current = true; setBusy(true);
    try { return await action(); } catch (e) { onError(e as ApiError); return undefined; } finally { guard.current = false; setBusy(false); }
  }
  function open(row?: Row) {
    requestKey.current = null; setFormError(undefined); form.resetFields();
    form.setFieldsValue((row ? { ...row, reason: '' } : { ...section.defaults }) as Record<string, never>);
    if (section.key === 'lab-work-types') void api<Option[]>('/api/reference-options?kind=lab-providers').then(setLabProviders).catch(() => setLabProviders([]));
    setEditing({ row });
  }
  async function save() {
    if (!editing) return;
    const values = await form.validateFields().catch(() => null);
    if (!values) return;
    const { reason, ...rest } = values;
    const entry = Object.fromEntries(Object.entries({ ...section.defaults, ...rest })
      .filter(([k]) => !['id', 'active', 'version', 'createdAt', 'createdByName', 'updatedAt', 'updatedByName', 'providerName', 'providerActive'].includes(k))
      .map(([k, v]) => [k, typeof v === 'string' ? (v.trim() || null) : v ?? null]));
    const body = editing.row ? { expectedVersion: editing.row.version, reason: String(reason ?? '').trim(), entry } : { entry };
    const requestKeyValue = keyFor(JSON.stringify({ resource: section.resource, id: editing.row?.id, body }));
    const saved = await run(() => api<Row>(`${base}/${section.resource}${editing.row ? `/${editing.row.id}` : ''}`, {
      method: editing.row ? 'PUT' : 'POST', body: JSON.stringify({ ...body, requestKey: requestKeyValue }) }), failure => {
      setFormError(failure);
      if (failure.field && failure.field !== 'requestKey') form.setFields([{ name: failure.field, errors: [failure.message] }]);
    });
    if (!saved) return;
    setEditing(null); requestKey.current = null; setRevision(v => v + 1);
    void message.success(editing.row ? 'Đã cập nhật.' : 'Đã thêm mới.');
  }
  async function reloadEditing() {
    if (!editing?.row) return;
    const latest = await run(() => api<Row>(`${base}/${section.resource}/${editing.row!.id}`), failure => setFormError(failure));
    if (latest) { setEditing({ row: latest }); requestKey.current = null; setFormError(undefined); }
  }
  async function previewState(row: Row) {
    const preview = await run(() => api<StatePreview>(`${base}/${section.resource}/${row.id}/state-preview`, { method: 'POST', body: JSON.stringify({ active: !row.active }) }),
      failure => void message.error(failure.message));
    if (preview) { setStateTarget(preview); setStateReason(''); setStateError(undefined); requestKey.current = null; }
  }
  async function changeState() {
    if (!stateTarget) return;
    const reason = stateReason.trim();
    if (!reason) { setStateError('Vui lòng nhập Lý do.'); return; }
    const body = { expectedVersion: stateTarget.entry.version, active: !stateTarget.entry.active, reason };
    const k = keyFor(JSON.stringify({ id: stateTarget.entry.id, body }));
    const saved = await run(() => api<Row>(`${base}/${section.resource}/${stateTarget.entry.id}/state`, { method: 'POST', body: JSON.stringify({ ...body, requestKey: k }) }),
      failure => setStateError(failure.message));
    if (!saved) return;
    setStateTarget(null); setRevision(v => v + 1);
    void message.success(saved.active ? 'Đã khôi phục.' : 'Đã ngừng dùng.');
  }
  async function openHistory(row: Row) {
    setHistory({ row, items: null });
    try { setHistory({ row, items: await api<HistoryItem[]>(`${base}/${section.resource}/${row.id}/history`) }); }
    catch (e) { setHistory({ row, items: [], error: (e as Error).message }); }
  }
  // The file is fetched with the bearer token, so a plain link would not be authorized.
  async function exportExcel() {
    await run(async () => {
      const apiBase = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080').replace(/\/$/, '');
      const response = await fetch(`${apiBase}${base}/${section.resource}/export?${new URLSearchParams({ q: query, status })}`,
        { headers: { Authorization: `Bearer ${readAccessToken() ?? ''}` }, cache: 'no-store' });
      if (!response.ok) throw new ApiError(response.status, 'MSG144', 'Không thể xuất Excel. Vui lòng thử lại.');
      const url = URL.createObjectURL(await response.blob());
      const link = Object.assign(document.createElement('a'), { href: url, download: `danh-muc-${section.resource}.xlsx` });
      link.click(); URL.revokeObjectURL(url);
    }, failure => void message.error(failure.message));
  }

  return <div className="reference-catalogs">
    <Menu className="reference-catalogs-menu" mode="inline" selectedKeys={[key]} onClick={e => select(e.key as SectionKey)} items={sections.map(x => ({ key: x.key, label: x.title }))} />
    <div className="reference-catalogs-body">
      <Typography.Title level={4}>{section.title}</Typography.Title>
      {section.help && <Typography.Paragraph type="secondary">{section.help}</Typography.Paragraph>}
      <div className="system-settings-toolbar">
        <Input.Search aria-label="Tìm theo tên hoặc mã" placeholder="Tìm theo tên hoặc mã" maxLength={100} allowClear value={search}
          onChange={e => { setSearch(e.target.value); if (!e.target.value) { setQuery(''); setPage(1); } }} onSearch={v => { setQuery(v.trim()); setPage(1); }} />
        <Segmented<Status> value={status} onChange={v => { setStatus(v); setPage(1); }}
          options={[{ value: 'Active', label: 'Đang dùng' }, { value: 'Inactive', label: 'Ngừng dùng' }, { value: 'All', label: 'Tất cả' }]} />
        <Button type="primary" onClick={() => open()}>Thêm mới</Button>
        {section.exportable && <Button disabled={busy} onClick={() => void exportExcel()}>Xuất Excel</Button>}
      </div>
      {error && <Alert type="error" showIcon message={error} action={<Button onClick={() => setRevision(v => v + 1)}>Thử lại</Button>} />}
      <Table<Row> rowKey="id" loading={loading} dataSource={data?.items ?? []} scroll={{ x: 900 }}
        locale={{ emptyText: <Empty description={query ? 'Không có kết quả phù hợp.' : 'Chưa có dữ liệu.'} /> }}
        pagination={{ current: page, pageSize: size, total: data?.total ?? 0, showSizeChanger: true, pageSizeOptions: [20, 50, 100],
          onChange: (p, s) => { setPage(s !== size ? 1 : p); setSize(s); } }}
        columns={[
          { title: 'Mã', dataIndex: 'code', width: 130 },
          { title: 'Tên', dataIndex: 'name' },
          ...section.columns,
          { title: 'Trạng thái', dataIndex: 'active', width: 110, render: (v: boolean) => v ? <Tag color="green">Đang dùng</Tag> : <Tag>Ngừng dùng</Tag> },
          { title: 'Thao tác', key: 'actions', width: 240, fixed: 'right', render: (_, row) => <Space wrap>
            <Button size="small" aria-label={`Sửa ${row.name}`} onClick={() => open(row)}>Sửa</Button>
            <Button size="small" danger={row.active} disabled={busy} onClick={() => void previewState(row)}>{row.active ? 'Ngừng dùng' : 'Khôi phục'}</Button>
            <Button size="small" type="link" onClick={() => void openHistory(row)}>Lịch sử</Button>
          </Space> },
        ]} />
    </div>

    <Drawer open={!!editing} width={560} title={`${editing?.row ? 'Sửa' : 'Thêm'} · ${section.title}`} forceRender
      onClose={() => { if (!busy) setEditing(null); }}
      extra={<Space><Button disabled={busy} onClick={() => setEditing(null)}>Hủy</Button><Button type="primary" loading={busy} onClick={() => void save()}>Lưu</Button></Space>}>
      {formError && <Alert type="error" showIcon message={formError.message}
        action={formError.code === 'MSG111' && editing?.row ? <Button size="small" onClick={() => void reloadEditing()}>Tải lại bản mới</Button> : undefined} />}
      <Form form={form} layout="vertical" disabled={busy}>
        <Form.Item name="code" label="Mã" rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập Mã.' }, { max: 40, message: 'Mã tối đa 40 ký tự.' },
          { pattern: /^\s*[A-Za-z0-9][A-Za-z0-9._-]*\s*$/, message: 'Mã gồm chữ không dấu, số, dấu chấm, gạch dưới hoặc gạch nối.' }]}><Input maxLength={40} autoComplete="off" /></Form.Item>
        <Form.Item name="name" label="Tên" rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập Tên.' }, { max: section.nameMax, message: `Tên tối đa ${section.nameMax} ký tự.` }]}>
          <Input maxLength={section.nameMax} autoComplete="off" /></Form.Item>
        {section.fields(labProviders, editing?.row)}
        {editing?.row && <Form.Item name="reason" label="Lý do sửa" rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập Lý do.' }, { max: 500, message: 'Lý do tối đa 500 ký tự.' }]}>
          <Input.TextArea rows={2} maxLength={500} showCount /></Form.Item>}
      </Form>
    </Drawer>

    <Modal open={!!stateTarget} title={stateTarget?.entry.active ? `Ngừng dùng "${stateTarget.entry.name}"?` : `Khôi phục "${stateTarget?.entry.name}"?`}
      okText="Xác nhận" cancelText="Hủy" confirmLoading={busy} okButtonProps={{ disabled: !stateTarget?.allowed, danger: stateTarget?.entry.active }}
      closable={!busy} cancelButtonProps={{ disabled: busy }} onCancel={() => { if (!busy) setStateTarget(null); }} onOk={() => void changeState()}>
      <Typography.Paragraph>{stateTarget?.impact}</Typography.Paragraph>
      <Form layout="vertical" disabled={busy}>
        <Form.Item label="Lý do" required validateStatus={stateError ? 'error' : undefined} help={stateError}>
          <Input.TextArea rows={2} maxLength={500} showCount value={stateReason} onChange={e => { setStateReason(e.target.value); setStateError(undefined); }} />
        </Form.Item>
      </Form>
    </Modal>

    <Drawer open={!!history} width={480} title={`Lịch sử · ${history?.row.name ?? ''}`} onClose={() => setHistory(null)} loading={history?.items === null}>
      {history?.error && <Alert type="error" showIcon message={history.error} />}
      <Timeline items={(history?.items ?? []).map(x => ({ key: `${x.occurredAt}-${x.command}`, children: <>
        <Typography.Text strong>{commandLabels[x.command] ?? x.command}</Typography.Text> · {x.actorName}<br />
        <Typography.Text type="secondary">{new Date(x.occurredAt).toLocaleString('vi-VN')}</Typography.Text>
        {x.before && x.before.name !== x.after.name && <div>Tên: {x.before.name} → {x.after.name}</div>}
        {x.before && x.before.referencePrice !== x.after.referencePrice && <div>Giá: {vnd(x.before.referencePrice)} → {vnd(x.after.referencePrice)}</div>}
        {x.before && x.before.active !== x.after.active && <div>{x.after.active ? 'Khôi phục' : 'Ngừng dùng'}</div>}
        {x.reason && <div>Lý do: {x.reason}</div>}
      </> }))} />
    </Drawer>
  </div>;
}
