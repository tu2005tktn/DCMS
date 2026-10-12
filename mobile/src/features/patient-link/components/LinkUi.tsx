import type { PropsWithChildren } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, Text, TextInput, View, type TextInputProps } from 'react-native';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import type { LinkStatus } from '../patient-link.types';

// Small UI pieces local to S07 so shared components stay untouched until the team agrees on them.
export const palette = { ...colors, border: '#D0D7E2', danger: '#B42318', dangerBg: '#FEF3F2', success: '#067647', successBg: '#ECFDF3', card: '#F6F8FB' };

export function Card({ title, children }: PropsWithChildren<{ title?: string }>) {
  return <View style={styles.card}>{title && <Text style={styles.cardTitle}>{title}</Text>}{children}</View>;
}

export function Field({ label, error, hint, ...input }: TextInputProps & { label: string; error?: string; hint?: string }) {
  return <View style={styles.field}>
    <Text style={styles.label}>{label}</Text>
    <TextInput {...input} style={[styles.input, input.multiline && styles.multiline, error && styles.inputError]}
      placeholderTextColor={palette.muted} accessibilityLabel={label} />
    {hint && !error && <Text style={styles.hint}>{hint}</Text>}
    {error && <Text style={styles.error}>{error}</Text>}
  </View>;
}

export function Button({ title, onPress, busy, disabled, variant = 'primary' }:
  { title: string; onPress: () => void; busy?: boolean; disabled?: boolean; variant?: 'primary' | 'secondary' | 'danger' }) {
  const inactive = disabled || busy;
  return <Pressable accessibilityRole="button" accessibilityState={{ disabled: inactive, busy }} disabled={inactive} onPress={onPress}
    style={({ pressed }) => [styles.button, styles[variant], inactive && styles.disabled, pressed && styles.pressed]}>
    {busy ? <ActivityIndicator color={variant === 'primary' ? '#fff' : palette.primary} />
      : <Text style={[styles.buttonText, variant !== 'primary' && { color: variant === 'danger' ? palette.danger : palette.primary }]}>{title}</Text>}
  </Pressable>;
}

export function Banner({ tone, message, action }: { tone: 'error' | 'success' | 'info'; message: string; action?: { label: string; onPress: () => void } }) {
  return <View accessibilityRole="alert" style={[styles.banner, tone === 'error' ? styles.bannerError : tone === 'success' ? styles.bannerSuccess : styles.bannerInfo]}>
    <Text style={[styles.bannerText, { color: tone === 'error' ? palette.danger : tone === 'success' ? palette.success : palette.text }]}>{message}</Text>
    {action && <Pressable onPress={action.onPress} accessibilityRole="button"><Text style={styles.bannerAction}>{action.label}</Text></Pressable>}
  </View>;
}

const statusText: Record<LinkStatus, string> = {
  NOT_LINKED: 'Chưa liên kết', PENDING: 'Đang chờ xác minh', VERIFIED: 'Đã xác minh',
  REJECTED: 'Chưa được chấp thuận', REVOKED: 'Đã thu hồi', WITHDRAWN: 'Đã rút yêu cầu',
};
const statusColor: Record<LinkStatus, [string, string]> = {
  NOT_LINKED: ['#344054', '#F2F4F7'], PENDING: ['#B54708', '#FFFAEB'], VERIFIED: ['#067647', '#ECFDF3'],
  REJECTED: ['#B42318', '#FEF3F2'], REVOKED: ['#B42318', '#FEF3F2'], WITHDRAWN: ['#344054', '#F2F4F7'],
};
export function StatusBadge({ status }: { status: LinkStatus }) {
  const [color, background] = statusColor[status];
  return <View style={[styles.badge, { backgroundColor: background }]}><Text style={[styles.badgeText, { color }]}>{statusText[status]}</Text></View>;
}

export const formatTime = (value: string | null) =>
  value ? new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : '—';

const styles = StyleSheet.create({
  card: { backgroundColor: palette.card, borderRadius: 12, padding: spacing.md, gap: spacing.sm, borderWidth: 1, borderColor: palette.border },
  cardTitle: { fontSize: 17, fontWeight: '600', color: palette.text },
  field: { gap: spacing.xs },
  label: { fontWeight: '600', color: palette.text },
  input: { borderWidth: 1, borderColor: palette.border, borderRadius: 8, paddingHorizontal: 12, paddingVertical: 10, fontSize: 16, color: palette.text, backgroundColor: '#fff' },
  multiline: { minHeight: 96, textAlignVertical: 'top' },
  inputError: { borderColor: palette.danger },
  hint: { color: palette.muted, fontSize: 12 },
  error: { color: palette.danger, fontSize: 13 },
  button: { minHeight: 48, borderRadius: 8, alignItems: 'center', justifyContent: 'center', paddingHorizontal: spacing.md, borderWidth: 1 },
  primary: { backgroundColor: palette.primary, borderColor: palette.primary },
  secondary: { backgroundColor: '#fff', borderColor: palette.primary },
  danger: { backgroundColor: '#fff', borderColor: palette.danger },
  disabled: { opacity: 0.5 },
  pressed: { opacity: 0.8 },
  buttonText: { color: '#fff', fontWeight: '600', fontSize: 16 },
  banner: { borderRadius: 8, padding: spacing.md, gap: spacing.sm },
  bannerError: { backgroundColor: palette.dangerBg },
  bannerSuccess: { backgroundColor: palette.successBg },
  bannerInfo: { backgroundColor: '#EFF8FF' },
  bannerText: { fontSize: 15 },
  bannerAction: { color: palette.primary, fontWeight: '600' },
  badge: { alignSelf: 'flex-start', borderRadius: 999, paddingHorizontal: 12, paddingVertical: 4 },
  badgeText: { fontWeight: '600' },
});
