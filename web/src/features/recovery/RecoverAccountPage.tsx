import { Alert, Button, Card, Form, Input, Space, Spin, Typography, message } from 'antd';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ApiError } from '../../services/api';
import { channelHelpText, recoveryFields, recoveryMessages, requiredMessage } from './recovery.messages';
import {
  fetchRecoveryPolicy,
  requestRecovery,
  resendRecovery,
  resetPassword,
  verifyRecovery,
  type RecoveryPolicy,
  type RecoveryStepId,
} from './recovery.api';
import {
  RESEND_COOLDOWN_SECONDS,
  passwordHints,
  validateAccount,
  validatePasswordPair,
  validateProof,
  type FieldErrors,
} from './recovery.validation';
import '../../app/recovery.css';

const stepOrder: RecoveryStepId[] = ['request', 'verify', 'reset', 'done'];
const stepNames: Record<RecoveryStepId, string> = {
  request: 'Nhập tài khoản',
  verify: 'Xác minh mã',
  reset: 'Đặt mật khẩu mới',
  done: 'Hoàn tất',
};

/**
 * UC-02 account recovery (S03).
 *
 * Four steps driven by one state machine. After the request step the neutral message is always the
 * same text, whether or not the account exists, and nothing on the screen branches on account
 * existence. The reset grant lives in memory only: it is never put in a URL, a console log or
 * analytics.
 */
