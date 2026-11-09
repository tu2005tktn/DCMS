import { PlusOutlined, SearchOutlined, ReloadOutlined, RightOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Checkbox, Empty, Input, Select, Space, Table, Tag, Tooltip, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import { useCanPickBranch } from '../authentication/useCanPickBranch';
import { formatDate, formatTime } from '../patient-links/types';
import PatientFormDrawer from './PatientFormDrawer';
import './patientTreatment.css';
import { genderLabels, noPatientsMessage, type PatientListItem, type PatientPage } from './types';

type Filters = { keyword: string; birthDate: string; includeArchived: boolean; branchId?: number };
const initialFilters: Filters = { keyword: '', birthDate: '', includeArchived: false };

// S05 Patient Search and Record (UC-08, UC-38). Search never creates, merges or changes a record.
export default function PatientSearchPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const canPickBranch = useCanPickBranch();
  const [branches, setBranches] = useState<{ id: number; name: string }[]>([]);
  useEffect(() => { if (canPickBranch) api<{ branches: { id: number; name: string }[] }>('/api/appointments/options').then(o => setBranches(o.branches)).catch(() => setBranches([])); }, [canPickBranch]);
  const canManage = auth.user?.permissions.some(p => p === 'patients.manage' || p === 'patients.create') ?? false;
  const [draft, setDraft] = useState<Filters>(initialFilters);
  const [filters, setFilters] = useState<Filters>(initialFilters);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [revision, setRevision] = useState(0);
  const [data, setData] = useState<PatientPage>({ items: [], total: 0, page: 1, pageSize: 20 });
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  useEffect(() => {
    let active = true;
    const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (filters.keyword) query.set('keyword', filters.keyword);
    if (filters.birthDate) query.set('birthDate', filters.birthDate);
    if (filters.includeArchived) query.set('includeArchived', 'true');
    if (filters.branchId) query.set('branchId', String(filters.branchId));
    setBusy(true); setError(null);
    api<PatientPage>(`/api/patients?${query}`)
      .then(result => { if (active) setData(result); })
      .catch(failure => { if (active) { setError((failure as Error).message); void auth.refresh().catch(() => {}); } })
      .finally(() => { if (active) setBusy(false); });
    return () => { active = false; };
  }, [filters, page, pageSize, revision]);

  function apply() { setPage(1); setFilters({ ...draft, keyword: draft.keyword.trim() }); }
  const open = (item: PatientListItem) => navigate(`/patients/${item.id}`);

  return <div className="staff-patient-page">
    <div className="page-heading"><div><Typography.Title level={2}>Bệnh nhân</Typography.Title>
    </div>
      {canManage && <Button aria-label="Thêm hồ sơ" type="primary" icon={<PlusOutlined />} onClick={() => setCreating(true)}>Thêm hồ sơ</Button>}</div>
    <Card className="staff-patient-table-panel">
      <div className="staff-patient-filter-panel"><div className="patient-filters">
        <label>Từ khóa<Input prefix={<SearchOutlined aria-hidden="true" />} allowClear maxLength={100} placeholder="Mã bệnh nhân, họ tên (có hoặc không dấu), SĐT" value={draft.keyword}
          onChange={e => setDraft({ ...draft, keyword: e.target.value })} onPressEnter={apply} /></label>
        {canPickBranch && branches.length > 1 && <label>Chi nhánh<Select aria-label="Chi nhánh" allowClear placeholder="Tất cả chi nhánh" style={{ minWidth: 200 }} value={draft.branchId}
          options={branches.map(b => ({ value: b.id, label: b.name }))} onChange={value => { const next = { ...draft, branchId: value }; setDraft(next); setPage(1); setFilters({ ...next, keyword: next.keyword.trim() }); }} /></label>}
        <label>Ngày sinh<Input type="date" value={draft.birthDate} onChange={e => setDraft({ ...draft, birthDate: e.target.value })} /></label>
        <Checkbox checked={draft.includeArchived} onChange={e => setDraft({ ...draft, includeArchived: e.target.checked })}>Gồm hồ sơ ngừng sử dụng</Checkbox>
        <Space className="link-filter-actions">
          <Button type="primary" onClick={apply}>Tìm kiếm</Button>
          <Button onClick={() => { setDraft(initialFilters); setPage(1); setFilters(initialFilters); }}>Đặt lại</Button>
          <Button aria-label="Tải lại" icon={<ReloadOutlined />} onClick={() => setRevision(value => value + 1)} loading={busy}>Tải lại</Button>
        </Space>
      </div></div>
      <div className="staff-patient-table-heading"><strong>Danh sách bệnh nhân</strong><Typography.Text type="secondary">{error ? 'Chưa tải được dữ liệu' : `${data.total} hồ sơ`}</Typography.Text></div>
      {error && <Alert className="form-alert" type="error" message={error} showIcon
        action={<Button size="small" onClick={() => setRevision(value => value + 1)}>Thử lại</Button>} />}
      <Table<PatientListItem> rowKey="id" dataSource={error ? [] : data.items} className="patient-list-table" loading={busy}
        rowClassName={() => 'link-row'} onRow={item => ({ onClick: () => open(item) })}
        locale={{ emptyText: <Empty description={error ? 'Chưa tải được dữ liệu.' : noPatientsMessage}>
          {!error && canManage && <Button onClick={() => setCreating(true)}>Thêm hồ sơ mới</Button>}</Empty> }}
        pagination={{ current: page, pageSize, total: data.total, showSizeChanger: true, pageSizeOptions: [20, 50, 100],
          onChange: (nextPage, size) => { setPage(size !== pageSize ? 1 : nextPage); setPageSize(size); } }}
        columns={([
          { title: 'Mã bệnh nhân', dataIndex: 'patientCode', render: code => <Typography.Text className="staff-patient-code" strong>{code}</Typography.Text> },
          { title: 'Họ tên', dataIndex: 'fullName', render: name => <Typography.Text strong>{name}</Typography.Text> },
          { title: 'Ngày sinh', render: (_, item) => item.birthDate ? formatDate(item.birthDate) : <Typography.Text type="secondary">Chưa rõ</Typography.Text> },
          { title: 'Giới tính', render: (_, item) => item.gender ? genderLabels[item.gender] : <Typography.Text type="secondary">Chưa rõ</Typography.Text> },
          { title: 'Số điện thoại', render: (_, item) => item.phone ?? '—' },
          { title: 'Lần khám gần nhất', render: (_, item) => item.lastVisitAt ? formatTime(item.lastVisitAt)
            : <Tooltip title="Chức năng tiếp nhận/lượt khám (UC-07) chưa triển khai"><Typography.Text type="secondary">Chưa có dữ liệu</Typography.Text></Tooltip> },
          { title: 'Trạng thái', render: (_, item) => item.isArchived ? <Tag className="staff-patient-status">Ngừng sử dụng</Tag> : <Tag className="staff-patient-status" color="green">Đang sử dụng</Tag> },
          { title: '', render: (_, item) => <Button type="link" size="small" onClick={e => { e.stopPropagation(); open(item); }}>Mở hồ sơ <RightOutlined aria-hidden="true" /></Button> },
        ] as ColumnsType<PatientListItem>).map(c => ({ ...c, onCell: () => ({ 'data-label': typeof c.title === 'string' ? c.title : '' }) as React.HTMLAttributes<HTMLElement> }))} />
    </Card>
    <PatientFormDrawer open={creating} patient={null} onClose={() => setCreating(false)} onSaved={change => {
      setCreating(false);
      // S06 shows the authoritative read-back and the MSG222 notice.
      navigate(`/patients/${change.patient.id}`, { state: { notice: change.notification.message } });
    }} />
  </div>;
}
