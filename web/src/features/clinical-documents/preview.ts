import { ApiError } from '../../services/api';
import { previewDelay } from '../ui-preview/preview';
import type { DocumentsApi } from './documents.api';
import type { DocumentHistoryEntry, DocumentItem, PatientDocumentContexts } from './types';
import { defaultConstraints } from './types';

// UI PREVIEW ONLY (VITE_UI_PREVIEW=true). Illustrative data so the S20 layout can be reviewed before DCMS-95 exists.
// Nothing here is persisted or shown when the flag is off. Delete this file once the real API is available.
const day = (offset: number) => new Date(Date.now() + offset * 86_400_000).toISOString();
const items: DocumentItem[] = [
  { documentId: 'pv-1', version: 1, fileName: 'xquang-toan-cung.jpg', contentType: 'image/jpeg', sizeBytes: 1_850_000, documentType: 'XRay',
    source: 'Clinic', sourceDate: day(-3).slice(0, 10), description: 'Phim toàn cảnh trước điều trị', uploaderName: 'Trợ lý demo',
    uploadedAt: day(-3), context: { visitId: 'v-1', visitLabel: 'Lượt khám (minh họa)' }, fileState: 'Ready', allowedActions: ['view', 'download'] },
  { documentId: 'pv-2', version: 1, fileName: 'ket-qua-xet-nghiem.pdf', contentType: 'application/pdf', sizeBytes: 420_000, documentType: 'LabResult',
    source: 'External', sourceDate: day(-1).slice(0, 10), uploaderName: 'Trợ lý demo', uploadedAt: day(-1),
    context: { visitId: 'v-1', visitLabel: 'Lượt khám (minh họa)', investigationId: 'i-1', investigationLabel: 'Chỉ định cận lâm sàng (minh họa)' },
    fileState: 'PendingScan', allowedActions: [] },
  { documentId: 'pv-3', version: 1, fileName: 'anh-tu-benh-nhan.png', contentType: 'image/png', sizeBytes: 900_000, documentType: 'IntraoralPhoto',
    source: 'External', sourceDate: day(-2).slice(0, 10), uploaderName: 'Trợ lý demo', uploadedAt: day(-2), context: {},
    fileState: 'Rejected', rejectReason: 'Nội dung tệp không khớp định dạng khai báo.', allowedActions: [] },
];
let counter = 10;

export const previewDocumentsApi: DocumentsApi = {
  async contexts(patientId): Promise<PatientDocumentContexts> {
    await previewDelay();
    return { patient: { id: patientId, patientCode: 'BN-MINH-HOA', fullName: 'Bệnh nhân minh họa', birthDate: null },
      visits: [{ id: 'v-1', label: 'Lượt khám (minh họa)', date: day(-3).slice(0, 10) }],
      dossiers: [{ id: 'd-1', label: 'Hồ sơ điều trị (minh họa)' }], plans: [{ id: 'p-1', label: 'Kế hoạch (minh họa)', dossierId: 'd-1' }] };
  },
  async list(q) {
    await previewDelay();
    const rows = items.filter(x => (!q.type || x.documentType === q.type) && (!q.visitId || x.context.visitId === q.visitId)
      && (!q.from || x.sourceDate >= q.from) && (!q.to || x.sourceDate <= q.to));
    return { items: rows.slice((q.page - 1) * q.pageSize, q.page * q.pageSize), total: rows.length, page: q.page, pageSize: q.pageSize,
      constraints: defaultConstraints, allowedActions: ['upload'] };
  },
  async detail(id) {
    await previewDelay();
    const item = items.find(x => x.documentId === id);
    if (!item) throw new ApiError(404, 'MSG113', 'Không tìm thấy dữ liệu được yêu cầu. Vui lòng tải lại danh sách.');
    return item;
  },
  async history(id): Promise<DocumentHistoryEntry[]> {
    await previewDelay();
    const item = items.find(x => x.documentId === id);
    return item ? [{ action: 'Uploaded', actorName: item.uploaderName, occurredAt: item.uploadedAt },
      ...(item.fileState !== 'PendingScan' ? [{ action: item.fileState === 'Ready' ? 'ScanPassed' : 'ScanRejected', actorName: 'Hệ thống', occurredAt: item.uploadedAt }] : [])] : [];
  },
  async upload(metadata, file, onProgress) {
    for (let p = 10; p <= 100; p += 30) { onProgress(Math.min(p, 100)); await previewDelay(150); }
    const extension = file.name.split('.').pop()?.toLowerCase();
    const mismatch = (file.type === 'application/pdf') !== (extension === 'pdf');
    const item: DocumentItem = { documentId: `pv-${++counter}`, version: 1, fileName: file.name, contentType: file.type, sizeBytes: file.size,
      documentType: metadata.documentType, source: metadata.source, sourceDate: metadata.sourceDate, description: metadata.description,
      uploaderName: 'Bạn (minh họa)', uploadedAt: new Date().toISOString(), context: { visitId: metadata.visitId, visitLabel: metadata.visitId ? 'Lượt khám (minh họa)' : null },
      fileState: mismatch ? 'Rejected' : 'PendingScan', rejectReason: mismatch ? 'Nội dung tệp không khớp định dạng khai báo.' : null, allowedActions: [] };
    items.unshift(item);
    if (!mismatch) setTimeout(() => { item.fileState = 'Ready'; item.allowedActions = ['view', 'download']; }, 6000);
    return { ...item };
  },
  async content(id) {
    await previewDelay();
    const item = items.find(x => x.documentId === id);
    if (!item || item.fileState !== 'Ready') throw new ApiError(409, 'MSG221', 'Tệp chưa sẵn sàng để xem.');
    return new Blob([`Xem trước giao diện: nội dung tệp "${item.fileName}" sẽ lấy từ API thật.`], { type: 'text/plain' });
  },
};
