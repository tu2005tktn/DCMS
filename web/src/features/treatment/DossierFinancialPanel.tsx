import { usePermissions } from '../authentication/usePermissions';
import { Alert, Button, Drawer, Skeleton, Space, Table, Tooltip, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { api } from '../../services/api';
import DossierPaymentModal from './DossierPaymentModal';
import DossierAdvanceModal, { type DossierAdvance } from './DossierAdvanceModal';
import type { DossierPayment } from './sheetTypes';
import { money } from './types';

const day = (v: string) => new Date(`${v}T00:00:00`).toLocaleDateString('vi-VN');

// Payment entry points of a dossier. Data and rules: docs/specs/TREATMENT-DOSSIER-PAYMENTS.md.
export default function DossierFinancialPanel({ dossierId, patientId, branchId, patientName, onChanged }: {
  dossierId: string; patientId: number; branchId: number; patientName: string; onChanged: () => void;
}) {
  const has = usePermissions();
  const [paying, setPaying] = useState(false), [historyOpen, setHistoryOpen] = useState(false);
  const [history, setHistory] = useState<DossierPayment[] | null>(null);
  const [advancing, setAdvancing] = useState(false);
  const [advances, setAdvances] = useState<DossierAdvance[]>([]);
  const [advanceError, setAdvanceError] = useState<string>();
  const [advanceReload, setAdvanceReload] = useState(0);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    void api<DossierAdvance[]>(`/api/finance/advances?branchId=${branchId}&patientId=${patientId}`)
      .then(rows => { if (active) { setAdvances(rows.filter(x => x.dossierId === dossierId)); setAdvanceError(undefined); } })
      .catch(failure => { if (active) setAdvanceError((failure as Error).message); });
    return () => { active = false; };
  }, [branchId, patientId, dossierId, advanceReload]);
  const savedAdvance = () => { setAdvanceReload(v => v + 1); onChanged(); };

  async function openHistory() {
    setHistoryOpen(true); setError(null); setHistory(null);
    try { setHistory(await api<DossierPayment[]>(`/api/dossiers/${dossierId}/payments`)); setAdvanceReload(v => v + 1); }
    catch (failure) { setError((failure as Error).message); }
  }

  return <Space wrap>
    <Typography.Text>Số dư nhận trước: {money(advances.filter(x => x.paymentState === 'Posted').reduce((sum, x) => sum + x.availableVnd, 0))}</Typography.Text>
    <Button disabled={!has('finance.payments.create')} onClick={() => setAdvancing(true)}>NHẬN TRƯỚC...</Button>
    <Button disabled={!has('finance.read')} onClick={() => void openHistory()}>LỊCH SỬ</Button>
    <Tooltip title={!has('finance.payments.create') ? 'Tài khoản cần quyền Lập phiếu thu để tạo thanh toán.' : undefined}>
      <span><Button disabled={!has('finance.payments.create')} className="treatment-sheet-create" onClick={() => setPaying(true)}>TRẢ TIỀN...</Button></span>
    </Tooltip>
    {!has('finance.payments.create') && <Typography.Text type="secondary">Chỉ xem: tài khoản chưa có quyền Lập phiếu thu.</Typography.Text>}
    <DossierPaymentModal dossierId={dossierId} open={paying} onClose={() => setPaying(false)} onSaved={onChanged} />
    <DossierAdvanceModal open={advancing} dossierId={dossierId} patientId={patientId} branchId={branchId}
      patientName={patientName} onClose={() => setAdvancing(false)} onSaved={savedAdvance} />
    <Drawer open={historyOpen} width={720} title="Lịch sử thanh toán" onClose={() => setHistoryOpen(false)}>
      {error && <Alert role="alert" type="error" showIcon message={error}
        action={<Button onClick={() => void openHistory()}>Thử lại</Button>} />}
      {!history && !error && <Skeleton active />}
      {history && <Table<DossierPayment> rowKey="id" dataSource={history} pagination={false} size="small"
        locale={{ emptyText: 'Phiếu chưa có thanh toán.' }}
        columns={[
          { title: 'Ngày', render: (_, p) => day(p.paidOn) },
          { title: 'Số tiền', align: 'right', render: (_, p) => money(p.totalVnd) },
          { title: 'Phương thức', render: (_, p) => p.method === 'Cash' ? 'Tiền mặt' : 'Chuyển khoản' },
          { title: 'Người thu', dataIndex: 'collectorName' },
          { title: 'Nội dung', render: (_, p) => <>{p.lines.map(l => <div key={l.activityId}>{l.serviceName ?? '—'}: {money(l.amountVnd)}</div>)}
            {p.note && <small>{p.note}</small>}</> },
        ]} />}
      <Typography.Title level={5}>Tiền nhận trước của phiếu</Typography.Title>
      {advanceError && <Alert type="error" showIcon message={advanceError} action={<Button onClick={() => setAdvanceReload(v => v + 1)}>Thử lại</Button>} />}
      <Table<DossierAdvance> rowKey="id" dataSource={advances} pagination={{ pageSize: 10 }} size="small"
        locale={{ emptyText: 'Chưa có khoản nhận trước.' }}
        columns={[
          { title: 'Ngày', render: (_, a) => new Date(a.createdAt).toLocaleDateString('vi-VN') },
          { title: 'Loại', render: (_, a) => a.kind === 'Deposit' ? 'Đặt cọc' : 'Ứng trước lộ trình' },
          { title: 'Đã nhận', render: (_, a) => a.paymentState === 'Posted' ? money(a.amountVnd) : 'Chờ xác nhận' },
          { title: 'Đã phân bổ', render: (_, a) => money(a.allocatedVnd) },
          { title: 'Còn lại', render: (_, a) => money(a.availableVnd) },
        ]} />
    </Drawer>
  </Space>;
}
