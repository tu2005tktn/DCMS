import { Alert, Button, Input, Select, Table, Tag, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import './workStatistics.css';
import { api } from '../../services/api';
import { clinicToday, formatDate, localDate } from '../appointments/types';
import { useCanPickBranch } from '../authentication/useCanPickBranch';

// Work/service statistics: one summary row per treatment dossier, click to expand its read-only detail.
// Rules and API: docs/specs/WORK-SERVICE-STATISTICS.md.
type Line = { activityId: string; serviceName: string; costVnd: number; paidVnd: number; dueVnd: number;
  performedAt: string; quantity: number; unitPriceVnd: number; discountVnd: number };
type Dossier = { dossierId: string; dossierCode: string; startedOn: string; patientId: number; patientCode: string; patientName: string; nextAppointmentAt: string | null;
  dentistName: string; assistantNames: string[]; costVnd: number; paidVnd: number; dueVnd: number; lines: Line[];
  state: string; note: string | null; createdByName: string };
type Totals = { costVnd: number; paidVnd: number; dueVnd: number };
type Result = { items: Dossier[]; lineCount: number; total: number; page: number; pageSize: number; totals: Totals };
const vnd = (n: number) => `${n.toLocaleString('vi-VN')} ₫`;
const states: Record<string, { label: string; color: string }> = { Open: { label: 'Đang điều trị', color: 'processing' },
  Completed: { label: 'Hoàn thành', color: 'success' }, Stopped: { label: 'Đã dừng', color: 'warning' }, Cancelled: { label: 'Hủy phiếu', color: 'error' } };
const dateTime = (v: string) => new Date(v).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' });
const iso = (d: Date) => d.toISOString().slice(0, 10);
type Period = 'all' | 'today' | 'week' | 'month' | 'year' | 'custom';
const periods: { value: Period; label: string }[] = [{ value: 'all', label: 'Tất cả' }, { value: 'today', label: 'Hôm nay' }, { value: 'week', label: 'Tuần này' },
  { value: 'month', label: 'Tháng này' }, { value: 'year', label: 'Năm này' }, { value: 'custom', label: 'Tùy chọn' }];
// Ranges in clinic time: today, the Monday-Sunday week, the calendar month and year containing today.
function periodRange(kind: Period) {
  const t = new Date(clinicToday() + 'T00:00:00Z'), y = t.getUTCFullYear(), m = t.getUTCMonth();
  if (kind === 'today') return { from: iso(t), to: iso(t) };
  if (kind === 'month') return { from: iso(new Date(Date.UTC(y, m, 1))), to: iso(new Date(Date.UTC(y, m + 1, 0))) };
  if (kind === 'year') return { from: iso(new Date(Date.UTC(y, 0, 1))), to: iso(new Date(Date.UTC(y, 11, 31))) };
  const monday = new Date(t); monday.setUTCDate(t.getUTCDate() - ((t.getUTCDay() + 6) % 7));
  const sunday = new Date(monday); sunday.setUTCDate(monday.getUTCDate() + 6);
  return { from: iso(monday), to: iso(sunday) };
}

// Read-only detail of one dossier, shown under its summary row.
function DossierDetail({ d }: { d: Dossier }) {
  const state = states[d.state] ?? { label: d.state, color: 'default' };
  const discount = d.lines.reduce((sum, l) => sum + l.discountVnd, 0);
  return <section className="work-stats-detail" aria-label={`Chi tiết phiếu ${d.dossierCode}`}>
    <div className="work-stats-detail-head">
      <Link to={`/patients/${d.patientId}`} className="work-stats-detail-title">{d.patientCode} - {d.patientName}</Link>
      <span className="work-stats-muted">{d.dossierCode}</span>
      <Tag color={state.color}>{state.label}</Tag>
      <Link to={`/patients/${d.patientId}?tab=treatment&dossierId=${d.dossierId}`} className="work-stats-open-dossier">Mở phiếu điều trị</Link>
    </div>
    <dl className="work-stats-facts">
      <div><dt>Người tạo</dt><dd>{d.createdByName || '—'}</dd></div>
      <div><dt>Bác sĩ</dt><dd>{d.dentistName || '—'}</dd></div>
      {d.assistantNames.length > 0 && <div><dt>Phụ tá</dt><dd>{d.assistantNames.join(', ')}</dd></div>}
      <div><dt>Ngày tạo phiếu</dt><dd>{formatDate(d.startedOn)}</dd></div>
      <div><dt>Lịch hẹn tiếp theo</dt><dd>{d.nextAppointmentAt ? dateTime(d.nextAppointmentAt) : '—'}</dd></div>
    </dl>
    <Table<Line> rowKey="activityId" size="small" className="work-stats-lines" pagination={false} dataSource={d.lines} scroll={{ x: 'max-content' }}
      columns={[
        { title: 'Điều trị/dịch vụ', dataIndex: 'serviceName' },
        { title: 'Ngày thực hiện', key: 'at', width: 120, render: (_v, l) => formatDate(localDate(l.performedAt)) },
        { title: 'Số lượng', key: 'qty', width: 90, align: 'right', render: (_v, l) => l.quantity.toLocaleString('vi-VN') },
        { title: 'Đơn giá', key: 'unit', width: 120, align: 'right', render: (_v, l) => vnd(l.unitPriceVnd) },
        { title: 'Giảm giá', key: 'discount', width: 110, align: 'right', render: (_v, l) => l.discountVnd ? vnd(l.discountVnd) : '' },
        { title: 'Thành tiền', key: 'cost', width: 120, align: 'right', render: (_v, l) => <strong>{vnd(l.costVnd)}</strong> },
        { title: 'Đã trả', key: 'paid', width: 120, align: 'right', render: (_v, l) => vnd(l.paidVnd) },
        { title: 'Còn lại', key: 'due', width: 120, align: 'right', render: (_v, l) => vnd(l.dueVnd) },
      ]} />
    <div className="work-stats-foot">
      <div className="work-stats-note">
        <div className="work-stats-muted">Ghi chú</div>
        <div className={d.note ? 'work-stats-note-box' : 'work-stats-note-box work-stats-muted'}>{d.note || 'Không có ghi chú'}</div>
      </div>
      <dl className="work-stats-sum">
        <div><dt>Tổng tiền dịch vụ ({d.lines.length})</dt><dd>{vnd(d.costVnd + discount)}</dd></div>
        <div><dt>Giảm giá</dt><dd>{vnd(discount)}</dd></div>
        <div><dt>Khách cần trả</dt><dd><strong>{vnd(d.costVnd)}</strong></dd></div>
        <div><dt>Khách đã trả</dt><dd><strong>{vnd(d.paidVnd)}</strong></dd></div>
        <div><dt>Còn lại</dt><dd>{vnd(d.dueVnd)}</dd></div>
      </dl>
    </div>
  </section>;
}

export default function WorkStatisticsPage() {
  const canPickBranch = useCanPickBranch();
  const [branches, setBranches] = useState<{ id: number; name: string }[]>([]), [branchId, setBranchId] = useState<number>();
  const [from, setFrom] = useState(''), [to, setTo] = useState(''), [q, setQ] = useState(''), [search, setSearch] = useState('');
  const [open, setOpen] = useState<string>();
  const [period, setPeriod] = useState<Period>('all');
  const [page, setPage] = useState(1), [pageSize, setPageSize] = useState(20), [attempt, setAttempt] = useState(0);
  const [data, setData] = useState<Result>(), [loading, setLoading] = useState(true), [error, setError] = useState<string>();
  useEffect(() => { if (canPickBranch) api<{ branches: { id: number; name: string }[] }>('/api/appointments/options').then(o => setBranches(o.branches)).catch(() => setBranches([])); }, [canPickBranch]);
  useEffect(() => { const t = window.setTimeout(() => setSearch(q.trim()), 300); return () => window.clearTimeout(t); }, [q]);
  useEffect(() => setPage(1), [from, to, branchId, search]);
  useEffect(() => {
    let current = true; setLoading(true); setError(undefined);
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (from) params.set('from', from); if (to) params.set('to', to); if (branchId) params.set('branchId', String(branchId)); if (search) params.set('q', search);
    api<Result>(`/api/work-statistics?${params}`).then(r => { if (current) setData(r); })
      .catch((e: unknown) => { if (current) setError(e instanceof Error ? e.message : 'Không tải được thống kê.'); })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [from, to, branchId, search, page, pageSize, attempt]);
  const totals = data?.totals;
  const toggle = (id: string) => setOpen(current => current === id ? undefined : id);
  function choose(kind: Period) {
    setPeriod(kind);
    if (kind === 'all') { setFrom(''); setTo(''); } else if (kind !== 'custom') { const r = periodRange(kind); setFrom(r.from); setTo(r.to); }
  }
  return <div className="system-settings-page">
    <Typography.Title level={2}>Thống kê công việc/dịch vụ</Typography.Title>
    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: 12 }}>
      <Select aria-label="Thời gian" style={{ width: 150 }} value={period} onChange={choose} options={periods} />
      {period === 'custom' && <>
        <Input type="date" aria-label="Từ ngày" value={from} max={to || undefined} onChange={e => { setFrom(e.target.value); }} style={{ width: 160 }} />
        <Input type="date" aria-label="Đến ngày" value={to} min={from || undefined} onChange={e => { setTo(e.target.value); }} style={{ width: 160 }} />
      </>}
      {canPickBranch && branches.length > 1 && <Select allowClear aria-label="Chi nhánh" placeholder="Tất cả chi nhánh" style={{ minWidth: 200 }} value={branchId} onChange={setBranchId}
        options={branches.map(b => ({ value: b.id, label: b.name }))} />}
      <Input.Search allowClear aria-label="Tìm công việc" placeholder="Tìm MSKH, khách hàng, dịch vụ" value={q} onChange={e => setQ(e.target.value)} style={{ maxWidth: 320 }} />
    </div>
    <div style={{ display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8, marginBottom: 8 }} aria-label="Tổng hợp">
      <span>{totals && <><strong>Tổng cộng:</strong> <span style={{ color: '#1677ff' }}>{vnd(totals.costVnd)}</span>
        {'  '}<strong>Đã trả:</strong> <span style={{ color: '#1677ff' }}>{vnd(totals.paidVnd)}</span>
        {'  '}<strong>Còn lại:</strong> <span style={{ color: '#1677ff' }}>{vnd(totals.dueVnd)}</span></>}</span>
      <span style={{ color: '#667085' }}>{data && <>Tìm thấy {data.lineCount} công việc/dịch vụ.</>}</span>
    </div>
    {error && <Alert type="error" showIcon style={{ marginBottom: 12 }} message={error} action={<Button size="small" onClick={() => setAttempt(a => a + 1)}>Thử lại</Button>} />}
    <Table<Dossier> rowKey="dossierId" size="small" className="work-stats-table" loading={loading} dataSource={data?.items ?? []}
      locale={{ emptyText: 'Chưa có công việc nào.' }}
      pagination={{ current: page, pageSize, total: data?.total ?? 0, showSizeChanger: true, pageSizeOptions: [20, 50, 100], showTotal: t => `Có ${t} phiếu điều trị.`,
        onChange: (p, s) => { setPage(p); setPageSize(s); setOpen(undefined); } }}
      rowClassName={d => d.dossierId === open ? 'work-stats-row work-stats-row-open' : 'work-stats-row'}
      onRow={d => ({ tabIndex: 0, 'aria-expanded': d.dossierId === open, onClick: () => toggle(d.dossierId),
        onKeyDown: e => { if (e.target === e.currentTarget && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); toggle(d.dossierId); } } })}
      expandable={{ showExpandColumn: false, expandedRowKeys: open ? [open] : [], expandedRowRender: d => <DossierDetail d={d} /> }}
      columns={[
        { title: 'Mã phiếu', dataIndex: 'dossierCode', width: 130 },
        { title: 'Ngày', key: 'date', width: 105, render: (_v, d) => formatDate(d.startedOn) },
        { title: 'MSKH', dataIndex: 'patientCode', width: 110 },
        { title: 'Khách hàng', dataIndex: 'patientName' },
        { title: 'Bác sĩ', dataIndex: 'dentistName', width: 190 },
        { title: 'Chi phí', key: 'cost', width: 130, align: 'right', render: (_v, d) => vnd(d.costVnd) },
        { title: 'Đã trả', key: 'paid', width: 130, align: 'right', render: (_v, d) => vnd(d.paidVnd) },
        { title: 'Còn lại', key: 'due', width: 130, align: 'right', render: (_v, d) => vnd(d.dueVnd) },
      ]}
      />
  </div>;
}
