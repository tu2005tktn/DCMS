/**
 * useSignOut — Hook đăng xuất.
 *
 *  1. Gọi POST /api/auth/sign-out để thu hồi phiên phía server.
 *  2. Xóa token local dù API có lỗi hay không.
 *  3. Reset session state qua onCleared callback.
 *
 * "Cảnh báo thay đổi chưa lưu khi đăng xuất" là trách nhiệm của từng form (PatientData, v.v.)
 * gọi useUnsavedChanges(). Hook này không tự chặn sign-out — đó là design cho phép thoát nhanh
 * khi cần, và form khác phải tự phát hiện dirty state trước khi submit sign-out (xem ghi chú S01).
 */

import { useCallback, useState } from 'react';
import { signInApi } from '../sign-in.api';
import { tokenStorage } from '@/services/storage/token-storage';

interface UseSignOutOptions {
  onCleared: () => void | Promise<void>;
}

interface UseSignOutResult {
  signingOut: boolean;
  signOut: (accessToken: string) => Promise<void>;
}

export function useSignOut({ onCleared }: UseSignOutOptions): UseSignOutResult {
  const [signingOut, setSigningOut] = useState(false);

  const signOut = useCallback(
    async (accessToken: string) => {
      if (!accessToken) {
        await onCleared();
        return;
      }
      setSigningOut(true);
      try {
        await signInApi.signOut(accessToken);
      } catch {
        // Bỏ qua lỗi mạng/5xx — local vẫn phải xóa token để bảo vệ tài khoản.
      } finally {
        await tokenStorage.clear();
        await onCleared();
        setSigningOut(false);
      }
    },
    [onCleared],
  );

  return { signingOut, signOut };
}
