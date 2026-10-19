import { Alert, Button, Card, Empty, Form, Input, Pagination, Select, Space, Spin, Table, Tabs, Tag, Typography } from 'antd';
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

  return <section className="customer-appointments-page">
    <div className="calendar-toolbar"><div className="customer-page-heading"><span className="customer-kicker">Lịch khám</span>
      <Typography.Title level={2}>Lịch hẹn của tôi</Typography.Title>
      <Typography.Paragraph>Bạn xem được lịch do mình đặt và lịch của chính mình qua liên kết hồ sơ đã xác minh. Giờ hẹn theo giờ Việt Nam.</Typography.Paragraph></div>
      <Space><Button loading={busy} onClick={() => setRevision(value => value + 1)}>Tải lại</Button>
        <Link to="/customer/book" className="customer-link-button customer-link-button-primary">Đặt lịch mới</Link></Space></div>
    <Tabs activeKey={scope} onChange={value => { setScope(value); setPage(1); }} items={[
      { key: 'UPCOMING', label: 'Sắp tới' }, { key: 'HISTORY', label: 'Lịch sử' },
    ]} />
    <p className="customer-appointment-scope-note">{scope === 'UPCOMING'
      ? 'Các lịch sắp tới được xếp theo thời gian khám.'
      : 'Xem lại lịch đã khám, đã hủy hoặc đã qua ngày hẹn.'}</p>
    <Card className="customer-appointment-filters" title="Tìm lịch hẹn">
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
    {error ? <Alert type="error" showIcon message={error} action={<Button onClick={() => setRevision(value => value + 1)}>Thử lại</Button>} /> : <>
      <div className="customer-appointment-table"><Table<AppointmentCard> rowKey="code" loading={busy} dataSource={data?.items ?? []} scroll={{ x: 760 }}
        locale={{ emptyText: busy ? 'Đang tải lịch hẹn…' : <Empty description={<span>Không có lịch hẹn phù hợp. <Link to="/customer/book">Đặt lịch khám mới</Link></span>} /> }}
        pagination={{ current: page, pageSize: 20, total: data?.total ?? 0, showSizeChanger: false, onChange: setPage }} columns={[
          { title: 'Mã lịch', dataIndex: 'code', render: code => <Link to={`/customer/appointments/${encodeURIComponent(code)}`}>{code}</Link> },
          { title: 'Thời gian', dataIndex: 'start', render: (start, item) => <>{formatTime(start)}<br /><Typography.Text type="secondary">{item.durationMinutes} phút</Typography.Text></> },
          { title: 'Nha sĩ', dataIndex: 'dentistName' },
          { title: 'Người khám', dataIndex: 'attendeeName', render: (name, item) => <>{name}<br /><Typography.Text type="secondary">{item.attendeeKind === 'RELATIVE' ? 'Người thân' : 'Bản thân'} · {item.access === 'BOOKED_BY_ME' ? 'Lịch tôi đặt' : 'Lịch của tôi'}</Typography.Text></> },
          { title: 'Trạng thái', dataIndex: 'status', render: (status: AppointmentStatus) => <Tag color={statusColors[status]}>{statusLabels[status]}</Tag> },
          { title: '', key: 'action', render: (_, item) => <Link aria-label={`Xem lịch ${item.code}`} to={`/customer/appointments/${encodeURIComponent(item.code)}`}>Chi tiết</Link> },
        ]} /></div>
      <div className="customer-appointment-mobile-list">
        {busy ? <Spin tip="Đang tải lịch hẹn"><div className="customer-loading" /></Spin> : data?.items.length ? data.items.map(item =>
          <Card key={item.code} className="customer-appointment-mobile-card">
            <div className="customer-appointment-mobile-head"><Link to={`/customer/appointments/${encodeURIComponent(item.code)}`}><strong>{item.code}</strong></Link>
              <Tag color={statusColors[item.status]}>{statusLabels[item.status]}</Tag></div>
            <div className="customer-appointment-mobile-time">{formatTime(item.start)}</div>
            <p>Nha sĩ: {item.dentistName} · {item.durationMinutes} phút</p>
            <p>Người khám: {item.attendeeName} ({item.attendeeKind === 'RELATIVE' ? 'Người thân' : 'Bản thân'})</p>
            <Link className="customer-text-action" to={`/customer/appointments/${encodeURIComponent(item.code)}`}>Xem chi tiết <span aria-hidden="true">→</span></Link>
          </Card>) : <Empty description={<span>Không có lịch hẹn phù hợp. <Link to="/customer/book">Đặt lịch khám mới</Link></span>} />}
        {(data?.total ?? 0) > 20 && <Pagination className="customer-appointment-mobile-pagination" current={page} pageSize={20}
          total={data?.total ?? 0} showSizeChanger={false} onChange={setPage} />}
      </div>
    </>}
  </section>;
}
