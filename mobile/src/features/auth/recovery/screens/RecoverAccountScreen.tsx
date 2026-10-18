import { useEffect, useMemo, useRef, useState } from 'react';
import {
  ActivityIndicator,
  Linking,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { useRouter } from 'expo-router';
import { ApiError } from '@/services/api/api-error';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { typography } from '@/theme/typography';
import {
  fetchRecoveryPolicy,
  requestRecovery,
  resendRecovery,
  resetPassword,
  verifyRecovery,
  type RecoveryPolicy,
  type RecoveryStepId,
} from '../recovery.api';
import {
  RESEND_COOLDOWN_SECONDS,
  recoveryFields,
  recoveryMessages,
} from '../recovery.messages';
import {
  passwordHints,
  validateAccount,
  validatePasswordPair,
  validateProof,
  type FieldErrors,
} from '../recovery.schema';

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
 * Four steps driven by one state machine, mirroring the web screen. After the request step the
 * neutral message is always the same text whether or not the account exists, and nothing here
 * branches on account existence. The reset grant lives in memory only: it never enters a URL,
 * a console log or analytics.
 */
export default function RecoverAccountScreen() {
  const router = useRouter();
  const [step, setStep] = useState<RecoveryStepId>('request');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [policy, setPolicy] = useState<RecoveryPolicy | null>(null);
  const [cooldown, setCooldown] = useState(0);
  const [resetGrant, setResetGrant] = useState('');
  const identifier = useRef('');
  const inFlight = useRef(false);

  useEffect(() => {
    // The shared S02 policy is fetched once; its hints appear at the password step. Failing to load
    // them is not an error: the API answer (MSG109) stays the authority.
    void fetchRecoveryPolicy()
      .then(setPolicy)
      .catch(() => undefined);
  }, []);

  useEffect(() => {
    if (cooldown <= 0) return undefined;
    const timer = setInterval(() => setCooldown(value => (value <= 1 ? 0 : value - 1)), 1000);
    return () => clearInterval(timer);
  }, [cooldown]);

  // A verification link sent through the Link channel opens this screen with the recovery id.
  useEffect(() => {
    void Linking.getInitialURL().then(url => {
      if (!url) return;
      const marker = '/recover-account?';
      if (!url.includes(marker)) return;
      const query = url.slice(url.indexOf(marker) + marker.length);
      const recoveryId = new URLSearchParams(query).get('id');
      // The link only carries the opaque recovery id; the proof still has to be typed in.
      if (recoveryId) setStep('verify');
    });
  }, []);

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

  async function submitRequest(value: string) {
    const errors = validateAccount(value, 254);
    if (!errors.identifier && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim()))
      errors.identifier = 'Email không đúng định dạng.';
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }
    setFieldErrors({});
    setError(null);
    await run(async () => {
      try {
        const result = await requestRecovery(value.trim());
        identifier.current = value.trim();
        setCooldown(RESEND_COOLDOWN_SECONDS);
        setStep('verify');
        // Neutral by design: identical text for every valid-format identifier.
        setNotice(result.message);
      } catch (failure) {
        setError(messageOf(failure));
      }
    });
  }

  async function submitVerify(value: string) {
    const errors = validateProof(value);
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }
    setFieldErrors({});
    setError(null);
    await run(async () => {
      try {
        const grant = await verifyRecovery(identifier.current, value.trim());
        setResetGrant(grant);
        setStep('reset');
      } catch (failure) {
        setError(messageOf(failure));
      }
    });
  }

  async function submitReset(newPassword: string, confirmPassword: string) {
    const errors = validatePasswordPair(newPassword, confirmPassword);
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }
    setFieldErrors({});
    setError(null);
    await run(async () => {
      try {
        const result = await resetPassword(identifier.current, resetGrant, newPassword, confirmPassword);
        // Drop the grant as soon as it is consumed; it is single-use on the server too.
        setResetGrant('');
        setNotice(null);
        setError(null);
        setStep('done');
        setSuccess(result.message);
      } catch (failure) {
        setError(messageOf(failure));
      }
    });
  }

  async function resend() {
    if (busy || cooldown > 0) return;
    setError(null);
    await run(async () => {
      try {
        // The answer is the same neutral message; the cooldown restarts either way so a user cannot
        // hammer the endpoint when the account does not exist.
        setNotice(recoveryMessages.MSG12);
        await resendRecovery(identifier.current);
        setCooldown(RESEND_COOLDOWN_SECONDS);
      } catch (failure) {
        setError(messageOf(failure));
      }
    });
  }

  function startOver() {
    setStep('request');
    setError(null);
    setNotice(null);
    setSuccess(null);
    setFieldErrors({});
    setResetGrant('');
    setCooldown(0);
    identifier.current = '';
  }

  const [identifierValue, setIdentifierValue] = useState('');
  const [proofValue, setProofValue] = useState('');
  const [passwordValue, setPasswordValue] = useState('');
  const [confirmValue, setConfirmValue] = useState('');
  const currentIndex = stepOrder.indexOf(step);

  return (
    <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
      <View style={styles.header}>
        <Text style={styles.eyebrow}>DCMS · Phòng khám nha khoa</Text>
        <Text style={styles.title}>Khôi phục tài khoản</Text>
        <View style={styles.steps}>
          {stepOrder.map((item, index) => (
            <View
              key={item}
              style={[
                styles.step,
                index < currentIndex && styles.stepDone,
                index === currentIndex && styles.stepCurrent,
              ]}
              accessibilityRole="progressbar"
              accessibilityLabel={`Bước ${index + 1}: ${stepNames[item]}`}
            />
          ))}
        </View>
        <Text style={styles.stepLabel}>{stepNames[step]}</Text>
      </View>

      {error ? (
        <View style={[styles.callout, styles.error]} accessibilityLiveRegion="polite">
          <Text style={styles.errorText}>{error}</Text>
        </View>
      ) : null}
      {notice && step !== 'request' ? (
        <View style={[styles.callout, styles.neutral]} accessibilityLiveRegion="polite">
          <Text style={styles.neutralText}>{notice}</Text>
        </View>
      ) : null}

      <View style={styles.card}>
        {step === 'request' ? (
          <View style={styles.form}>
            <Text style={styles.hint}>
              Nhập email đã đăng ký của tài khoản khách hàng để nhận mã đặt lại mật khẩu.
            </Text>
            <LabelledField
              label="Email"
              value={identifierValue}
              onChange={setIdentifierValue}
              error={fieldErrors.identifier}
              editable={!busy}
              autoFocus
              autoCapitalize="none"
              autoCorrect={false}
              returnKeyType="go"
              onSubmitEditing={() => void submitRequest(identifierValue)}
              maxLength={254}
            />
            <PrimaryButton
              label="Gửi yêu cầu"
              busy={busy}
              disabled={busy}
              onPress={() => void submitRequest(identifierValue)}
            />
            <Text style={styles.hint}>Nếu email có tài khoản phù hợp, mã sẽ được gửi tới hộp thư này.</Text>
          </View>
        ) : null}

        {step === 'verify' ? (
          <View style={styles.form}>
            <Text style={styles.hint}>
              Nhập mã trong email nếu địa chỉ này đủ điều kiện khôi phục.
            </Text>
            <LabelledField
              label={recoveryFields.proof}
              value={proofValue}
              onChange={setProofValue}
              error={fieldErrors.proof}
              editable={!busy}
              autoFocus
              maxLength={24}
              keyboardType="number-pad"
              textContentType="oneTimeCode"
              returnKeyType="go"
              onSubmitEditing={() => void submitVerify(proofValue)}
            />
            <View style={styles.resendRow}>
              <Pressable
                onPress={() => void resend()}
                disabled={busy || cooldown > 0}
                accessibilityRole="button"
                accessibilityState={{ disabled: busy || cooldown > 0 }}
              >
                <Text style={[styles.link, (busy || cooldown > 0) && styles.linkDisabled]}>
                  {cooldown > 0 ? `Gửi lại sau ${cooldown}s` : 'Gửi lại mã'}
                </Text>
              </Pressable>
              <Pressable onPress={startOver} disabled={busy} accessibilityRole="button">
                <Text style={[styles.link, busy && styles.linkDisabled]}>Nhập tài khoản khác</Text>
              </Pressable>
            </View>
            <PrimaryButton
              label="Xác minh"
              busy={busy}
              disabled={busy}
              onPress={() => void submitVerify(proofValue)}
            />
          </View>
        ) : null}

        {step === 'reset' ? (
          <View style={styles.form}>
            <Text style={styles.hint}>Nhập mật khẩu mới cho tài khoản của bạn.</Text>
            {hints.length > 0 ? (
              <View style={styles.policy}>
                {hints.map(hint => (
                  <Text key={hint} style={styles.policyItem}>
                    • {hint}
                  </Text>
                ))}
              </View>
            ) : null}
            <LabelledField
              label={recoveryFields.newPassword}
              value={passwordValue}
              onChange={setPasswordValue}
              error={fieldErrors.newPassword}
              editable={!busy}
              secure
              autoFocus
              autoCapitalize="none"
              textContentType="newPassword"
              maxLength={256}
            />
            <LabelledField
              label={recoveryFields.confirmPassword}
              value={confirmValue}
              onChange={setConfirmValue}
              error={fieldErrors.confirmPassword}
              editable={!busy}
              secure
              autoCapitalize="none"
              textContentType="newPassword"
              maxLength={256}
              returnKeyType="go"
              onSubmitEditing={() => void submitReset(passwordValue, confirmValue)}
            />
            <PrimaryButton
              label="Đặt lại mật khẩu"
              busy={busy}
              disabled={busy}
              onPress={() => void submitReset(passwordValue, confirmValue)}
            />
          </View>
        ) : null}

        {step === 'done' ? (
          <View style={styles.form}>
            <View style={[styles.callout, styles.neutral]} accessibilityLiveRegion="polite">
              <Text style={styles.neutralText}>{success ?? recoveryMessages.MSG13}</Text>
            </View>
            <Text style={styles.hint}>
              Các phiên đăng nhập trước đã bị thu hồi. Hãy đăng nhập lại bằng mật khẩu mới.
            </Text>
            <PrimaryButton
              label="Về trang đăng nhập"
              busy={false}
              disabled={false}
              onPress={() => router.replace('/(auth)/sign-in')}
            />
            <Pressable onPress={startOver} accessibilityRole="button">
              <Text style={styles.link}>Khôi phục tài khoản khác</Text>
            </Pressable>
          </View>
        ) : null}
      </View>

      <Pressable onPress={() => router.replace('/(auth)/sign-in')} accessibilityRole="link">
        <Text style={styles.footerLink}>Quay lại đăng nhập</Text>
      </Pressable>
    </ScrollView>
  );
}

