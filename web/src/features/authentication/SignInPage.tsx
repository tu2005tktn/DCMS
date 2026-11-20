import { ArrowLeftOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Form, Input, Typography } from 'antd';
import { useState } from 'react';
import { Navigate, Link } from 'react-router-dom';
import { useAuth } from './AuthContext';

export default function SignInPage() {
  const auth = useAuth();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // Every staff account lands on the work/service statistics after signing in (docs/specs/WORK-SERVICE-STATISTICS.md).
  if (auth.user) return <Navigate to="/work-statistics" replace />;
  async function submit(values: { userName: string; password: string }) {
    setBusy(true); setError(null);
    try { await auth.signIn(values.userName, values.password); }
    catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }
  return <main className="page"><div className="content login-content">
    <Link to="/access" className="login-back"><ArrowLeftOutlined /> Chọn cổng đăng nhập</Link>
    <Typography.Text className="eyebrow">DCMS · Phòng khám nha khoa</Typography.Text>
    <Typography.Title level={1}>Đăng nhập</Typography.Title>
    <Typography.Paragraph>Truy cập các chức năng theo vai trò được cấp cho tài khoản của bạn.</Typography.Paragraph>
    <Card>
      {(error || auth.sessionMessage) && <Alert type="error" showIcon message={error ?? auth.sessionMessage} className="form-alert" />}
      <Form layout="vertical" onFinish={values => void submit(values)} requiredMark={false}>
        <Form.Item name="userName" label="Tên đăng nhập" rules={[{ required: true, message: 'Vui lòng nhập Tên đăng nhập.' }]}>
          <Input autoComplete="username" autoFocus disabled={busy} />
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
