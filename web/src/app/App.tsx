import { Alert, Button, Card, Layout, Menu, Result, Space, Tag, Typography } from 'antd';
import { useState, type ReactNode } from 'react';
import { Link, Navigate, Outlet, Route, Routes, useLocation } from 'react-router-dom';
import { useAuth } from '../features/authentication/AuthContext';
import SignInPage from '../features/authentication/SignInPage';
import AccountsPage from '../features/accounts/AccountsPage';
import WorkingSchedulesPage from '../features/working-schedules/WorkingSchedulesPage';
import BookingSettingsPage from '../features/working-schedules/BookingSettingsPage';
import LinkVerificationPage from '../features/patient-links/LinkVerificationPage';
import PatientSearchPage from '../features/patients/PatientSearchPage';
import PatientProfilePage from '../features/patients/PatientProfilePage';
import SupportInboxPage from '../features/support-inbox/SupportInboxPage';
import RecoverAccountPage from '../features/recovery/RecoverAccountPage';
import AppointmentCalendarPage from '../features/appointments/AppointmentCalendarPage';
import AppointmentDetailPage from '../features/appointments/AppointmentDetailPage';
import { roleLabels } from '../features/accounts/types';
import HealthStatus from '../features/HealthStatus';
import { AccessPage, CustomerGuard, CustomerHomePage, CustomerRegisterPage, CustomerShell, CustomerSignInPage } from '../features/customer-auth/CustomerPages';
import { useCustomerAuth } from '../features/customer-auth/CustomerAuthContext';
import CustomerBookingPage from '../features/customer-booking/CustomerBookingPage';
import CustomerPatientLinkPage from '../features/customer-patient-link/CustomerPatientLinkPage';
import CustomerAppointmentsPage from '../features/customer-appointments/CustomerAppointmentsPage';
import CustomerAppointmentDetailPage from '../features/customer-appointments/CustomerAppointmentDetailPage';
import DossierPage from '../features/treatment/DossierPage';
import PlanPage from '../features/treatment/PlanPage';
import RevisionPage from '../features/treatment/RevisionPage';
import TemplatePage from '../features/treatment/TemplatePage';

export default function App() {
  return <Routes>
    <Route path="/sign-in" element={<SignInPage />} />
    <Route path="/access" element={<AccessPage />} />
    <Route path="/customer/sign-in" element={<CustomerSignInPage />} />
    <Route path="/customer/register" element={<CustomerRegisterPage />} />
    <Route path="/customer/recover" element={<RecoverAccountPage customer />} />
    <Route path="/customer/book" element={<CustomerGuard><CustomerBookingPage /></CustomerGuard>} />
    <Route path="/customer" element={<CustomerGuard><CustomerShell /></CustomerGuard>}>
      <Route index element={<Navigate to="/customer/home" replace />} />
      <Route path="home" element={<CustomerHomePage />} />
      <Route path="verify-email" element={<Navigate to="/customer/home" replace />} />
      <Route path="patient-link" element={<CustomerPatientLinkPage />} />
      <Route path="appointments" element={<CustomerAppointmentsPage />} />
      <Route path="appointments/:code" element={<CustomerAppointmentDetailPage />} />
    </Route>
    <Route path="/customer/*" element={<Result status="404" title="Không tìm thấy trang" extra={<Link to="/customer/home">Về trang chủ</Link>} />} />
    <Route path="/recover" element={<RecoverAccountPage />} />
    <Route path="/health" element={<HealthStatus />} />
    <Route element={<Entry />}>
      <Route index element={<Home />} />
    </Route>
    <Route element={<Protected><Workspace /></Protected>}>
      <Route path="working-schedules" element={<WorkingSchedulesPage />} />
      <Route path="booking-settings" element={<BookingSettingsPage />} />
      <Route path="accounts" element={<Protected permission="accounts.manage"><AccountsPage /></Protected>} />
      <Route path="patients" element={<Protected permission="patients.read"><PatientSearchPage /></Protected>} />
      <Route path="patients/:id" element={<Protected permission="patients.read"><PatientProfilePage /></Protected>} />
      <Route path="patients/:id/dossiers/:dossierId" element={<Protected permission="patients.read"><DossierPage /></Protected>} />
      <Route path="patients/:id/dossiers/:dossierId/plan" element={<Protected permission="patients.read"><PlanPage /></Protected>} />
      <Route path="patients/:id/dossiers/:dossierId/plan/revise/:itemId" element={<Protected permission="patients.read"><RevisionPage /></Protected>} />
      <Route path="treatment-templates" element={<Protected><TemplatePage /></Protected>} />
      <Route path="appointments" element={<Protected><AppointmentCalendarPage /></Protected>} />
      <Route path="appointments/:id" element={<Protected><AppointmentDetailPage /></Protected>} />
      <Route path="link-verification" element={<Protected permission="patient-links.verify"><LinkVerificationPage /></Protected>} />
      <Route path="support-inbox" element={<Protected permission="appointments.support.handle"><SupportInboxPage /></Protected>} />
      <Route path="*" element={<Result status="404" title="Không tìm thấy trang" extra={<Link to="/">Về trang chủ</Link>} />} />
    </Route>
  </Routes>;
}

