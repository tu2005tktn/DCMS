import Workspace from './StaffWorkspace';
import FamilyMembersPage from '../features/family-members/FamilyMembersPage';
import VisitPage from '../features/examinations/ExaminationPage';
import PaymentsPage from '../features/finance/PaymentsPage';
import ReceiptPrintPage from '../features/finance/ReceiptPrintPage';
import ClinicalServicesPage from '../features/finance/ClinicalServicesPage';
import { Alert, Button, Card, Result, Space, Tag, Typography } from 'antd';
import { useState, type ReactNode } from 'react';
import { Link, Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from '../features/authentication/AuthContext';
import SignInPage from '../features/authentication/SignInPage';
import SystemSettingsPage from '../features/system-settings/SystemSettingsPage';
import MaterialsPage from '../features/system-settings/MaterialsPage';
import WorkStatisticsPage from '../features/finance/WorkStatisticsPage';
import RevenuePage from '../features/finance/RevenuePage';
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
import ReceptionPage from '../features/reception/ReceptionPage';
import CheckInPage from '../features/reception/CheckInPage';
import PatientDocumentsPage from '../features/clinical-documents/PatientDocumentsPage';
import { AccessPage, CustomerGuard, CustomerHomePage, CustomerRegisterPage, CustomerShell, CustomerSignInPage } from '../features/customer-auth/CustomerPages';
import { useCustomerAuth } from '../features/customer-auth/CustomerAuthContext';
import CustomerBookingPage from '../features/customer-booking/CustomerBookingPage';
import CustomerPatientLinkPage from '../features/customer-patient-link/CustomerPatientLinkPage';
import CustomerAppointmentsPage from '../features/customer-appointments/CustomerAppointmentsPage';
import CustomerAppointmentDetailPage from '../features/customer-appointments/CustomerAppointmentDetailPage';
import DossierPage from '../features/treatment/DossierPage';
import PlanPage from '../features/treatment/PlanPage';
import RevisionPage from '../features/treatment/RevisionPage';

import ItemPlansPage from '../features/treatment/ItemPlansPage';
import CommercialTreatmentPage from '../features/treatment/CommercialTreatmentPage';

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
      <Route path="family-members" element={<FamilyMembersPage />} />
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
      <Route path="system-settings" element={<Protected permission="settings.read"><SystemSettingsPage /></Protected>} />
      <Route path="materials" element={<Protected permission={['materials.purchases.read', 'materials.stock.read', 'materials.usage.read']}><MaterialsPage /></Protected>} />
      <Route path="payments" element={<Protected permission="finance.read"><PaymentsPage /></Protected>} />
      <Route path="work-statistics" element={<Protected permission="finance.work-statistics.read"><WorkStatisticsPage /></Protected>} />
      <Route path="revenue" element={<Protected permission="finance.revenue.read"><RevenuePage /></Protected>} />
      <Route path="visits/:visitId/payments" element={<Protected permission="finance.read"><PaymentsPage /></Protected>} />
      <Route path="payments/:id/print" element={<Protected permission="finance.receipts.print"><ReceiptPrintPage /></Protected>} />
      <Route path="clinical-services" element={<Protected permission="services.read"><ClinicalServicesPage /></Protected>} />
      <Route path="working-schedules" element={<Protected permission="working-schedules.read"><WorkingSchedulesPage /></Protected>} />
      <Route path="booking-settings" element={<Protected permission="booking-settings.read"><BookingSettingsPage /></Protected>} />
      <Route path="accounts" element={<Protected permission="accounts.read"><AccountsPage /></Protected>} />
      <Route path="patients" element={<Protected permission="patients.read"><PatientSearchPage /></Protected>} />
      <Route path="patients/:id" element={<Protected permission="patients.read"><PatientProfilePage /></Protected>} />
      <Route path="patients/:id/documents" element={<Protected permission="documents.read"><PatientDocumentsPage /></Protected>} />
      <Route path="patients/:id/dossiers/:dossierId" element={<Protected permission="treatment.read"><DossierPage /></Protected>} />
      <Route path="patients/:id/dossiers/:dossierId/plan" element={<Protected permission="treatment.read"><PlanPage /></Protected>} />
      <Route path="patients/:id/dossiers/:dossierId/plan/revise/:itemId" element={<Protected permission="treatment.revisions.manage"><RevisionPage /></Protected>} />
      <Route path="treatment-templates" element={<Protected permission="treatment.templates.read"><ItemPlansPage /></Protected>} />
      <Route path="treatment-templates/legacy" element={<Navigate to="/treatment-templates" replace />} />
      <Route path="commercial/treatment" element={<Protected permission={['treatment.prices.read','treatment.commercial.read']}><CommercialTreatmentPage /></Protected>} />
      <Route path="appointments" element={<Protected permission="appointments.read"><AppointmentCalendarPage /></Protected>} />
      <Route path="reception" element={<Protected permission="reception.read"><ReceptionPage /></Protected>} />
      <Route path="reception/appointments/:appointmentId" element={<Protected permission="reception.read"><CheckInPage /></Protected>} />
      <Route path="visits/:visitId" element={<Protected permission="examinations.read"><VisitPage /></Protected>} />
      <Route path="appointments/:id" element={<Protected permission="appointments.read"><AppointmentDetailPage /></Protected>} />
      <Route path="link-verification" element={<Protected permission="patient-links.read"><LinkVerificationPage /></Protected>} />
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

function Protected({ children, permission }: { children: ReactNode; permission?: string | string[] }) {
  const { user, initializing } = useAuth();
  if (initializing) return <main className="page">Đang kiểm tra phiên…</main>;
  if (!user) return <Navigate to="/sign-in" replace />;
  if (permission && !(Array.isArray(permission) ? permission : [permission]).some(key => user.permissions.includes(key))) return <Result status="403" title="Bạn không có quyền thực hiện thao tác này." extra={<Link to="/">Về trang chủ</Link>} />;
  return children;
}


function Home() {
  const { user, refresh } = useAuth();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  return <><Typography.Title level={2}>Tài khoản của tôi</Typography.Title>
    <Card title={user?.fullName} extra={<Button aria-label="Cập nhật quyền" loading={busy} onClick={() => {
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
