import { Alert, Button, Drawer, Dropdown, Layout, Menu, Result, Space, Tag, message } from 'antd';
import { useEffect, useState, type ReactNode } from 'react';
import { Link, Navigate, Outlet, Route, Routes, useLocation } from 'react-router-dom';
import {
  ClockCircleOutlined,
  IdcardOutlined,
  LogoutOutlined,
  MenuOutlined,
  ReloadOutlined,
  SafetyCertificateOutlined,
  TeamOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { useAuth } from '../features/authentication/AuthContext';
import SignInPage from '../features/authentication/SignInPage';
import AccountsPage from '../features/accounts/AccountsPage';
import RecoverAccountPage from '../features/recovery/RecoverAccountPage';
import { roleLabels } from '../features/accounts/types';
import HealthStatus from '../features/HealthStatus';
import BrandLogo from '../components/BrandLogo';
import AppointmentRoutes from '../routes/Appointments';
import WorkingSchedulesPage from '../features/working-schedules/WorkingSchedulesPage';
import LinkVerificationPage from '../features/patient-links/LinkVerificationPage';
import PatientSearchPage from '../features/patients/PatientSearchPage';
import PatientProfilePage from '../features/patients/PatientProfilePage';
import { roleLabels } from '../features/accounts/types';
import HealthStatus from '../features/HealthStatus';
import { AccessPage, CustomerGuard, CustomerHomePage, CustomerRegisterPage, CustomerShell, CustomerSignInPage } from '../features/customer-auth/CustomerPages';
import { useCustomerAuth } from '../features/customer-auth/CustomerAuthContext';
import CustomerBookingPage from '../features/customer-booking/CustomerBookingPage';
import CustomerPatientLinkPage from '../features/customer-patient-link/CustomerPatientLinkPage';

export default function App() {
  return <Routes>
    <Route path="/sign-in" element={<SignInPage />} />
    <Route path="/recover" element={<RecoverAccountPage />} />
    <Route path="/access" element={<AccessPage />} />
    <Route path="/customer/sign-in" element={<CustomerSignInPage />} />
    <Route path="/customer/register" element={<CustomerRegisterPage />} />
    <Route path="/customer/book" element={<CustomerBookingPage />} />
    <Route path="/customer" element={<CustomerGuard><CustomerShell /></CustomerGuard>}>
      <Route index element={<Navigate to="/customer/home" replace />} />
      <Route path="home" element={<CustomerHomePage />} />
      <Route path="patient-link" element={<CustomerPatientLinkPage />} />
    </Route>
    <Route path="/customer/*" element={<Result status="404" title="Không tìm thấy trang" extra={<Link to="/customer/home">Về trang chủ</Link>} />} />
    <Route path="/health" element={<HealthStatus />} />
    <Route element={<Entry />}>
      <Route index element={<Home />} />
    </Route>
    <Route element={<Protected><Workspace /></Protected>}>
      <Route path="working-schedules" element={<WorkingSchedulesPage />} />
      <Route path="accounts" element={<Protected permission="accounts.manage"><AccountsPage /></Protected>} />
      <Route path="appointments" element={<AppointmentRoutes />} />
      <Route path="patients" element={<Protected permission="patients.read"><PatientSearchPage /></Protected>} />
      <Route path="patients/:id" element={<Protected permission="patients.read"><PatientProfilePage /></Protected>} />
      <Route path="link-verification" element={<Protected permission="patient-links.verify"><LinkVerificationPage /></Protected>} />
      <Route path="*" element={<Result status="404" title="Không tìm thấy trang" extra={<Link to="/">Về trang chủ</Link>} />} />
    </Route>
  </Routes>;
}

function Entry() {
  const staff = useAuth();
  const customer = useCustomerAuth();
  if (staff.user) return <Workspace />;
  if (customer.state.status === 'initializing') return <main className="page"><Typography.Text>Đang kiểm tra phiên…</Typography.Text></main>;
  if (customer.state.status === 'authenticated') return <Navigate to="/customer/home" replace />;
  return <Navigate to="/access" replace />;
}

function Protected({ children, permission }: { children: ReactNode; permission?: string }) {
  const { user } = useAuth();
  if (!user) return <Navigate to="/sign-in" replace />;
  if (permission && !user.permissions.includes(permission)) return <Result status="403" title="Bạn không có quyền thực hiện thao tác này." extra={<Link to="/">Về trang chủ</Link>} />;
  return children;
}

function initials(fullName: string) {
  const parts = fullName.trim().split(/\s+/);
  const letters = parts.length > 1 ? `${parts[0][0]}${parts[parts.length - 1][0]}` : parts[0] ?? '';
  return letters.toUpperCase();
}

function BrandMark() {
  return (
    <span className="sider-logo" aria-hidden><BrandLogo /></span>
  );
}

const pageContext: Record<string, { title: string; subtitle: string }> = {
  '/': { title: 'Tài khoản của tôi', subtitle: 'Thông tin phiên đăng nhập và quyền hiệu lực' },
  '/accounts': { title: 'Quản lý tài khoản', subtitle: 'Tạo tài khoản, cấp vai trò và quản lý trạng thái truy cập' },
};

function Workspace() {
  const auth = useAuth();
  const location = useLocation();
  const [navOpen, setNavOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [clock, setClock] = useState(() => new Date());
  useEffect(() => {
    const timer = window.setInterval(() => setClock(new Date()), 30_000);
    return () => window.clearInterval(timer);
  }, []);
  async function signOut() {
    setBusy(true); setError(null);
    try { await auth.signOut(); window.location.replace('/sign-in'); } catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }
  async function refreshPermissions() {
    setError(null);
    try { await auth.refresh(); void message.success('Đã cập nhật quyền hiện tại của tài khoản.'); }
    catch (failure) { setError((failure as Error).message); }
  }
  const user = auth.user;
  const canManage = !!user?.permissions.includes('accounts.manage');
  const navItems = [
    { key: '/', icon: <UserOutlined />, label: 'Tài khoản của tôi' },
    ...(canManage ? [{ key: '/accounts', icon: <TeamOutlined />, label: 'Quản lý tài khoản' }] : []),
  ];
  const navMenu = (onNavigate?: () => void) => (
    <Menu className="sider-nav" theme="dark" mode="inline" selectedKeys={[location.pathname]}
      onClick={onNavigate}
      items={navItems.map(item => ({ ...item, label: <Link to={item.key} onClick={onNavigate}>{item.label}</Link> }))} />
  );
  const context = pageContext[location.pathname] ?? { title: 'DCMS', subtitle: 'Hệ thống quản lý phòng khám nha khoa' };
  const userMenu = {
    items: [
      { key: 'refresh', icon: <ReloadOutlined />, label: 'Làm mới quyền' },
      { type: 'divider' as const },
      { key: 'sign-out', icon: <LogoutOutlined />, danger: true, disabled: busy, label: 'Đăng xuất' },
    ],
    onClick: ({ key }: { key: string }) => {
      if (key === 'refresh') void refreshPermissions();
      if (key === 'sign-out') void signOut();
    },
  };
  return <Layout className="workspace">
    <Layout.Sider className="workspace-sider" width={248}>
      <div className="sider-head"><BrandMark />
        <span className="sider-brand"><strong>DCMS</strong><span>Phòng khám nha khoa</span></span>
      </div>
      <div className="sider-section-label">Điều hướng</div>
      {navMenu()}
      <div className="sider-foot">
        <div className="sider-user">
          <span className="dcms-avatar dcms-avatar--sm" aria-hidden>{user ? initials(user.fullName) : ''}</span>
          <span className="sider-user-meta">
            <strong>{user?.fullName}</strong>
            <span>{user?.roles.map(role => roleLabels[role] ?? role).join(' · ')}</span>
          </span>
        </div>
      </div>
    </Layout.Sider>
    <Layout>
      <Layout.Header className="workspace-header">
        <Space>
          <Button className="header-menu-toggle" icon={<MenuOutlined />} onClick={() => setNavOpen(true)} aria-label="Mở điều hướng" />
          <span className="header-context"><strong>{context.title}</strong><span>{context.subtitle}</span></span>
        </Space>
        <div className="header-actions">
          <span className="header-clock"><ClockCircleOutlined />
            {clock.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}
          </span>
          <Dropdown menu={userMenu} placement="bottomRight" trigger={['click']}>
            <span className="user-avatar-trigger dcms-avatar dcms-avatar--sm" role="button" tabIndex={0}
              aria-label={`Menu người dùng: ${user?.fullName ?? ''}`}>
              {user ? initials(user.fullName) : ''}
            </span>
          </Dropdown>
        </div>
      </Layout.Header>
      <Layout.Content className="workspace-content dcms-fade-in" key={location.pathname}>
        {error && <Alert className="form-alert" type="error" message={error} showIcon />}
        <Outlet />
      </Layout.Content>
    </Layout>
    <Drawer className="workspace-drawer" placement="left" open={navOpen} onClose={() => setNavOpen(false)}
      width={272} closable={false} styles={{ body: { padding: 0 } }}>
      <div className="sider-head"><BrandMark />
        <span className="sider-brand"><strong>DCMS</strong><span>Phòng khám nha khoa</span></span>
      </div>
      <div className="sider-section-label">Điều hướng</div>
      {navMenu(() => setNavOpen(false))}
      <div className="sider-foot"><Button block onClick={() => setNavOpen(false)}>Đóng menu</Button></div>
    </Drawer>
    <Layout.Header className="workspace-header"><Link to="/" className="brand">DCMS</Link>
      <Space><Typography.Text>{auth.user?.fullName}</Typography.Text><Button loading={busy} onClick={() => void signOut()}>Đăng xuất</Button></Space></Layout.Header>
    <Menu mode="horizontal" selectedKeys={[location.pathname.startsWith('/patients') ? '/patients' : location.pathname]} items={[
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
  return <>
    <div className="page-header">
      <div className="page-header-main">
        <span className="page-header-icon" aria-hidden><UserOutlined /></span>
        <div className="page-header-text">
          <h2>Tài khoản của tôi</h2>
          <p>Xem thông tin định danh, vai trò được cấp và quyền hiệu lực của phiên đăng nhập.</p>
        </div>
      </div>
      <div className="page-header-actions">
        <Button loading={busy} onClick={() => {
          setBusy(true); setError(null); void refresh().catch(e => setError((e as Error).message)).finally(() => setBusy(false));
        }}>Cập nhật quyền</Button>
        <Link to="/health"><Button type="primary">Xem trạng thái hệ thống</Button></Link>
      </div>
    </div>
    {error && <Alert className="form-alert" type="error" message={error} showIcon />}
    <div className="profile-hero">
      <div className="profile-hero-main">
        <span className="dcms-avatar dcms-avatar--lg" aria-hidden>{user ? initials(user.fullName) : ''}</span>
        <div>
          <h2>{user?.fullName}</h2>
          <div className="profile-user">@{user?.userName}</div>
          <div className="profile-chips">
            <span className={`dcms-chip ${user?.accountType === 'Staff' ? 'dcms-chip--primary' : 'dcms-chip--neutral'}`}>
              {user?.accountType === 'Staff' ? 'Nhân viên' : 'Công khai'}
            </span>
            <span className="dcms-chip dcms-chip--success"><span className="dcms-dot" />Phiên hoạt động</span>
          </div>
        </div>
      </div>
    </div>
    <div className="info-grid">
      <div className="info-tile">
        <div className="info-tile-head"><span className="info-tile-icon"><UserOutlined /></span>Tên đăng nhập</div>
        <div className="info-tile-body"><span className="info-value">{user?.userName}</span></div>
      </div>
      <div className="info-tile">
        <div className="info-tile-head"><span className="info-tile-icon"><IdcardOutlined /></span>Loại tài khoản</div>
        <div className="info-tile-body"><span className="info-value">{user?.accountType === 'Staff' ? 'Nhân viên' : 'Công khai'}</span></div>
      </div>
      <div className="info-tile">
        <div className="info-tile-head"><span className="info-tile-icon"><TeamOutlined /></span>Vai trò được cấp</div>
        <div className="info-tile-body"><Space wrap>{user?.roles.map(role => <Tag key={role} color="blue">{roleLabels[role] ?? role}</Tag>)}</Space></div>
      </div>
      <div className="info-tile">
        <div className="info-tile-head"><span className="info-tile-icon"><SafetyCertificateOutlined /></span>Vai trò hiệu lực</div>
        <div className="info-tile-body"><Space wrap>{user?.effectiveRoles.map(role => <Tag key={role} color="geekblue">{roleLabels[role] ?? role}</Tag>)}</Space></div>
      </div>
      {user?.accountType === 'Public' && <div className="info-tile">
        <div className="info-tile-head"><span className="info-tile-icon"><SafetyCertificateOutlined /></span>Liên kết hồ sơ bệnh án</div>
        <div className="info-tile-body"><span className="info-note">Chưa có liên kết hồ sơ bệnh án được xác minh. Tài khoản công khai không tự có quyền đọc hồ sơ lâm sàng.</span></div>
      </div>}
    </div>
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
