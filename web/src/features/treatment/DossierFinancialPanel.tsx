import { Alert, Button, Drawer, Skeleton, Space, Table } from 'antd';
import { useState } from 'react';
import { api } from '../../services/api';
import DossierPaymentModal from './DossierPaymentModal';
import type { DossierPayment } from './sheetTypes';
import { money } from './types';

const day = (v: string) => new Date(`${v}T00:00:00`).toLocaleDateString('vi-VN');

// Payment entry points of a dossier. Data and rules: docs/specs/TREATMENT-DOSSIER-PAYMENTS.md.
export default function DossierFinancialPanel({ dossierId, onChanged }: { dossierId: string; onChanged: () => void }) {
  const [paying, setPaying] = useState(false), [historyOpen, setHistoryOpen] = useState(false);
  const [history, setHistory] = useState<DossierPayment[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function openHistory() {
    setHistoryOpen(true); setError(null); setHistory(null);
    try { setHistory(await api<DossierPayment[]>(`/api/dossiers/${dossierId}/payments`)); }
    catch (failure) { setError((failure as Error).message); }
  }

  return <Space wrap>
    <Button onClick={() => void openHistory()}>LỊCH SỬ</Button>
    <Button className="treatment-sheet-create" onClick={() => setPaying(true)}>TRẢ TIỀN...</Button>
    <DossierPaymentModal dossierId={dossierId} open={paying} onClose={() => setPaying(false)} onSaved={onChanged} />
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
    </Drawer>
  </Space>;
}