function Entry() {
  const staff = useAuth();
  const customer = useCustomerAuth();
  if (staff.initializing) return <main className="page">Đang kiểm tra phiên…</main>;
  if (staff.user) return <Workspace />;
  if (customer.state.status === 'initializing') return <main className="page"><Typography.Text>Đang kiểm tra phiên…</Typography.Text></main>;
  if (customer.state.status === 'authenticated') return <Navigate to="/customer/home" replace />;
  return <Navigate to="/access" replace />;
}

function Protected({ children, permission }: { children: ReactNode; permission?: string }) {
  const { user, initializing } = useAuth();
  if (initializing) return <main className="page">Đang kiểm tra phiên…</main>;
  if (!user) return <Navigate to="/sign-in" replace />;
  if (permission && !user.permissions.includes(permission)) return <Result status="403" title="Bạn không có quyền thực hiện thao tác này." extra={<Link to="/">Về trang chủ</Link>} />;
  return children;
}

function Workspace() {
  const auth = useAuth();
  const location = useLocation();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  async function signOut() {
    setBusy(true); setError(null);
    try { await auth.signOut(); window.location.replace('/sign-in'); } catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }
  return <Layout className="workspace">
    <Layout.Header className="workspace-header"><Link to="/" className="brand">DCMS</Link>
      <Space><Typography.Text>{auth.user?.fullName}</Typography.Text><Button loading={busy} onClick={() => void signOut()}>Đăng xuất</Button></Space></Layout.Header>
    <Menu mode="horizontal" selectedKeys={[location.pathname.startsWith('/patients') ? '/patients' : location.pathname]} items={[
      ...(auth.user?.accountType === 'Staff' ? [{ key: '/working-schedules', label: <Link to="/working-schedules">Lịch làm việc</Link> }] : []),
      ...(auth.user?.accountType === 'Staff' ? [{ key: '/booking-settings', label: <Link to="/booking-settings">Cài đặt đặt lịch</Link> }] : []),
      ...(auth.user?.effectiveRoles.some(r => ['ClinicAssistant', 'Dentist', 'ClinicOwner'].includes(r)) ? [{ key: '/appointments', label: <Link to="/appointments">Lịch hẹn</Link> }] : []),
      ...(auth.user?.permissions.includes('appointments.support.handle') ? [{ key: '/support-inbox', label: <Link to="/support-inbox">Hộp thư hỗ trợ</Link> }] : []),
      { key: '/', label: <Link to="/">Tài khoản của tôi</Link> },
      ...(auth.user?.permissions.includes('accounts.manage') ? [{ key: '/accounts', label: <Link to="/accounts">Quản lý tài khoản</Link> }] : []),
      ...(auth.user?.permissions.includes('patients.read') ? [{ key: '/patients', label: <Link to="/patients">Bệnh nhân</Link> }] : []),
      ...(auth.user?.effectiveRoles.includes('Dentist') ? [{ key: '/treatment-templates', label: <Link to="/treatment-templates">Mẫu điều trị</Link> }] : []),
      ...(auth.user?.permissions.includes('patient-links.verify') ? [{ key: '/link-verification', label: <Link to="/link-verification">Xác minh liên kết hồ sơ</Link> }] : []),
    ]} />
    <Layout.Content className="workspace-content">{error && <Alert className="form-alert" type="error" message={error} showIcon />}<Outlet /></Layout.Content>
  </Layout>;
}

function Home() {
  const { user, refresh } = useAuth();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  return <><Typography.Title level={2}>Tài khoản của tôi</Typography.Title>
    <Card title={user?.fullName} extra={<Button loading={busy} onClick={() => {
      setBusy(true); setError(null); void refresh().catch(e => setError((e as Error).message)).finally(() => setBusy(false));
    }}>Cập nhật quyền</Button>}>
      {error && <Alert type="error" message={error} showIcon className="form-alert" />}
      <Typography.Paragraph>Tên đăng nhập: <strong>{user?.userName}</strong></Typography.Paragraph>
      <Typography.Paragraph>Loại tài khoản: {user?.accountType === 'Staff' ? 'Nhân viên' : 'Công khai'}</Typography.Paragraph>
      <Typography.Paragraph>Vai trò được cấp: <Space wrap>{user?.roles.map(role => <Tag key={role}>{roleLabels[role] ?? role}</Tag>)}</Space></Typography.Paragraph>
      <Typography.Paragraph>Vai trò hiệu lực: <Space wrap>{user?.effectiveRoles.map(role => <Tag key={role}>{roleLabels[role] ?? role}</Tag>)}</Space></Typography.Paragraph>
      {user?.accountType === 'Public' && <Typography.Paragraph>{user.hasVerifiedPatientLink
        ? 'Tài khoản đã được liên kết với hồ sơ của chính bạn.'
        : 'Chưa có liên kết hồ sơ bệnh án được xác minh. Tài khoản công khai không tự có quyền đọc hồ sơ lâm sàng.'}</Typography.Paragraph>}
    </Card>
    <Typography.Paragraph className="login-help"><Link to="/health">Xem trạng thái hệ thống</Link></Typography.Paragraph>
  </>;
}
