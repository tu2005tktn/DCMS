import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { ApiError } from '../../services/api';
import { customerApi } from '../../services/customerApi';

export type CustomerUser = {
  id: number; userName: string; fullName: string; accountType: string; roles: string[];
  effectiveRoles: string[]; permissions: string[]; hasVerifiedPatientLink: boolean;
  phoneNumber?: string | null; email?: string | null;
};
type SignInResponse = { accessToken: string; user: CustomerUser };
type CustomerState =
  | { status: 'initializing' }
  | { status: 'error'; message: string }
  | { status: 'anonymous'; message?: string }
  | { status: 'authenticated'; user: CustomerUser; accessToken: string };
type CustomerAuth = {
  state: CustomerState;
  signIn: (userName: string, password: string) => Promise<void>;
  signInWithGoogle: (idToken: string) => Promise<void>;
  signOut: () => Promise<void>;
  expire: () => void;
  reload: () => Promise<void>;
};

const storageKey = 'dcms.customer.accessToken';
const Context = createContext<CustomerAuth | null>(null);
function readToken() { try { return sessionStorage.getItem(storageKey); } catch { return null; } }
function storeToken(value: string) { try { sessionStorage.setItem(storageKey, value); } catch { /* Memory state still works. */ } }
function clearToken() { try { sessionStorage.removeItem(storageKey); } catch { /* No stored token. */ } }

export function CustomerAuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<CustomerState>({ status: 'initializing' });
  const activeToken = useRef<string | null>(readToken());

  const expire = useCallback((expectedToken?: string) => {
    if (expectedToken && activeToken.current !== expectedToken) return;
    activeToken.current = null;
    clearToken();
    setState({ status: 'anonymous', message: 'Phiên khách hàng đã hết hạn. Vui lòng đăng nhập lại.' });
  }, []);

  const reload = useCallback(async () => {
    const token = readToken();
    if (!token) { setState({ status: 'anonymous' }); return; }
    activeToken.current = token;
    setState({ status: 'initializing' });
    try {
      const user = await customerApi<CustomerUser>('/api/auth/me', {}, token);
      if (activeToken.current !== token) return;
      if (user.accountType !== 'Public') {
        activeToken.current = null; clearToken(); setState({ status: 'anonymous' }); return;
      }
      setState({ status: 'authenticated', user, accessToken: token });
    } catch (error) {
      if (activeToken.current !== token) return;
      if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
        expire(token);
      } else {
        setState({ status: 'error', message: (error as Error).message });
      }
    }
  }, [expire]);

  useEffect(() => { void reload(); }, [reload]);
  useEffect(() => {
    if (state.status !== 'authenticated') return;
    const { accessToken } = state;
    const check = () => {
      void customerApi<CustomerUser>('/api/auth/me', {}, accessToken)
        .then(user => {
          if (user.accountType !== 'Public') { expire(accessToken); return; }
          setState(current => current.status === 'authenticated' && current.accessToken === accessToken
            ? { ...current, user } : current);
        })
        .catch(error => {
          if (error instanceof ApiError && (error.status === 401 || error.status === 403)) expire(accessToken);
        });
    };
    const timer = window.setInterval(check, 30_000);
    window.addEventListener('focus', check);
    return () => { window.clearInterval(timer); window.removeEventListener('focus', check); };
  }, [state.status === 'authenticated' ? state.accessToken : null, expire]);

  async function acceptSession(response: SignInResponse) {
    if (!response.accessToken || !response.user) throw new ApiError(502, 'MSG144', 'Phản hồi đăng nhập không hợp lệ.');
    if (response.user.accountType !== 'Public') {
      await customerApi<void>('/api/auth/sign-out', { method: 'POST' }, response.accessToken).catch(() => undefined);
      throw new ApiError(403, 'MSG06', 'Đây là tài khoản nhân viên. Vui lòng chọn đăng nhập Nhân viên.');
    }
    activeToken.current = response.accessToken;
    storeToken(response.accessToken);
    setState({ status: 'authenticated', user: response.user, accessToken: response.accessToken });
  }

  async function signIn(userName: string, password: string) {
    const response = await customerApi<SignInResponse>('/api/auth/sign-in', {
      method: 'POST', body: JSON.stringify({ userName: userName.trim(), password }),
    });
    await acceptSession(response);
  }

  async function signInWithGoogle(idToken: string) {
    const response = await customerApi<SignInResponse>('/api/auth/google/sign-in', {
      method: 'POST', body: JSON.stringify({ idToken }),
    });
    await acceptSession(response);
  }

  async function signOut() {
    const token = state.status === 'authenticated' ? state.accessToken : readToken();
    activeToken.current = null;
    clearToken();
    setState({ status: 'anonymous' });
    if (token) await customerApi<void>('/api/auth/sign-out', { method: 'POST' }, token).catch(() => undefined);
  }

  return <Context.Provider value={{ state, signIn, signInWithGoogle, signOut, expire, reload }}>{children}</Context.Provider>;
}

export function useCustomerAuth() {
  const value = useContext(Context);
  if (!value) throw new Error('CustomerAuthProvider is required');
  return value;
}
