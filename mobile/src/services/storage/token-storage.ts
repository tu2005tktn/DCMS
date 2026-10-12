import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

const key = 'dcms.access-token';
function requireNativeStorage() {
  if (Platform.OS === 'web') throw new Error('Persistent token storage is not configured for web.');
}
export const tokenStorage = {
  async get(): Promise<string | null> { requireNativeStorage(); return SecureStore.getItemAsync(key); },
  async set(token: string): Promise<void> { requireNativeStorage(); await SecureStore.setItemAsync(key, token); },
  async clear(): Promise<void> { requireNativeStorage(); await SecureStore.deleteItemAsync(key); },
};
