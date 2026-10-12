import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { api, ApiError, onSessionLost, setAccessToken } from '../../services/api';

export type CurrentUser = {
  id: number; userName: string; fullName: string; accountType: string; roles: string[];
  effectiveRoles: string[]; permissions: string[]; hasVerifiedPatientLink: boolean;
};
type AuthState = {
  user: CurrentUser | null; sessionMessage: string | null;
  signIn: (userName: string, password: string) => Promise<void>;
  signOut: () => Promise<void>; refresh: () => Promise<void>;
};
const Context = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [sessionMessage, setSessionMessage] = useState<string | null>(null);
  useEffect(() => {
    onSessionLost(error => { setAccessToken(null); setUser(null); setSessionMessage(error.message); });
    return () => onSessionLost(undefined);
  }, []);
  async function refresh() { setUser(await api<CurrentUser>('/api/auth/me')); }
  useEffect(() => {
    if (!user) return;
    const update = () => { void refresh().catch(() => { /* API displays errors on the next action. */ }); };
    const timer = window.setInterval(update, 30_000);
    window.addEventListener('focus', update);
    return () => { window.clearInterval(timer); window.removeEventListener('focus', update); };
  }, [user?.id]);
  async function signIn(userName: string, password: string) {
    const response = await api<{ accessToken: string; user: CurrentUser }>('/api/auth/sign-in',
      { method: 'POST', body: JSON.stringify({ userName, password }) }, false);
    setAccessToken(response.accessToken); setUser(response.user); setSessionMessage(null);
  }
  async function signOut() {
    try { await api<void>('/api/auth/sign-out', { method: 'POST' }); }
    catch (error) { if (!(error instanceof ApiError && error.status === 401)) throw error; }
    setAccessToken(null); setUser(null); setSessionMessage(null);
  }
  return <Context.Provider value={{ user, sessionMessage, signIn, signOut, refresh }}>{children}</Context.Provider>;
}
export function useAuth() {
  const context = useContext(Context);
  if (!context) throw new Error('AuthProvider is required');
  return context;
}
