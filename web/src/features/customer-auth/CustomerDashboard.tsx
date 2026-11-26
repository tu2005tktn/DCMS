import { Alert, Button, Card, Spin, Tag } from 'antd';
import { CalendarOutlined, ClockCircleOutlined, RightOutlined, UserOutlined, LinkOutlined, MedicineBoxOutlined, FileTextOutlined } from '@ant-design/icons';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ApiError } from '../../services/api';
import { customerApi } from '../../services/customerApi';
import { clinicDate, formatTime, statusView, type AppointmentCard, type AppointmentPage } from '../customer-appointments/types';
import { useCustomerAuth } from './CustomerAuthContext';
import './customerDashboard.css';

type Nearest = { upcoming?: AppointmentCard; past?: AppointmentCard };

function NearestAppointment() {
  const { state, expire } = useCustomerAuth();
  const token = state.status === 'authenticated' ? state.accessToken : null;
  const [data, setData] = useState<Nearest | null>(null);
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    if (!token) return;
    const controller = new AbortController();
    let active = true;
    setBusy(true); setError(null); setData(null);
    const load = (scope: 'UPCOMING' | 'HISTORY', extra: Record<string, string> = {}) => {
      const query = new URLSearchParams({ scope, status: 'ALL', page: '1', pageSize: '20', ...extra });
      return customerApi<AppointmentPage>(`/api/me/appointments?${query}`, { signal: controller.signal }, token);
    };
    // History also holds cancelled bookings of later days, so it is cut at today.
    void Promise.all([load('UPCOMING'), load('HISTORY', { to: clinicDate() })])
      .then(([upcoming, history]) => {
        if (active) setData({ upcoming: upcoming.items[0],
          past: history.items.find(item => item.status !== 'Cancelled' && Date.parse(item.start) <= Date.now()) });
      })
      .catch(failure => {
        if (!active) return;
        if (failure instanceof ApiError && failure.status === 401) expire();
        else setError((failure as Error).message);
      }).finally(() => { if (active) setBusy(false); });
    return () => { active = false; controller.abort(); };
  }, [token, revision, expire]);
  const appointment = data?.upcoming ?? data?.past;
  // With an upcoming booking in the main block, the one that just passed stays visible in a short row under it.
  const recent = data?.upcoming ? data.past : undefined;
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
              <Tag color={statusView(appointment).color}>{statusView(appointment).label}</Tag>
              <h3>{appointment.code}</h3>
              <p><ClockCircleOutlined aria-hidden="true" /><span>{formatTime(appointment.start)} · {appointment.durationMinutes} phút</span></p>
              <p><UserOutlined aria-hidden="true" /><span>Nha sĩ: {appointment.dentistName}</span></p>
              <p><span>Người khám: {appointment.attendeeName} ({appointment.attendeeKind === 'SELF' ? 'Bản thân' : 'Người thân'})</span></p>
            </div>
          </div>
          {recent && <Link to={`/customer/appointments/${encodeURIComponent(recent.code)}`} className="customer-dashboard-recent">
            <strong>Lịch vừa qua</strong>
            <span>{formatTime(recent.start)} · {recent.attendeeName}</span>
            <Tag color={statusView(recent).color}>{statusView(recent).label}</Tag>
            <RightOutlined aria-hidden="true" />
          </Link>}
          <div className="customer-dashboard-appointment-footer">
            <Link to={`/customer/appointments/${encodeURIComponent(appointment.code)}`} className="customer-link-button customer-link-button-primary">Xem lịch hẹn</Link>
            {data?.upcoming ? <span>Giờ Việt Nam</span>
              : <Link to="/customer/book">Đặt lịch mới</Link>}
          </div>
        </> : <div className="customer-dashboard-empty"><span className="customer-dashboard-empty-icon" aria-hidden="true"><CalendarOutlined /></span>
          <p>Bạn chưa có lịch hẹn sắp tới.</p>
          <Link to="/customer/book" className="customer-link-button customer-link-button-primary">Đặt lịch ngay</Link></div>}
  </Card>;
}

export default function CustomerDashboard() {
  const { state } = useCustomerAuth();
  if (state.status !== 'authenticated') return null;
  return <div className="customer-dashboard">
    <div className="customer-dashboard-heading"><h1>Xin chào, {state.user.fullName}</h1>
      <p>Đặt lịch khám, xem lịch hẹn, đơn thuốc và theo dõi liên kết hồ sơ của chính bạn.</p></div>
    <div className="customer-dashboard-main">
      <NearestAppointment />
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
        <Link to="/customer/prescriptions" aria-label="Đơn thuốc của tôi" className="customer-dashboard-shortcut"><span className="customer-dashboard-shortcut-icon"><MedicineBoxOutlined aria-hidden="true" /></span>
          <span><strong>Đơn thuốc của tôi</strong><span>Xem thuốc, liều dùng và lời dặn của bác sĩ.</span></span><RightOutlined aria-hidden="true" /></Link>
        <Link to="/customer/patient-link" aria-label="Liên kết hồ sơ của tôi" className="customer-dashboard-shortcut"><span className="customer-dashboard-shortcut-icon"><LinkOutlined aria-hidden="true" /></span>
          <span><strong>Liên kết hồ sơ của tôi</strong><span>Gửi yêu cầu và theo dõi việc xác minh hồ sơ.</span></span><RightOutlined aria-hidden="true" /></Link>
        <Link to="/customer/treatment-records" className="customer-dashboard-shortcut"><span className="customer-dashboard-shortcut-icon"><FileTextOutlined aria-hidden="true" /></span>
          <span><strong>Hồ sơ điều trị</strong><span>Xem hạng mục bác sĩ đã thực hiện.</span></span><RightOutlined aria-hidden="true" /></Link>
      </div>
    </section>
  </div>;
}
