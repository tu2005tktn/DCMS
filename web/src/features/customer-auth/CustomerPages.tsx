import { Alert, Button, Card, Form, Input, Spin, Typography } from 'antd';
import { useEffect, useState, type ReactNode } from 'react';
import { Link, Navigate, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { ApiError } from '../../services/api';
import { customerApi } from '../../services/customerApi';
import { CustomerGoogleButton } from './CustomerGoogleButton';
import { useCustomerAuth } from './CustomerAuthContext';

type Registration = { fullName: string; phoneNumber: string; email: string; password: string; confirmPassword: string };
type CredentialPolicy = { requiredLength: number; maximumLength: number; requireDigit: boolean;
  requireLowercase: boolean; requireUppercase: boolean; requireNonAlphanumeric: boolean };
const phonePattern = /^0[35789][0-9]{8}$/;

function ToothMark({ className = '' }: { className?: string }) {
  return <svg className={className} viewBox="0 0 64 64" fill="none" aria-hidden="true" focusable="false">
    <path d="M31.9 10.8C24.6 6.6 14.7 7.5 10.7 15.2c-4.2 8.1-.8 17.1 2.5 25.5 2.5 6.4 5.8 14.5 10.2 14.5 4.6 0 4.1-13.5 8.6-13.5s4 13.5 8.6 13.5c4.4 0 7.7-8.1 10.2-14.5 3.3-8.4 6.7-17.4 2.5-25.5-4-7.7-13.9-8.6-21.2-4.4" stroke="currentColor" strokeWidth="3.5" strokeLinecap="round" strokeLinejoin="round" />
    <path d="M32 11c3.1 1.8 6 2.4 9.2 2" stroke="currentColor" strokeWidth="3.5" strokeLinecap="round" />
  </svg>;
}

function CustomerAuthFrame({ title, description, children, footer }: {
  title: string; description: string; children: ReactNode; footer?: ReactNode;
}) {
  return <main className="customer-portal customer-auth-page">
    <div className="customer-auth-layout">
      <aside className="customer-auth-intro">
        <Link to="/access" className="customer-brand customer-brand-light">DCMS <span>Cổng bệnh nhân</span></Link>
        <div className="customer-auth-intro-copy">
          <span className="customer-kicker">Phòng khám nha khoa</span>
          <h2>Chủ động chăm sóc nụ cười của bạn</h2>
          <p>Đặt lịch khám, theo dõi lịch hẹn và quản lý yêu cầu liên kết hồ sơ tại một nơi.</p>
          <div className="customer-auth-note">Bạn cũng có thể đặt lịch cho người thân mà không cần tạo tài khoản.</div>
        </div>
      </aside>
      <section className="customer-auth-panel">
        <div className="customer-auth-heading"><span className="customer-kicker">Dành cho khách hàng</span>
          <h1>{title}</h1><p>{description}</p></div>
        <Card className="customer-auth-card">{children}</Card>
        {footer && <div className="customer-auth-footer">{footer}</div>}
      </section>
    </div>
  </main>;
}

export function AccessPage() {
  return <main className="customer-portal customer-access-page">
    <div className="customer-access-wrap">
      <div className="customer-access-header"><span className="customer-brand">DCMS <span>Phòng khám nha khoa</span></span></div>
      <div className="customer-access-heading">
        <h1>Bạn muốn vào khu vực nào?</h1>
        <p>Chọn Khách hàng để đặt lịch hoặc Nhân viên để làm việc tại phòng khám.</p></div>
      <div className="customer-access-options">
        <Link to="/customer/sign-in" aria-label="Khách hàng" className="customer-access-option customer-access-option-primary">
          <span className="customer-option-icon"><ToothMark /></span>
          <span className="customer-option-copy"><strong>Khách hàng</strong><span>Đặt lịch khám và xem lịch hẹn của bạn</span></span>
          <span className="customer-option-arrow" aria-hidden="true">→</span>
        </Link>
        <Link to="/sign-in" aria-label="Nhân viên" className="customer-access-option">
          <span className="customer-option-icon" aria-hidden="true">+</span>
          <span className="customer-option-copy"><strong>Nhân viên</strong><span>Vào khu vực làm việc của phòng khám</span></span>
          <span className="customer-option-arrow" aria-hidden="true">→</span>
        </Link>
      </div>
      <p className="customer-access-help">Chưa có tài khoản? <Link to="/customer/register">Tạo tài khoản khách hàng</Link> hoặc <Link to="/customer/book">đặt lịch không cần đăng nhập</Link>.</p>
    </div>
  </main>;
}

export function CustomerGuard({ children }: { children: ReactNode }) {
  const { state, reload } = useCustomerAuth();
  if (state.status === 'initializing') return <main className="page"><Spin size="large" /></main>;
  if (state.status === 'error') return <main className="page"><div className="content login-content">
    <Alert type="error" showIcon message={state.message} action={<Button onClick={() => void reload()}>Thử lại</Button>} />
  </div></main>;
  if (state.status !== 'authenticated') return <Navigate to="/customer/sign-in" replace />;
  return children;
}

export function CustomerShell() {
  const { state, signOut } = useCustomerAuth();
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  async function leave() {
    setBusy(true);
    await signOut();
    navigate('/customer/sign-in', { replace: true });
  }
  return <div className="customer-portal customer-shell">
    <a className="customer-skip-link" href="#customer-main">Bỏ qua điều hướng</a>
    <header className="customer-header"><div className="customer-header-inner">
      <Link to="/customer/home" className="customer-brand">DCMS <span>Cổng bệnh nhân</span></Link>
      <div className="customer-header-account"><span className="customer-header-name">{state.status === 'authenticated' ? state.user.fullName : ''}</span>
        <Button loading={busy} onClick={() => void leave()}>Đăng xuất</Button></div>
    </div></header>
    <nav aria-label="Điều hướng khách hàng" className="customer-navigation"><div className="customer-nav-inner">
      <NavLink to="/customer/home" end className={({ isActive }) => `customer-nav-link${isActive ? ' active' : ''}`}>Trang chủ</NavLink>
      <NavLink to="/customer/appointments" aria-label="Lịch hẹn của tôi" className={({ isActive }) => `customer-nav-link${isActive ? ' active' : ''}`}><span className="customer-nav-long">Lịch hẹn của tôi</span><span className="customer-nav-short" aria-hidden="true">Lịch hẹn</span></NavLink>
      <NavLink to="/customer/patient-link" aria-label="Liên kết hồ sơ" className={({ isActive }) => `customer-nav-link${isActive ? ' active' : ''}`}><span className="customer-nav-long">Liên kết hồ sơ</span><span className="customer-nav-short" aria-hidden="true">Hồ sơ</span></NavLink>
    </div></nav>
    <main className="customer-content" id="customer-main"><Outlet /></main>
  </div>;
}

export function CustomerSignInPage() {
  const { state, signIn, signInWithGoogle } = useCustomerAuth();
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  if (state.status === 'authenticated') return <Navigate to="/customer/home" replace />;

  async function submit(values: { userName: string; password: string }) {
    setBusy(true); setError(null);
    try { await signIn(values.userName, values.password); navigate('/customer/home', { replace: true }); }
    catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }
  async function google(idToken: string) {
    await signInWithGoogle(idToken);
    navigate('/customer/home', { replace: true });
  }

  return <CustomerAuthFrame title="Đăng nhập" description="Tiếp tục đặt lịch và theo dõi các cuộc hẹn của bạn."
    footer={<Link to="/access">Chọn lại cổng đăng nhập</Link>}>
      {(error || (state.status === 'anonymous' && state.message)) &&
        <Alert className="form-alert" type="error" showIcon message={error ?? (state.status === 'anonymous' ? state.message : '')} />}
      <Form layout="vertical" requiredMark={false} onFinish={values => void submit(values)} disabled={busy}>
        <Form.Item name="userName" label="Số điện thoại" rules={[{ required: true, message: 'Vui lòng nhập số điện thoại.' },
          { pattern: phonePattern, message: 'Nhập số di động Việt Nam gồm 10 chữ số.' }]}>
          <Input autoComplete="username" inputMode="numeric" maxLength={10} />
        </Form.Item>
        <Form.Item name="password" label="Mật khẩu" rules={[{ required: true, message: 'Vui lòng nhập mật khẩu.' }]}>
          <Input.Password autoComplete="current-password" />
        </Form.Item>
        <div className="customer-auth-help-row"><Link to="/customer/recover">Quên mật khẩu?</Link></div>
        <Button type="primary" htmlType="submit" block loading={busy}>Đăng nhập</Button>
      </Form>
      <div className="customer-links"><Link to="/customer/register">Tạo tài khoản</Link><Link to="/customer/book">Đặt lịch không cần tài khoản</Link></div>
      <Typography.Paragraph className="customer-or">Hoặc đăng nhập bằng Google</Typography.Paragraph>
      <CustomerGoogleButton onCredential={google} />
  </CustomerAuthFrame>;
}

export function CustomerRegisterPage() {
  const [form] = Form.useForm<Registration>();
  const navigate = useNavigate();
  const [policy, setPolicy] = useState<CredentialPolicy | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [duplicateContact, setDuplicateContact] = useState(false);
  const [success, setSuccess] = useState<string | null>(null);
  useEffect(() => { void customerApi<CredentialPolicy>('/api/auth/policy').then(setPolicy).catch(() => undefined); }, []);

  async function submit(values: Registration) {
    if (busy) return;
    setBusy(true); setError(null); setDuplicateContact(false);
    try {
      const result = await customerApi<{ notification: { message: string } }>('/api/auth/register', {
        method: 'POST', body: JSON.stringify({ ...values, fullName: values.fullName.trim(),
          phoneNumber: values.phoneNumber.trim(), email: values.email.trim() }),
      });
      setSuccess(result.notification.message);
      window.setTimeout(() => navigate('/customer/sign-in', { replace: true }), 1200);
    } catch (failure) {
      const message = (failure as Error).message;
      if (failure instanceof ApiError && failure.code === 'MSG139') {
        setDuplicateContact(true);
        setError(message);
      }
      else if (message.includes('Email')) form.setFields([{ name: 'email', errors: [message] }]);
      else if (message.includes('mật khẩu')) form.setFields([{ name: 'password', errors: [message] }]);
      else setError(message);
    } finally { setBusy(false); }
  }

  return <CustomerAuthFrame title="Tạo tài khoản" description="Đăng ký bằng số di động Việt Nam để quản lý lịch hẹn thuận tiện hơn."
    footer={<>Đã có tài khoản? <Link to="/customer/sign-in">Đăng nhập</Link></>}>
      {error && <Alert className="form-alert" type="error" showIcon message={error}
        description={duplicateContact ? <>Số điện thoại hoặc email có thể đã được sử dụng. Hãy <Link to="/customer/sign-in">đăng nhập</Link> hoặc <Link to="/customer/recover">khôi phục tài khoản</Link>.</> : undefined} />}
      {success && <Alert className="form-alert" type="success" showIcon message={`${success} Đang chuyển tới đăng nhập...`} />}
      <Form form={form} layout="vertical" requiredMark={false} onFinish={values => void submit(values)} disabled={busy || !!success}>
        <Form.Item name="fullName" label="Họ tên" rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập họ tên.' },
          { max: 160, message: 'Họ tên không được vượt quá 160 ký tự.' }]}>
          <Input autoComplete="name" maxLength={160} />
        </Form.Item>
        <Form.Item name="phoneNumber" label="Số điện thoại" rules={[{ required: true, message: 'Vui lòng nhập số điện thoại.' },
          { pattern: phonePattern, message: 'Nhập số di động Việt Nam gồm 10 chữ số, bắt đầu bằng 03, 05, 07, 08 hoặc 09.' }]}>
          <Input autoComplete="tel" inputMode="numeric" maxLength={10} placeholder="0912345678" />
        </Form.Item>
        <Form.Item name="email" label="Email" rules={[{ required: true, message: 'Vui lòng nhập email.' },
          { type: 'email', message: 'Email không đúng định dạng.' }, { max: 254, message: 'Email không được vượt quá 254 ký tự.' }]}>
          <Input autoComplete="email" maxLength={254} />
        </Form.Item>
        <Form.Item name="password" label="Mật khẩu" rules={[{ required: true, message: 'Vui lòng nhập mật khẩu.' },
          { validator: async (_, value: string) => {
            if (!value || !policy) return;
            if (value.length < policy.requiredLength || value.length > policy.maximumLength
              || (policy.requireDigit && !/\p{Nd}/u.test(value))
              || (policy.requireLowercase && !/\p{Ll}/u.test(value))
              || (policy.requireUppercase && !/\p{Lu}/u.test(value))
              || (policy.requireNonAlphanumeric && !/[^\p{L}\p{N}]/u.test(value)))
              throw new Error('Mật khẩu chưa đáp ứng chính sách của hệ thống.');
          } }]}>
          <Input.Password autoComplete="new-password" />
        </Form.Item>
        {policy && <Typography.Paragraph className="customer-password-hint" type="secondary">Mật khẩu tối thiểu {policy.requiredLength} ký tự
          {policy.requireUppercase ? ', có chữ hoa' : ''}{policy.requireLowercase ? ', chữ thường' : ''}
          {policy.requireDigit ? ', số' : ''}{policy.requireNonAlphanumeric ? ' và ký tự đặc biệt' : ''}.</Typography.Paragraph>}
        <Form.Item name="confirmPassword" label="Xác nhận mật khẩu" dependencies={['password']}
          rules={[{ required: true, message: 'Vui lòng xác nhận mật khẩu.' },
            ({ getFieldValue }) => ({ validator: async (_, value: string) => {
              if (value && value !== getFieldValue('password')) throw new Error('Xác nhận mật khẩu không khớp.');
            } })]}>
          <Input.Password autoComplete="new-password" />
        </Form.Item>
        <Button type="primary" htmlType="submit" block loading={busy}>Đăng ký</Button>
      </Form>
      <Typography.Paragraph className="customer-form-note">Không cần mã xác nhận khi đăng ký.</Typography.Paragraph>
  </CustomerAuthFrame>;
}

