import { Alert, Input, Table, Tag } from 'antd';
import { useEffect, useState } from 'react';
import { api } from '../../services/api';
import { clinicToday, formatDate } from '../appointments/types';

// Stock and usage history of materials/instruments. Rules and API: docs/specs/MATERIAL-STOCK.md.
type Stock = { purchaseId: string; materialId: string; name: string; categoryName: string; usageUnit: string | null; supplierName: string | null; lotNumber: string | null; receivedOn: string; expiresOn: string | null; received: number; used: number; remaining: number };
type Usage = { id: string; usedAt: string; materialId: string; materialName: string; quantity: number; usageUnit: string | null;
  patientId: number; patientName: string; branchName: string; dossierCode: string | null; recordedByName: string | null };
type Page<T> = { items: T[]; total: number; page: number; pageSize: number };
const num = (n: number) => n.toLocaleString('vi-VN', { maximumFractionDigits: 3 });
function expiryTag(expiresOn: string | null) {
  if (!expiresOn) return <span style={{ color: '#667085' }}>—</span>;
  const today = clinicToday(), limit = new Date(`${today}T00:00:00`); limit.setDate(limit.getDate() + 90);
  return <>{formatDate(expiresOn)}{expiresOn < today ? <><br /><Tag color="red">Hết hạn</Tag></> : expiresOn <= limit.toLocaleDateString('sv-SE') ? <><br /><Tag color="orange">Sắp hết hạn</Tag></> : null}</>;
}
const base = '/api/system-settings';

function usePagedList<T>(path: string, q: string) {
  const [data, setData] = useState<Page<T>>(), [page, setPage] = useState(1), [pageSize, setPageSize] = useState(20);
  const [loading, setLoading] = useState(true), [error, setError] = useState<string>();
  useEffect(() => setPage(1), [q]);
  useEffect(() => {
    let current = true; setLoading(true); setError(undefined);
    const params = new URLSearchParams({ q, page: String(page), pageSize: String(pageSize) });
    api<Page<T>>(`${base}/${path}?${params}`).then(r => { if (current) setData(r); })
      .catch((e: unknown) => { if (current) setError(e instanceof Error ? e.message : 'Không tải được dữ liệu.'); })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [path, q, page, pageSize]);
  return { data, loading, error, page, pageSize, setPage, setPageSize };
}

export function MaterialStockPanel() {
  const [q, setQ] = useState(''), [search, setSearch] = useState('');
  useEffect(() => { const t = window.setTimeout(() => setSearch(q.trim()), 300); return () => window.clearTimeout(t); }, [q]);
  const list = usePagedList<Stock>('material-stock', search);
  return <section aria-label="Tồn kho">
    <Input.Search allowClear aria-label="Tìm tồn kho" placeholder="Tìm tên dụng cụ/vật liệu" value={q} onChange={e => setQ(e.target.value)} style={{ maxWidth: 360, marginBottom: 12 }} />
    {list.error && <Alert type="error" showIcon message={list.error} style={{ marginBottom: 12 }} />}
    <Table<Stock> rowKey="purchaseId" size="small" bordered loading={list.loading} dataSource={list.data?.items ?? []} scroll={{ x: 1090 }}
      pagination={{ current: list.page, pageSize: list.pageSize, total: list.data?.total ?? 0, showSizeChanger: true, pageSizeOptions: [20, 50, 100],
        onChange: (p, s) => { list.setPage(p); list.setPageSize(s); } }}
      locale={{ emptyText: 'Chưa có dụng cụ/vật liệu.' }}
      columns={[
        { title: 'Dụng cụ/vật liệu', dataIndex: 'name', render: (v: string, r: Stock) => <>{v}<br /><span style={{ color: '#667085' }}>{r.categoryName}</span></> },
        { title: 'Số lô', dataIndex: 'lotNumber', width: 110, render: (v: string | null) => v ?? '—' },
        { title: 'Ngày nhận', dataIndex: 'receivedOn', width: 110, render: (v: string) => formatDate(v) },
        { title: 'Hạn dùng', dataIndex: 'expiresOn', width: 130, render: (v: string | null) => expiryTag(v) },
        { title: 'Công ty', dataIndex: 'supplierName', width: 150, render: (v: string | null) => v ?? '—' },
        { title: 'Đã nhập', dataIndex: 'received', align: 'right', width: 100, render: (v: number) => num(v) },
        { title: 'Đã dùng', dataIndex: 'used', align: 'right', width: 100, render: (v: number) => num(v) },
        { title: 'Còn lại', dataIndex: 'remaining', align: 'right', width: 110, render: (v: number) => <strong>{num(v)}</strong> },
      ]} />
  </section>;
}

export function MaterialUsagePanel() {
  const [q, setQ] = useState(''), [search, setSearch] = useState('');
  useEffect(() => { const t = window.setTimeout(() => setSearch(q.trim()), 300); return () => window.clearTimeout(t); }, [q]);
  const list = usePagedList<Usage>('material-usage', search);
  return <section aria-label="Lịch sử xuất dùng">
    <Input.Search allowClear aria-label="Tìm lịch sử xuất dùng" placeholder="Tìm tên dụng cụ/vật liệu" value={q} onChange={e => setQ(e.target.value)} style={{ maxWidth: 360, marginBottom: 12 }} />
    {list.error && <Alert type="error" showIcon message={list.error} style={{ marginBottom: 12 }} />}
    <Table<Usage> rowKey="id" size="small" bordered loading={list.loading} dataSource={list.data?.items ?? []} scroll={{ x: 900 }}
      pagination={{ current: list.page, pageSize: list.pageSize, total: list.data?.total ?? 0, showSizeChanger: true, pageSizeOptions: [20, 50, 100],
        onChange: (p, s) => { list.setPage(p); list.setPageSize(s); } }}
      locale={{ emptyText: 'Chưa có lần xuất dùng nào.' }}
      columns={[
        { title: 'Thời gian', dataIndex: 'usedAt', width: 120, render: (v: string) => formatDate(v.slice(0, 10)) },
        { title: 'Dụng cụ/vật liệu', dataIndex: 'materialName' },
        { title: 'Số lượng', dataIndex: 'quantity', align: 'right', width: 120, render: (v: number, r: Usage) => `${num(v)}${r.usageUnit ? ` ${r.usageUnit}` : ''}` },
        { title: 'Bệnh nhân', dataIndex: 'patientName', width: 170 },
        { title: 'Chi nhánh', dataIndex: 'branchName', width: 140 },
        { title: 'Hồ sơ', dataIndex: 'dossierCode', width: 120, render: (v: string | null) => v ?? '—' },
        { title: 'Người ghi nhận', dataIndex: 'recordedByName', width: 150, render: (v: string | null) => v ?? '—' },
      ]} />
  </section>;
}
