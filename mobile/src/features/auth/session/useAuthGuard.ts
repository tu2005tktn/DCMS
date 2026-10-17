/**
 * useAuthGuard — hook bảo vệ route theo trạng thái phiên + role.
 *
 * Theo đặc tả S01:
 *  - Routes bảo vệ (account) chỉ vào khi authenticated.
 *  - Guest có tài khoản (Public, role = ['Guest']) KHÔNG tự có quyền Patient; menu/deep link
 *    phải lọc theo EffectiveRoles.
 *
 * useAuthGuard({ requireRoles: ['Patient'] }) sẽ chỉ true nếu user có ít nhất 1 role khớp.
 * Nếu user chưa đăng nhập -> router.replace('/(auth)/sign-in').
 * Nếu user thiếu role -> router.replace('/(account)/account-link') với banner cảnh báo.
 */

import { useEffect } from 'react';
import { useRouter } from 'expo-router';
import { useSession } from '@/features/auth/session/useSession';

interface AuthGuardOptions {
  /** Role bắt buộc (ít nhất 1 khớp). Mặc định = bất kỳ authenticated user. */
  requireRoles?: string[];
  /** Đường dẫn đến khi không đủ quyền. Mặc định '/(auth)/sign-in'. */
  fallback?: string;
}

export function useAuthGuard(options: AuthGuardOptions = {}): { ready: boolean; authorized: boolean } {
  const { requireRoles, fallback = '/(auth)/sign-in' } = options;
  const router = useRouter();
  const { state, refresh } = useSession();

  useEffect(() => {
    if (state.status === 'initializing') return;
    if (state.status === 'anonymous' || state.status === 'error') {
      router.replace(fallback as never);
      return;
    }
    if (state.status === 'authenticated' && requireRoles && requireRoles.length > 0) {
      const has = state.user.effectiveRoles.some((r) => requireRoles.includes(r));
      if (!has) router.replace('/(account)/account-link' as never);
    }
  }, [state, requireRoles, fallback, router, refresh]);

  if (state.status === 'initializing') return { ready: false, authorized: false };
  if (state.status === 'anonymous' || state.status === 'error') return { ready: true, authorized: false };
  if (requireRoles && requireRoles.length > 0) {
    const has = state.user.effectiveRoles.some((r) => requireRoles.includes(r));
    return { ready: true, authorized: has };
  }
  return { ready: true, authorized: true };
}
