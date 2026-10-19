import { Alert, Button, Calendar, Card, Empty, Input, Radio, Select, Space, Spin, Table, Tag, Typography } from 'antd';
import type { Dayjs } from 'dayjs';
import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { ApiError } from '../../services/api';
import { PreviewBanner } from '../ui-preview/preview';
import { careApi } from './care.api';
import CareTaskDrawer from './CareTaskDrawer';
import {
  flagColors, flagLabels, formatStamp, statusColors, statusLabels, typeLabels,
  type CareOptions, type CareTaskItem, type CareTaskPage, type CareTaskStatus, type CareTaskType,
} from './types';

type Filters = { from?: string; to?: string; ownerId?: number; patient?: string; type?: CareTaskType; status?: CareTaskStatus; branchId?: number };
const loadMessage = (e: ApiError) => e.status === 401 ? 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.'
  : e.status === 403 ? 'Bạn không có quyền xem danh sách chăm sóc.'
  : e.status === 404 ? 'Chưa kết nối được API chăm sóc (DCMS-99).' : e.message;

function readFilters(search: URLSearchParams): Filters {
  const num = (k: string) => search.get(k) ? Number(search.get(k)) : undefined;
  return { from: search.get('from') || undefined, to: search.get('to') || undefined, ownerId: num('ownerId'),
    patient: search.get('patient') || undefined, type: (search.get('type') as CareTaskType) || undefined,
    status: (search.get('status') as CareTaskStatus) || undefined, branchId: num('branchId') };
}

