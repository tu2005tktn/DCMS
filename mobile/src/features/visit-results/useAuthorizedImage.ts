import { useEffect, useState } from 'react';
import { env } from '@/config/env';

// docs/specs/MOBILE-PATIENT-VISIT-RESULTS.vi.md §4: on Expo Go Android, <Image> does not reliably send the Authorization
// header, so content is fetched with the session token and shown as an in-memory data URI (no disk cache of the image
// loader; dropped together with screen state on sign-out or 403).
const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';
function base64(bytes: Uint8Array) {
  let out = '';
  for (let i = 0; i < bytes.length; i += 3) {
    const a = bytes[i], b = bytes[i + 1], c = bytes[i + 2];
    out += alphabet[a >> 2] + alphabet[((a & 3) << 4) | ((b ?? 0) >> 4)]
      + (b === undefined ? '=' : alphabet[((b & 15) << 2) | ((c ?? 0) >> 6)]) + (c === undefined ? '=' : alphabet[c & 63]);
  }
  return out;
}

export type AuthorizedImage = { uri: string | null; loading: boolean; failed: boolean; status: number | null };

export function useAuthorizedImage(token: string | null, documentId: string | null, attempt: number): AuthorizedImage {
  const [state, setState] = useState<AuthorizedImage>({ uri: null, loading: true, failed: false, status: null });
  useEffect(() => {
    if (!token || !documentId) { setState({ uri: null, loading: false, failed: false, status: null }); return; }
    let live = true;
    setState({ uri: null, loading: true, failed: false, status: null });
    (async () => {
      try {
        const response = await fetch(`${env.apiUrl}/api/patients/me/documents/${encodeURIComponent(documentId)}/content`,
          { headers: { Authorization: `Bearer ${token}` }, cache: 'no-store' });
        if (!response.ok) { if (live) setState({ uri: null, loading: false, failed: true, status: response.status }); return; }
        const type = response.headers.get('content-type') ?? 'image/png';
        const data = base64(new Uint8Array(await response.arrayBuffer()));
        if (live) setState({ uri: `data:${type};base64,${data}`, loading: false, failed: false, status: 200 });
      } catch {
        if (live) setState({ uri: null, loading: false, failed: true, status: 0 });
      }
    })();
    return () => { live = false; };
  }, [token, documentId, attempt]);
  return state;
}
