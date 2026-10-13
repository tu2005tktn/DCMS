/**
 * PasswordField — TextField có nút toggle hiện/ẩn mật khẩu.
 *
 * Theo đặc tả S01: form có mật khẩu với hiện/ẩn.
 */

import { useState } from 'react';
import { Pressable, StyleSheet, Text } from 'react-native';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { typography } from '@/theme/typography';
import { TextField } from './TextField';

interface PasswordFieldProps {
  label: string;
  value: string;
  onChangeText: (value: string) => void;
  onBlur?: () => void;
  error?: string | null;
  hint?: string;
  disabled?: boolean;
  testID?: string;
  /** Hiện mật khẩu mặc định. Mặc định false (ẩn). */
  defaultVisible?: boolean;
}

export function PasswordField(props: PasswordFieldProps) {
  const { label, value, onChangeText, onBlur, error, hint, disabled, testID, defaultVisible = false } = props;
  const [visible, setVisible] = useState(defaultVisible);

  return (
    <TextField
      label={label}
      value={value}
      onChangeText={onChangeText}
      onBlur={onBlur}
      secureTextEntry={!visible}
      autoCapitalize="none"
      autoCorrect={false}
      textContentType="password"
      error={error}
      hint={hint}
      disabled={disabled}
      testID={testID}
      trailing={
        <Pressable
          onPress={() => setVisible((prev) => !prev)}
          accessibilityRole="button"
          accessibilityLabel={visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
          hitSlop={8}
          disabled={disabled}
        >
          <Text style={styles.toggle}>{visible ? 'Ẩn' : 'Hiện'}</Text>
        </Pressable>
      }
    />
  );
}

const styles = StyleSheet.create({
  toggle: {
    color: colors.primary,
    fontSize: typography.body - 2,
    fontWeight: '600',
    paddingHorizontal: spacing.xs,
    paddingVertical: spacing.xs,
  },
});