function messageOf(failure: unknown): string {
  // ApiError already carries the server text (MSG108 / MSG109 / MSG144). Anything else is a
  // transport failure and is reported with the connection message.
  if (failure instanceof ApiError) return failure.message;
  return recoveryMessages.MSG144;
}

function LabelledField({
  label,
  value,
  onChange,
  error,
  editable = true,
  secure = false,
  autoFocus = false,
  maxLength,
  keyboardType,
  autoCapitalize,
  autoCorrect,
  textContentType,
  returnKeyType,
  onSubmitEditing,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  error?: string;
  editable?: boolean;
  secure?: boolean;
  autoFocus?: boolean;
  maxLength?: number;
  keyboardType?: 'default' | 'number-pad';
  autoCapitalize?: 'none' | 'sentences';
  autoCorrect?: boolean;
  textContentType?: 'oneTimeCode' | 'newPassword';
  returnKeyType?: 'go' | 'done';
  onSubmitEditing?: () => void;
}) {
  return (
    <View style={styles.field}>
      <Text style={styles.label}>{label}</Text>
      <TextInput
        style={[styles.input, error ? styles.inputError : null, !editable && styles.inputDisabled]}
        value={value}
        onChangeText={onChange}
        editable={editable}
        secureTextEntry={secure}
        autoFocus={autoFocus}
        maxLength={maxLength}
        keyboardType={keyboardType}
        autoCapitalize={autoCapitalize}
        autoCorrect={autoCorrect}
        textContentType={textContentType}
        returnKeyType={returnKeyType}
        onSubmitEditing={onSubmitEditing}
        accessibilityLabel={label}
        placeholderTextColor={colors.muted}
      />
      {error ? <Text style={styles.fieldError}>{error}</Text> : null}
    </View>
  );
}

