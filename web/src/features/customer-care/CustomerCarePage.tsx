import { Alert, Button, Card, DatePicker, Empty, Input, Modal, Result, Select, Space, Table, Tag, Typography } from 'antd';
import { LeftOutlined, PlusOutlined, RightOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useEffect, useState } from 'react';
import { useAuth } from '../authentication/AuthContext';
import ReferenceCatalogPanel from '../system-settings/ReferenceCatalogPanel';
import CareTaskDrawer from './CareTaskDrawer';
import CareTaskModal from './CareTaskModal';
import { customerCareApi } from './customerCare.api';
import './customerCare.css';
import { clinicTime, contactResultLabels, statusColors, statusFilters, statusLabels, type CareOptions, type CareWorklistItem, type CareWorklistPage,
  type StatusFilter, type WorklistFilter } from './types';

// S60 Chăm sóc khách hàng (docs/specs/S60-CUSTOMER-CARE-WEB.vi.md). Assistants work in their branches; the owner reads every
// branch, may pick one, and does not write (§2).
type Mode = 'day' | 'week' | 'month';
const fmt = (d: dayjs.Dayjs) => d.format('YYYY-MM-DD');
function rangeOf(mode: Mode, anchor: string): [string, string] {
  const d = dayjs(anchor);
  if (mode === 'day') return [anchor, anchor];
  if (mode === 'week') { const monday = d.subtract((d.day() + 6) % 7, 'day'); return [fmt(monday), fmt(monday.add(6, 'day'))]; }
  return [fmt(d.startOf('month')), fmt(d.endOf('month'))];
}

