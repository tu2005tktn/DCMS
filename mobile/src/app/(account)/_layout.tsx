import { Stack } from 'expo-router';
export default function AccountLayout() {
  // Add a session guard when authentication is implemented.
  return <Stack screenOptions={{ headerTitle: 'Liên kết tài khoản' }} />;
}
