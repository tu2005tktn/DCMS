import { Alert, Button, Drawer, Input, Select, Skeleton, Table, Tag, Typography } from 'antd';
import type { ColumnType } from 'antd/es/table';
import { useCallback, useEffect, useState } from 'react';
import { api, ApiError } from '../../services/api';
import { formatDate } from '../appointments/types';
import { usePermissions } from '../authentication/usePermissions';
import { useCanPickBranch } from '../authentication/useCanPickBranch';
import LabOrderModal from '../treatment/LabOrderModal';
import './labWork.css';

// Quản lý công việc labo. Quy tắc và API: docs/specs/LAB-ORDER-MANAGEMENT.md.
type Status = 'NotSent' | 'Sent' | 'Received' | 'Delivered' | 'Rework';
type Item = { id: string; code: string | null; dossierId: string; dossierCode: string; activityId: string; patientId: number; patientCode: string; patientName: string;
  workName: string; labProviderId: string | null; labName: string | null; dentistId: number; dentistName: string; orderedOn: string; dueOn: string | null;
  sentOn: string | null; receivedOn: string | null; deliveredOn: string | null; quantity: number; material: string | null; teeth: string[]; note: string | null;
  totalFeeVnd: number; paidVnd: number; status: Status; overdue: boolean; version: number };
type Counts = { notSent: number; sent: number; received: number; delivered: number; rework: number; overdue: number };
type Page = { items: Item[]; total: number; page: number; pageSize: number; counts: Counts; labs: { id: string; name: string }[]; dentists: { id: number; name: string }[] };
type Log = { fromStatus: Status | null; toStatus: Status; changedAt: string; changedByName: string; note: string | null };

const labels: Record<Status, string> = { NotSent: 'Chưa gửi', Sent: 'Đã gửi xưởng', Received: 'Đã nhận về', Delivered: 'Đã giao bệnh nhân', Rework: 'Làm lại' };
const colors: Record<Status, string> = { NotSent: 'default', Sent: 'blue', Received: 'cyan', Delivered: 'green', Rework: 'orange' };
const next: Record<Status, Status[]> = { NotSent: ['Sent'], Sent: ['Received', 'Rework'], Received: ['Delivered', 'Rework'], Delivered: ['Rework'], Rework: ['Sent'] };
const actionLabels: Record<Status, string> = { NotSent: '', Sent: 'Gửi xưởng', Received: 'Đã nhận về', Delivered: 'Giao bệnh nhân', Rework: 'Yêu cầu làm lại' };
const tiles: { key: string; label: string; field: keyof Counts }[] = [
  { key: 'NotSent', label: 'Chưa gửi', field: 'notSent' }, { key: 'Sent', label: 'Đã gửi xưởng', field: 'sent' }, { key: 'Received', label: 'Đã nhận về', field: 'received' },
  { key: 'Overdue', label: 'Quá hạn hẹn', field: 'overdue' }, { key: 'Delivered', label: 'Đã giao bệnh nhân', field: 'delivered' }, { key: 'Rework', label: 'Làm lại', field: 'rework' }];
const vnd = (n: number) => `${n.toLocaleString('vi-VN')} ₫`;
const day = (d: string | null) => d ? formatDate(d) : '—';
const stamp = (v: string) => new Date(v).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' });
const message = (e: unknown) => e instanceof Error ? e.message : 'Không thể hoàn tất thao tác. Vui lòng thử lại.';

