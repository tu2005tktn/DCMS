/**
 * S01 — Sign In screen.
 *
 * Tích hợp API thật /api/auth/sign-in qua useSignIn. Theo đặc tả S01:
 *  - Form: username + password (đặc tả gốc ghi "liên hệ đã xác minh" nhưng backend UC-01 chỉ
 *    nhận username; xem ghi chú trong sign-in.api.ts).
 *  - Hiện/ẩn mật khẩu: PasswordField.
 *  - Liên kết S02 (đăng ký) + S03 (khôi phục) — chỉ là link navigation, S02/S03 ngoài scope.
 *  - Lỗi sai hiển thị thông báo chung (MSG09), không phân biệt username hay password.
 *  - Loading: chặn submit, hiển thị FormError khi lỗi mạng.
 *  - Thành công -> về menu trung tâm (/(home)); "Trang chủ tài khoản" tự xuất hiện.
 *
 * Lưu ý cảnh báo thay đổi chưa lưu: SignInScreen không có form dirty state nên không cần chặn
 * sign-out. PatientData, v.v. sẽ dùng useUnsavedChanges() riêng.
 */

import { Link, useRouter } from 'expo-router';
import { useCallback, useMemo, useState } from 'react';
import { Image, KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { FormError } from '@/components/feedback/FormError';
import { ApiError } from '@/services/api/api-error';
import { GoogleIdentityButton } from '@/features/auth/google/GoogleIdentityButton';
import { googleAuthApi } from '@/features/auth/google/google-auth.api';
import { signInApi } from '@/features/auth/sign-in/sign-in.api';
import { PasswordField } from '@/components/forms/PasswordField';
import { TextField } from '@/components/forms/TextField';
import { PrimaryButton } from '@/components/ui/PrimaryButton';
import { useSession } from '@/features/auth/session/useSession';
import { useSignIn } from '@/features/auth/sign-in/hooks/useSignIn';
import { validateSignInField } from '@/features/auth/sign-in/sign-in.schema';
import type { SessionSnapshot } from '@/features/auth/sign-in/sign-in.types';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { typography } from '@/theme/typography';

interface FieldError {
  userName: string | null;
  password: string | null;
}

export default function SignInScreen() {
  const router = useRouter();
  const { signIn } = useSession();

  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [touched, setTouched] = useState<{ userName: boolean; password: boolean }>({ userName: false, password: false });
  const [fieldErrors, setFieldErrors] = useState<FieldError>({ userName: null, password: null });
  const [googleError, setGoogleError] = useState<string | null>(null);

  const validateField = useCallback((field: 'userName' | 'password', value: string) => {
    const err = validateSignInField(field, value);
    setFieldErrors((prev) => ({ ...prev, [field]: err?.message ?? null }));
  }, []);

  const handleSignIn = useCallback(
    async (snapshot: SessionSnapshot) => {
      await signIn(snapshot);
      // Trang chủ tự hiển thị nội dung phù hợp với vai trò sau khi đăng nhập.
      router.replace('/(home)/' as never);
    },
    [signIn, router],
  );

  const { feedback, submit } = useSignIn({ onSuccess: handleSignIn });

  const isSubmitting = feedback.kind === 'submitting';

  const onSubmit = useCallback(async () => {
    setTouched({ userName: true, password: true });
    const u = validateSignInField('userName', userName);
    const p = validateSignInField('password', password);
    setFieldErrors({ userName: u?.message ?? null, password: p?.message ?? null });
    if (u || p) return;
    await submit({ userName, password });
  }, [userName, password, submit]);

  const onGoogleCredential = useCallback(async (idToken: string) => {
    setGoogleError(null);
    try {
      const response = await googleAuthApi.signIn(idToken);
      await handleSignIn(signInApi.buildSnapshot(response));
    } catch (error) {
      setGoogleError(error instanceof ApiError ? error.message : 'Không thể kết nối máy chủ. Vui lòng thử lại.');
    }
  }, [handleSignIn]);

  const generalError = useMemo(() => {
    if (feedback.kind !== 'error') return null;
    if (feedback.code === 'NETWORK') return feedback.message;
    return feedback.message;
  }, [feedback]);

  return (
    <SafeAreaView style={styles.safe} edges={['top', 'bottom']}>
      <KeyboardAvoidingView
        style={styles.flex}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      >
        <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
          <View style={styles.hero}>
            <Link href="/(home)/" style={styles.backLink}>← Trang chủ</Link>
            <View style={styles.heroMark}><Image source={require('../../../../../assets/dcms-app-icon.png')} style={styles.logo} />
              <Text style={styles.brand}>DCMS</Text></View>
            <Text style={styles.title}>Chào mừng trở lại</Text>
            <Text style={styles.subtitle}>Đăng nhập để tiếp tục chăm sóc nụ cười của bạn.</Text>
          </View>

          <View style={styles.form}>
            <Text style={styles.formTitle}>Đăng nhập</Text>
            <FormError message={generalError} />
            <TextField
              label="Số điện thoại hoặc tên đăng nhập"
              value={userName}
              onChangeText={setUserName}
              onBlur={() => {
                setTouched((p) => ({ ...p, userName: true }));
                validateField('userName', userName);
              }}
              placeholder="Ví dụ: 0912345678"
              autoCapitalize="none"
              autoCorrect={false}
              textContentType="username"
              keyboardType="default"
              error={touched.userName ? fieldErrors.userName : null}
              disabled={isSubmitting}
              testID="sign-in-username"
            />

            <PasswordField
              label="Mật khẩu"
              value={password}
              onChangeText={setPassword}
              onBlur={() => {
                setTouched((p) => ({ ...p, password: true }));
                validateField('password', password);
              }}
              error={touched.password ? fieldErrors.password : null}
              disabled={isSubmitting}
              testID="sign-in-password"
            />

            <View style={styles.actions}>
              <PrimaryButton
                label={isSubmitting ? 'Đang đăng nhập…' : 'Đăng nhập'}
                onPress={onSubmit}
                loading={isSubmitting}
                disabled={isSubmitting}
                testID="sign-in-submit"
              />
            </View>

            <View style={styles.linksRow}>
              <Link href="/(auth)/register" asChild>
                <Pressable hitSlop={8}>
                  <Text style={styles.linkText}>Tạo tài khoản</Text>
                </Pressable>
              </Link>
              <Link href="/(auth)/recover-account" asChild>
                <Pressable hitSlop={8}>
                  <Text style={styles.linkText}>Quên mật khẩu?</Text>
                </Pressable>
              </Link>
            </View>

            <View style={styles.googleSection}>
              <Text style={styles.orText}>Hoặc</Text>
              <GoogleIdentityButton onCredential={onGoogleCredential} onError={setGoogleError} />
              <FormError message={googleError} />
            </View>
            <Link href="/book-appointment" asChild>
              <Pressable accessibilityRole="button" style={styles.guestLink}><Text style={styles.linkText}>Đặt lịch không cần tài khoản →</Text></Pressable>
            </Link>
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#F3F7FD' },
  flex: { flex: 1 },
  container: { padding: spacing.lg, paddingTop: 0, gap: 0, flexGrow: 1, justifyContent: 'center' },
  hero: { backgroundColor: '#175CD3', marginHorizontal: -spacing.lg, paddingHorizontal: spacing.lg,
    paddingTop: 30, paddingBottom: 72, borderBottomLeftRadius: 34, borderBottomRightRadius: 34 },
  backLink: { color: '#DCEAFF', fontWeight: '700', marginBottom: 36 },
  heroMark: { flexDirection: 'row', alignItems: 'center', gap: 12, marginBottom: 28 },
  logo: { width: 54, height: 54, borderRadius: 14 },
  brand: { color: '#FFFFFF', fontSize: 25, fontWeight: '800', letterSpacing: 1 },
  title: { fontSize: 31, fontWeight: '800', color: '#FFFFFF', lineHeight: 38 },
  subtitle: { color: '#DCEAFF', fontSize: typography.body, lineHeight: 23, marginTop: 8 },
  form: { gap: spacing.md, padding: spacing.lg, borderRadius: 24, backgroundColor: '#FFFFFF',
    borderWidth: 1, borderColor: '#DCE6F3', marginTop: -42, shadowColor: '#15355E', shadowOpacity: 0.12,
    shadowRadius: 18, shadowOffset: { width: 0, height: 8 }, elevation: 5 },
  formTitle: { color: colors.text, fontSize: 22, fontWeight: '800', marginBottom: 4 },
  actions: { marginTop: spacing.sm },
  linksRow: { flexDirection: 'row', justifyContent: 'space-between', flexWrap: 'wrap', gap: spacing.sm, marginTop: spacing.sm },
  linkText: { color: colors.primary, fontWeight: '700' },
  guestLink: { minHeight: 44, justifyContent: 'center', alignItems: 'center' },
  googleSection: { gap: spacing.sm, marginTop: spacing.md, alignItems: 'center' },
  orText: { color: colors.muted, textAlign: 'center' },
});
