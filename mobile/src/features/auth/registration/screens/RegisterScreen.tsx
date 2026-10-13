import { Link, useRouter } from 'expo-router';
import { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { ApiError } from '@/services/api/api-error';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { getCredentialPolicy, registerAccount } from '../registration.api';
import { validateRegistration } from '../registration.schema';
import type { CredentialPolicy, RegistrationErrors, RegistrationField, RegistrationForm } from '../registration.types';

const initialForm: RegistrationForm = { fullName: '', phoneNumber: '', email: '', password: '', confirmPassword: '' };

export default function RegisterScreen() {
  const router = useRouter();
  const submittingRef = useRef(false);
  const [form, setForm] = useState<RegistrationForm>(initialForm);
  const [errors, setErrors] = useState<RegistrationErrors>({});
  const [formError, setFormError] = useState('');
  const [policy, setPolicy] = useState<CredentialPolicy>();
  const [submitting, setSubmitting] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmation, setShowConfirmation] = useState(false);
  const [success, setSuccess] = useState('');

  useEffect(() => {
    let mounted = true;
    void getCredentialPolicy().then(value => { if (mounted) setPolicy(value); }).catch(() => {
      // The server still enforces its policy when the form is submitted.
    });
    return () => { mounted = false; };
  }, []);

  useEffect(() => {
    if (!success) return;
    const timer = setTimeout(() => router.replace('/(auth)/sign-in'), 1500);
    return () => clearTimeout(timer);
  }, [router, success]);

  function update(field: RegistrationField, value: string) {
    setForm(current => ({ ...current, [field]: value }));
    setErrors(current => ({ ...current, [field]: undefined }));
    setFormError('');
  }

  async function submit() {
    if (submittingRef.current) return;
    const nextErrors = validateRegistration(form, policy);
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length) return;
    submittingRef.current = true;
    setSubmitting(true);
    setFormError('');
    try {
      const result = await registerAccount(form);
      setSuccess(result.notification.message);
    } catch (error) {
      if (error instanceof ApiError) {
        if (error.code === 'MSG139') setErrors(current => ({ ...current, phoneNumber: error.message }));
        else if (error.code === 'MSG109') setErrors(current => ({ ...current, password: error.message }));
        else if (error.message.includes('Xác nhận mật khẩu')) {
          setErrors(current => ({ ...current, confirmPassword: error.message }));
        } else if (error.message.includes('Số điện thoại')) {
          setErrors(current => ({ ...current, phoneNumber: error.message }));
        } else if (error.message.includes('Email')) {
          setErrors(current => ({ ...current, email: error.message }));
        } else if (error.message.includes('Họ tên')) {
          setErrors(current => ({ ...current, fullName: error.message }));
        } else setFormError(error.message);
      } else setFormError('Không thể kết nối máy chủ. Vui lòng thử lại.');
    } finally {
      submittingRef.current = false;
      setSubmitting(false);
    }
  }

  return <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={styles.container}>
    <Text style={styles.heading}>Tạo tài khoản</Text>
    <Text style={styles.description}>Đăng ký tài khoản Guest bằng số di động Việt Nam. Số này sẽ dùng để đăng nhập.</Text>

    <View style={styles.field}>
      <Text style={styles.label}>Họ tên</Text>
      <TextInput accessibilityLabel="Họ tên" value={form.fullName} onChangeText={value => update('fullName', value)}
        autoCapitalize="words" autoComplete="name" placeholder="Nguyễn Văn A" style={styles.input} editable={!submitting} />
      {errors.fullName ? <Text style={styles.error}>{errors.fullName}</Text> : null}
    </View>

    <View style={styles.field}>
      <Text style={styles.label}>Số điện thoại</Text>
      <TextInput accessibilityLabel="Số điện thoại" value={form.phoneNumber} onChangeText={value => update('phoneNumber', value)}
        keyboardType="phone-pad" autoComplete="tel" placeholder="0912345678" style={styles.input} editable={!submitting} />
      {errors.phoneNumber ? <Text style={styles.error}>{errors.phoneNumber}</Text> : null}
    </View>

    <View style={styles.field}>
      <Text style={styles.label}>Email</Text>
      <TextInput accessibilityLabel="Email" value={form.email} onChangeText={value => update('email', value)}
        keyboardType="email-address" autoCapitalize="none" autoComplete="email" placeholder="ten@example.com"
        style={styles.input} editable={!submitting} />
      {errors.email ? <Text style={styles.error}>{errors.email}</Text> : null}
    </View>

    <View style={styles.field}>
      <Text style={styles.label}>Mật khẩu</Text>
      <View style={styles.passwordRow}>
        <TextInput accessibilityLabel="Mật khẩu" value={form.password} onChangeText={value => update('password', value)}
          secureTextEntry={!showPassword} autoComplete="new-password" placeholder="Nhập mật khẩu"
          style={[styles.input, styles.passwordInput]} editable={!submitting} />
        <Pressable accessibilityRole="button" accessibilityLabel={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
          onPress={() => setShowPassword(value => !value)} style={styles.visibilityButton}>
          <Text style={styles.link}>{showPassword ? 'Ẩn' : 'Hiện'}</Text>
        </Pressable>
      </View>
      {errors.password ? <Text style={styles.error}>{errors.password}</Text> : null}
    </View>

    <View style={styles.field}>
      <Text style={styles.label}>Xác nhận mật khẩu</Text>
      <View style={styles.passwordRow}>
        <TextInput accessibilityLabel="Xác nhận mật khẩu" value={form.confirmPassword}
          onChangeText={value => update('confirmPassword', value)} secureTextEntry={!showConfirmation}
          autoComplete="new-password" placeholder="Nhập lại mật khẩu"
          style={[styles.input, styles.passwordInput]} editable={!submitting} />
        <Pressable accessibilityRole="button" accessibilityLabel={showConfirmation ? 'Ẩn xác nhận mật khẩu' : 'Hiện xác nhận mật khẩu'}
          onPress={() => setShowConfirmation(value => !value)} style={styles.visibilityButton}>
          <Text style={styles.link}>{showConfirmation ? 'Ẩn' : 'Hiện'}</Text>
        </Pressable>
      </View>
      {errors.confirmPassword ? <Text style={styles.error}>{errors.confirmPassword}</Text> : null}
    </View>

    {policy ? (
      <Text style={styles.note}>
        {`Mật khẩu tối thiểu ${policy.requiredLength} ký tự${policy.requireUppercase ? ', có chữ hoa' : ''}${policy.requireLowercase ? ', chữ thường' : ''}${policy.requireDigit ? ', số' : ''}${policy.requireNonAlphanumeric ? ' và ký tự đặc biệt' : ''}.`}
      </Text>
    ) : null}
    {formError ? <Text style={styles.error}>{formError}</Text> : null}
    {success ? <Text accessibilityRole="alert" style={styles.success}>{`${success} Đang chuyển tới đăng nhập...`}</Text> : null}

    <Pressable accessibilityRole="button" disabled={submitting || !!success} onPress={() => void submit()}
      style={[styles.submitButton, (submitting || !!success) && styles.disabledButton]}>
      {submitting ? <ActivityIndicator color={colors.background} /> : <Text style={styles.submitText}>Đăng ký</Text>}
    </Pressable>
    <View style={styles.footerRow}>
      <Text style={styles.note}>Đã có tài khoản?</Text>
      <Link href="/(auth)/sign-in" style={styles.link}>Đăng nhập</Link>
    </View>
  </ScrollView>;
}

