/**
 * Token storage cho S01.
 *
 * Lưu trữ:
 *  - Native (iOS / Android): Expo SecureStore (Keychain / Keystore).
 *  - Web: sessionStorage — KHÔNG dùng localStorage theo yêu cầu S01 mục "Ghi tài liệu quản lý
 *    token". sessionStorage bị xoá khi đóng tab, đúng tinh thần "đóng tab cần đăng nhập lại"
 *    mà web đã ghi nhận trong UC01-UC31.md.
 *
 * Dữ liệu nhạy cảm Mobile (lưu ý từ đặc tả):
 *  - Access token được lưu trong SecureStore với key riêng; không commit, không log.
 *  - Không lưu password hay refresh token vì backend đợt này không có.
 *
 * Phòng hờ: các hàm trả về `null` thay vì throw khi storage không khả dụng để SessionProvider
 * có thể degrade gracefully (ví dụ web vừa reload trong khi storage lỗi).
 */

import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

const SECURE_KEY = 'dcms.access-token';
/** Key sessionStorage tách biệt SecureStore để tránh nhầm lẫn khi debug. */
const SESSION_KEY = 'dcms.session.access-token';

function hasSessionStorage(): boolean {
  try {
    return typeof globalThis !== 'undefined' && typeof globalThis.sessionStorage !== 'undefined';
  } catch {
    return false;
  }
}

function sessionGet(): string | null {
  if (!hasSessionStorage()) return null;
  try {
    return globalThis.sessionStorage.getItem(SESSION_KEY);
  } catch {
    return null;
  }
}

function sessionSet(value: string): void {
  if (!hasSessionStorage()) return;
  try {
    globalThis.sessionStorage.setItem(SESSION_KEY, value);
  } catch {
    // sessionStorage có thể bị tắt (private mode) — bỏ qua, phiên sẽ không hydrate qua refresh.
  }
}

function sessionClear(): void {
  if (!hasSessionStorage()) return;
  try {
    globalThis.sessionStorage.removeItem(SESSION_KEY);
  } catch {
    // ignore
  }
}

export const tokenStorage = {
  /** Lấy access token hiện tại. Trả null nếu không có hoặc storage lỗi. */
  async get(): Promise<string | null> {
    if (Platform.OS === 'web') return sessionGet();
    try {
      return await SecureStore.getItemAsync(SECURE_KEY);
    } catch {
      return null;
    }
  },

  /** Lưu access token. Native dùng SecureStore; web dùng sessionStorage. */
  async set(token: string): Promise<void> {
    if (!token) throw new Error('tokenStorage.set yêu cầu token khác rỗng.');
    if (Platform.OS === 'web') {
      sessionSet(token);
      return;
    }
    try {
      await SecureStore.setItemAsync(SECURE_KEY, token);
    } catch {
      // Không throw để tránh làm vỡ flow; SessionProvider sẽ cảnh báo qua log an toàn.
    }
  },

  /** Xóa token — gọi khi sign-out, 401, hoặc lock. */
  async clear(): Promise<void> {
    if (Platform.OS === 'web') {
      sessionClear();
      return;
    }
    try {
      await SecureStore.deleteItemAsync(SECURE_KEY);
    } catch {
      // ignore
    }
  },
};
