import { Alert, Spin } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { customerApi } from '../../services/customerApi';

type GoogleResponse = { credential?: string };
type GoogleSdk = { accounts: { id: {
  initialize: (value: { client_id: string; callback: (response: GoogleResponse) => void }) => void;
  renderButton: (element: HTMLElement, options: { theme: string; size: string; text: string; width: number }) => void;
} } };
declare global { interface Window { google?: GoogleSdk } }

let sdkPromise: Promise<void> | null = null;
function loadSdk() {
  if (window.google) return Promise.resolve();
  if (!sdkPromise) sdkPromise = new Promise<void>((resolve, reject) => {
    const script = document.createElement('script');
    script.src = 'https://accounts.google.com/gsi/client';
    script.async = true; script.defer = true;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error('Không tải được Google. Vui lòng kiểm tra kết nối.'));
    document.head.appendChild(script);
  }).catch(error => { sdkPromise = null; throw error; });
  return sdkPromise;
}

export function CustomerGoogleButton({ onCredential }: { onCredential: (idToken: string) => Promise<void> }) {
  const mount = useRef<HTMLDivElement>(null);
  const callback = useRef(onCredential);
  callback.current = onCredential;
  const [busy, setBusy] = useState(true);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    void (async () => {
      try {
        const config = await customerApi<{ enabled: boolean; clientId: string | null }>('/api/auth/google/config');
        if (!active) return;
        if (!config.enabled || !config.clientId) {
          setMessage('Đăng nhập Google chưa được cấu hình trên máy chủ.'); return;
        }
        await loadSdk();
        if (!active) return;
        if (!window.google || !mount.current) throw new Error('Không tải được Google. Vui lòng thử lại.');
        window.google.accounts.id.initialize({ client_id: config.clientId, callback: response => {
          if (!response.credential) { setMessage('Google không trả về thông tin đăng nhập.'); return; }
          setBusy(true); setMessage(null);
          void callback.current(response.credential)
            .catch(error => setMessage((error as Error).message))
            .finally(() => setBusy(false));
        } });
        window.google.accounts.id.renderButton(mount.current, {
          theme: 'outline', size: 'large', text: 'signin_with', width: Math.min(320, window.innerWidth - 64),
        });
      } catch (error) { if (active) setMessage((error as Error).message); }
      finally { if (active) setBusy(false); }
    })();
    return () => { active = false; };
  }, []);

  return <div className="customer-google"><div ref={mount} aria-label="Đăng nhập bằng Google" />
    {busy && <Spin size="small" />}
    {message && <Alert type="warning" showIcon message={message} />}
  </div>;
}
