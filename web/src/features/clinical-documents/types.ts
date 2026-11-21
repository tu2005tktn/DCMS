// Contract: docs/implementation/S20-CLINICAL-DOCUMENTS-UI.md §3 ([PROPOSED] for DCMS-95).
export type FileState = 'PendingScan' | 'Ready' | 'Rejected';
export type DocumentType = 'XRay' | 'IntraoralPhoto' | 'LabResult' | 'ConsentForm' | 'Referral' | 'Other';
export type DocumentSource = 'Clinic' | 'External';
export type DocumentAction = 'view' | 'download';

export const documentTypeLabels: Record<DocumentType, string> = {
  XRay: 'X-quang', IntraoralPhoto: 'Ảnh trong miệng', LabResult: 'Kết quả xét nghiệm', ConsentForm: 'Phiếu đồng thuận',
  Referral: 'Giấy chuyển/giới thiệu', Other: 'Khác',
};
export const sourceLabels: Record<DocumentSource, string> = { Clinic: 'Tại phòng khám', External: 'Bên ngoài' };
export const fileStateLabels: Record<FileState, string> = { PendingScan: 'Đang kiểm tra an toàn', Ready: 'Sẵn sàng', Rejected: 'Bị từ chối' };
export const fileStateColors: Record<FileState, string> = { PendingScan: 'gold', Ready: 'green', Rejected: 'red' };

export type DocumentContextRef = {
  visitId?: string | null; visitLabel?: string | null; dossierId?: string | null; dossierLabel?: string | null;
  investigationId?: string | null; investigationLabel?: string | null;
  consentVersion?: string | null;
};
export type DocumentItem = {
  documentId: string; version: number; fileName: string; contentType: string; sizeBytes: number;
  documentType: DocumentType; source: DocumentSource; sourceDate: string; description?: string | null;
  context: DocumentContextRef; uploaderName: string; uploadedAt: string; fileState: FileState; rejectReason?: string | null;
  allowedActions: DocumentAction[];
  reviews?: { visitId: string; reviewedBy: number; reviewedAt: string }[];
  aiAnalyses?: AiAnalysis[] | null;
};
// AI decision support on X-ray images: docs/specs/AI-CARIES-DETECTION.vi.md §5. Coordinates are original image pixels.
export type AiFinding = {
  findingId: string; kind: string; label: string; confidence: number; x1: number; y1: number; x2: number; y2: number;
  jaw: 'upper' | 'lower' | string; side: 'right' | 'left' | string;
};
export type AiAnalysis = {
  analysisId: string; kind: string; schemaVersion: string; modelName: string; modelSha256: string; inputSha256: string;
  requestedBy: number; requestedByName: string; requestedAt: string; elapsedMs: number; device: string;
  imageWidth: number; imageHeight: number; findings: AiFinding[];
};
export type DocumentConstraints = { maxFiles: number; maxBytes: number; contentTypes: string[]; descriptionMax: number };
export type DocumentPage = {
  items: DocumentItem[]; total: number; page: number; pageSize: number; constraints: DocumentConstraints; allowedActions: string[];
};
export type DocumentHistoryEntry = { action: string; actorName: string; occurredAt: string; note?: string | null };
export type PatientDocumentContexts = {
  patient: { id: number; patientCode: string; fullName: string; birthDate: string | null };
  visits: { id: string; label: string; date: string; acceptsUpload?: boolean }[];
  dossiers: { id: string; label: string }[];
};
export type DocumentQuery = {
  imagesOnly?: boolean;
  patientId: number; visitId?: string; dossierId?: string; type?: DocumentType; from?: string; to?: string;
  page: number; pageSize: number;
};
export type UploadMetadata = {
  patientId: number; visitId?: string | null; dossierId?: string | null;
  investigationId?: string | null; consentVersion?: string | null; documentType: DocumentType; source: DocumentSource;
  sourceDate: string; description: string; requestKey: string;
};

// Client-side limits used until the server sends its own constraints; the server stays the final decision.
export const defaultConstraints: DocumentConstraints = {
  maxFiles: 10, maxBytes: 20 * 1024 * 1024, contentTypes: ['image/jpeg', 'image/png', 'application/pdf'], descriptionMax: 1000,
};

export const formatBytes = (n: number) => n < 1024 * 1024 ? `${Math.max(1, Math.round(n / 1024))} KB` : `${(n / 1024 / 1024).toFixed(1)} MB`;
export const formatDay = (iso: string) => iso.slice(0, 10).split('-').reverse().join('/');
export const formatStamp = (iso: string) => new Date(iso).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' });
export function contextLabel(c: DocumentContextRef) {
  return [c.visitLabel, c.dossierLabel, c.investigationLabel].filter(Boolean).join(' · ') || 'Hồ sơ chung';
}
export function newRequestKey() {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID()
    : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => { const r = Math.floor(Math.random() * 16); return (c === 'x' ? r : (r & 3) | 8).toString(16); });
}
