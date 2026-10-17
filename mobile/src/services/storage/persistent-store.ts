/**
 * AsyncStorage wrapper dùng chung cho mobile.
 *
 * Tại sao cần file này:
 *  - Hiện mobile chỉ có token-storage (SecureStore / sessionStorage). Appointment + Notification
 *    mock cần persist qua reload nhưng KHÔNG phải dữ liệu nhạy cảm (chỉ là danh sách UI mock),
 *    nên dùng AsyncStorage là phù hợp — không tốn Keychain quota, không chứa token.
 *  - Cung cấp API đồng nhất cho cả native (iOS / Android) lẫn web (fallback localStorage khi
 *    AsyncStorage native chưa load) để giai đoạn 1 chạy được trên cả 3 nền tảng.
 *
 * Khi giai đoạn 2 có backend thật, các repo sẽ tự chuyển sang real API; wrapper này vẫn hữu
 * ích cho cache offline ngắn hạn.
 */

import AsyncStorageDefault from '@react-native-async-storage/async-storage';
import { Platform } from 'react-native';

const NS = 'dcms.v1';

const KEY_APPOINTMENTS = `${NS}.appointments`;
const KEY_NOTIFICATIONS = `${NS}.notifications`;
const KEY_HUB_CURSOR = `${NS}.hub.cursor`;

const ALL_KEYS = [KEY_APPOINTMENTS, KEY_NOTIFICATIONS, KEY_HUB_CURSOR] as const;

function hasLocalStorage(): boolean {
  try {
    return typeof globalThis !== 'undefined' && typeof globalThis.localStorage !== 'undefined';
  } catch {
    return false;
  }
}

function lsGet(key: string): string | null {
  if (!hasLocalStorage()) return null;
  try { return globalThis.localStorage.getItem(key); } catch { return null; }
}

function lsSet(key: string, value: string): void {
  if (!hasLocalStorage()) return;
  try { globalThis.localStorage.setItem(key, value); } catch { /* quota / private mode */ }
}

function lsRemove(key: string): void {
  if (!hasLocalStorage()) return;
  try { globalThis.localStorage.removeItem(key); } catch { /* ignore */ }
}

export const persistentStore = {
  keys: { appointments: KEY_APPOINTMENTS, notifications: KEY_NOTIFICATIONS, hubCursor: KEY_HUB_CURSOR },

  async getItem(key: string): Promise<string | null> {
    if (Platform.OS === 'web') return lsGet(key);
    try { return await AsyncStorageDefault.getItem(key); } catch { return null; }
  },

  async setItem(key: string, value: string): Promise<void> {
    if (Platform.OS === 'web') { lsSet(key, value); return; }
    try { await AsyncStorageDefault.setItem(key, value); } catch { /* ignore */ }
  },

  async removeItem(key: string): Promise<void> {
    if (Platform.OS === 'web') { lsRemove(key); return; }
    try { await AsyncStorageDefault.removeItem(key); } catch { /* ignore */ }
  },

  /**
   * Xóa tất cả key thuộc app — dùng cho "Đăng xuất" nếu sau này muốn wipe local cache.
   * KHÔNG xóa token (token-storage quản lý riêng).
   */
  async clearAll(): Promise<void> {
    for (const k of ALL_KEYS) await this.removeItem(k);
  },
};