export function CustomerHomePage() {
  const { state } = useCustomerAuth();
  if (state.status !== 'authenticated') return null;
  return <div className="customer-home">
    <div className="customer-page-heading"><span className="customer-kicker">Trang chủ</span>
      <h1>Xin chào, {state.user.fullName}</h1>
      <p>Đặt lịch khám, xem lịch hẹn và theo dõi hồ sơ của chính bạn tại đây.</p></div>
    <section className="customer-home-primary" aria-labelledby="customer-book-title">
      <div><h2 id="customer-book-title">Đặt lịch khám</h2>
        <p>Chọn chi nhánh, bác sĩ và khung giờ phù hợp cho bản thân hoặc người thân.</p>
        <Link to="/customer/book" className="customer-link-button customer-link-button-light">Bắt đầu đặt lịch</Link></div>
      <div className="customer-home-mark"><ToothMark /></div>
    </section>
    <div className="customer-home-grid">
      <Card className="customer-action-card"><h2>Lịch hẹn của tôi</h2>
        <p>Xem lịch sắp tới, lịch sử và những lịch có thể đổi hoặc hủy.</p>
        <Link to="/customer/appointments" className="customer-text-action">Xem lịch hẹn <span aria-hidden="true">→</span></Link></Card>
      <Card className="customer-action-card"><h2>Liên kết hồ sơ của tôi</h2>
        <p>Gửi yêu cầu và theo dõi việc xác minh hồ sơ của chính bạn.</p>
        <Link to="/customer/patient-link" className="customer-text-action">Liên kết hồ sơ của tôi <span aria-hidden="true">→</span></Link></Card>
    </div>
  </div>;
}
