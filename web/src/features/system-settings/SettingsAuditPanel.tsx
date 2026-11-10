import { Alert, Button, Empty, Input, Select, Space, Table, Tag, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { api } from '../../services/api';

// DCMS-132 settings history (GET /api/system-settings/settings/audit). Read-only; the server sanitizes before/after.
type Item = { id: string; section: string; target: string; action: string; actorName: string; at: string; reason: string | null; before: unknown; after: unknown };
type Page = { items: Item[]; total: number; page: number; pageSize: number; sections: string[] };
const sectionLabels: Record<string, string> = {
  general: 'Thiết lập chung', catalogs: 'Danh mục hồ sơ/nghiệp vụ', supplies: 'Vật tư, labo & thuốc', materials: 'Dụng cụ / Vật liệu', 'access-groups': 'Phân quyền truy cập',
};
// Commands are stored as e.g. ReferenceCreate, medicines:update, GeneralRestore; show what happened, keep the raw name as a tooltip.
function actionLabel(action: string) {
  if (action === 'PatientCodeSequence') return 'Đổi số mã bệnh nhân tiếp theo';
  const verb = action.includes(':') ? action.split(':')[1] : action.replace(/^(Reference|Material|Category|General)/, '');
  const labels: Record<string, string> = { Create: 'Thêm', create: 'Thêm', Update: 'Sửa', update: 'Sửa', State: 'Ngừng dùng / khôi phục', state: 'Ngừng dùng / khôi phục',
    Delete: 'Xóa (ẩn)', Restore: 'Khôi phục phiên bản' };
  return labels[verb] ?? action;
}
const targetLabel = (row: Item) => {
  const after = row.after as { name?: string; settings?: unknown; nextCode?: string } | null;
  return after?.name ?? (row.section === 'general' ? (after?.nextCode ? `Mã kế tiếp ${after.nextCode}` : 'Thiết lập chung') : row.target);
};
const time = (value: string) => new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' });
const json = (value: unknown) => value == null ? '—' : JSON.stringify(value, null, 2);

export default function SettingsAuditPanel() {
  const [section, setSection] = useState<string>(), [from, setFrom] = useState(''), [to, setTo] = useState('');
  const [page, setPage] = useState(1), [size, setSize] = useState(20), [revision, setRevision] = useState(0);
  const [data, setData] = useState<Page | null>(null), [loading, setLoading] = useState(false), [error, setError] = useState<string>();
  useEffect(() => {
    let live = true; setLoading(true); setError(undefined);
    const params = new URLSearchParams({ page: String(page), pageSize: String(size) });
    if (section) params.set('section', section); if (from) params.set('from', from); if (to) params.set('to', to);
    void api<Page>(`/api/system-settings/settings/audit?${params}`).then(v => { if (live) setData(v); })
      .catch(e => { if (live) { setError((e as Error).message); setData(null); } })
      .finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [section, from, to, page, size, revision]);
  const filter = (apply: () => void) => { apply(); setPage(1); };
  return <div>
    <Typography.Paragraph type="secondary">Lịch sử chỉ để xem, không sửa được. Mật khẩu, mã bí mật và chi phí ngoài quyền không được hiển thị.</Typography.Paragraph>
    <Space wrap className="system-settings-toolbar">
      <Select aria-label="Nhóm thiết lập" allowClear placeholder="Tất cả nhóm được xem" style={{ minWidth: 240 }} value={section}
        options={(data?.sections ?? Object.keys(sectionLabels)).map(x => ({ value: x, label: sectionLabels[x] ?? x }))}
        onChange={v => filter(() => setSection(v))} />
      <Input aria-label="Từ ngày" type="date" value={from} onChange={e => filter(() => setFrom(e.target.value))} />
      <Input aria-label="Đến ngày" type="date" value={to} onChange={e => filter(() => setTo(e.target.value))} />
    </Space>
    {error ? <Alert type="error" showIcon message={`Không tải được lịch sử: ${error}`} action={<Button onClick={() => setRevision(v => v + 1)}>Thử lại</Button>} />
      : <Table<Item> rowKey="id" loading={loading} dataSource={data?.items ?? []} locale={{ emptyText: <Empty description="Chưa có thay đổi trong phạm vi đang lọc" /> }}
        pagination={{ current: page, pageSize: size, total: data?.total ?? 0, showSizeChanger: true, pageSizeOptions: [20, 50, 100],
          onChange: (p, s) => { setPage(s === size ? p : 1); setSize(s); } }}
        expandable={{ expandedRowRender: row => <div className="settings-audit-diff">
          <div><Typography.Text strong>Trước</Typography.Text><pre>{json(row.before)}</pre></div>
          <div><Typography.Text strong>Sau</Typography.Text><pre>{json(row.after)}</pre></div>
        </div> }}
        columns={[
          { title: 'Thời gian', dataIndex: 'at', render: time, width: 190 },
          { title: 'Nhóm', dataIndex: 'section', render: v => <Tag>{sectionLabels[v] ?? v}</Tag> },
          { title: 'Thao tác', dataIndex: 'action', render: v => <span title={v}>{actionLabel(v)}</span> },
          { title: 'Đối tượng', key: 'target', ellipsis: true, render: (_, row) => <span title={row.target}>{targetLabel(row)}</span> },
          { title: 'Người thực hiện', dataIndex: 'actorName' },
          { title: 'Lý do', dataIndex: 'reason', render: v => v ?? '—' },
        ]} />}
  </div>;
}
