import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { api, ApiError, onSessionLost, readAccessToken, setAccessToken } from '../../services/api';

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

  // S26+ — đăng ký callback khi token hết hạn hoặc bị thu hồi.
  useEffect(() => {
    onSessionLost(error => { setUser(null); setSessionMessage(error.message); });
    return () => onSessionLost(undefined);
  }, []);

  // S26+ — hydrate session khi F5: nếu sessionStorage còn accessToken thì thử lấy user.
  // Nếu token hết hạn / sai, setAccessToken(null) đã được api.ts gọi → user = null.
  useEffect(() => {
    const token = readAccessToken();
    if (!token) return;
    let active = true;
    api<CurrentUser>('/api/auth/me')
      .then(me => { if (active) { setUser(me); setSessionMessage(null); } })
      .catch(error => { if (active && error instanceof ApiError && error.status !== 401) {
        // 401 đã được api.ts xử lý (xoá token + gọi sessionLost). Lỗi khác (0, 5xx) hiển thị nhẹ.
        setSessionMessage('Không thể khôi phục phiên. Vui lòng đăng nhập lại.');
      } });
    return () => { active = false; };
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
