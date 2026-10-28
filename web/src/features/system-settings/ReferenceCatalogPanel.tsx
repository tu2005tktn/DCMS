import { Alert, Button, Drawer, Empty, Form, Input, Menu, Modal, Segmented, Space, Table, Tag, Timeline, Typography, message } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';

// S58 danh mục tham chiếu. Contract: docs/specs/S58-REFERENCE-CATALOGS.md.
type Kind = 'diagnoses' | 'patient-sources' | 'occupations' | 'intake-history' | 'care-types' | 'expense-categories' | 'customer-groups';
type Entry = { id: string; kind: Kind; code: string | null; name: string; description: string | null; reserved: boolean; active: boolean; version: number;
  createdAt: string; createdByName: string; updatedAt: string; updatedByName: string };
type Page = { items: Entry[]; total: number; page: number; pageSize: number };
type StatePreview = { entry: Entry; allowed: boolean; blockers: string[]; impact: string };
type HistoryItem = { occurredAt: string; actorName: string; command: string; reason: string | null; before: Entry | null; after: Entry };
type Values = { code?: string; name: string; description?: string; reason?: string };
type Status = 'Active' | 'Inactive' | 'All';

const kinds: { key: Kind; label: string; codeLabel: string; help?: string }[] = [
  { key: 'diagnoses', label: 'Chẩn đoán/tên bệnh', codeLabel: 'Mã ICD', help: 'Mã ICD không bắt buộc và chỉ để tham khảo; hệ thống không tự xác nhận mã hợp lệ lâm sàng.' },
  { key: 'patient-sources', label: 'Nguồn khách hàng', codeLabel: 'Mã' },
  { key: 'occupations', label: 'Nghề nghiệp', codeLabel: 'Mã' },
  { key: 'intake-history', label: 'Tiền sử (lời khai)', codeLabel: 'Mã', help: 'Lời khai hành chính của bệnh nhân; không thay cho ghi nhận chuyên môn của nha sĩ.' },
  { key: 'care-types', label: 'Loại chăm sóc khách hàng', codeLabel: 'Mã' },
  { key: 'expense-categories', label: 'Loại chi', codeLabel: 'Mã', help: 'Loại chi "Hệ thống" dùng cho phiếu chi vật liệu, labo, lương và không ngừng dùng được.' },
  { key: 'customer-groups', label: 'Nhóm khách hàng', codeLabel: 'Mã', help: 'Chỉ phân nhóm; không tạo giá thẻ, điểm hay ưu đãi.' },
];
const commandLabels: Record<string, string> = { ReferenceCreate: 'Tạo mới', ReferenceUpdate: 'Sửa', ReferenceState: 'Đổi trạng thái' };
const base = '/api/system-settings/settings/catalogs';
const formatTime = (value: string) => new Date(value).toLocaleString('vi-VN');

// Each save carries a requestKey that stays the same while the payload is unchanged, so a retry cannot write twice (SET-02).
function useRequestKey() {
  const ref = useRef<{ fingerprint: string; key: string } | null>(null);
  return { key(fingerprint: string) { if (ref.current?.fingerprint !== fingerprint) ref.current = { fingerprint, key: crypto.randomUUID() }; return ref.current.key; },
    reset() { ref.current = null; } };
}

