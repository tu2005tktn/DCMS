import { Alert, Button, Descriptions, Drawer, Empty, Skeleton, Space, Tabs, Tag, Timeline, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { ApiError } from '../../services/api';
import { documentsApi } from './documents.api';
import {
  contextLabel, documentTypeLabels, fileStateColors, fileStateLabels, formatBytes, formatDay, formatStamp, sourceLabels,
  type DocumentHistoryEntry, type DocumentItem,
} from './types';

const historyLabels: Record<string, string> = {
  Uploaded: 'Tải lên', ScanPassed: 'Kiểm tra an toàn đạt', ScanRejected: 'Bị từ chối khi kiểm tra', Viewed: 'Xem', Downloaded: 'Tải về',
};
const accessMessage = (e: ApiError) => e.status === 403 ? 'Bạn không có quyền xem tệp này.'
  : e.status === 401 ? 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.' : e.message;

// S20 detail/viewer/history. Content is fetched with the session and shown through a temporary blob: URL that is
// revoked on close; only Ready files with the server-granted action can be opened.
export default function DocumentDetailDrawer({ documentId, onClose }: { documentId: string | null; onClose: () => void }) {
  const [doc, setDoc] = useState<DocumentItem | null>(null);
  const [history, setHistory] = useState<DocumentHistoryEntry[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [viewUrl, setViewUrl] = useState<string | null>(null);
  const [viewType, setViewType] = useState('');
  const [viewError, setViewError] = useState<string | null>(null);
  const [opening, setOpening] = useState(false);

  useEffect(() => {
    if (!documentId) return;
    let active = true;
    setDoc(null); setHistory(null); setError(null); setViewError(null);
    Promise.all([documentsApi.detail(documentId), documentsApi.history(documentId)])
      .then(([d, h]) => { if (active) { setDoc(d); setHistory(h); } })
      .catch(e => { if (active) setError(accessMessage(e as ApiError)); });
    return () => { active = false; };
  }, [documentId]);

  useEffect(() => () => { if (viewUrl) URL.revokeObjectURL(viewUrl); }, [viewUrl]);
  function closeAll() { if (viewUrl) URL.revokeObjectURL(viewUrl); setViewUrl(null); onClose(); }

  const canView = doc?.fileState === 'Ready' && doc.allowedActions.includes('view');
  const canDownload = doc?.fileState === 'Ready' && doc.allowedActions.includes('download');

  async function open(disposition: 'inline' | 'attachment') {
    if (!doc) return;
    setOpening(true); setViewError(null);
    try {
      const blob = await documentsApi.content(doc.documentId, disposition);
      const url = URL.createObjectURL(blob);
      if (disposition === 'attachment') {
        const a = document.createElement('a');
        a.href = url; a.download = doc.fileName; a.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
      } else { if (viewUrl) URL.revokeObjectURL(viewUrl); setViewUrl(url); setViewType(blob.type); }
    } catch (e) { setViewError(accessMessage(e as ApiError)); }
    finally { setOpening(false); }
  }

  return <Drawer open={documentId !== null} onClose={closeAll} width={820} destroyOnClose title={doc?.fileName ?? 'Tài liệu'}
    extra={doc && <Space>
      <Button disabled={!canView} loading={opening} onClick={() => void open('inline')}>Xem</Button>
      <Button disabled={!canDownload} onClick={() => void open('attachment')}>Tải về</Button></Space>}>
    {error ? <Alert type="error" showIcon message={error} /> : !doc ? <Skeleton active /> : <Tabs items={[
      { key: 'info', label: 'Thông tin', children: <>
        {doc.fileState !== 'Ready' && <Alert className="form-alert" showIcon type={doc.fileState === 'Rejected' ? 'error' : 'info'}
          message={doc.fileState === 'Rejected' ? `Tệp bị từ chối: ${doc.rejectReason ?? 'không đạt kiểm tra'}` : 'Tệp đang được kiểm tra an toàn, chưa thể mở.'} />}
        {viewError && <Alert className="form-alert" type="error" showIcon message={viewError} />}
        <Descriptions bordered size="small" column={1} items={[
          { label: 'Trạng thái', children: <Tag color={fileStateColors[doc.fileState]}>{fileStateLabels[doc.fileState]}</Tag> },
          { label: 'Loại', children: documentTypeLabels[doc.documentType] ?? doc.documentType },
          { label: 'Nguồn / ngày nguồn', children: `${sourceLabels[doc.source] ?? doc.source} · ${formatDay(doc.sourceDate)}` },
          { label: 'Ngữ cảnh', children: contextLabel(doc.context) },
          ...(doc.context.investigationLabel || doc.context.investigationId ? [{ label: 'Chỉ định cận lâm sàng', children: doc.context.investigationLabel ?? doc.context.investigationId }] : []),
          ...(doc.context.consentVersion ? [{ label: 'Phiên bản đồng thuận', children: doc.context.consentVersion }] : []),
          { label: 'Mô tả', children: doc.description || '—' },
          { label: 'Tệp', children: `${doc.contentType} · ${formatBytes(doc.sizeBytes)}` },
          { label: 'Người tải / lúc', children: `${doc.uploaderName} · ${formatStamp(doc.uploadedAt)}` },
          { label: 'Phiên bản', children: doc.version },
        ]} />
        {viewUrl && <div className="document-viewer">
          {viewType.startsWith('image/') ? <img src={viewUrl} alt={doc.fileName} />
            : <iframe src={viewUrl} title={doc.fileName} />}
        </div>}
        <Typography.Paragraph type="secondary" className="patient-section">
          Bác sĩ đánh giá lâm sàng tại workspace S19; màn này không có duyệt, công bố hay xóa tài liệu.</Typography.Paragraph>
      </> },
      { key: 'history', label: 'Lịch sử', children: !history ? <Skeleton active /> : history.length === 0 ? <Empty description="Chưa có lịch sử." />
        : <Timeline items={history.map((h, i) => ({ key: i, children: <>
          <Typography.Text strong>{historyLabels[h.action] ?? h.action}</Typography.Text> · {h.actorName} ·{' '}
          <Typography.Text type="secondary">{formatStamp(h.occurredAt)}</Typography.Text>{h.note ? <div>{h.note}</div> : null}</> }))} /> },
    ]} />}
  </Drawer>;
}
