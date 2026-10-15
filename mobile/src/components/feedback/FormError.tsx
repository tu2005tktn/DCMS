/**
 * FormError — thanh thông báo lỗi/thành công trên đầu form.
 */

import { StyleSheet, Text, View } from 'react-native';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { typography } from '@/theme/typography';

interface FormErrorProps {
  message: string | null;
  variant?: 'error' | 'info';
}

export function FormError({ message, variant = 'error' }: FormErrorProps) {
  if (!message) return null;
  const isError = variant === 'error';
  return (
    <View style={[styles.box, { backgroundColor: isError ? colors.dangerSoft : colors.infoSoft, borderColor: isError ? colors.danger : colors.primary }]}>
      <Text style={[styles.text, { color: isError ? colors.danger : colors.primary }]}>{message}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  box: {
    padding: spacing.md,
    borderRadius: 8,
    borderWidth: 1,
  },
  text: { fontSize: typography.body - 2, lineHeight: 20 },
});
