import { createContext, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { api, ApiError, onSessionLost, readAccessToken, setAccessToken, setSessionTokens } from '../../services/api';

export type CurrentUser = {
  id: number; userName: string; fullName: string; accountType: string; roles: string[];
  effectiveRoles: string[]; permissions: string[]; hasVerifiedPatientLink: boolean;
};
type AuthState = {
  initializing: boolean;
  user: CurrentUser | null; sessionMessage: string | null;
  idleSecondsLeft: number | null;
  signIn: (userName: string, password: string) => Promise<void>;
  signOut: () => Promise<void>; refresh: () => Promise<void>;
};
const Context = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [initializing, setInitializing] = useState(() => !!readAccessToken());
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [sessionMessage, setSessionMessage] = useState<string | null>(null);
  const [idleDeadline, setIdleDeadline] = useState<number | null>(null);
  const [clockNow, setClockNow] = useState(Date.now());
  const activityVersion = useRef<number | null>(null);

  // S26+ — đăng ký callback khi token hết hạn hoặc bị thu hồi.
  useEffect(() => {
    onSessionLost(error => { setUser(null); setIdleDeadline(null); setSessionMessage(error.message); });
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
      } })
      .finally(() => { if (active) setInitializing(false); });
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

  useEffect(() => {
    if (user?.accountType !== 'Staff') { setIdleDeadline(null); return; }
    let active = true;
    type Idle = { deadlineAt:string; serverNow:string; activityVersion:number };
    let lastActivitySent = Date.now();
    let activityRequestKey: string | null = null;
    const update = (path:string, method?:string) => {
      if (method && activityVersion.current == null) return;
      if (method && !activityRequestKey) activityRequestKey = crypto.randomUUID();
      const init = method ? { method, body:JSON.stringify({expectedVersion:activityVersion.current,
        requestKey:activityRequestKey}) } : {};
      void api<Idle>(path, init).then(status => {
        if (active) {
          activityVersion.current = status.activityVersion;
          if (method) activityRequestKey = null;
          setIdleDeadline(Date.now() + new Date(status.deadlineAt).getTime()
            - new Date(status.serverNow).getTime());
        }
      }).catch(error => { if (active && error instanceof ApiError && error.status === 409) {
        lastActivitySent = 0; update('/api/auth/session/idle');
      } });
    };
    update('/api/auth/session/idle');
    const interact = (event:Event) => {
      if (!event.isTrusted || Date.now() - lastActivitySent < 30_000) return;
      lastActivitySent = Date.now(); update('/api/auth/session/activity', 'POST');
    };
    window.addEventListener('pointerdown', interact); window.addEventListener('keydown', interact);
    window.addEventListener('touchstart', interact);
    const statusTimer = window.setInterval(() => update('/api/auth/session/idle'), 30_000);
    const displayTimer = window.setInterval(() => setClockNow(Date.now()), 1_000);
    return () => { active = false; activityVersion.current = null; window.removeEventListener('pointerdown', interact);
      window.removeEventListener('keydown', interact); window.removeEventListener('touchstart', interact);
      window.clearInterval(statusTimer); window.clearInterval(displayTimer); };
  }, [user?.id, user?.accountType]);

  async function signIn(userName: string, password: string) {
    const response = await api<{ accessToken: string; refreshToken: string; user: CurrentUser }>('/api/auth/sign-in',
      { method: 'POST', body: JSON.stringify({ userName, password }) }, false);
    setSessionTokens(response); setUser(response.user); setSessionMessage(null);
  }
  async function signOut() {
    try { await api<void>('/api/auth/sign-out', { method: 'POST' }); }
    catch (error) { if (!(error instanceof ApiError && error.status === 401)) throw error; }
    setAccessToken(null); setUser(null); setIdleDeadline(null); setSessionMessage(null);
  }
  const idleSecondsLeft = idleDeadline == null ? null : Math.max(0, Math.ceil((idleDeadline - clockNow) / 1000));
  return <Context.Provider value={{ user, initializing, sessionMessage, idleSecondsLeft, signIn, signOut, refresh }}>{children}</Context.Provider>;
}
export function useAuth() {
  const context = useContext(Context);
  if (!context) throw new Error('AuthProvider is required');
  return context;
}
