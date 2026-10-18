import { Alert, Button, Card, Empty, Form, Input, Select, Space, Table, Tabs, Tag, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ApiError } from '../../services/api';
import { customerApi } from '../../services/customerApi';
import { useCustomerAuth } from '../customer-auth/CustomerAuthContext';
import { formatTime, statusColors, statusLabels, type AppointmentCard, type AppointmentPage, type AppointmentStatus } from './types';

type Filters = { status: AppointmentStatus | 'ALL'; from?: string; to?: string };

export default function CustomerAppointmentsPage() {
  const { state, expire } = useCustomerAuth();
  const token = state.status === 'authenticated' ? state.accessToken : null;
  const [form] = Form.useForm<Filters>();
  const [scope, setScope] = useState('UPCOMING');
  const [filters, setFilters] = useState<Filters>({ status: 'ALL' });
  const [page, setPage] = useState(1);
  const [data, setData] = useState<AppointmentPage | null>(null);
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    if (!token) return;
    let active = true;
    const controller = new AbortController();
    setBusy(true); setError(null); setData(null);
    const query = new URLSearchParams({ scope, status: filters.status, page: String(page), pageSize: '20' });
    if (filters.from) query.set('from', filters.from);
    if (filters.to) query.set('to', filters.to);
    void customerApi<AppointmentPage>(`/api/me/appointments?${query}`, { signal: controller.signal }, token)
      .then(value => { if (active) setData(value); })
      .catch(failure => {
        if (!active) return;
        if (failure instanceof ApiError && failure.status === 401) expire();
        else setError((failure as Error).message);
      }).finally(() => { if (active) setBusy(false); });
    return () => { active = false; controller.abort(); };
  }, [token, scope, filters, page, revision, expire]);

  return <section>
    <div className="calendar-toolbar"><Typography.Title level={2}>Lịch hẹn của tôi</Typography.Title>
      <Space><Button loading={busy} onClick={() => setRevision(value => value + 1)}>Tải lại</Button>
        <Link to="/customer/book"><Button type="primary">Đặt lịch mới</Button></Link></Space></div>
    <Typography.Paragraph>Bạn xem được lịch do mình đặt và lịch của chính mình qua liên kết hồ sơ đã xác minh. Giờ hẹn theo giờ Việt Nam.</Typography.Paragraph>
    <Tabs activeKey={scope} onChange={value => { setScope(value); setPage(1); }} items={[
      { key: 'UPCOMING', label: 'Sắp tới' }, { key: 'HISTORY', label: 'Lịch sử' },
    ]} />
    <Card className="customer-appointment-filters">
      <Form form={form} layout="vertical" initialValues={{ status: 'ALL' }} onFinish={value => { setFilters(value); setPage(1); }}>
        <div className="customer-appointment-filter-grid">
          <Form.Item name="status" label="Trạng thái"><Select options={[
            { value: 'ALL', label: 'Tất cả' }, ...Object.entries(statusLabels).map(([value, label]) => ({ value, label })),
          ]} /></Form.Item>
          <Form.Item name="from" label="Từ ngày"><Input type="date" /></Form.Item>
          <Form.Item name="to" label="Đến ngày" dependencies={['from']} rules={[({ getFieldValue }) => ({
            validator: async (_, value: string) => { if (value && getFieldValue('from') && value < getFieldValue('from')) throw new Error('Đến ngày phải từ ngày bắt đầu trở đi.'); },
          })]}><Input type="date" /></Form.Item>
        </div>
        <Space><Button type="primary" htmlType="submit">Áp dụng</Button><Button onClick={() => {
          form.resetFields(); setFilters({ status: 'ALL' }); setPage(1);
        }}>Đặt lại</Button></Space>
      </Form>
    </Card>
    {error ? <Alert type="error" showIcon message={error} action={<Button onClick={() => setRevision(value => value + 1)}>Thử lại</Button>} /> :
      <Table<AppointmentCard> rowKey="code" loading={busy} dataSource={data?.items ?? []} scroll={{ x: 760 }}
        locale={{ emptyText: busy ? 'Đang tải lịch hẹn…' : <Empty description="Không có lịch hẹn phù hợp." /> }}
        pagination={{ current: page, pageSize: 20, total: data?.total ?? 0, showSizeChanger: false, onChange: setPage }} columns={[
          { title: 'Mã lịch', dataIndex: 'code', render: code => <Link to={`/customer/appointments/${encodeURIComponent(code)}`}>{code}</Link> },
          { title: 'Thời gian', dataIndex: 'start', render: (start, item) => <>{formatTime(start)}<br /><Typography.Text type="secondary">{item.durationMinutes} phút</Typography.Text></> },
          { title: 'Nha sĩ', dataIndex: 'dentistName' },
          { title: 'Người khám', dataIndex: 'attendeeName', render: (name, item) => <>{name}<br /><Typography.Text type="secondary">{item.attendeeKind === 'RELATIVE' ? 'Người thân' : 'Bản thân'} · {item.access === 'BOOKED_BY_ME' ? 'Lịch tôi đặt' : 'Lịch của tôi'}</Typography.Text></> },
          { title: 'Trạng thái', dataIndex: 'status', render: (status: AppointmentStatus) => <Tag color={statusColors[status]}>{statusLabels[status]}</Tag> },
          { title: '', key: 'action', render: (_, item) => <Link aria-label={`Xem lịch ${item.code}`} to={`/customer/appointments/${encodeURIComponent(item.code)}`}>Chi tiết</Link> },
        ]} />}
  </section>;
}
