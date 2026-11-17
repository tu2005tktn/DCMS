import { useCallback } from 'react';
import { useSession } from '@/features/auth/session/useSession';
import { FamilyApiError } from './family-members.api';

// Access token of a signed-in patient account, plus the shared rule that an expired session signs out.
export function useFamilySession() {
  const session = useSession();
  const token = session.state.status === 'authenticated' && session.state.user.accountType === 'Public' ? session.state.accessToken : null;
  const handleAuth = useCallback((failure: unknown) => {
    if (failure instanceof FamilyApiError && failure.status === 401) { void session.signOut('expired'); return true; }
    return false;
  }, [session.signOut]);
  return { token, handleAuth };
}

export const messageOf = (failure: unknown) => failure instanceof Error ? failure.message : 'Không thể hoàn tất thao tác. Vui lòng thử lại.';
