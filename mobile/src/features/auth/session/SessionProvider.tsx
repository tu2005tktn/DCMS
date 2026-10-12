import { createContext, type PropsWithChildren } from 'react';
import type { SessionState } from './session.types';

export const SessionContext = createContext<SessionState | undefined>(undefined);
const initialSession: SessionState = { status: 'anonymous' };
export function SessionProvider({ children }: PropsWithChildren) {
  return <SessionContext.Provider value={initialSession}>{children}</SessionContext.Provider>;
}
