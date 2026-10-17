import { Alert, Button, Card, Form, Input } from 'antd';
import { AuditOutlined, LockOutlined, SafetyCertificateOutlined } from '@ant-design/icons';
import { useState } from 'react';
import { Navigate, Link, useNavigate } from 'react-router-dom';
import { useAuth } from './AuthContext';
import BrandLogo from '../../components/BrandLogo';

export default function SignInPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  if (auth.user) return <Navigate to="/" replace />;
  async function submit(values: { userName: string; password: string }) {
    setBusy(true); setError(null);
    try { await auth.signIn(values.userName, values.password); navigate('/', { replace: true }); }
    catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }
  return <main className="auth-page"><div className="auth-shell">
    <aside className="auth-brand">
      <span className="auth-brand-logo"><BrandLogo size={26} /></span>
      <div>
        <h1>DCMS</h1>
        <p className="auth-brand-sub">Hệ thống quản lý dữ liệu phòng khám nha khoa: tài khoản, vai trò, phiên đăng nhập và nhật ký kiểm toán trên một nền tảng.</p>
      </div>
      <ul className="auth-brand-features">
        <li><span className="feature-icon"><SafetyCertificateOutlined /></span>
          <span><strong>Phân quyền theo vai trò</strong><br />Mỗi tài khoản chỉ thấy chức năng thuộc vai trò được cấp.</span></li>
        <li><span className="feature-icon"><AuditOutlined /></span>
          <span><strong>Nhật ký kiểm toán</strong><br />Mọi thao tác quản trị được ghi lại kèm lý do và trạng thái trước/sau.</span></li>
        <li><span className="feature-icon"><LockOutlined /></span>
          <span><strong>Phiên đăng nhập an toàn</strong><br />Phiên có thời hạn và bị thu hồi ngay khi tài khoản bị khóa.</span></li>
      </ul>
      <div className="auth-brand-foot">DCMS · Môi trường phát triển nội bộ</div>
    </aside>
    <section className="auth-panel">
      <div className="auth-head">
        <span className="auth-eyebrow"><LockOutlined />DCMS · Phòng khám nha khoa</span>
        <h1>Đăng nhập</h1>
        <p>Truy cập các chức năng theo vai trò được cấp cho tài khoản của bạn.</p>
      </div>
      <Card className="auth-card">
        {(error || auth.sessionMessage) && <Alert type="error" showIcon message={error ?? auth.sessionMessage} className="form-alert" />}
        <Form layout="vertical" onFinish={values => void submit(values)} requiredMark={false}>
          <Form.Item name="userName" label="Tên đăng nhập" rules={[{ required: true, message: 'Vui lòng nhập Tên đăng nhập.' }]}>
            <Input autoComplete="username" maxLength={64} autoFocus disabled={busy} size="large" placeholder="ten.dangnhap" />
          </Form.Item>
          <Form.Item name="password" label="Mật khẩu" rules={[{ required: true, message: 'Vui lòng nhập Mật khẩu.' }]}>
            <Input.Password autoComplete="current-password" maxLength={256} disabled={busy} size="large" placeholder="••••••••" />
          </Form.Item>
          <Button type="primary" htmlType="submit" block loading={busy} size="large">Đăng nhập</Button>
        </Form>
      </Card>
      <p className="auth-foot">
        Quên mật khẩu? <Link to="/recover" className="dcms-link">Khôi phục tài khoản</Link>
      </p>
    </section>
  </div></main>;
}
