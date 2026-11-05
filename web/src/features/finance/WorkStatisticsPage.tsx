import { Alert, Button, Input, Select, Table, Typography } from 'antd';
import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../services/api';
import { clinicToday, formatDate } from '../appointments/types';

// Work/service statistics grouped by treatment dossier. Rules and API: docs/specs/WORK-SERVICE-STATISTICS.md.
type Line = { activityId: string; serviceName: string; costVnd: number; paidVnd: number; dueVnd: number };
type Dossier = { dossierId: string; dossierCode: string; startedOn: string; patientId: number; patientCode: string; patientName: string; nextAppointmentAt: string | null;
  dentistName: string; assistantNames: string[]; costVnd: number; paidVnd: number; dueVnd: number; lines: Line[] };
type Totals = { costVnd: number; paidVnd: number; dueVnd: number };
type Result = { items: Dossier[]; lineCount: number; total: number; page: number; pageSize: number; totals: Totals };
// One table row per service; the dossier-level cells span all rows of the dossier and render only on its first row.
type Flat = { key: string; group: Dossier; line: Line; span: number };
const vnd = (n: number) => `${n.toLocaleString('vi-VN')} ₫`;
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

const spanned = (r: Flat) => ({ rowSpan: r.span });

export default function WorkStatisticsPage() {
  const [branches, setBranches] = useState<{ id: number; name: string }[]>([]), [branchId, setBranchId] = useState<number>();
  const [from, setFrom] = useState(''), [to, setTo] = useState(''), [q, setQ] = useState(''), [search, setSearch] = useState('');
  const [period, setPeriod] = useState<Period>('all');
  const [page, setPage] = useState(1), [pageSize, setPageSize] = useState(20), [attempt, setAttempt] = useState(0);
  const [data, setData] = useState<Result>(), [loading, setLoading] = useState(true), [error, setError] = useState<string>();
  useEffect(() => { api<{ branches: { id: number; name: string }[] }>('/api/finance/capabilities').then(c => setBranches(c.branches)).catch(() => undefined); }, []);
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
  const rows = useMemo<Flat[]>(() => (data?.items ?? []).flatMap(group => group.lines.map((line, index) =>
    ({ key: line.activityId, group, line, span: index === 0 ? group.lines.length : 0 }))), [data]);
  const totals = data?.totals;
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
      <Select allowClear aria-label="Chi nhánh" placeholder="Tất cả chi nhánh" style={{ minWidth: 200 }} value={branchId} onChange={setBranchId}
        options={branches.map(b => ({ value: b.id, label: b.name }))} />
      <Input.Search allowClear aria-label="Tìm công việc" placeholder="Tìm MSKH, khách hàng, dịch vụ" value={q} onChange={e => setQ(e.target.value)} style={{ maxWidth: 320 }} />
    </div>
    <div style={{ display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8, marginBottom: 8 }} aria-label="Tổng hợp">
      <span>{totals && <><strong>Tổng cộng:</strong> <span style={{ color: '#1677ff' }}>{vnd(totals.costVnd)}</span>
        {'  '}<strong>Đã trả:</strong> <span style={{ color: '#1677ff' }}>{vnd(totals.paidVnd)}</span>
        {'  '}<strong>Còn lại:</strong> <span style={{ color: '#1677ff' }}>{vnd(totals.dueVnd)}</span></>}</span>
      <span style={{ color: '#667085' }}>{data && <>Tìm thấy {data.lineCount} công việc/dịch vụ.</>}</span>
    </div>
    {error && <Alert type="error" showIcon style={{ marginBottom: 12 }} message={error} action={<Button size="small" onClick={() => setAttempt(a => a + 1)}>Thử lại</Button>} />}
    <Table<Flat> rowKey="key" size="small" bordered loading={loading} dataSource={rows} scroll={{ x: 1150 }}
      locale={{ emptyText: 'Chưa có công việc nào.' }}
      pagination={{ current: page, pageSize, total: data?.total ?? 0, showSizeChanger: true, pageSizeOptions: [20, 50, 100], showTotal: t => `Có ${t} phiếu điều trị.`,
        onChange: (p, s) => { setPage(p); setPageSize(s); } }}
      columns={[
        { title: 'Ngày', key: 'date', width: 105, onCell: spanned, render: (_v, r) => formatDate(r.group.startedOn) },
        { title: 'MSKH', key: 'code', width: 100, onCell: spanned, render: (_v, r) => r.group.patientCode },
        { title: 'Khách hàng', key: 'patient', width: 160, onCell: spanned, render: (_v, r) => <Link to={`/patients/${r.group.patientId}`}><strong>{r.group.patientName}</strong></Link> },
        { title: 'Lịch hẹn tiếp theo', key: 'next', width: 150, onCell: spanned, render: (_v, r) => r.group.nextAppointmentAt ? dateTime(r.group.nextAppointmentAt) : '—' },
        { title: 'Điều trị/dịch vụ', key: 'service', width: 230, render: (_v, r) => <>{r.line.serviceName}</> },
        { title: 'Bác sĩ & phụ tá', key: 'staff', width: 170, onCell: spanned, render: (_v, r) => <>{r.group.dentistName}{r.group.assistantNames.map(n => <div key={n}><small style={{ color: '#667085' }}>Phụ tá: {n}</small></div>)}</> },
        { title: 'Chi phí', key: 'cost', width: 120, align: 'right', render: (_v, r) => vnd(r.line.costVnd) },
        { title: 'Đã trả', key: 'paid', width: 120, align: 'right', render: (_v, r) => vnd(r.line.paidVnd) },
        { title: 'Còn lại', key: 'due', width: 120, align: 'right', render: (_v, r) => vnd(r.line.dueVnd) },
      ]}
      />
  </div>;
}
