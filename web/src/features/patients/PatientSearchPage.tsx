import { Alert, Button, Card, Checkbox, Empty, Input, Space, Table, Tag, Tooltip, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import { formatDate, formatTime } from '../patient-links/types';
import PatientFormDrawer from './PatientFormDrawer';
import { genderLabels, noPatientsMessage, type PatientListItem, type PatientPage } from './types';

type Filters = { keyword: string; birthDate: string; includeArchived: boolean };
const initialFilters: Filters = { keyword: '', birthDate: '', includeArchived: false };

// S05 Patient Search and Record (UC-08, UC-38). Search never creates, merges or changes a record.
export default function PatientSearchPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const canManage = auth.user?.permissions.includes('patients.manage') ?? false;
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
    setBusy(true); setError(null);
    api<PatientPage>(`/api/patients?${query}`)
      .then(result => { if (active) setData(result); })
      .catch(failure => { if (active) { setError((failure as Error).message); void auth.refresh().catch(() => {}); } })
      .finally(() => { if (active) setBusy(false); });
    return () => { active = false; };
  }, [filters, page, pageSize, revision]);

  function apply() { setPage(1); setFilters({ ...draft, keyword: draft.keyword.trim() }); }
  const open = (item: PatientListItem) => navigate(`/patients/${item.id}`);

  return <>
    <div className="page-heading"><div><Typography.Title level={2}>Bệnh nhân</Typography.Title>
      <Typography.Paragraph>Tìm hồ sơ theo mã, họ tên hoặc số điện thoại. Nhiều người có thể dùng chung số điện thoại; hệ thống không tự gộp hồ sơ.</Typography.Paragraph></div>
      {canManage && <Button type="primary" onClick={() => setCreating(true)}>Thêm hồ sơ</Button>}</div>
    <Card>
      <div className="patient-filters">
        <label>Từ khóa<Input allowClear maxLength={100} placeholder="Mã bệnh nhân, họ tên (có hoặc không dấu), SĐT" value={draft.keyword}
          onChange={e => setDraft({ ...draft, keyword: e.target.value })} onPressEnter={apply} /></label>
        <label>Ngày sinh<Input type="date" value={draft.birthDate} onChange={e => setDraft({ ...draft, birthDate: e.target.value })} /></label>
        <Checkbox checked={draft.includeArchived} onChange={e => setDraft({ ...draft, includeArchived: e.target.checked })}>Gồm hồ sơ ngừng sử dụng</Checkbox>
        <Space className="link-filter-actions">
          <Button type="primary" onClick={apply}>Tìm kiếm</Button>
          <Button onClick={() => { setDraft(initialFilters); setPage(1); setFilters(initialFilters); }}>Đặt lại</Button>
          <Button onClick={() => setRevision(value => value + 1)} loading={busy}>Tải lại</Button>
        </Space>
      </div>
      {error && <Alert className="form-alert" type="error" message={error} showIcon
        action={<Button size="small" onClick={() => setRevision(value => value + 1)}>Thử lại</Button>} />}
      <Table<PatientListItem> rowKey="id" dataSource={error ? [] : data.items} loading={busy} scroll={{ x: 960 }}
        rowClassName={() => 'link-row'} onRow={item => ({ onClick: () => open(item) })}
        locale={{ emptyText: <Empty description={error ? 'Chưa tải được dữ liệu.' : noPatientsMessage}>
          {!error && canManage && <Button onClick={() => setCreating(true)}>Thêm hồ sơ mới</Button>}</Empty> }}
        pagination={{ current: page, pageSize, total: data.total, showSizeChanger: true, pageSizeOptions: [20, 50, 100],
          showTotal: total => `${total} hồ sơ`,
          onChange: (nextPage, size) => { setPage(size !== pageSize ? 1 : nextPage); setPageSize(size); } }}
        columns={[
          { title: 'Mã bệnh nhân', dataIndex: 'patientCode', render: code => <Typography.Text strong>{code}</Typography.Text> },
          { title: 'Họ tên', dataIndex: 'fullName' },
          { title: 'Ngày sinh', render: (_, item) => item.birthDate ? formatDate(item.birthDate) : <Typography.Text type="secondary">Chưa rõ</Typography.Text> },
          { title: 'Giới tính', render: (_, item) => item.gender ? genderLabels[item.gender] : <Typography.Text type="secondary">Chưa rõ</Typography.Text> },
          { title: 'Số điện thoại', render: (_, item) => item.phone ?? '—' },
          { title: 'Lần khám gần nhất', render: (_, item) => item.lastVisitAt ? formatTime(item.lastVisitAt)
            : <Tooltip title="Chức năng tiếp nhận/lượt khám (UC-07) chưa triển khai"><Typography.Text type="secondary">Chưa có dữ liệu</Typography.Text></Tooltip> },
          { title: 'Trạng thái', render: (_, item) => item.isArchived ? <Tag>Ngừng sử dụng</Tag> : <Tag color="green">Đang sử dụng</Tag> },
          { title: '', render: (_, item) => <Button size="small" onClick={e => { e.stopPropagation(); open(item); }}>Mở hồ sơ</Button> },
        ]} />
    </Card>
    <PatientFormDrawer open={creating} patient={null} onClose={() => setCreating(false)} onSaved={change => {
      setCreating(false);
      // S06 shows the authoritative read-back and the MSG222 notice.
      navigate(`/patients/${change.patient.id}`, { state: { notice: change.notification.message } });
    }} />
  </>;
}