export default function CustomerCarePage() {
  const { user } = useAuth();
  const allowed = ['ClinicAssistant', 'ClinicOwner'].some(role => user?.effectiveRoles.includes(role));
  const canManage = user?.permissions.includes('care-tasks.manage') ?? false;
  const canManageTypes = (user?.effectiveRoles.includes('ClinicOwner') ?? false) || (user?.permissions.includes('catalog.care-types.read') ?? false);
  const [options, setOptions] = useState<CareOptions | null>(null), [optionsError, setOptionsError] = useState<string>(), [optionsRevision, setOptionsRevision] = useState(0);
  const [branchId, setBranchId] = useState<number>();
  const [mode, setMode] = useState<Mode>('day'), [from, setFrom] = useState(''), [to, setTo] = useState('');
  const [ownerAccountId, setOwner] = useState<number>(), [type, setType] = useState<string>(), [status, setStatus] = useState<StatusFilter>();
  const [searchText, setSearchText] = useState(''), [search, setSearch] = useState('');
  const [page, setPage] = useState(1), [pageSize, setPageSize] = useState(20), [revision, setRevision] = useState(0);
  const [data, setData] = useState<CareWorklistPage | null>(null), [loading, setLoading] = useState(false), [error, setError] = useState<string>();
  const [exporting, setExporting] = useState(false), [exportError, setExportError] = useState<string>();
  const [selected, setSelected] = useState<CareWorklistItem | null>(null);
  const [editor, setEditor] = useState<{ item?: CareWorklistItem } | null>(null), [managingTypes, setManagingTypes] = useState(false);

  useEffect(() => {
    if (!allowed) return;
    let live = true; setOptionsError(undefined);
    customerCareApi.options().then(o => {
      if (!live) return;
      setOptions(o);
      // Only the owner picks a branch (default: all); other staff read every branch they are assigned to.
      setBranchId(current => o.allBranches && current && o.branches.some(b => b.id === current) ? current : undefined);
      setFrom(current => current || o.clinicDate); setTo(current => current || o.clinicDate);
    }).catch(e => { if (live) setOptionsError((e as Error).message); });
    return () => { live = false; };
  }, [allowed, optionsRevision]);

  const rangeError = from && to && (from > to ? 'Từ ngày phải trước hoặc bằng đến ngày.' : dayjs(to).diff(dayjs(from), 'day') > 366 ? 'Khoảng ngày tối đa 366 ngày.' : undefined);
  const filter: WorklistFilter | null = options?.branches.length && from && to && !rangeError ? { branchId, from, to, ownerAccountId, type, status, search } : null;
  useEffect(() => {
    if (!filter) return;
    let live = true; setLoading(true); setError(undefined);
    customerCareApi.worklist(filter, page, pageSize).then(v => {
      if (!live) return;
      if (!v.items.length && v.total && page > 1) { setPage(Math.max(1, Math.ceil(v.total / pageSize))); return; }
      setData(v);
    }).catch(e => { if (live) { setData(null); setError((e as Error).message); } }).finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [branchId, from, to, ownerAccountId, type, status, search, page, pageSize, revision, rangeError]);

  if (!allowed) return <Result status="403" title="Chỉ Trợ lý và Chủ phòng khám dùng màn Chăm sóc khách hàng." />;
  const shown = options?.branches.filter(b => branchId == null || b.id === branchId) ?? [];
  const staff = [...new Map(shown.flatMap(b => b.staff).map(x => [x.accountId, x])).values()].sort((a, b) => a.name.localeCompare(b.name, 'vi'));
  const creatable = options?.branches.filter(b => b.canCreate) ?? [];
  const multiBranch = shown.length > 1;
  const reset = () => setPage(1);
  function changeMode(next: Mode) { const [f, t] = rangeOf(next, from || options!.clinicDate); setMode(next); setFrom(f); setTo(t); reset(); }
  function shift(direction: 1 | -1) {
    if (mode === 'month') { const start = dayjs(from).add(direction, 'month'); setFrom(fmt(start.startOf('month'))); setTo(fmt(start.endOf('month'))); }
    else { const days = (dayjs(to).diff(dayjs(from), 'day') + 1) * direction; setFrom(fmt(dayjs(from).add(days, 'day'))); setTo(fmt(dayjs(to).add(days, 'day'))); }
    reset();
  }
  async function exportExcel() {
    if (!filter) return;
    setExporting(true); setExportError(undefined);
    try {
      const url = URL.createObjectURL(await customerCareApi.export(filter));
      Object.assign(document.createElement('a'), { href: url, download: `cham-soc-khach-hang-${from.replaceAll('-', '')}-${to.replaceAll('-', '')}.xlsx` }).click();
      URL.revokeObjectURL(url);
    } catch (e) { setExportError((e as Error).message); } finally { setExporting(false); }
  }

  return <main className="page">
    <Space style={{ width: '100%', justifyContent: 'space-between' }} wrap>
      <Typography.Title level={2} style={{ margin: 0 }}>Chăm sóc khách hàng</Typography.Title>
      {canManage && creatable.length > 0 && <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditor({})}>TẠO MỚI</Button>}
    </Space>
    {optionsError && <Alert type="error" showIcon style={{ marginTop: 16 }} message={optionsError}
      action={<Button size="small" onClick={() => setOptionsRevision(x => x + 1)}>Thử lại</Button>} />}
    {options && options.branches.length === 0 && <Alert type="info" showIcon style={{ marginTop: 16 }} message="Bạn chưa thuộc chi nhánh nào để chăm sóc khách hàng." />}
    {options && options.branches.length > 0 && <>
      <Card style={{ marginTop: 16 }}>
        <Space wrap>
          {options.allBranches && options.branches.length > 1 &&
            <Select aria-label="Chi nhánh" style={{ minWidth: 200 }} value={branchId ?? 0}
              options={[{ value: 0, label: 'Tất cả chi nhánh' }, ...options.branches.map(b => ({ value: b.id, label: b.name }))]}
              onChange={v => { setBranchId(v || undefined); setOwner(undefined); reset(); }} />}
          <Select aria-label="Xem theo" style={{ width: 140 }} value={mode} onChange={changeMode}
            options={[{ value: 'day', label: 'Xem theo ngày' }, { value: 'week', label: 'Xem theo tuần' }, { value: 'month', label: 'Xem theo tháng' }]} />
          <Space.Compact>
            <Button aria-label="Kỳ trước" icon={<LeftOutlined />} onClick={() => shift(-1)} />
            <DatePicker aria-label="Từ ngày" format="DD/MM/YYYY" allowClear={false} value={from ? dayjs(from) : null} onChange={v => { if (v) { setFrom(fmt(v)); reset(); } }} />
            <DatePicker aria-label="Đến ngày" format="DD/MM/YYYY" allowClear={false} value={to ? dayjs(to) : null} onChange={v => { if (v) { setTo(fmt(v)); reset(); } }} />
            <Button aria-label="Kỳ sau" icon={<RightOutlined />} onClick={() => shift(1)} />
          </Space.Compact>
          <Select aria-label="Người phụ trách" style={{ minWidth: 180 }} allowClear placeholder="Người phụ trách" value={ownerAccountId}
            options={staff.map(x => ({ value: x.accountId, label: x.name }))} onChange={v => { setOwner(v); reset(); }} />
          <Select aria-label="Loại chăm sóc" style={{ minWidth: 200 }} allowClear placeholder="Loại chăm sóc" value={type}
            options={[{ label: 'Danh mục', options: options.types.filter(x => !x.system).map(x => ({ value: x.code, label: x.name })) },
              { label: 'Hệ thống tự tạo', options: options.types.filter(x => x.system).map(x => ({ value: x.code, label: x.name })) }]}
            onChange={v => { setType(v); reset(); }} />
          <Select aria-label="Trạng thái" style={{ minWidth: 200 }} allowClear placeholder="Tất cả trạng thái" value={status}
            options={statusFilters} onChange={v => { setStatus(v); reset(); }} />
          <Input.Search aria-label="Khách hàng" placeholder="Khách hàng: mã, tên hoặc SĐT" allowClear maxLength={100} style={{ width: 260 }}
            value={searchText} onChange={e => { setSearchText(e.target.value); if (!e.target.value) { setSearch(''); reset(); } }}
            onSearch={v => { setSearch(v.trim()); reset(); }} />
        </Space>
        {rangeError && <Alert type="warning" showIcon style={{ marginTop: 12 }} message={rangeError} />}
      </Card>

      <Space style={{ width: '100%', justifyContent: 'space-between', margin: '12px 0' }} wrap>
        <Typography.Text italic>{data ? `Tìm thấy ${data.total} phiếu.` : ' '}</Typography.Text>
        <Space>
          <Button type="link" loading={exporting} disabled={!filter} onClick={() => void exportExcel()}>[Excel]</Button>
          <Button onClick={() => setRevision(x => x + 1)}>Tải lại</Button>
        </Space>
      </Space>
      {exportError && <Alert type="error" showIcon closable style={{ marginBottom: 12 }} message={exportError} afterClose={() => setExportError(undefined)} />}
      {error && <Alert type="error" showIcon style={{ marginBottom: 12 }} message={error} action={<Button size="small" onClick={() => setRevision(x => x + 1)}>Thử lại</Button>} />}
      {/* Small text, fixed layout and wrapped content keep every column on one screen from 1280px (spec §3). */}
      <Table<CareWorklistItem> className="care-table" size="small" tableLayout="fixed" rowKey={x => x.task.id} loading={loading}
        dataSource={data?.items ?? []} scroll={{ x: multiBranch ? 980 : 860 }}
        locale={{ emptyText: error ? ' ' : <Empty description="Không có phiếu chăm sóc phù hợp" /> }}
        onRow={row => ({ onClick: () => setSelected(row), style: { cursor: 'pointer' } })}
        pagination={{ current: page, pageSize, total: data?.total ?? 0, showSizeChanger: true, pageSizeOptions: [20, 50, 100],
          onChange: (p, s) => { if (s !== pageSize) { setPageSize(s); setPage(1); } else setPage(p); } }}
        columns={[
          { title: 'Thời gian', width: 92, render: (_, x) => { const [d, t] = clinicTime(x.task.dueAt).split(' '); return <>{d}<div>{t}</div></>; } },
          ...(multiBranch ? [{ title: 'Chi nhánh', width: 120, render: (_: unknown, x: CareWorklistItem) => x.branchName }] : []),
          { title: 'Khách hàng', width: 170, render: (_, x) => <><div><Typography.Text strong>{x.patientName}</Typography.Text></div>
            <Typography.Text type="secondary">{x.patientCode}{x.phone ? ` · ${x.phone}` : ''}</Typography.Text></> },
          { title: 'Loại chăm sóc', width: 120, dataIndex: 'typeName' },
          { title: 'Nội dung', render: (_, x) => <div className="care-content">{x.task.content}</div> },
          { title: 'Người phụ trách', width: 116, render: (_, x) => x.ownerName ?? <Typography.Text type="secondary">Chưa giao</Typography.Text> },
          { title: 'Trạng thái', width: 92, render: (_, x) => <Tag color={statusColors[x.task.status]}>{statusLabels[x.task.status]}</Tag> },
          { title: 'Liên hệ gần nhất', width: 124, render: (_, x) => x.lastContactResult
            ? <>{contactResultLabels[x.lastContactResult] ?? x.lastContactResult}<div><Typography.Text type="secondary">{clinicTime(x.lastContactAt!)}</Typography.Text></div></>
            : <Typography.Text type="secondary">Chưa liên hệ</Typography.Text> },
        ]} />

      <CareTaskDrawer item={selected} staff={options.branches.flatMap(b => b.staff)} canManage={canManage && creatable.some(b => b.id === selected?.task.branchId)} onClose={() => setSelected(null)}
        onEdit={item => setEditor({ item })} onChanged={() => setRevision(x => x + 1)} />
      <CareTaskModal open={!!editor} options={options} editing={editor?.item} canManageTypes={canManageTypes}
        branches={editor?.item ? options.branches.filter(b => b.id === editor.item!.task.branchId) : creatable} initialBranchId={branchId}
        onManageTypes={() => setManagingTypes(true)} onClose={() => setEditor(null)}
        onSaved={() => { setEditor(null); setSelected(null); setRevision(x => x + 1); }} />
      <Modal open={managingTypes} title="Loại chăm sóc khách hàng" width={1000} footer={null} destroyOnClose
        onCancel={() => { setManagingTypes(false); setOptionsRevision(x => x + 1); }}>
        <ReferenceCatalogPanel initialKind="care-types" />
      </Modal>
    </>}
  </main>;
}
