import { Stack } from 'expo-router';
import { SessionProvider } from '@/features/auth/session/SessionProvider';

export default function RootLayout() {
  return <SessionProvider><Stack><Stack.Screen name="index" options={{ title: 'DCMS Mobile' }} /><Stack.Screen name="(auth)" options={{ headerShown: false }} /><Stack.Screen name="(account)" options={{ headerShown: false }} /></Stack></SessionProvider>;
}
