/**
 * SessionProvider — quản lý vòng đời phiên cho cả app.
 *
 * Tích hợp:
 *  - Hydrate lúc khởi động: đọc token từ SecureStore (native) / sessionStorage (web), gọi /me.
 *  - Auto refresh: cứ 30s gọi /me (giống web UC01-UC31.md), kèm khi app focus.
 *  - 401 từ /me -> tự động sign out (khoá hoặc phiên bị thu hồi).
 *  - Cung cấp signIn / signOut / refresh qua context.
 *
 * Lưu ý: Provider không can thiệp vào form "thay đổi chưa lưu". Mỗi form cần tự phát hiện
 * dirty state và gọi useUnsavedChanges() nếu cần chặn navigation.
 */

import { createContext, useCallback, useEffect, useMemo, useRef, useState, type PropsWithChildren } from 'react';
import { AppState, type AppStateStatus, Platform } from 'react-native';
import { ApiError } from '@/services/api/api-error';
import { signInApi } from '@/features/auth/sign-in/sign-in.api';
import { tokenStorage } from '@/services/storage/token-storage';
import type { SessionContextValue, SessionState } from './session.types';

export const SessionContext = createContext<SessionContextValue | undefined>(undefined);

const REFRESH_INTERVAL_MS = 30_000;

// eslint-disable-next-line no-console
const debug = (...args: unknown[]) => console.log('[SessionProvider]', ...args);

interface SessionProviderProps extends PropsWithChildren {
  /**
   * Bật auto refresh. Mặc định bật; tắt để test/SSR.
   */
  enableAutoRefresh?: boolean;
}

export function SessionProvider({ children, enableAutoRefresh = true }: SessionProviderProps) {
  const [state, setState] = useState<SessionState>({ status: 'initializing' });
  /** Lưu interval id để cleanup đúng. */
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  /** Tránh gọi refresh chồng nhau. */
  const inFlightRef = useRef<Promise<boolean> | null>(null);

  const clearTimer = useCallback(() => {
    if (intervalRef.current !== null) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
  }, []);

  const startTimer = useCallback(() => {
    clearTimer();
    if (!enableAutoRefresh) return;
    intervalRef.current = setInterval(() => {
      void refresh();
    }, REFRESH_INTERVAL_MS);
  }, [clearTimer, enableAutoRefresh]);

  const refresh = useCallback(async (): Promise<boolean> => {
    if (inFlightRef.current) return inFlightRef.current;
    const task = (async () => {
      if (state.status !== 'authenticated') return false;
      try {
        const user = await signInApi.me(state.accessToken);
        setState((prev) =>
          prev.status === 'authenticated'
            ? { ...prev, user }
            : prev,
        );
        return true;
      } catch (error) {
        if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
          // Phiên hết hạn hoặc bị thu hồi — tự đăng xuất, KHÔNG báo lỗi cho người dùng.
          await tokenStorage.clear();
          setState({ status: 'anonymous' });
          return false;
        }
        if (error instanceof ApiError && error.status >= 500) {
          setState({ status: 'error', message: 'Máy chủ đang bận. Vui lòng thử lại sau.' });
          return false;
        }
        // Lỗi mạng — không thay đổi state, giữ phiên, lần refresh sau sẽ thử lại.
        return true;
      } finally {
        inFlightRef.current = null;
      }
    })();
    inFlightRef.current = task;
    return task;
  }, [state]);

  // Hydrate khi mount.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const token = await tokenStorage.get();
        if (!token) {
          if (!cancelled) setState({ status: 'anonymous' });
          return;
        }
        const user = await signInApi.me(token);
        if (cancelled) return;
        setState({ status: 'authenticated', accessToken: token, expiresAt: '', user });
      } catch (error) {
        if (cancelled) return;
        if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
          await tokenStorage.clear().catch(() => undefined);
          setState({ status: 'anonymous' });
          return;
        }
        setState({ status: 'error', message: 'Không thể kết nối máy chủ. Vui lòng thử lại.' });
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  // Bật timer khi authenticated, tắt khi không còn.
  useEffect(() => {
    if (state.status === 'authenticated') {
      startTimer();
    } else {
      clearTimer();
    }
    return clearTimer;
  }, [state.status, startTimer, clearTimer]);

  // Focus / foreground -> refresh.
  useEffect(() => {
    if (!enableAutoRefresh) return;
    const sub = AppState.addEventListener('change', (next: AppStateStatus) => {
      if (next === 'active' && state.status === 'authenticated') {
        void refresh();
      }
    });
    return () => sub.remove();
  }, [enableAutoRefresh, refresh, state.status]);

  // Web visibility change.
  useEffect(() => {
    if (!enableAutoRefresh || Platform.OS !== 'web') return;
    const handler = () => {
      if (document.visibilityState === 'visible' && state.status === 'authenticated') {
        void refresh();
      }
    };
    document.addEventListener('visibilitychange', handler);
    return () => document.removeEventListener('visibilitychange', handler);
  }, [enableAutoRefresh, refresh, state.status]);

  const signIn = useCallback<SessionContextValue['signIn']>(async (snapshot) => {
    await tokenStorage.set(snapshot.accessToken);
    setState({
      status: 'authenticated',
      user: snapshot.user,
      accessToken: snapshot.accessToken,
      expiresAt: snapshot.expiresAt,
    });
  }, []);

  const signOut = useCallback<SessionContextValue['signOut']>(async (reason = 'user') => {
    const current = state;
    debug('signOut called', { reason, status: current.status });
    if (current.status === 'authenticated') {
      try {
        const result = await signInApi.signOut(current.accessToken);
        debug('signOut API response', { revoked: result.revoked, status: result.status });
      } catch (error) {
        // ignore — vẫn phải clear local để bảo vệ tài khoản
        debug('signOut API error (continuing)', error);
      }
    }
    try {
      await tokenStorage.clear();
    } catch (e) {
      debug('tokenStorage.clear error', e);
    }
    setState({ status: 'anonymous' });
    debug('state -> anonymous');
    // Lý do có thể được UI dùng để hiển thị thông báo (ví dụ "Phiên đã hết hạn").
    if (reason === 'expired' || reason === 'role-revoked') {
      // Đẩy thông báo qua global event — screen đang hiển thị sẽ tự subscribe nếu cần.
      // Ở đây giữ đơn giản: SessionProvider không gắn message, để screen S01 tự dịch.
    }
  }, [state]);

  const value = useMemo<SessionContextValue>(
    () => ({ state, signIn, signOut, refresh }),
    [state, signIn, signOut, refresh],
  );

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}
