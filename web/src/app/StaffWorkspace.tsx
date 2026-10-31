import { Alert, Avatar, Button, Drawer, Layout, Menu, Typography } from 'antd';
import type { MenuProps } from 'antd';
import { CalendarOutlined, TeamOutlined, ScheduleOutlined, MedicineBoxOutlined, ExperimentOutlined,
  CreditCardOutlined, SettingOutlined, UserOutlined, FileTextOutlined, AuditOutlined, InboxOutlined,
  LinkOutlined, MenuOutlined, LogoutOutlined, CloseOutlined } from '@ant-design/icons';
import { useEffect, useState, type ReactNode } from 'react';
import { Link, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../features/authentication/AuthContext';
import { roleLabels } from '../features/accounts/types';
import './staffWorkspace.css';

export default function StaffWorkspace() {
  const auth = useAuth();
  const location = useLocation();
  const [busy, setBusy] = useState(false), [error, setError] = useState<string | null>(null);
  const [mobileOpen, setMobileOpen] = useState(false);
  useEffect(() => { setMobileOpen(false); }, [location.pathname]);
  async function signOut() {
    setBusy(true); setError(null);
    try { await auth.signOut(); window.location.replace('/sign-in'); } catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }
  const user = auth.user;
  const has = (p: string) => user?.permissions.includes(p) ?? false;
  const role = (...roles: string[]) => user?.effectiveRoles.some(r => roles.includes(r)) ?? false;
  const links: { key: string; title: string; icon: ReactNode; group: string; visible: boolean }[] = [
    { key:'/appointments', title:'Lịch hẹn', icon:<CalendarOutlined />, group:'Vận hành', visible:role('ClinicAssistant','Dentist','ClinicOwner') },
    { key:'/reception', title:'Tiếp nhận và hàng chờ', icon:<ScheduleOutlined />, group:'Vận hành', visible:has('reception.read') },
    { key:'/patients', title:'Bệnh nhân', icon:<TeamOutlined />, group:'Vận hành', visible:has('patients.read') },
    { key:'/working-schedules', title:'Lịch làm việc', icon:<CalendarOutlined />, group:'Vận hành', visible:user?.accountType==='Staff' },
    { key:'/support-inbox', title:'Hộp thư hỗ trợ', icon:<InboxOutlined />, group:'Vận hành', visible:has('appointments.support.handle') },
    { key:'/treatment-templates', title:'Mẫu điều trị', icon:<FileTextOutlined />, group:'Chuyên môn', visible:role('Dentist') },
    { key:'/commercial/treatment', title:'Giá và duyệt điều trị', icon:<MedicineBoxOutlined />, group:'Chuyên môn', visible:role('Admin','ClinicOwner','Dentist') },
    { key:'/clinical-services', title:'Dịch vụ cận lâm sàng', icon:<ExperimentOutlined />, group:'Chuyên môn', visible:has('services.read') },
    { key:'/payments', title:'Thanh toán', icon:<CreditCardOutlined />, group:'Quản trị', visible:has('finance.read') },
    { key:'/accounts', title:'Quản lý tài khoản', icon:<AuditOutlined />, group:'Quản trị', visible:has('accounts.manage') },
    { key:'/system-settings', title:'Thiết lập hệ thống', icon:<SettingOutlined />, group:'Quản trị', visible:role('Dentist')||has('settings.general.manage') },
    { key:'/booking-settings', title:'Cài đặt đặt lịch', icon:<ScheduleOutlined />, group:'Quản trị', visible:user?.accountType==='Staff' },
    { key:'/link-verification', title:'Xác minh liên kết hồ sơ', icon:<LinkOutlined />, group:'Quản trị', visible:has('patient-links.verify') },
  ];
  const visible = links.filter(x=>x.visible);
  const path = location.pathname;
  const selected = visible.find(x=>path===x.key || path.startsWith(x.key+'/'))?.key
    ?? (path.startsWith('/visits/') ? (path.endsWith('/payments') ? '/payments' : '/reception') : '/');
  const title = visible.find(x=>x.key===selected)?.title ?? 'Tài khoản của tôi';
  const items: MenuProps['items'] = ['Vận hành','Chuyên môn','Quản trị'].flatMap(group=> {
    const children = visible.filter(x=>x.group===group).map(x=>({key:x.key,icon:x.icon,label:<Link aria-label={x.title} title={x.title} to={x.key}>{x.title}</Link>}));
    return children.length ? [{key:group,type:'group' as const,label:group,children}] : [];
  });
  const initials = user?.fullName.trim().split(/\s+/).slice(-2).map(x=>x[0]).join('').toUpperCase() || 'NV';
  const brand = <Link to="/" className="staff-brand" aria-label="DCMS — Tài khoản của tôi">
    <svg viewBox="0 0 40 46" aria-hidden="true"><path d="M20 7C11-2 1 5 3 16c1 6 4 9 5 18 1 8 6 9 7 1l2-10c1-5 5-5 6 0l2 10c1 8 6 7 7-1 1-9 4-12 5-18C39 5 29-2 20 7Z" fill="none" stroke="currentColor" strokeWidth="2.6" strokeLinejoin="round"/></svg>
    <span><strong>DCMS</strong><small>Quản lý phòng khám</small></span>
  </Link>;
  const navigation = <><div className="staff-brand-wrap">{brand}</div>
    <nav aria-label="Chức năng nhân viên" className="staff-navigation"><Menu mode="inline" theme="dark" selectedKeys={[selected]} items={items} onClick={()=>setMobileOpen(false)} /></nav>
    <Link to="/" aria-label="Tài khoản của tôi" className={`staff-account-link${selected==='/'?' is-current':''}`} onClick={()=>setMobileOpen(false)}><UserOutlined aria-hidden="true" /> Tài khoản của tôi</Link></>;
  return <Layout className="workspace staff-workspace">
    <a className="staff-skip" href="#staff-content">Đến nội dung chính</a>
    <aside className="staff-sidebar">{navigation}</aside>
    <Drawer className="staff-mobile-navigation" title="Điều hướng nhân viên" placement="left" width={280} open={mobileOpen} onClose={()=>setMobileOpen(false)} closeIcon={<CloseOutlined />} destroyOnClose>{navigation}</Drawer>
    <Layout className="staff-main">
      <Layout.Header className="workspace-header staff-header">
        <div className="staff-header-location"><Button className="staff-menu-toggle" type="text" icon={<MenuOutlined />} aria-label="Mở menu nhân viên" onClick={()=>setMobileOpen(true)} />
          <div className="staff-breadcrumb" aria-label="Vị trí hiện tại"><span>Phòng khám</span><span aria-hidden="true">/</span><strong>{title}</strong></div></div>
        <div className="staff-user"><Avatar>{initials}</Avatar><div className="staff-user-label"><strong>{user?.fullName}</strong><Typography.Text type="secondary">{user?.roles.map(r=>roleLabels[r]??r).join(', ')}</Typography.Text></div>
          <Button aria-label="Đăng xuất" loading={busy} icon={<LogoutOutlined />} onClick={()=>void signOut()}>Đăng xuất</Button></div>
      </Layout.Header>
      <Layout.Content id="staff-content" tabIndex={-1} className="workspace-content staff-content">
        {error && <Alert className="form-alert" type="error" message={error} showIcon />}
        {auth.idleSecondsLeft !== null && auth.idleSecondsLeft <= 60 && <Alert className="form-alert" type="warning" showIcon message={auth.idleSecondsLeft > 0 ? `Phiên staff sẽ hết hạn sau ${auth.idleSecondsLeft} giây nếu không tương tác.` : 'Phiên đã hết hạn. Vui lòng đăng nhập lại.'} />}
        <Outlet />
      </Layout.Content>
    </Layout>
  </Layout>;
}
