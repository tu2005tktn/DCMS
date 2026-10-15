/**
 * useSession — đọc state phiên hiện tại.
 */

import { useContext } from 'react';
import { SessionContext } from './SessionProvider';
import type { SessionContextValue, SessionState } from './session.types';

export function useSession(): SessionContextValue {
  const ctx = useContext(SessionContext);
  if (!ctx) throw new Error('useSession phải được dùng trong SessionProvider.');
  return ctx;
}

/** Trả về state gọn để UI điều kiện. */
export function useSessionState(): SessionState {
  return useSession().state;
}
