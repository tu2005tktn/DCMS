import { Alert, Button, Card, Empty, Space, Table, Tag, Typography } from 'antd';
import { useEffect, useMemo, useState } from 'react';
import { useAuth } from '../authentication/AuthContext';
import {
  formatDateTime,
  kindLabels,
  statusColors,
  statusFilterOptions,
  statusLabels,
  type StatusFilterGroup,
  type SupportRequestDto,
  type SupportRequestPage,
} from './types';
import { supportInboxApi } from './supportInbox.api';
import { subscribeSupportInbox, type RealtimeState } from './supportInbox.realtime';
import RequestDrawer from './RequestDrawer';

type LoadState =
  | { kind: 'loading' }
  | { kind: 'denied' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; page: SupportRequestPage };

/**
 * S26 — Staff Support Inbox (Web).
 *
 * Trang Hộp thư hỗ trợ dành cho staff DCMS: xem & xử lý yêu cầu đổi/huỷ/xác nhận
 * từ booker & attendee. Tương đương màn hình S12 trên mobile nhưng phù hợp quy
 * trình desktop (bảng, drawer, command bar).
 *
 * Trang đọc/ghi qua API thật (`supportInbox.api.ts` → `GET/POST /api/support-requests/...`, xem
 * docs/implementation/S26-Staff-Support-Inbox.md §API) và tự cập nhật qua realtime SignalR;
 * `supportInbox.mock.ts` chỉ là repo in-memory dành cho phát triển offline, trang này không dùng nó.
 */