function PrimaryButton({
  label,
  onPress,
  busy,
  disabled,
}: {
  label: string;
  onPress: () => void;
  busy: boolean;
  disabled: boolean;
}) {
  return (
    <Pressable
      style={[styles.button, disabled && styles.buttonDisabled]}
      onPress={onPress}
      disabled={disabled}
      accessibilityRole="button"
      accessibilityState={{ disabled, busy }}
    >
      {busy ? <ActivityIndicator color="#FFFFFF" size="small" /> : null}
      <Text style={styles.buttonLabel}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  container: { padding: spacing.lg, gap: spacing.md, backgroundColor: colors.background, flexGrow: 1 },
  header: { gap: spacing.sm, marginTop: spacing.md },
  eyebrow: { color: colors.primary, fontSize: typography.caption, fontWeight: '700', letterSpacing: 1 },
  title: { fontSize: typography.heading, fontWeight: '700', color: colors.text },
  steps: { flexDirection: 'row', gap: spacing.xs, marginTop: spacing.sm },
  step: { flex: 1, height: 4, borderRadius: 999, backgroundColor: '#D9E2EC' },
  stepDone: { backgroundColor: colors.primary },
  stepCurrent: { backgroundColor: colors.primary, opacity: 0.55 },
  stepLabel: { fontSize: typography.caption, color: colors.muted, marginTop: spacing.xs },
  card: {
    backgroundColor: '#FFFFFF',
    borderRadius: 14,
    borderWidth: 1,
    borderColor: '#E4E9F2',
    padding: spacing.md,
    gap: spacing.md,
  },
  form: { gap: spacing.md },
  hint: { fontSize: 14, color: colors.muted, lineHeight: 21 },
  field: { gap: spacing.xs },
  label: { fontSize: 14, fontWeight: '600', color: colors.text },
  input: {
    borderWidth: 1,
    borderColor: '#C7D2E0',
    borderRadius: 10,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    fontSize: typography.body,
    color: colors.text,
    backgroundColor: '#FFFFFF',
  },
  inputError: { borderColor: '#D64545' },
  inputDisabled: { backgroundColor: '#F5F7FA' },
  fieldError: { fontSize: 13, color: '#D64545' },
  policy: { gap: spacing.xs },
  policyItem: { fontSize: 13, color: colors.muted },
  button: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: spacing.sm,
    backgroundColor: colors.primary,
    borderRadius: 10,
    paddingVertical: spacing.md,
  },
  buttonDisabled: { opacity: 0.6 },
  buttonLabel: { color: '#FFFFFF', fontSize: typography.body, fontWeight: '600' },
  resendRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  link: { color: colors.primary, fontSize: 14, paddingVertical: spacing.xs },
  linkDisabled: { color: colors.muted },
  callout: { borderRadius: 10, padding: spacing.md },
  neutral: { backgroundColor: '#EAF2FE', borderWidth: 1, borderColor: '#B6D4FE' },
  neutralText: { color: '#0B3A86', fontSize: 14, lineHeight: 21 },
  error: { backgroundColor: '#FDECEC', borderWidth: 1, borderColor: '#F3B5B5' },
  errorText: { color: '#A11B1B', fontSize: 14, lineHeight: 21 },
  doneTitle: { fontSize: 18, fontWeight: '700', color: colors.text },
  footerLink: { color: colors.primary, fontSize: 14, textAlign: 'center', paddingVertical: spacing.md },
});
