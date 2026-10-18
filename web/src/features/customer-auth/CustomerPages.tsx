import { Alert, Button, Card, Form, Input, Space, Spin, Typography } from 'antd';
import { useEffect, useState, type ReactNode } from 'react';
import { Link, Navigate, Outlet, useNavigate } from 'react-router-dom';
import { ApiError } from '../../services/api';
import { customerApi } from '../../services/customerApi';
import { CustomerGoogleButton } from './CustomerGoogleButton';
import { useCustomerAuth } from './CustomerAuthContext';

type Registration = { fullName: string; phoneNumber: string; email: string; password: string; confirmPassword: string };
type CredentialPolicy = { requiredLength: number; maximumLength: number; requireDigit: boolean;
  requireLowercase: boolean; requireUppercase: boolean; requireNonAlphanumeric: boolean };
const phonePattern = /^0[35789][0-9]{8}$/;

export function AccessPage() {
  return <main className="page"><div className="content login-content">
    <Typography.Text className="eyebrow">DCMS · Phòng khám nha khoa</Typography.Text>
    <Typography.Title level={1}>Chọn cách truy cập</Typography.Title>
    <Typography.Paragraph>Chọn cổng phù hợp với tài khoản của bạn.</Typography.Paragraph>
    <Card><Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <Link to="/sign-in"><Button block size="large">Nhân viên</Button></Link>
      <Link to="/customer/sign-in"><Button block size="large" type="primary">Khách hàng</Button></Link>
    </Space></Card>
  </div></main>;
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
  return <div className="customer-shell">
    <header className="customer-header"><Link to="/customer/home" className="brand">DCMS</Link>
      <Space><Typography.Text>{state.status === 'authenticated' ? state.user.fullName : ''}</Typography.Text>
        <Button loading={busy} onClick={() => void leave()}>Đăng xuất</Button></Space>
    </header>
    <main className="customer-content"><nav aria-label="Điều hướng khách hàng" className="customer-navigation">
      <Space wrap size="middle"><Link to="/customer/home">Trang chủ</Link>
        <Link to="/customer/appointments">Lịch hẹn của tôi</Link>
        <Link to="/customer/patient-link">Liên kết hồ sơ</Link></Space>
    </nav><Outlet /></main>
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

  return <main className="page"><div className="content login-content">
    <Typography.Text className="eyebrow">DCMS · Khách hàng</Typography.Text>
    <Typography.Title level={1}>Đăng nhập khách hàng</Typography.Title>
    <Card>
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
        <Button type="primary" htmlType="submit" block loading={busy}>Đăng nhập</Button>
      </Form>
      <div className="customer-links"><Link to="/customer/register">Tạo tài khoản</Link><Link to="/customer/recover">Quên mật khẩu</Link><Link to="/customer/book">Đặt lịch không cần tài khoản</Link></div>
      <Typography.Paragraph className="customer-or">Hoặc đăng nhập bằng Google</Typography.Paragraph>
      <CustomerGoogleButton onCredential={google} />
    </Card>
    <Typography.Paragraph className="login-help"><Link to="/access">Chọn lại cổng đăng nhập</Link></Typography.Paragraph>
  </div></main>;
}

export function CustomerRegisterPage() {
  const [form] = Form.useForm<Registration>();
  const navigate = useNavigate();
  const [policy, setPolicy] = useState<CredentialPolicy | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  useEffect(() => { void customerApi<CredentialPolicy>('/api/auth/policy').then(setPolicy).catch(() => undefined); }, []);

  async function submit(values: Registration) {
    if (busy) return;
    setBusy(true); setError(null);
    try {
      const result = await customerApi<{ notification: { message: string } }>('/api/auth/register', {
        method: 'POST', body: JSON.stringify({ ...values, fullName: values.fullName.trim(),
          phoneNumber: values.phoneNumber.trim(), email: values.email.trim() }),
      });
      setSuccess(result.notification.message);
      window.setTimeout(() => navigate('/customer/sign-in', { replace: true }), 1200);
    } catch (failure) {
      const message = (failure as Error).message;
      if (failure instanceof ApiError && failure.code === 'MSG139') form.setFields([{ name: 'phoneNumber', errors: [message] }]);
      else if (message.includes('Email')) form.setFields([{ name: 'email', errors: [message] }]);
      else if (message.includes('mật khẩu')) form.setFields([{ name: 'password', errors: [message] }]);
      else setError(message);
    } finally { setBusy(false); }
  }

  return <main className="page"><div className="content login-content">
    <Typography.Text className="eyebrow">DCMS · Khách hàng</Typography.Text>
    <Typography.Title level={1}>Tạo tài khoản</Typography.Title>
    <Typography.Paragraph>Đăng ký bằng số di động Việt Nam; không cần mã xác nhận.</Typography.Paragraph>
    <Card>
      {error && <Alert className="form-alert" type="error" showIcon message={error} />}
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
        {policy && <Typography.Paragraph type="secondary">Mật khẩu tối thiểu {policy.requiredLength} ký tự
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
      <Typography.Paragraph className="login-help">Đã có tài khoản? <Link to="/customer/sign-in">Đăng nhập</Link></Typography.Paragraph>
    </Card>
  </div></main>;
}

export function CustomerHomePage() {
  const { state } = useCustomerAuth();
  if (state.status !== 'authenticated') return null;
  return <div className="customer-home">
    <Typography.Title level={2}>Xin chào, {state.user.fullName}</Typography.Title>
    <Typography.Paragraph>Tài khoản khách hàng: {state.user.userName}</Typography.Paragraph>
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <Card title="Đặt lịch khám"><Typography.Paragraph>Chọn lịch cho bản thân hoặc người thân.</Typography.Paragraph>
        <Link to="/customer/book"><Button type="primary">Bắt đầu đặt lịch</Button></Link></Card>
      <Card title="Lịch hẹn của tôi"><Typography.Paragraph>Xem lịch sắp tới, lịch sử và đổi hoặc hủy lịch đủ điều kiện.</Typography.Paragraph>
        <Link to="/customer/appointments"><Button>Xem lịch hẹn</Button></Link></Card>
      <Card title="Liên kết hồ sơ của tôi"><Typography.Paragraph>Gửi yêu cầu hoặc xem trạng thái liên kết hồ sơ của chính bạn.</Typography.Paragraph>
        <Link to="/customer/patient-link"><Button>Liên kết hồ sơ của tôi</Button></Link></Card>
    </Space>
  </div>;
}