export default function SupportInboxPage() {
  const auth = useAuth();
  const [filter, setFilter] = useState<StatusFilterGroup>('open');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [revision, setRevision] = useState(0);
  const [drawerRevision, setDrawerRevision] = useState(0);
  const [realtimeState, setRealtimeState] = useState<RealtimeState>('connecting');
  const [load, setLoad] = useState<LoadState>({ kind: 'loading' });
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const isStaff = useMemo(() => {
    const user = auth.user;
    if (!user) return false;
    if (user.accountType === 'Public') return false;
    return (user.roles?.length ?? 0) > 0 || user.accountType === 'Staff';
  }, [auth.user]);

  useEffect(() => {
    if (!isStaff || !auth.user) {
      setRealtimeState('offline');
      return;
    }
    const refresh = () => {
      setRevision(value => value + 1);
      setDrawerRevision(value => value + 1);
    };
    return subscribeSupportInbox(refresh, refresh, setRealtimeState);
  }, [isStaff, auth.user?.id]);

  useEffect(() => {
    if (!isStaff) {
      setLoad({ kind: 'denied' });
      return;
    }
    let active = true;
    setLoad({ kind: 'loading' });
    const statuses = statusFilterOptions.find(option => option.value === filter)?.statuses ?? [];
    supportInboxApi
      .listRequestsForStaff({ statuses, page, pageSize })
      .then(result => { if (active) setLoad({ kind: 'ready', page: result }); })
      .catch((failure: Error) => {
        if (active) setLoad({ kind: 'error', message: failure.message || 'Không thể kết nối máy chủ.' });
      });
    return () => { active = false; };
  }, [isStaff, filter, page, pageSize, revision]);

  if (load.kind === 'denied' || !isStaff) {
    return <Card>
      <Empty description={<>
        <Typography.Title level={4} style={{ marginTop: 8 }}>Không có quyền truy cập</Typography.Title>
        <Typography.Paragraph type="secondary">Màn hình Hộp thư hỗ trợ chỉ dành cho nhân viên DCMS.</Typography.Paragraph>
      </>} />
    </Card>;
  }

  const items = load.kind === 'ready' ? load.page.items : [];
  const total = load.kind === 'ready' ? load.page.total : 0;

  return <>
    <div className="page-heading">
      <div>
        <Typography.Title level={2}>Hộp thư hỗ trợ</Typography.Title>
        <Typography.Paragraph>
          Xử lý yêu cầu đổi/huỷ/xác nhận từ booker &amp; attendee thuộc chi nhánh của bạn.
          Resolve chỉ được tính khi mọi thay đổi lịch cần thiết đã commit trên hệ thống.
        </Typography.Paragraph>
      </div>
      <Space>
        <Tag color={realtimeState === 'connected' ? 'green' : realtimeState === 'offline' ? 'default' : 'blue'}>
          {realtimeState === 'connected' ? 'Tự động cập nhật' :
            realtimeState === 'reconnecting' ? 'Đang kết nối lại' :
              realtimeState === 'connecting' ? 'Đang kết nối' : 'Realtime ngoại tuyến'}
        </Tag>
        <Button onClick={() => setRevision(value => value + 1)} loading={load.kind === 'loading'}>Tải lại</Button>
      </Space>
    </div>

    <Card>
      <div className="support-inbox-tabs">
        {statusFilterOptions.map(option => (
          <Button
            key={option.value}
            type={filter === option.value ? 'primary' : 'default'}
            onClick={() => { setFilter(option.value); setPage(1); }}
            className="support-inbox-tab"
          >
            {option.label}
          </Button>
        ))}
      </div>

      {load.kind === 'error' && <Alert className="form-alert" type="error" showIcon
        message={load.message}
        action={<Button size="small" onClick={() => setRevision(value => value + 1)}>Thử lại</Button>} />}

      <Table<SupportRequestDto>
        rowKey="id"
        className="support-inbox-table"
        dataSource={load.kind === 'ready' ? items : []}
        loading={load.kind === 'loading'}
        scroll={{ x: 1100 }}
        rowClassName={item => item.id === selectedId ? 'support-row-selected' : 'support-row'}
        onRow={item => ({ onClick: () => setSelectedId(item.id) })}
        locale={{ emptyText: <Empty description={load.kind === 'error' ? 'Chưa tải được dữ liệu.' : 'Không có yêu cầu nào trong bộ lọc này.'} /> }}
        pagination={{
          current: page, pageSize, total,
          showSizeChanger: true, pageSizeOptions: [10, 20, 50],
          showTotal: value => `${value} yêu cầu`,
          onChange: (nextPage, size) => { setPage(size !== pageSize ? 1 : nextPage); setPageSize(size); },
        }}
        columns={[
          {
            title: 'Mã lịch', dataIndex: 'appointmentCode', width: 130,
            render: code => <Typography.Text strong>{code}</Typography.Text>,
          },
          {
            title: 'Loại yêu cầu', dataIndex: 'kind', width: 180,
            render: (_, item) => <Tag>{kindLabels[item.kind]}</Tag>,
          },
          {
            title: 'Lý do', dataIndex: 'reason', ellipsis: true,
            render: value => value || <Typography.Text type="secondary">—</Typography.Text>,
          },
          {
            title: 'Người yêu cầu', width: 200,
            render: (_, item) => {
              const name = item.requester.name || '—';
              return <Space direction="vertical" size={0}>
                <Typography.Text>{name}{item.requester.isAnonymous ? ' (vãng lai)' : ''}</Typography.Text>
                {item.requester.phone && <Typography.Text type="secondary" style={{ fontSize: 12 }}>{item.requester.phone}</Typography.Text>}
              </Space>;
            },
          },
          {
            title: 'Trạng thái', dataIndex: 'status', width: 160,
            render: (_, item) => <Tag color={statusColors[item.status]}>{statusLabels[item.status]}</Tag>,
          },
          {
            title: 'Cập nhật', dataIndex: 'updatedAt', width: 160,
            render: value => formatDateTime(value),
          },
        ]}
      />
    </Card>

    <RequestDrawer
      id={selectedId}
      refreshToken={drawerRevision}
      onClose={() => setSelectedId(null)}
      onChanged={() => {
        setRevision(value => value + 1);
        // KHÔNG tự đóng drawer ở đây — drawer tự quyết định khi nào đóng
        // (sau Resolve/Reject, drawer setSelectedId(null) nếu muốn).
      }}
    />
  </>;
}
