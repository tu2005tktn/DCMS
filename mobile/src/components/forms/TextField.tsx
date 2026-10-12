/**
 * TextField — input text một dòng.
 *
 * Props:
 *  - label: nhãn hiển thị phía trên
 *  - value / onChangeText: controlled
 *  - error: thông báo lỗi dưới ô (đã validate)
 *  - hint: gợi ý (hiển thị khi không có lỗi)
 *  - trailing: slot cho icon (ví dụ nút hiện/ẩn mật khẩu)
 */

import { useState, type ReactNode } from 'react';
import { StyleSheet, Text, TextInput, View } from 'react-native';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { typography } from '@/theme/typography';

interface TextFieldProps {
  label: string;
  value: string;
  onChangeText: (value: string) => void;
  onBlur?: () => void;
  placeholder?: string;
  secureTextEntry?: boolean;
  autoCapitalize?: 'none' | 'sentences' | 'words' | 'characters';
  autoCorrect?: boolean;
  keyboardType?: 'default' | 'email-address' | 'numeric' | 'phone-pad';
  textContentType?: 'username' | 'password';
  error?: string | null;
  hint?: string;
  disabled?: boolean;
  testID?: string;
  trailing?: ReactNode;
}

export function TextField(props: TextFieldProps) {
  const { label, value, onChangeText, onBlur, placeholder, secureTextEntry, autoCapitalize = 'none',
    autoCorrect = false, keyboardType = 'default', textContentType, error, hint, disabled, testID, trailing } = props;
  const [focused, setFocused] = useState(false);

  const borderColor = error ? colors.danger : focused ? colors.primary : colors.border;
  const labelColor = error ? colors.danger : focused ? colors.primary : colors.muted;

  return (
    <View style={styles.wrapper}>
      <Text style={[styles.label, { color: labelColor }]}>{label}</Text>
      <View style={[styles.field, { borderColor }]}>
        <TextInput
          style={styles.input}
          value={value}
          onChangeText={onChangeText}
          onFocus={() => setFocused(true)}
          onBlur={() => {
            setFocused(false);
            onBlur?.();
          }}
          placeholder={placeholder}
          placeholderTextColor={colors.muted}
          secureTextEntry={secureTextEntry}
          autoCapitalize={autoCapitalize}
          autoCorrect={autoCorrect}
          keyboardType={keyboardType}
          textContentType={textContentType}
          autoComplete={textContentType}
          editable={!disabled}
          testID={testID}
        />
        {trailing ? <View style={styles.trailing}>{trailing}</View> : null}
      </View>
      {error ? <Text style={styles.error}>{error}</Text> : hint ? <Text style={styles.hint}>{hint}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrapper: { gap: spacing.xs },
  label: { fontSize: typography.body - 2, fontWeight: '600' },
  field: {
    flexDirection: 'row',
    alignItems: 'center',
    borderWidth: 1,
    borderRadius: 8,
    backgroundColor: colors.background,
    paddingHorizontal: spacing.md,
    minHeight: 48,
  },
  input: { flex: 1, fontSize: typography.body, color: colors.text, paddingVertical: spacing.sm },
  trailing: { marginLeft: spacing.sm },
  error: { color: colors.danger, fontSize: typography.caption },
  hint: { color: colors.muted, fontSize: typography.caption },
});
