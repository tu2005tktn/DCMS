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
            <View style={styles.heroCircleLarge} />
            <View style={styles.heroCircleSmall} />
            <View style={styles.heroMark}>
              <Image source={require('../../../../../assets/dcms-app-icon.png')} style={styles.logo} />
              <View><Text style={styles.brand}>DCMS</Text><Text style={styles.brandCaption}>NHA KHOA CỦA BẠN</Text></View>
            </View>
            <Text style={styles.title}>Chào mừng trở lại</Text>
            <Text style={styles.subtitle}>Chăm sóc nụ cười, bắt đầu từ đây.</Text>
          </View>

          <View style={styles.form}>
            <Text style={styles.formTitle}>Đăng nhập</Text>
            <Text style={styles.formSubtitle}>Nhập thông tin tài khoản để tiếp tục</Text>
            <FormError message={generalError} />
            <TextField
              label="Số điện thoại hoặc tên đăng nhập"
              value={userName}
              onChangeText={setUserName}
              onBlur={() => {
                setTouched((p) => ({ ...p, userName: true }));
                validateField('userName', userName);
              }}
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

            <Link href="/(auth)/recover-account" asChild>
              <Pressable hitSlop={10} style={styles.forgotLink}>
                <Text style={styles.linkText}>Quên mật khẩu?</Text>
              </Pressable>
            </Link>

            <View style={styles.actions}>
              <PrimaryButton
                label={isSubmitting ? 'Đang đăng nhập…' : 'Đăng nhập'}
                onPress={onSubmit}
                loading={isSubmitting}
                disabled={isSubmitting}
                testID="sign-in-submit"
              />
            </View>

            <View style={styles.googleSection}>
              <View style={styles.separator}><View style={styles.separatorLine} /><Text style={styles.orText}>hoặc tiếp tục bằng</Text><View style={styles.separatorLine} /></View>
              <GoogleIdentityButton onCredential={onGoogleCredential} onError={setGoogleError} />
              <FormError message={googleError} />
            </View>
          </View>

          <View style={styles.registerRow}>
            <Text style={styles.registerPrompt}>Chưa có tài khoản? </Text>
            <Link href="/(auth)/register" asChild>
              <Pressable hitSlop={10}><Text style={styles.registerLink}>Đăng ký ngay</Text></Pressable>
            </Link>
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#F5F8FC' },
  flex: { flex: 1 },
  container: { flexGrow: 1, paddingBottom: spacing.xl, justifyContent: 'center' },
  hero: { minHeight: 255, overflow: 'hidden', backgroundColor: '#123F91', paddingHorizontal: spacing.lg,
    paddingTop: spacing.xl, paddingBottom: 56, borderBottomLeftRadius: 32, borderBottomRightRadius: 32 },
  heroCircleLarge: { position: 'absolute', width: 260, height: 260, borderRadius: 130, borderWidth: 42,
    borderColor: 'rgba(255,255,255,0.07)', right: -100, top: -95 },
  heroCircleSmall: { position: 'absolute', width: 120, height: 120, borderRadius: 60,
    backgroundColor: 'rgba(255,255,255,0.06)', right: -42, bottom: -55 },
  heroMark: { flexDirection: 'row', alignItems: 'center', gap: 12, marginBottom: spacing.xl },
  logo: { width: 46, height: 46, borderRadius: 12 },
  brand: { color: '#FFFFFF', fontSize: 22, fontWeight: '800', letterSpacing: 0.5 },
  brandCaption: { color: '#BFD7FF', fontSize: 10, fontWeight: '700', letterSpacing: 1.5 },
  title: { fontSize: 29, fontWeight: '800', color: '#FFFFFF', lineHeight: 36 },
  subtitle: { color: '#D9E8FF', fontSize: typography.body - 1, lineHeight: 22, marginTop: 6 },
  form: { gap: spacing.md, marginHorizontal: spacing.lg, marginTop: -30, padding: spacing.lg,
    borderRadius: 24, backgroundColor: '#FFFFFF', borderWidth: 1, borderColor: '#E7EDF6',
    shadowColor: '#18395E', shadowOpacity: 0.08, shadowRadius: 16,
    shadowOffset: { width: 0, height: 6 }, elevation: 4 },
  formTitle: { color: colors.text, fontSize: 22, fontWeight: '800' },
  formSubtitle: { color: colors.muted, fontSize: 13, marginTop: -spacing.sm, marginBottom: spacing.xs },
  forgotLink: { alignSelf: 'flex-end', minHeight: 36, justifyContent: 'center', marginTop: -spacing.sm },
  actions: { marginTop: 0 },
  linkText: { color: colors.primary, fontWeight: '700', fontSize: 13 },
  googleSection: { gap: spacing.md, marginTop: spacing.sm, alignItems: 'center' },
  separator: { width: '100%', flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  separatorLine: { flex: 1, height: 1, backgroundColor: colors.border },
  orText: { color: colors.muted, fontSize: 12 },
  registerRow: { flexDirection: 'row', justifyContent: 'center', alignItems: 'center', marginTop: spacing.lg },
  registerPrompt: { color: colors.muted, fontSize: 14 },
  registerLink: { color: colors.primary, fontSize: 14, fontWeight: '800' },
});