const styles = StyleSheet.create({
  container: { flexGrow: 1, padding: spacing.lg, gap: spacing.md, backgroundColor: colors.background },
  heading: { fontSize: 24, fontWeight: '700', color: colors.text },
  description: { color: colors.muted, fontSize: 16, lineHeight: 24 },
  field: { gap: spacing.xs },
  label: { color: colors.text, fontWeight: '600', fontSize: 16 },
  input: { minHeight: 48, borderWidth: 1, borderColor: '#A9B5C2', borderRadius: 8,
    paddingHorizontal: spacing.md, backgroundColor: colors.background, color: colors.text, fontSize: 16 },
  passwordRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  passwordInput: { flex: 1 },
  visibilityButton: { minHeight: 48, justifyContent: 'center', paddingHorizontal: spacing.sm },
  link: { color: colors.primary, fontWeight: '600' },
  note: { color: colors.muted, lineHeight: 20 },
  error: { color: '#B42318', lineHeight: 20 },
  success: { color: '#067647', fontWeight: '600', lineHeight: 22 },
  submitButton: { minHeight: 48, borderRadius: 8, alignItems: 'center', justifyContent: 'center',
    backgroundColor: colors.primary, marginTop: spacing.sm },
  disabledButton: { opacity: 0.65 },
  submitText: { color: colors.background, fontWeight: '700', fontSize: 16 },
  footerRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.xs },
});
