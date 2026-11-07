import { Alert, Button, Card, Empty, Spin, Tag } from 'antd';
import { CalendarOutlined, ClockCircleOutlined, RightOutlined, UserOutlined, LinkOutlined } from '@ant-design/icons';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ApiError } from '../../services/api';
import { customerApi } from '../../services/customerApi';
import { formatTime, statusColors, statusLabels, type AppointmentPage } from '../customer-appointments/types';
import { useCustomerAuth } from './CustomerAuthContext';
import './customerDashboard.css';

function UpcomingAppointment() {
  const { state, expire } = useCustomerAuth();
  const token = state.status === 'authenticated' ? state.accessToken : null;
  const [data, setData] = useState<AppointmentPage | null>(null);
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    if (!token) return;
    const controller = new AbortController();
    let active = true;
    setBusy(true); setError(null); setData(null);
    const query = new URLSearchParams({ scope: 'UPCOMING', status: 'ALL', page: '1', pageSize: '20' });
    void customerApi<AppointmentPage>(`/api/me/appointments?${query}`, { signal: controller.signal }, token)
      .then(result => { if (active) setData(result); })
      .catch(failure => {
        if (!active) return;
        if (failure instanceof ApiError && failure.status === 401) expire();
        else setError((failure as Error).message);
      }).finally(() => { if (active) setBusy(false); });
    return () => { active = false; controller.abort(); };
  }, [token, revision, expire]);
  const appointment = data?.items[0];
  return <Card className="customer-dashboard-upcoming" aria-labelledby="customer-upcoming-title">
    <div className="customer-dashboard-section-heading"><h2 id="customer-upcoming-title">Lịch hẹn gần nhất</h2>
      <Link to="/customer/appointments">Xem tất cả</Link></div>
    {busy ? <div className="customer-dashboard-loading" role="status"><Spin /><span>Đang tải lịch hẹn…</span></div>
      : error ? <Alert type="error" showIcon message={error}
        action={<Button onClick={() => setRevision(value => value + 1)}>Thử lại</Button>} />
        : appointment ? <>
          <div className="customer-dashboard-appointment">
            <div className="customer-dashboard-date" aria-hidden="true">
              <span>{new Intl.DateTimeFormat('vi-VN', { month: 'long', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(appointment.start))}</span>
              <strong>{new Intl.DateTimeFormat('vi-VN', { day: '2-digit', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(appointment.start))}</strong>
            </div>
            <div className="customer-dashboard-appointment-info">
              <Tag color={statusColors[appointment.status]}>{statusLabels[appointment.status]}</Tag>
              <h3>{appointment.code}</h3>
              <p><ClockCircleOutlined aria-hidden="true" /><span>{formatTime(appointment.start)} · {appointment.durationMinutes} phút</span></p>
              <p><UserOutlined aria-hidden="true" /><span>Nha sĩ: {appointment.dentistName}</span></p>
              <p><span>Người khám: {appointment.attendeeName} ({appointment.attendeeKind === 'SELF' ? 'Bản thân' : 'Người thân'})</span></p>
            </div>
          </div>
          <div className="customer-dashboard-appointment-footer">
            <Link to={`/customer/appointments/${encodeURIComponent(appointment.code)}`} className="customer-link-button customer-link-button-primary">Xem lịch hẹn</Link>
            <span>Giờ Việt Nam</span>
          </div>
        </> : <Empty description="Bạn chưa có lịch hẹn sắp tới." />}
  </Card>;
}

export default function CustomerDashboard() {
  const { state } = useCustomerAuth();
  if (state.status !== 'authenticated') return null;
  return <div className="customer-dashboard">
    <div className="customer-dashboard-heading"><h1>Xin chào, {state.user.fullName}</h1>
      <p>Đặt lịch khám, xem lịch hẹn và theo dõi liên kết hồ sơ của chính bạn.</p></div>
    <div className="customer-dashboard-main">
      <UpcomingAppointment />
      <section className="customer-dashboard-booking" aria-labelledby="customer-dashboard-book-title">
        <span className="customer-dashboard-booking-icon"><CalendarOutlined aria-hidden="true" /></span>
        <h2 id="customer-dashboard-book-title">Đặt lịch khám</h2>
        <p>Chọn chi nhánh, bác sĩ và khung giờ phù hợp cho bản thân hoặc người thân.</p>
        <Link to="/customer/book" className="customer-link-button customer-link-button-light">Bắt đầu đặt lịch</Link>
      </section>
    </div>
    <section aria-labelledby="customer-dashboard-shortcuts-title" className="customer-dashboard-shortcuts">
      <h2 id="customer-dashboard-shortcuts-title">Truy cập nhanh</h2>
      <div className="customer-dashboard-shortcut-grid">
        <Link to="/customer/family-members" aria-label="Người thân của tôi" className="customer-dashboard-shortcut"><span className="customer-dashboard-shortcut-icon"><UserOutlined aria-hidden="true" /></span>
          <span><strong>Người thân của tôi</strong><span>Lưu thông tin để chọn nhanh khi đặt lịch hộ.</span></span><RightOutlined aria-hidden="true" /></Link>
        <Link to="/customer/appointments" className="customer-dashboard-shortcut"><span className="customer-dashboard-shortcut-icon"><CalendarOutlined aria-hidden="true" /></span>
          <span><strong>Lịch hẹn của tôi</strong><span>Xem lịch sắp tới, lịch sử và chi tiết lịch hẹn.</span></span><RightOutlined aria-hidden="true" /></Link>
        <Link to="/customer/patient-link" aria-label="Liên kết hồ sơ của tôi" className="customer-dashboard-shortcut"><span className="customer-dashboard-shortcut-icon"><LinkOutlined aria-hidden="true" /></span>
          <span><strong>Liên kết hồ sơ của tôi</strong><span>Gửi yêu cầu và theo dõi việc xác minh hồ sơ.</span></span><RightOutlined aria-hidden="true" /></Link>
      </div>
    </section>
  </div>;
}
