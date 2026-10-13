import { Alert, Button, Card, Empty, Input, Select, Space, Table, Tag, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { api } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import ReviewDrawer from './ReviewDrawer';
import { formatTime, statusColors, statusLabels, type LinkQueueItem, type LinkQueuePage, type LinkStatus } from './types';

type Filters = { status: LinkStatus | 'ALL'; from: string; to: string; keyword: string };
const initialFilters: Filters = { status: 'PENDING', from: '', to: '', keyword: '' };

// S08 Link Verification Queue (UC-36). Server enforces ClinicAssistant; this page only mirrors the permission.
export default function LinkVerificationPage() {
  const auth = useAuth();
  const [draft, setDraft] = useState<Filters>(initialFilters);
  const [filters, setFilters] = useState<Filters>(initialFilters);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [revision, setRevision] = useState(0);
  const [data, setData] = useState<LinkQueuePage>({ items: [], total: 0, page: 1, pageSize: 10 });
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filterError, setFilterError] = useState<string | null>(null);
  const [selected, setSelected] = useState<number | null>(null);

  useEffect(() => {
    let active = true;
    const query = new URLSearchParams({ status: filters.status, page: String(page), pageSize: String(pageSize) });
    if (filters.from) query.set('from', filters.from);
    if (filters.to) query.set('to', filters.to);
    if (filters.keyword) query.set('keyword', filters.keyword);
    setBusy(true); setError(null);
    api<LinkQueuePage>(`/api/patient-link-reviews?${query}`)
      .then(result => { if (active) setData(result); })
      .catch(failure => { if (active) { setError((failure as Error).message); void auth.refresh().catch(() => {}); } })
      .finally(() => { if (active) setBusy(false); });
    return () => { active = false; };
  }, [filters, page, pageSize, revision]);

  function apply() {
    if (draft.from && draft.to && draft.from > draft.to) { setFilterError('Khoảng ngày không đúng định dạng.'); return; }
    setFilterError(null); setPage(1); setFilters({ ...draft, keyword: draft.keyword.trim() });
  }

  return <>
    <div className="page-heading"><div><Typography.Title level={2}>Xác minh liên kết hồ sơ</Typography.Title>
      <Typography.Paragraph>Đối chiếu tài khoản với hồ sơ tự thân trước khi cấp quyền xem hồ sơ. Không xác minh chỉ dựa vào số điện thoại.</Typography.Paragraph></div></div>
    <Card>
      <div className="link-filters">
        <label>Trạng thái<Select<Filters['status']> value={draft.status} onChange={status => setDraft({ ...draft, status })} style={{ width: '100%' }}
          options={[...(['PENDING', 'VERIFIED', 'REJECTED', 'REVOKED', 'WITHDRAWN'] as LinkStatus[]).map(value => ({ value, label: statusLabels[value] })),
            { value: 'ALL', label: 'Tất cả' }]} /></label>
        <label>Từ ngày<Input type="date" value={draft.from} max={draft.to || undefined} onChange={e => setDraft({ ...draft, from: e.target.value })} /></label>
        <label>Đến ngày<Input type="date" value={draft.to} min={draft.from || undefined} onChange={e => setDraft({ ...draft, to: e.target.value })} /></label>
        <label>Từ khóa<Input allowClear maxLength={100} placeholder="Mã yêu cầu, tài khoản, họ tên, email, SĐT" value={draft.keyword}
          onChange={e => setDraft({ ...draft, keyword: e.target.value })} onPressEnter={apply} /></label>
        <Space className="link-filter-actions">
          <Button type="primary" onClick={apply}>Tìm kiếm</Button>
          <Button onClick={() => { setDraft(initialFilters); setFilterError(null); setPage(1); setFilters(initialFilters); }}>Đặt lại</Button>
          <Button onClick={() => setRevision(value => value + 1)} loading={busy}>Tải lại</Button>
        </Space>
      </div>
      {filterError && <Alert className="form-alert" type="error" message={filterError} showIcon />}
      {error && <Alert className="form-alert" type="error" message={error} showIcon
        action={<Button size="small" onClick={() => setRevision(value => value + 1)}>Thử lại</Button>} />}
      <Table<LinkQueueItem> rowKey="id" dataSource={error ? [] : data.items} loading={busy} scroll={{ x: 900 }}
        rowClassName={item => item.id === selected ? 'link-row-selected' : 'link-row'}
        onRow={item => ({ onClick: () => setSelected(item.id) })}
        locale={{ emptyText: <Empty description={error ? 'Chưa tải được dữ liệu.' : 'Không có yêu cầu phù hợp với bộ lọc.'} /> }}
        pagination={{ current: page, pageSize, total: data.total, showSizeChanger: true, pageSizeOptions: [10, 20, 50],
          showTotal: total => `${total} yêu cầu`,
          onChange: (nextPage, size) => { setPage(size !== pageSize ? 1 : nextPage); setPageSize(size); } }}
        columns={[
          { title: 'Mã yêu cầu', dataIndex: 'requestCode', render: code => <Typography.Text strong>{code}</Typography.Text> },
          { title: 'Tài khoản', render: (_, item) => <>{item.accountUserName}<br /><Typography.Text type="secondary">{item.accountFullName}</Typography.Text></> },
          { title: 'Họ tên khai báo', dataIndex: 'declaredFullName' },
          { title: 'Thời gian gửi', render: (_, item) => formatTime(item.requestedAt) },
          { title: 'Trạng thái', render: (_, item) => <Tag color={statusColors[item.status]}>{statusLabels[item.status]}</Tag> },
          { title: 'Người xử lý', render: (_, item) => item.handlerName
            ? <>{item.handlerName}<br /><Typography.Text type="secondary">{formatTime(item.handledAt)}</Typography.Text></> : '—' },
        ]} />
    </Card>
    <ReviewDrawer id={selected} onClose={() => setSelected(null)} onChanged={() => setRevision(value => value + 1)} />
  </>;
}
