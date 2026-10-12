import { Alert, Button, Card, Form, Input, Typography } from 'antd';
import { useState } from 'react';
import { Navigate, Link, useNavigate } from 'react-router-dom';
import { useAuth } from './AuthContext';

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
  return <main className="page"><div className="content login-content">
    <Typography.Text className="eyebrow">DCMS · Phòng khám nha khoa</Typography.Text>
    <Typography.Title level={1}>Đăng nhập</Typography.Title>
    <Typography.Paragraph>Truy cập các chức năng theo vai trò được cấp cho tài khoản của bạn.</Typography.Paragraph>
    <Card>
      {(error || auth.sessionMessage) && <Alert type="error" showIcon message={error ?? auth.sessionMessage} className="form-alert" />}
      <Form layout="vertical" onFinish={values => void submit(values)} requiredMark={false}>
        <Form.Item name="userName" label="Tên đăng nhập" rules={[{ required: true, message: 'Vui lòng nhập Tên đăng nhập.' }]}>
          <Input autoComplete="username" maxLength={64} autoFocus disabled={busy} />
        </Form.Item>
        <Form.Item name="password" label="Mật khẩu" rules={[{ required: true, message: 'Vui lòng nhập Mật khẩu.' }]}>
          <Input.Password autoComplete="current-password" maxLength={256} disabled={busy} />
        </Form.Item>
        <Button type="primary" htmlType="submit" block loading={busy}>Đăng nhập</Button>
      </Form>
    </Card>
    <Typography.Paragraph className="login-help">
      Quên mật khẩu? <Link to="/recover">Khôi phục tài khoản</Link>
    </Typography.Paragraph>
  </div></main>;
}
