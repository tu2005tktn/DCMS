import { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Text, View } from 'react-native';
import { googleAuthApi } from './google-auth.api';
import { ApiError } from '@/services/api/api-error';
import { colors } from '@/theme/colors';

interface GoogleCredentialResponse { credential?: string }
interface GoogleIdentitySdk {
  accounts: { id: {
    initialize: (options: {
      client_id: string;
      callback: (response: GoogleCredentialResponse) => void;
    }) => void;
    renderButton: (element: HTMLElement, options: { theme: string; size: string; text: string; width: number }) => void;
  } };
}

declare global { interface Window { google?: GoogleIdentitySdk } }

interface Props {
  onCredential: (token: string) => Promise<void>;
  onError: (message: string) => void;
}

let sdkPromise: Promise<void> | null = null;
function loadGoogleSdk(): Promise<void> {
  if (window.google) return Promise.resolve();
  if (!sdkPromise) {
    sdkPromise = new Promise<void>((resolve, reject) => {
      const script = document.createElement('script');
      script.src = 'https://accounts.google.com/gsi/client';
      script.async = true;
      script.defer = true;
      script.onload = () => resolve();
      script.onerror = () => reject(new Error('Không tải được giao diện Google. Vui lòng thử lại.'));
      document.head.appendChild(script);
    }).catch((error) => { sdkPromise = null; throw error; });
  }
  return sdkPromise;
}

export function GoogleIdentityButton({ onCredential, onError }: Props) {
  const mount = useRef<HTMLDivElement>(null);
  const onCredentialRef = useRef(onCredential);
  const onErrorRef = useRef(onError);
  const [status, setStatus] = useState<'loading' | 'ready' | 'disabled' | 'error'>('loading');
  const [setupError, setSetupError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  onCredentialRef.current = onCredential;
  onErrorRef.current = onError;

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const config = await googleAuthApi.config();
        if (!active) return;
        if (!config.enabled || !config.clientId) { setStatus('disabled'); return; }
        await loadGoogleSdk();
        if (!active) return;
        if (!mount.current || !window.google) {
          setSetupError('Không tải được thư viện Google. Vui lòng kiểm tra kết nối mạng.');
          setStatus('error');
          return;
        }
        window.google.accounts.id.initialize({
          client_id: config.clientId,
          callback: (response) => {
            if (!response.credential) { onErrorRef.current('Google không trả về thông tin đăng nhập.'); return; }
            setBusy(true);
            void onCredentialRef.current(response.credential)
              .catch(() => onErrorRef.current('Không thể đăng nhập bằng Google. Vui lòng thử lại.'))
              .finally(() => setBusy(false));
          },
        });
        window.google.accounts.id.renderButton(mount.current, {
          theme: 'outline', size: 'large', text: 'signin_with', width: Math.min(300, window.innerWidth - 64),
        });
        setStatus('ready');
      } catch (error) {
        if (!active) return;
        setSetupError(error instanceof ApiError && (error.status === 401 || error.status === 404)
          ? 'API đang chạy chưa hỗ trợ Google. Vui lòng khởi động lại backend.'
          : error instanceof ApiError
            ? 'Không đọc được cấu hình Google từ API. Vui lòng thử lại.'
            : 'Không tải được thư viện Google. Vui lòng kiểm tra kết nối mạng.');
        setStatus('error');
      }
    })();
    return () => { active = false; };
  }, []);

  return <View style={{ alignItems: 'center', gap: 8 }}>
    <div ref={mount} aria-label="Đăng nhập bằng Google" />
    {(status === 'loading' || busy) && <ActivityIndicator color={colors.primary} />}
    {status === 'disabled' && <Text style={{ color: colors.muted, textAlign: 'center' }}>Đăng nhập Google chưa được cấu hình trên máy chủ.</Text>}
    {status === 'error' && <Text style={{ color: colors.danger, textAlign: 'center' }}>{setupError}</Text>}
  </View>;
}
