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
 *  - Thành công -> router.replace('/(account)/account-link') (hoặc '/(account)' tuỳ đặc tả).
 *
 * Lưu ý cảnh báo thay đổi chưa lưu: SignInScreen không có form dirty state nên không cần chặn
 * sign-out. PatientData, v.v. sẽ dùng useUnsavedChanges() riêng.
 */

import { Link, useRouter } from 'expo-router';
import { useCallback, useMemo, useState } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { FormError } from '@/components/feedback/FormError';
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

  const validateField = useCallback((field: 'userName' | 'password', value: string) => {
    const err = validateSignInField(field, value);
    setFieldErrors((prev) => ({ ...prev, [field]: err?.message ?? null }));
  }, []);

  const handleSignIn = useCallback(
    async (snapshot: SessionSnapshot) => {
      await signIn(snapshot);
      router.replace('/(account)/account-link' as never);
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
          <View style={styles.header}>
            <Text style={styles.title}>Đăng nhập</Text>
            <Text style={styles.subtitle}>
              Dùng tài khoản DCMS đã được cấp để truy cập hệ thống.
            </Text>
          </View>

          <FormError message={generalError} />

          <View style={styles.form}>
            <TextField
              label="Tên đăng nhập"
              value={userName}
              onChangeText={setUserName}
              onBlur={() => {
                setTouched((p) => ({ ...p, userName: true }));
                validateField('userName', userName);
              }}
              placeholder="vd: admin.demo"
              autoCapitalize="none"
              autoCorrect={false}
              textContentType="username"
              keyboardType="default"
              error={touched.userName ? fieldErrors.userName : null}
              hint="3–64 ký tự, gồm chữ, số và . _ -"
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
              hint="Ít nhất 12 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt."
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
                  <Text style={styles.linkText}>Tạo tài khoản (S02)</Text>
                </Pressable>
              </Link>
              <Link href="/(auth)/recover-account" asChild>
                <Pressable hitSlop={8}>
                  <Text style={styles.linkText}>Quên mật khẩu? (S03)</Text>
                </Pressable>
              </Link>
            </View>
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  flex: { flex: 1 },
  container: { padding: spacing.lg, gap: spacing.lg, flexGrow: 1 },
  header: { gap: spacing.xs, marginTop: spacing.md },
  title: { fontSize: typography.heading, fontWeight: '700', color: colors.text },
  subtitle: { color: colors.muted, fontSize: typography.body - 2 },
  form: { gap: spacing.md },
  actions: { marginTop: spacing.sm },
  linksRow: { flexDirection: 'row', justifyContent: 'space-between', flexWrap: 'wrap', gap: spacing.sm, marginTop: spacing.sm },
  linkText: { color: colors.primary, fontWeight: '600' },
});