export default function ReferenceCatalogPanel() {
  const [kind, setKind] = useState<Kind>('diagnoses'), [status, setStatus] = useState<Status>('Active');
  const [search, setSearch] = useState(''), [query, setQuery] = useState(''), [page, setPage] = useState(1), [size, setSize] = useState(20);
  const [data, setData] = useState<Page | null>(null), [loading, setLoading] = useState(false), [error, setError] = useState<string>(), [revision, setRevision] = useState(0);
  const [editing, setEditing] = useState<{ entry?: Entry } | null>(null), [formError, setFormError] = useState<ApiError>();
  const [stateTarget, setStateTarget] = useState<StatePreview | null>(null), [stateReason, setStateReason] = useState(''), [stateError, setStateError] = useState<string>();
  const [history, setHistory] = useState<{ entry: Entry; items: HistoryItem[] | null; error?: string } | null>(null);
  const [busy, setBusy] = useState(false), guard = useRef(false);
  const saveKey = useRequestKey(), stateKey = useRequestKey();
  const [form] = Form.useForm<Values>();
  const meta = kinds.find(x => x.key === kind)!;

  useEffect(() => {
    let live = true; setLoading(true); setError(undefined);
    const params = new URLSearchParams({ q: query, status, page: String(page), pageSize: String(size) });
    void api<Page>(`${base}/${kind}?${params}`).then(v => {
      if (!live) return;
      if (!v.items.length && v.total && page > 1) { setPage(Math.max(1, Math.ceil(v.total / size))); return; }
      setData(v);
    }).catch(e => { if (live) { setError((e as Error).message); setData(null); } }).finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [kind, status, query, page, size, revision]);

  function selectKind(next: Kind) { setKind(next); setPage(1); setSearch(''); setQuery(''); setData(null); }
  function open(entry?: Entry) {
    saveKey.reset(); setFormError(undefined); form.resetFields();
    form.setFieldsValue({ code: entry?.code ?? '', name: entry?.name ?? '', description: entry?.description ?? '', reason: '' });
    setEditing({ entry });
  }
  async function run<T>(action: () => Promise<T>, onError: (e: ApiError) => void) {
    if (guard.current) return undefined;
    guard.current = true; setBusy(true);
    try { return await action(); } catch (e) { onError(e as ApiError); return undefined; } finally { guard.current = false; setBusy(false); }
  }
  async function save(values: Values) {
    if (!editing) return;
    const entry = { code: values.code?.trim() || null, name: values.name.trim(), description: values.description?.trim() || null };
    const body = editing.entry ? { expectedVersion: editing.entry.version, reason: values.reason?.trim(), entry } : { entry };
    const key = saveKey.key(JSON.stringify({ kind, id: editing.entry?.id, body }));
    const saved = await run(() => api<Entry>(`${base}/${kind}${editing.entry ? `/${editing.entry.id}` : ''}`, {
      method: editing.entry ? 'PUT' : 'POST', body: JSON.stringify({ ...body, requestKey: key }) }), failure => {
      setFormError(failure);
      if (failure.field && ['code', 'name', 'description', 'reason'].includes(failure.field)) form.setFields([{ name: failure.field as keyof Values, errors: [failure.message] }]);
    });
    if (!saved) return;
    setEditing(null); saveKey.reset(); setRevision(v => v + 1);
    void message.success(editing.entry ? 'Đã cập nhật danh mục.' : 'Đã thêm vào danh mục.');
  }
  async function reloadEditing() {
    if (!editing?.entry) return;
    const latest = await run(() => api<Entry>(`${base}/${kind}/${editing.entry!.id}`), failure => setFormError(failure));
    if (latest) { setEditing({ entry: latest }); saveKey.reset(); setFormError(undefined); }
  }
  async function previewState(entry: Entry) {
    const preview = await run(() => api<StatePreview>(`${base}/${kind}/${entry.id}/state-preview`, { method: 'POST', body: JSON.stringify({ active: !entry.active }) }),
      failure => void message.error(failure.message));
    if (preview) { setStateTarget(preview); setStateReason(''); setStateError(undefined); stateKey.reset(); }
  }
  async function changeState() {
    if (!stateTarget) return;
    const reason = stateReason.trim();
    if (!reason) { setStateError('Vui lòng nhập Lý do.'); return; }
    const body = { expectedVersion: stateTarget.entry.version, active: !stateTarget.entry.active, reason };
    const key = stateKey.key(JSON.stringify({ id: stateTarget.entry.id, body }));
    const saved = await run(() => api<Entry>(`${base}/${kind}/${stateTarget.entry.id}/state`, { method: 'POST', body: JSON.stringify({ ...body, requestKey: key }) }),
      failure => setStateError(failure.message));
    if (!saved) return;
    setStateTarget(null); setRevision(v => v + 1);
    void message.success(saved.active ? 'Đã khôi phục mục danh mục.' : 'Đã ngừng dùng mục danh mục.');
  }
  async function openHistory(entry: Entry) {
    setHistory({ entry, items: null });
    try { setHistory({ entry, items: await api<HistoryItem[]>(`${base}/${kind}/${entry.id}/history`) }); }
    catch (e) { setHistory({ entry, items: [], error: (e as Error).message }); }
  }

  return <div className="reference-catalogs">
    <Menu className="reference-catalogs-menu" mode="inline" selectedKeys={[kind]} onClick={e => selectKind(e.key as Kind)}
      items={kinds.map(x => ({ key: x.key, label: x.label }))} />
    <div className="reference-catalogs-body">
      <Typography.Title level={4}>{meta.label}</Typography.Title>
      {meta.help && <Typography.Paragraph type="secondary">{meta.help}</Typography.Paragraph>}
      <div className="system-settings-toolbar">
        <Input.Search aria-label="Tìm theo tên hoặc mã" placeholder="Tìm theo tên hoặc mã" maxLength={100} allowClear value={search}
          onChange={e => { setSearch(e.target.value); if (!e.target.value) { setQuery(''); setPage(1); } }} onSearch={v => { setQuery(v.trim()); setPage(1); }} />
        <Segmented<Status> value={status} onChange={v => { setStatus(v); setPage(1); }}
          options={[{ value: 'Active', label: 'Đang dùng' }, { value: 'Inactive', label: 'Ngừng dùng' }, { value: 'All', label: 'Tất cả' }]} />
        <Button type="primary" onClick={() => open()}>Thêm mới</Button>
      </div>
      {error && <Alert type="error" showIcon message={error} action={<Button onClick={() => setRevision(v => v + 1)}>Thử lại</Button>} />}
      <Table<Entry> rowKey="id" loading={loading} dataSource={data?.items ?? []} scroll={{ x: 760 }}
        locale={{ emptyText: <Empty description={query ? 'Không có kết quả phù hợp.' : 'Danh mục chưa có mục nào.'} /> }}
        pagination={{ current: page, pageSize: size, total: data?.total ?? 0, showSizeChanger: true, pageSizeOptions: [20, 50, 100],
          onChange: (p, s) => { setPage(s !== size ? 1 : p); setSize(s); } }}
        columns={[
          { title: meta.codeLabel, dataIndex: 'code', width: 140, render: (v: string | null) => v ?? '—' },
          { title: 'Tên', dataIndex: 'name', render: (v: string, row) => <>{v}{row.reserved && <Tag className="general-settings-tag">Hệ thống</Tag>}</> },
          { title: 'Mô tả', dataIndex: 'description', ellipsis: true, render: (v: string | null) => v ?? '—' },
          { title: 'Trạng thái', dataIndex: 'active', width: 120, render: (v: boolean) => v ? <Tag color="green">Đang dùng</Tag> : <Tag>Ngừng dùng</Tag> },
          { title: 'Thao tác', key: 'actions', width: 250, render: (_, row) => <Space wrap>
            <Button size="small" aria-label={`Sửa ${row.name}`} onClick={() => open(row)}>Sửa</Button>
            {!(row.reserved && row.active) && <Button size="small" danger={row.active} disabled={busy} onClick={() => void previewState(row)}>{row.active ? 'Ngừng dùng' : 'Khôi phục'}</Button>}
            <Button size="small" type="link" onClick={() => void openHistory(row)}>Lịch sử</Button>
          </Space> },
        ]} />
    </div>

    <Modal forceRender open={!!editing} title={`${editing?.entry ? 'Sửa' : 'Thêm'} · ${meta.label}`} okText="Lưu" cancelText="Hủy" confirmLoading={busy}
      closable={!busy} cancelButtonProps={{ disabled: busy }} onCancel={() => { if (!busy) setEditing(null); }} onOk={() => form.submit()}>
      {formError && <Alert type="error" showIcon message={formError.message}
        action={formError.code === 'MSG111' && editing?.entry ? <Button size="small" onClick={() => void reloadEditing()}>Tải lại bản mới</Button> : undefined} />}
      <Form form={form} layout="vertical" disabled={busy} onFinish={v => void save(v)}>
        <Form.Item name="code" label={meta.codeLabel} rules={[{ required: kind !== 'diagnoses', whitespace: true, message: `Vui lòng nhập ${meta.codeLabel}.` },
          { max: 40, message: `${meta.codeLabel} tối đa 40 ký tự.` }, { pattern: /^\s*([A-Za-z0-9][A-Za-z0-9._-]*)?\s*$/, message: 'Mã gồm chữ không dấu, số, dấu chấm, gạch dưới hoặc gạch nối.' }]}>
          <Input maxLength={40} autoComplete="off" /></Form.Item>
        <Form.Item name="name" label="Tên" rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập Tên.' }, { max: 160, message: 'Tên tối đa 160 ký tự.' }]}>
          <Input maxLength={160} autoComplete="off" /></Form.Item>
        <Form.Item name="description" label="Mô tả" rules={[{ max: 1000, message: 'Mô tả tối đa 1000 ký tự.' }]}><Input.TextArea rows={2} maxLength={1000} showCount /></Form.Item>
        {editing?.entry && <Form.Item name="reason" label="Lý do sửa" rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập Lý do.' }, { max: 500, message: 'Lý do tối đa 500 ký tự.' }]}>
          <Input.TextArea rows={2} maxLength={500} showCount /></Form.Item>}
      </Form>
    </Modal>

    <Modal open={!!stateTarget} title={stateTarget?.entry.active ? `Ngừng dùng "${stateTarget.entry.name}"?` : `Khôi phục "${stateTarget?.entry.name}"?`}
      okText="Xác nhận" cancelText="Hủy" confirmLoading={busy} okButtonProps={{ disabled: !stateTarget?.allowed, danger: stateTarget?.entry.active }}
      closable={!busy} cancelButtonProps={{ disabled: busy }} onCancel={() => { if (!busy) setStateTarget(null); }} onOk={() => void changeState()}>
      <Typography.Paragraph>{stateTarget?.impact}</Typography.Paragraph>
      {stateTarget?.blockers.map(x => <Alert key={x} type="warning" showIcon message={x} />)}
      <Form layout="vertical" disabled={busy}>
        <Form.Item label="Lý do" required validateStatus={stateError ? 'error' : undefined} help={stateError}>
          <Input.TextArea rows={2} maxLength={500} showCount value={stateReason} onChange={e => { setStateReason(e.target.value); setStateError(undefined); }} />
        </Form.Item>
      </Form>
    </Modal>

    <Drawer open={!!history} width={480} title={`Lịch sử · ${history?.entry.name ?? ''}`} onClose={() => setHistory(null)} loading={history?.items === null}>
      {history?.error && <Alert type="error" showIcon message={history.error} />}
      <Timeline items={(history?.items ?? []).map(x => ({ key: `${x.occurredAt}-${x.command}`, children: <>
        <Typography.Text strong>{commandLabels[x.command] ?? x.command}</Typography.Text> · {x.actorName}<br />
        <Typography.Text type="secondary">{formatTime(x.occurredAt)}</Typography.Text>
        {x.before && x.before.name !== x.after.name && <div>Tên: {x.before.name} → {x.after.name}</div>}
        {x.before && x.before.code !== x.after.code && <div>Mã: {x.before.code ?? '—'} → {x.after.code ?? '—'}</div>}
        {x.before && x.before.active !== x.after.active && <div>{x.after.active ? 'Khôi phục' : 'Ngừng dùng'}</div>}
        {x.reason && <div>Lý do: {x.reason}</div>}
      </> }))} />
    </Drawer>
  </div>;
}