export default function RecoverAccountPage() {
  const navigate = useNavigate();
  const [toast, toastContext] = message.useMessage();
  const [step, setStep] = useState<RecoveryStepId>('request');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [policy, setPolicy] = useState<RecoveryPolicy | null>(null);
  const [cooldown, setCooldown] = useState(0);
  const [resetGrant, setResetGrant] = useState('');
  const identifier = useRef('');
  // Guards against a second submission while the first request is still in flight.
  const inFlight = useRef(false);

  useEffect(() => {
    // The shared S02 policy is fetched once; its hints are shown from step 3 onward. Failing to
    // load the hints is not an error: the API answer (MSG109) stays the authority.
    void fetchRecoveryPolicy()
      .then(setPolicy)
      .catch(() => undefined);
  }, []);

  useEffect(() => {
    if (cooldown <= 0) return undefined;
    const timer = window.setInterval(() => setCooldown(value => (value <= 1 ? 0 : value - 1)), 1000);
    return () => window.clearInterval(timer);
  }, [cooldown]);

  const hints = useMemo(() => (policy ? passwordHints(policy) : []), [policy]);

  async function run(action: () => Promise<void>) {
    if (inFlight.current) return;
    inFlight.current = true;
    setBusy(true);
    try {
      await action();
    } finally {
      inFlight.current = false;
      setBusy(false);
    }
  }

  async function submitRequest(values: { identifier: string }) {
    const errors = validateAccount(values.identifier);
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }
    setFieldErrors({});
    setError(null);
    await run(async () => {
      try {
        const result = await requestRecovery(values.identifier.trim());
        identifier.current = values.identifier.trim();
        setCooldown(RESEND_COOLDOWN_SECONDS);
        setStep('verify');
        // Neutral by design: identical text for every valid-format identifier.
        setNotice(result.message);
      } catch (failure) {
        setError(connectionError(failure));
      }
    });
  }

  async function submitVerify(values: { proof: string }) {
    const errors = validateProof(values.proof);
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }
    setFieldErrors({});
    setError(null);
    await run(async () => {
      try {
        const grant = await verifyRecovery(identifier.current, values.proof.trim());
        setResetGrant(grant);
        setStep('reset');
      } catch (failure) {
        setError(connectionError(failure));
      }
    });
  }

  async function submitReset(values: { newPassword: string; confirmPassword: string }) {
    const errors = validatePasswordPair(values.newPassword, values.confirmPassword);
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }
    setFieldErrors({});
    setError(null);
    await run(async () => {
      try {
        const result = await resetPassword(
          identifier.current,
          resetGrant,
          values.newPassword,
          values.confirmPassword,
        );
        // Drop the grant as soon as it is consumed; it is single-use on the server too.
        setResetGrant('');
        setNotice(null);
        setStep('done');
        void toast.open({ type: 'success', content: result.message, duration: 0 });
      } catch (failure) {
        setError(connectionError(failure));
      }
    });
  }

  async function resend() {
    if (busy || cooldown > 0) return;
    setError(null);
    await run(async () => {
      try {
        // The answer is the same neutral message; the cooldown restarts either way so a user
        // cannot hammer the endpoint when the account does not exist.
        setNotice(recoveryMessages.MSG12);
        await resendRecovery(identifier.current);
        setCooldown(RESEND_COOLDOWN_SECONDS);
      } catch (failure) {
        setError(connectionError(failure));
      }
    });
  }

  function startOver() {
    setStep('request');
    setError(null);
    setNotice(null);
    setFieldErrors({});
    setResetGrant('');
    setCooldown(0);
    identifier.current = '';
  }

  const currentIndex = stepOrder.indexOf(step);
  const disableForm = busy;

  return (
    <main className="page">
      <div className="content">
        <div className="recovery-shell">
          <div>
            <Typography.Text className="eyebrow">DCMS · Phòng khám nha khoa</Typography.Text>
            <Typography.Title level={1} style={{ marginBottom: 10 }}>Khôi phục tài khoản</Typography.Title>
            <ol className="recovery-step" aria-label="Tiến trình khôi phục">
              {stepOrder.map((item, index) => (
                <li
                  key={item}
                  className="recovery-step-item"
                  data-state={index < currentIndex ? 'done' : index === currentIndex ? 'current' : 'todo'}
                  aria-current={index === currentIndex ? 'step' : undefined}
                  aria-label={`Bước ${index + 1}: ${stepNames[item]}`}
                />
              ))}
            </ol>
          </div>

          <Card className="recovery-card" bordered>
            {error && <Alert className="form-alert" type="error" showIcon message={error} />}
            {notice && step !== 'request' && (
              <div className="recovery-neutral recovery-fade-in" role="status">
                {notice}
              </div>
            )}

            {step === 'request' && (
              <Form
                layout="vertical"
                onFinish={values => void submitRequest({ identifier: values.identifier ?? '' })}
                requiredMark={false}
                className="recovery-fade-in"
              >
                <Typography.Paragraph className="recovery-hint">
                  Nhập tên đăng nhập hoặc email đã đăng ký để nhận hướng dẫn đặt lại mật khẩu.
                </Typography.Paragraph>
                <Form.Item
                  name="identifier"
                  label={recoveryFields.account}
                  validateStatus={fieldErrors.identifier ? 'error' : ''}
                  help={fieldErrors.identifier}
                  rules={[{ required: true, message: requiredMessage(recoveryFields.account) }]}
                >
                  <Input autoFocus maxLength={64} disabled={disableForm} autoComplete="username" />
                </Form.Item>
                <Button type="primary" htmlType="submit" block loading={busy} disabled={busy}>
                  Gửi yêu cầu
                </Button>
                <Typography.Paragraph className="recovery-hint" style={{ marginTop: 14 }}>
                  {channelHelpText}
                </Typography.Paragraph>
              </Form>
            )}

            {step === 'verify' && (
              <Form
                layout="vertical"
                onFinish={values => void submitVerify({ proof: values.proof ?? '' })}
                requiredMark={false}
                className="recovery-fade-in"
              >
                <Typography.Paragraph className="recovery-hint" style={{ marginTop: 14 }}>
                  Nhập mã xác minh đã được gửi qua kênh liên hệ đã đăng ký.
                </Typography.Paragraph>
                <Form.Item
                  name="proof"
                  label={recoveryFields.proof}
                  validateStatus={fieldErrors.proof ? 'error' : ''}
                  help={fieldErrors.proof}
                  rules={[{ required: true, message: requiredMessage(recoveryFields.proof) }]}
                >
                  <Input autoFocus maxLength={24} disabled={disableForm} inputMode="numeric" autoComplete="one-time-code" />
                </Form.Item>
                <div className="recovery-resend">
                  <Button
                    type="link"
                    style={{ padding: 0 }}
                    disabled={disableForm || cooldown > 0}
                    onClick={() => void resend()}
                  >
                    {cooldown > 0 ? `Gửi lại sau ${cooldown}s` : 'Gửi lại mã'}
                  </Button>
                  <Button type="text" disabled={disableForm} onClick={startOver}>
                    Nhập tài khoản khác
                  </Button>
                </div>
                <Button type="primary" htmlType="submit" block loading={busy} disabled={busy} style={{ marginTop: 8 }}>
                  Xác minh
                </Button>
              </Form>
            )}

            {step === 'reset' && (
              <Form
                layout="vertical"
                onFinish={values => void submitReset({ newPassword: values.newPassword ?? '', confirmPassword: values.confirmPassword ?? '' })}
                requiredMark={false}
                className="recovery-fade-in"
              >
                <Typography.Paragraph className="recovery-hint">Nhập mật khẩu mới cho tài khoản của bạn.</Typography.Paragraph>
                {hints.length > 0 && (
                  <ul className="recovery-policy">
                    {hints.map(hint => (
                      <li key={hint}>{hint}</li>
                    ))}
                  </ul>
                )}
                <Form.Item
                  name="newPassword"
                  label={recoveryFields.newPassword}
                  validateStatus={fieldErrors.newPassword ? 'error' : ''}
                  help={fieldErrors.newPassword}
                  rules={[{ required: true, message: requiredMessage(recoveryFields.newPassword) }]}
                >
                  <Input.Password autoFocus maxLength={256} disabled={disableForm} autoComplete="new-password" />
                </Form.Item>
                <Form.Item
                  name="confirmPassword"
                  label={recoveryFields.confirmPassword}
                  validateStatus={fieldErrors.confirmPassword ? 'error' : ''}
                  help={fieldErrors.confirmPassword}
                >
                  <Input.Password maxLength={256} disabled={disableForm} autoComplete="new-password" />
                </Form.Item>
                <Button type="primary" htmlType="submit" block loading={busy} disabled={busy}>
                  Đặt lại mật khẩu
                </Button>
              </Form>
            )}

            {step === 'done' && (
              <div className="recovery-fade-in" style={{ textAlign: 'center' }}>
                <Typography.Title level={4}>{recoveryMessages.MSG13}</Typography.Title>
                <Typography.Paragraph className="recovery-hint">
                  Các phiên đăng nhập trước đã bị thu hồi. Hãy đăng nhập lại bằng mật khẩu mới.
                </Typography.Paragraph>
                <Space>
                  <Button type="primary" onClick={() => navigate('/sign-in', { replace: true })}>
                    Về trang đăng nhập
                  </Button>
                  <Button onClick={startOver}>Khôi phục tài khoản khác</Button>
                </Space>
              </div>
            )}

            {step === 'reset' && busy && (
              <div style={{ marginTop: 12, textAlign: 'center' }}>
                <Spin size="small" />
              </div>
            )}
          </Card>

          <Typography.Paragraph className="login-help">
            <Link to="/sign-in">Quay lại đăng nhập</Link>
          </Typography.Paragraph>
        </div>
      </div>
      {toastContext}
    </main>
  );
}

function connectionError(failure: unknown): string {
  // ApiError already carries the server text (MSG108 / MSG109 / MSG144). Anything else is a
  // transport failure and is reported with the connection message.
  if (failure instanceof ApiError) return failure.message;
  return recoveryMessages.MSG144;
}
