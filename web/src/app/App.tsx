import { Alert, Button, Card, Layout, Menu, Result, Space, Tag, Typography } from 'antd';
import { useState, type ReactNode } from 'react';
import { Link, Navigate, Outlet, Route, Routes, useLocation } from 'react-router-dom';
import { useAuth } from '../features/authentication/AuthContext';
import SignInPage from '../features/authentication/SignInPage';
import AccountsPage from '../features/accounts/AccountsPage';
import WorkingSchedulesPage from '../features/working-schedules/WorkingSchedulesPage';
import LinkVerificationPage from '../features/patient-links/LinkVerificationPage';
import PatientSearchPage from '../features/patients/PatientSearchPage';
import PatientProfilePage from '../features/patients/PatientProfilePage';
import { roleLabels } from '../features/accounts/types';
import HealthStatus from '../features/HealthStatus';

export default function App() {
  return <Routes>
    <Route path="/sign-in" element={<SignInPage />} />
    <Route path="/health" element={<HealthStatus />} />
    <Route element={<Protected><Workspace /></Protected>}>
      <Route index element={<Home />} />
      <Route path="working-schedules" element={<WorkingSchedulesPage />} />
      <Route path="accounts" element={<Protected permission="accounts.manage"><AccountsPage /></Protected>} />
      <Route path="patients" element={<Protected permission="patients.read"><PatientSearchPage /></Protected>} />
      <Route path="patients/:id" element={<Protected permission="patients.read"><PatientProfilePage /></Protected>} />
      <Route path="link-verification" element={<Protected permission="patient-links.verify"><LinkVerificationPage /></Protected>} />
      <Route path="*" element={<Result status="404" title="Không tìm thấy trang" extra={<Link to="/">Về trang chủ</Link>} />} />
    </Route>
  </Routes>;
}

function Protected({ children, permission }: { children: ReactNode; permission?: string }) {
  const { user } = useAuth();
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
    try { await auth.signOut(); } catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }
  return <Layout className="workspace">
    <Layout.Header className="workspace-header"><Link to="/" className="brand">DCMS</Link>
      <Space><Typography.Text>{auth.user?.fullName}</Typography.Text><Button loading={busy} onClick={() => void signOut()}>Đăng xuất</Button></Space></Layout.Header>
    <Menu mode="horizontal" selectedKeys={[location.pathname.startsWith('/patients') ? '/patients' : location.pathname]} items={[
    <Menu mode="horizontal" selectedKeys={[location.pathname]} items={[
      ...(auth.user?.accountType === 'Staff' ? [{ key: '/working-schedules', label: <Link to="/working-schedules">Lịch làm việc</Link> }] : []),
      { key: '/', label: <Link to="/">Tài khoản của tôi</Link> },
      ...(auth.user?.permissions.includes('accounts.manage') ? [{ key: '/accounts', label: <Link to="/accounts">Quản lý tài khoản</Link> }] : []),
      ...(auth.user?.permissions.includes('patients.read') ? [{ key: '/patients', label: <Link to="/patients">Bệnh nhân</Link> }] : []),
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