// S25 queue (/care-tasks) and S60 calendar (/care-tasks/calendar) share filters (in the URL) and the same task drawer,
// so a task opened from either screen shows the same server state.
export default function CareTasksPage({ view }: { view: 'list' | 'calendar' }) {
  const navigate = useNavigate();
  const [search, setSearch] = useSearchParams();
  const filters = useMemo(() => readFilters(search), [search]);
  const [options, setOptions] = useState<CareOptions | null>(null);
  const [patientDraft, setPatientDraft] = useState(filters.patient ?? '');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [month, setMonth] = useState(() => new Date().toISOString().slice(0, 7));
  const [data, setData] = useState<CareTaskPage | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);
  const selected = search.get('task');

  useEffect(() => { careApi.options().then(setOptions).catch(() => setOptions(null)); }, []);

  useEffect(() => {
    let active = true;
    // The calendar reads the visible month; the queue uses the date filters and paging.
    const range = view === 'calendar'
      ? { from: `${month}-01`, to: new Date(Number(month.slice(0, 4)), Number(month.slice(5, 7)), 0).toLocaleDateString('sv-SE') }
      : { from: filters.from, to: filters.to };
    setLoading(true); setError(null);
    careApi.list({ ...filters, ...range, page: view === 'calendar' ? 1 : page, pageSize: view === 'calendar' ? 100 : pageSize })
      .then(r => { if (active) setData(r); })
      .catch(e => { if (active) { setData(null); setError(loadMessage(e as ApiError)); } })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [filters, page, pageSize, month, view, revision]);

  function set(next: Partial<Filters & { task: string | null }>) {
    const params = new URLSearchParams(search);
    for (const [k, v] of Object.entries(next)) { if (v === undefined || v === null || v === '') params.delete(k); else params.set(k, String(v)); }
    setSearch(params, { replace: true });
    if (!('task' in next)) setPage(1);
  }
  const byDay = useMemo(() => {
    const map = new Map<string, CareTaskItem[]>();
    for (const t of data?.items ?? []) { const d = new Date(t.dueAt).toLocaleDateString('sv-SE'); map.set(d, [...(map.get(d) ?? []), t]); }
    return map;
  }, [data]);

  return <>
    <div className="page-heading"><div><Typography.Title level={2}>{view === 'list' ? 'Việc chăm sóc cần xử lý' : 'Lịch chăm sóc'}</Typography.Title>
      <Typography.Paragraph>Theo dõi hẹn lại và chăm sóc sau điều trị; ghi kết quả liên hệ. Không gửi SMS/email/Zalo từ màn này.</Typography.Paragraph></div>
      <Radio.Group optionType="button" value={view} onChange={e => navigate({ pathname: e.target.value === 'list' ? '/care-tasks' : '/care-tasks/calendar', search: search.toString() })}
        options={[{ value: 'list', label: 'Danh sách (S25)' }, { value: 'calendar', label: 'Lịch (S60)' }]} /></div>
    <PreviewBanner />
    <Card>
      <div className="document-filters">
        {view === 'list' && <>
          <Input type="date" aria-label="Đến hạn từ" value={filters.from ?? ''} max={filters.to} style={{ width: 160 }} onChange={e => set({ from: e.target.value })} />
          <Input type="date" aria-label="Đến hạn đến" value={filters.to ?? ''} min={filters.from} style={{ width: 160 }} onChange={e => set({ to: e.target.value })} />
        </>}
        <Input.Search placeholder="Mã hoặc tên bệnh nhân" allowClear style={{ width: 230 }} value={patientDraft} maxLength={100}
          onChange={e => setPatientDraft(e.target.value)} onSearch={v => set({ patient: v.trim() })} />
        <Select allowClear placeholder="Người phụ trách" style={{ width: 180 }} value={filters.ownerId} options={options?.owners.map(o => ({ value: o.id, label: o.name }))} onChange={v => set({ ownerId: v })} />
        <Select allowClear placeholder="Loại" style={{ width: 190 }} value={filters.type} options={(options?.types ?? Object.keys(typeLabels) as CareTaskType[]).map(t => ({ value: t, label: typeLabels[t] ?? t }))} onChange={v => set({ type: v })} />
        <Select allowClear placeholder="Trạng thái" style={{ width: 150 }} value={filters.status} options={(Object.keys(statusLabels) as CareTaskStatus[]).map(s => ({ value: s, label: statusLabels[s] }))} onChange={v => set({ status: v })} />
        <Select allowClear placeholder="Chi nhánh" style={{ width: 200 }} value={filters.branchId} options={options?.branches.map(b => ({ value: b.id, label: b.name }))} onChange={v => set({ branchId: v })} />
        <Space><Button onClick={() => { setPatientDraft(''); setSearch(new URLSearchParams(), { replace: true }); }}>Xóa lọc</Button>
          <Button loading={loading} onClick={() => setRevision(v => v + 1)}>Tải lại</Button></Space>
      </div>
      {error ? <Alert type="error" showIcon message={error} description="Chưa tải được dữ liệu — đây không phải là danh sách trống."
        action={<Button size="small" onClick={() => setRevision(v => v + 1)}>Thử lại</Button>} />
        : view === 'list' ? <Table<CareTaskItem> rowKey="taskId" loading={loading} dataSource={data?.items ?? []} scroll={{ x: 960 }}
          rowClassName={() => 'link-row'} onRow={t => ({ onClick: () => set({ task: t.taskId }) })}
          locale={{ emptyText: <Empty description="Không có việc chăm sóc phù hợp bộ lọc." /> }}
          pagination={{ current: page, pageSize, total: data?.total ?? 0, showSizeChanger: true, pageSizeOptions: [20, 50, 100],
            showTotal: total => `${total} việc`, onChange: (p, s) => { setPage(s !== pageSize ? 1 : p); setPageSize(s); } }}
          columns={[
            { title: 'Đến hạn', render: (_, t) => formatStamp(t.dueAt) },
            { title: 'Bệnh nhân', render: (_, t) => <>{t.patient.fullName}<br /><Typography.Text type="secondary">{t.patient.patientCode}</Typography.Text></> },
            { title: 'Việc', render: (_, t) => <>{t.title}<br /><Typography.Text type="secondary">{typeLabels[t.type] ?? t.type}</Typography.Text></> },
            { title: 'Tình trạng', render: (_, t) => t.flags.map(f => <Tag key={f} color={flagColors[f]}>{flagLabels[f]}</Tag>) },
            { title: 'Trạng thái', render: (_, t) => <Tag color={statusColors[t.status]}>{statusLabels[t.status]}</Tag> },
            { title: 'Phụ trách', render: (_, t) => t.owner?.name ?? 'Chưa giao' },
            { title: 'Chi nhánh', dataIndex: 'branchName' },
          ]} />
        : <Spin spinning={loading}>
          {data && data.total > data.items.length && <Alert className="form-alert" type="info" showIcon message={`Tháng này có ${data.total} việc; lịch đang hiện ${data.items.length}. Dùng bộ lọc để thu hẹp.`} />}
          <Calendar onPanelChange={(d: Dayjs) => setMonth(d.format('YYYY-MM'))}
            cellRender={(d: Dayjs, info) => info.type !== 'date' ? info.originNode
              : <div className="care-day">{(byDay.get(d.format('YYYY-MM-DD')) ?? []).map(t =>
                <button type="button" key={t.taskId} className={`care-chip status-${t.status}`} onClick={() => set({ task: t.taskId })}
                  title={`${t.patient.fullName} · ${t.title}`}>{t.patient.fullName}{t.flags.length ? ' •' : ''}</button>)}</div>} />
        </Spin>}
    </Card>
    <CareTaskDrawer taskId={selected} options={options} onClose={() => set({ task: null })} onChanged={() => setRevision(v => v + 1)} />
  </>;
}
