import { api, ApiError, readAccessToken, type SystemMessage } from '../../services/api';
import type {
  DocumentHistoryEntry, DocumentItem, DocumentPage, DocumentQuery, PatientDocumentContexts, UploadMetadata,
} from './types';

// S20 data access. Endpoints follow the [PROPOSED] DCMS-95 contract in docs/implementation/S20-CLINICAL-DOCUMENTS-UI.md;
// when Tư finalises the API only this file changes.
export type DocumentsApi = {
  contexts: (patientId: number) => Promise<PatientDocumentContexts>;
  list: (query: DocumentQuery) => Promise<DocumentPage>;
  detail: (documentId: string) => Promise<DocumentItem>;
  history: (documentId: string) => Promise<DocumentHistoryEntry[]>;
  upload: (metadata: UploadMetadata, file: File, onProgress: (percent: number) => void, signal: AbortSignal) => Promise<DocumentItem>;
  content: (documentId: string, disposition: 'inline' | 'attachment') => Promise<Blob>;
};

const base = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080').replace(/\/$/, '');
const connectionError = () => new ApiError(0, 'MSG144', 'Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.');

async function errorFrom(response: Response): Promise<ApiError> {
  const body = await response.json().catch(() => null) as Partial<SystemMessage> | null;
  return new ApiError(response.status, body?.code ?? 'MSG144', body?.message ?? 'Không thể hoàn tất thao tác. Vui lòng thử lại.', body?.field);
}

const httpDocumentsApi: DocumentsApi = {
  contexts: patientId => api(`/api/patients/${patientId}/document-contexts`),
  list: q => {
    const query = new URLSearchParams({ patientId: String(q.patientId), page: String(q.page), pageSize: String(q.pageSize) });
    for (const key of ['visitId', 'dossierId', 'planId', 'type', 'from', 'to'] as const) if (q[key]) query.set(key, String(q[key]));
    return api(`/api/clinical-documents?${query}`);
  },
  detail: id => api(`/api/clinical-documents/${encodeURIComponent(id)}`),
  history: id => api(`/api/clinical-documents/${encodeURIComponent(id)}/history`),
  // XHR rather than fetch so each file reports its own upload progress.
  upload: (metadata, file, onProgress, signal) => new Promise<DocumentItem>((resolve, reject) => {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('metadata', new Blob([JSON.stringify(metadata)], { type: 'application/json' }));
    const xhr = new XMLHttpRequest();
    xhr.open('POST', `${base}/api/clinical-documents`);
    const token = readAccessToken();
    if (token) xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    xhr.upload.onprogress = e => { if (e.lengthComputable) onProgress(Math.round(e.loaded / e.total * 100)); };
    xhr.onload = () => {
      let body: unknown = null;
      try { body = JSON.parse(xhr.responseText); } catch { /* empty or non-JSON body */ }
      if (xhr.status >= 200 && xhr.status < 300 && (body as { document?: DocumentItem })?.document) { resolve((body as { document: DocumentItem }).document); return; }
      const error = body as Partial<SystemMessage> | null;
      reject(new ApiError(xhr.status, error?.code ?? 'MSG144', error?.message ?? 'Không thể tải tệp lên. Vui lòng thử lại.', error?.field));
    };
    xhr.onerror = () => reject(connectionError());
    xhr.onabort = () => reject(new ApiError(0, 'ABORTED', 'Đã hủy tải lên.'));
    signal.addEventListener('abort', () => xhr.abort(), { once: true });
    xhr.send(form);
  }),
  // Content is fetched with the session and shown through a temporary blob: URL; no stored or public link.
  content: async (id, disposition) => {
    const token = readAccessToken();
    let response: Response;
    try {
      response = await fetch(`${base}/api/clinical-documents/${encodeURIComponent(id)}/content?disposition=${disposition}`,
        { headers: token ? { Authorization: `Bearer ${token}` } : {}, cache: 'no-store' });
    } catch { throw connectionError(); }
    if (!response.ok) throw await errorFrom(response);
    return await response.blob();
  },
};

export const documentsApi: DocumentsApi = httpDocumentsApi;
