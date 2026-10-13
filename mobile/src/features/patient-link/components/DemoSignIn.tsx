import { useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { spacing } from '@/theme/spacing';
import { patientLinkApi } from '../patient-link.api';
import { Banner, Button, Card, Field, palette } from './LinkUi';

// TEMPORARY until S01 provides the mobile session: signs in with the existing API and keeps the token in memory only.
export function DemoSignIn({ onSignedIn, notice }: { onSignedIn: (token: string) => void; notice?: string | null }) {
  const [userName, setUserName] = useState('patient.demo');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (busy) return;
    if (!userName.trim() || !password) { setError('Vui lòng nhập Tên đăng nhập và Mật khẩu.'); return; }
    setBusy(true); setError(null);
    try {
      const result = await patientLinkApi.signIn(userName.trim(), password);
      setPassword('');
      onSignedIn(result.accessToken);
    } catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }

  return <Card title="Đăng nhập (demo)">
    <Text style={styles.note}>Màn hình đăng nhập tạm thời cho S07 cho tới khi S01 hoàn thành. Token chỉ lưu trong bộ nhớ.</Text>
    {notice && <Banner tone="info" message={notice} />}
    {error && <Banner tone="error" message={error} />}
    <View style={styles.form}>
      <Field label="Tên đăng nhập" value={userName} onChangeText={setUserName} autoCapitalize="none" autoCorrect={false} />
      <Field label="Mật khẩu" value={password} onChangeText={setPassword} secureTextEntry onSubmitEditing={() => void submit()} />
      <Button title="Đăng nhập" busy={busy} onPress={() => void submit()} />
    </View>
  </Card>;
}

const styles = StyleSheet.create({
  note: { color: palette.muted },
  form: { gap: spacing.md },
});
