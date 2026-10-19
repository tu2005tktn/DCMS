import { Alert, Button, Empty, Input, Select, Space, Table, Tag, Tooltip, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { ApiError } from '../../services/api';
import DocumentDetailDrawer from './DocumentDetailDrawer';
import { documentsApi } from './documents.api';
import UploadDrawer, { type UploadContext } from './UploadDrawer';
import {
  contextLabel, defaultConstraints, documentTypeLabels, fileStateColors, fileStateLabels, formatDay, formatStamp,
  type DocumentItem, type DocumentPage, type DocumentType, type PatientDocumentContexts,
} from './types';

export type DocumentFilters = { type?: DocumentType; from?: string; to?: string; visitId?: string };
const POLL_MS = 5000;
const POLL_LIMIT = 24; // about two minutes of waiting for scans

const loadMessage = (e: ApiError) => e.status === 401 ? 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.'
  : e.status === 403 ? 'Bạn không có quyền xem tài liệu của bệnh nhân này.'
  : e.status === 404 ? 'Chưa kết nối được API tài liệu (DCMS-95) hoặc không tìm thấy bệnh nhân.' : e.message;

// S20 list, reused by the patient profile tab and the S20 page (and later the S19 workspace) so there is one list/viewer.
export default function DocumentList({ patientId, contexts, filters, onFiltersChange, uploadContext }: {
  patientId: number; contexts: PatientDocumentContexts | null; filters: DocumentFilters;
  onFiltersChange: (next: DocumentFilters) => void; uploadContext: UploadContext;
}) {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [data, setData] = useState<DocumentPage | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);
  const [selected, setSelected] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const polls = useRef(0);

  useEffect(() => {
    let active = true;
    setLoading(true); setError(null);
    documentsApi.list({ patientId, ...filters, page, pageSize })
      .then(result => { if (active) setData(result); })
      .catch(e => { if (active) { setData(null); setError(loadMessage(e as ApiError)); } })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [patientId, filters, page, pageSize, revision]);

  // Re-read while any file is still being scanned, so the state shown is always the server's.
  useEffect(() => {
    if (!data?.items.some(x => x.fileState === 'PendingScan') || polls.current >= POLL_LIMIT) return;
    const timer = setTimeout(() => { polls.current += 1; setRevision(v => v + 1); }, POLL_MS);
    return () => clearTimeout(timer);
  }, [data]);

  const reload = () => { polls.current = 0; setRevision(v => v + 1); };
  const canUpload = data?.allowedActions.includes('upload') ?? false;

  return <>
    <div className="document-filters">
      <Select allowClear placeholder="Tất cả loại" value={filters.type} style={{ width: 190 }}
        options={(Object.keys(documentTypeLabels) as DocumentType[]).map(v => ({ value: v, label: documentTypeLabels[v] }))}
        onChange={type => { setPage(1); onFiltersChange({ ...filters, type }); }} />
      <Select allowClear placeholder="Mọi lượt khám" value={filters.visitId} style={{ width: 220 }}
        options={contexts?.visits.map(v => ({ value: v.id, label: v.label }))}
        onChange={visitId => { setPage(1); onFiltersChange({ ...filters, visitId }); }} />
      <Input type="date" aria-label="Từ ngày" value={filters.from ?? ''} max={filters.to} style={{ width: 160 }}
        onChange={e => { setPage(1); onFiltersChange({ ...filters, from: e.target.value || undefined }); }} />
      <Input type="date" aria-label="Đến ngày" value={filters.to ?? ''} min={filters.from} style={{ width: 160 }}
        onChange={e => { setPage(1); onFiltersChange({ ...filters, to: e.target.value || undefined }); }} />
      <Space>
        <Button onClick={() => { setPage(1); onFiltersChange({}); }}>Xóa lọc</Button>
        <Button loading={loading} onClick={reload}>Tải lại</Button>
        {canUpload && <Button type="primary" onClick={() => setUploading(true)}>Tải tài liệu lên</Button>}
      </Space>
    </div>
    {error ? <Alert type="error" showIcon message={error} description="Chưa tải được danh sách — đây không phải là danh sách trống."
      action={<Button size="small" onClick={reload}>Thử lại</Button>} />
      : <Table<DocumentItem> rowKey="documentId" loading={loading} dataSource={data?.items ?? []} scroll={{ x: 900 }}
        rowClassName={() => 'link-row'} onRow={item => ({ onClick: () => setSelected(item.documentId) })}
        locale={{ emptyText: <Empty description={Object.values(filters).some(Boolean) ? 'Không có tài liệu phù hợp bộ lọc.' : 'Chưa có tài liệu nào.'} /> }}
        pagination={{ current: page, pageSize, total: data?.total ?? 0, showSizeChanger: true, pageSizeOptions: [20, 50, 100],
          showTotal: total => `${total} tài liệu`, onChange: (p, s) => { setPage(s !== pageSize ? 1 : p); setPageSize(s); } }}
        columns={[
          { title: 'Tên tệp', render: (_, d) => <Typography.Text strong>{d.fileName}</Typography.Text> },
          { title: 'Loại', render: (_, d) => documentTypeLabels[d.documentType] ?? d.documentType },
          { title: 'Ngữ cảnh', render: (_, d) => contextLabel(d.context) },
          { title: 'Ngày nguồn', render: (_, d) => formatDay(d.sourceDate) },
          { title: 'Người tải', render: (_, d) => <>{d.uploaderName}<br /><Typography.Text type="secondary">{formatStamp(d.uploadedAt)}</Typography.Text></> },
          { title: 'Trạng thái', render: (_, d) => d.fileState === 'Rejected' && d.rejectReason
            ? <Tooltip title={d.rejectReason}><Tag color={fileStateColors.Rejected}>{fileStateLabels.Rejected}</Tag></Tooltip>
            : <Tag color={fileStateColors[d.fileState]}>{fileStateLabels[d.fileState]}</Tag> },
        ]} />}
    <DocumentDetailDrawer documentId={selected} onClose={() => setSelected(null)} />
    <UploadDrawer open={uploading} patientId={patientId} contexts={contexts} initialContext={{ ...uploadContext, visitId: uploadContext.visitId ?? filters.visitId }}
      constraints={data?.constraints ?? defaultConstraints} onClose={() => { setUploading(false); reload(); }} onUploaded={reload} />
  </>;
}