export default function LabWorkPage() {
  const has = usePermissions();
  const canTrack = has('treatment.lab.track'), canManage = has('treatment.lab.manage');
  const [status, setStatus] = useState<string>(), [q, setQ] = useState(''), [search, setSearch] = useState(''), [lab, setLab] = useState<string>(), [dentist, setDentist] = useState<number>();
  const [from, setFrom] = useState(''), [to, setTo] = useState(''), [page, setPage] = useState(1), [pageSize, setPageSize] = useState(20), [attempt, setAttempt] = useState(0);
  const [data, setData] = useState<Page>(), [error, setError] = useState<string>(), [loading, setLoading] = useState(true);
  const [open, setOpen] = useState<Item>(), [history, setHistory] = useState<Log[]>(), [note, setNote] = useState(''), [busy, setBusy] = useState(false), [actionError, setActionError] = useState<string>();
  const [editing, setEditing] = useState(false);
  const canPickBranch = useCanPickBranch();
  const [branch, setBranch] = useState<number>();
  const [branches, setBranches] = useState<{ id: number; name: string }[]>([]);
  useEffect(() => { if (canPickBranch) api<{ branches: { id: number; name: string }[] }>('/api/appointments/options').then(o => setBranches(o.branches)).catch(() => setBranches([])); }, [canPickBranch]);

  useEffect(() => {
    let current = true; setLoading(true); setError(undefined);
    const p = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (status) p.set('status', status);
    if (branch) p.set('branchId', String(branch));
    if (search) p.set('q', search);
    if (lab) p.set('labProviderId', lab);
    if (dentist) p.set('dentistId', String(dentist));
    if (from) p.set('from', from);
    if (to) p.set('to', to);
    api<Page>(`/api/lab-orders?${p}`).then(r => { if (current) setData(r); }).catch((e: unknown) => { if (current) setError(message(e)); }).finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [status, branch, search, lab, dentist, from, to, page, pageSize, attempt]);

  const loadHistory = useCallback((id: string) => {
    setHistory(undefined);
    api<Log[]>(`/api/lab-orders/${id}/history`).then(setHistory).catch(() => setHistory([]));
  }, []);
  function show(item: Item) { setOpen(item); setNote(''); setActionError(undefined); loadHistory(item.id); }
  async function move(item: Item, target: Status) {
    setBusy(true); setActionError(undefined);
    try {
      const updated = await api<Item>(`/api/lab-orders/${item.id}/status`, { method: 'PUT', body: JSON.stringify({ status: target, expectedVersion: item.version, note: note.trim() || null }) });
      setOpen(updated); setNote(''); loadHistory(item.id); setAttempt(a => a + 1);
    } catch (e) {
      setActionError(message(e));
      if (e instanceof ApiError && e.status === 409) setAttempt(a => a + 1);
    } finally { setBusy(false); }
  }
  const reset = () => { setStatus(undefined); setQ(''); setSearch(''); setLab(undefined); setDentist(undefined); setFrom(''); setTo(''); setPage(1); };
  const filtered = !!(status || search || lab || dentist || from || to);
  const counts = data?.counts;

  const columns: ColumnType<Item>[] = [
    { title: 'Mã phiếu', dataIndex: 'code', render: (_v, r) => r.code ?? '—' },
    { title: 'Bệnh nhân', render: (_v, r) => <><strong>{r.patientName}</strong><div className="lab-work-sub">{r.patientCode}</div></> },
    { title: 'Công việc', render: (_v, r) => <><strong>{r.workName}</strong><div className="lab-work-sub">SL {r.quantity}{r.material ? ` · ${r.material}` : ''}</div></> },
    { title: 'Công ty labo', render: (_v, r) => r.labName ?? '—' },
    { title: 'Hẹn', render: (_v, r) => <span className={r.overdue ? 'lab-work-overdue' : undefined}>{day(r.dueOn)}{r.overdue ? ' (quá hạn)' : ''}</span> },
    { title: 'Phí / Đã trả', render: (_v, r) => `${vnd(r.totalFeeVnd)} / ${vnd(r.paidVnd)}` },
    { title: 'Trạng thái', render: (_v, r) => <Tag color={colors[r.status]}>{labels[r.status]}</Tag> },
  ];

  return <section className="lab-work-page" aria-labelledby="lab-work-title">
    <h2 id="lab-work-title">Quản lý công việc labo</h2>
    <div className="lab-work-tiles" role="group" aria-label="Lọc theo trạng thái">
      {tiles.map(t => <button type="button" key={t.key} className={`lab-work-tile${status === t.key ? ' is-active' : ''}${t.key === 'Overdue' ? ' is-danger' : ''}`} aria-pressed={status === t.key}
        onClick={() => { setStatus(status === t.key ? undefined : t.key); setPage(1); }}>
        <span>{t.label}</span><strong>{counts ? counts[t.field] : '—'}</strong></button>)}
    </div>
    <div className="lab-work-filters">
      <Input.Search allowClear placeholder="Mã phiếu, MSKH, tên bệnh nhân, công việc" aria-label="Tìm phiếu labo" value={q} maxLength={160}
        onChange={e => { setQ(e.target.value); if (!e.target.value) { setSearch(''); setPage(1); } }} onSearch={v => { setSearch(v.trim()); setPage(1); }} />
      {canPickBranch && branches.length > 1 && <Select allowClear placeholder="Tất cả chi nhánh" aria-label="Chi nhánh" value={branch} onChange={v => { setBranch(v); setLab(undefined); setDentist(undefined); setPage(1); }} options={branches.map(b => ({ value: b.id, label: b.name }))} />}
      <Select allowClear placeholder="Công ty labo" aria-label="Công ty labo" value={lab} onChange={v => { setLab(v); setPage(1); }} options={data?.labs.map(l => ({ value: l.id, label: l.name }))} />
      <Select allowClear placeholder="Bác sĩ" aria-label="Bác sĩ" value={dentist} onChange={v => { setDentist(v); setPage(1); }} options={data?.dentists.map(d => ({ value: d.id, label: d.name }))} />
      <label>Hẹn từ <input type="date" aria-label="Hẹn từ ngày" value={from} max={to || undefined} onChange={e => { setFrom(e.target.value); setPage(1); }} /></label>
      <label>đến <input type="date" aria-label="Hẹn đến ngày" value={to} min={from || undefined} onChange={e => { setTo(e.target.value); setPage(1); }} /></label>
      <Button onClick={reset} disabled={!filtered}>Đặt lại</Button>
    </div>
    {error ? <Alert type="error" showIcon message={error} action={<Button size="small" onClick={() => setAttempt(a => a + 1)}>Thử lại</Button>} /> : null}
    {loading && !data ? <Skeleton active /> : <Table<Item> className="lab-work-table" rowKey="id" columns={columns.map(c => ({ ...c, onCell: () => ({ 'data-label': String(c.title) } as object) }))}
      dataSource={data?.items ?? []} loading={loading} locale={{ emptyText: filtered ? 'Không có phiếu labo phù hợp bộ lọc.' : 'Chưa có phiếu labo nào' }}
      pagination={{ current: page, pageSize, total: data?.total ?? 0, showSizeChanger: true, pageSizeOptions: [20, 50, 100], onChange: (p, s) => { setPage(p); setPageSize(s); } }}
      onRow={r => ({ onClick: () => show(r), onKeyDown: e => { if (e.key === 'Enter') show(r); }, tabIndex: 0, role: 'button', 'aria-label': `Mở phiếu labo ${r.workName} của ${r.patientName}` })} />}
    <Drawer open={!!open} width={460} title={open ? `Phiếu labo ${open.code ?? ''}`.trim() : ''} onClose={() => setOpen(undefined)} destroyOnClose>
      {open ? <div className="lab-work-detail">
        <Tag color={colors[open.status]}>{labels[open.status]}</Tag>{open.overdue ? <Tag color="red">Quá hạn hẹn</Tag> : null}
        <dl>
          <dt>Bệnh nhân</dt><dd>{open.patientName} ({open.patientCode})</dd>
          <dt>Công việc</dt><dd>{open.workName} · SL {open.quantity}{open.material ? ` · ${open.material}` : ''}</dd>
          <dt>Răng</dt><dd>{open.teeth.length ? open.teeth.join(', ') : '—'}</dd>
          <dt>Công ty labo</dt><dd>{open.labName ?? '—'}</dd><dt>Bác sĩ</dt><dd>{open.dentistName}</dd>
          <dt>Ngày lập</dt><dd>{day(open.orderedOn)}</dd><dt>Hẹn</dt><dd>{day(open.dueOn)}</dd><dt>Đã gửi</dt><dd>{day(open.sentOn)}</dd>
          <dt>Đã nhận về</dt><dd>{day(open.receivedOn)}</dd><dt>Đã giao</dt><dd>{day(open.deliveredOn)}</dd>
          <dt>Phí / Đã trả</dt><dd>{vnd(open.totalFeeVnd)} / {vnd(open.paidVnd)}</dd><dt>Ghi chú</dt><dd>{open.note ?? '—'}</dd>
        </dl>
        <div className="lab-work-links">
          <Button onClick={() => setEditing(true)}>{canManage ? 'Sửa phiếu' : 'Xem phiếu đầy đủ'}</Button>
        </div>
        {canTrack ? <div className="lab-work-actions">
          <Input.TextArea aria-label="Ghi chú đổi trạng thái" placeholder="Ghi chú (không bắt buộc)" maxLength={300} rows={2} value={note} onChange={e => setNote(e.target.value)} />
          <div>{next[open.status].map(s => <Button key={s} type={s === 'Rework' ? 'default' : 'primary'} danger={s === 'Rework'} loading={busy} disabled={busy} onClick={() => void move(open, s)}>{actionLabels[s]}</Button>)}</div>
        </div> : null}
        {actionError ? <Alert type="error" showIcon message={actionError} /> : null}
        <Typography.Title level={5}>Nhật ký trạng thái</Typography.Title>
        {history ? <ol className="lab-work-history">{history.map((h, i) => <li key={i}><strong>{labels[h.toStatus]}</strong> · {stamp(h.changedAt)}{h.changedByName ? ` · ${h.changedByName}` : ''}{h.note ? <div>{h.note}</div> : null}</li>)}</ol> : <Skeleton active paragraph={{ rows: 2 }} />}
      </div> : null}
    </Drawer>
    {open && editing ? <LabOrderModal dossierId={open.dossierId} activityId={open.activityId} serviceName={open.workName} canEdit={canManage} onClose={() => setEditing(false)}
      onSaved={() => { setEditing(false); setAttempt(a => a + 1); setOpen(undefined); }} /> : null}
  </section>;
}
