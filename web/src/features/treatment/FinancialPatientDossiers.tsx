import { Alert, Button, Empty, Skeleton } from 'antd';
import { useEffect, useState } from 'react';
import { api } from '../../services/api';
import DossierFinancialPanel from './DossierFinancialPanel';
import { money } from './types';

type FinancialDossier = { id: string; code: string; clinicalState: string;
  summary: { available: boolean; postedChargeVnd: number | null; allocatedVnd: number | null; outstandingVnd: number | null } | null };
export default function FinancialPatientDossiers({ patientId }: { patientId: number }) {
  const [rows, setRows] = useState<FinancialDossier[] | null>(null);
  const [error, setError] = useState<string | null>(null), [reload, setReload] = useState(0);
  useEffect(() => {
    let live = true; setError(null);
    api<FinancialDossier[]>(`/api/patients/${patientId}/dossiers/financial-context`).then(v => { if (live) setRows(v); })
      .catch(f => { if (live) setError((f as Error).message); });
    return () => { live = false; };
  }, [patientId, reload]);
  if (error) return <Alert type="error" showIcon message={error} action={<Button onClick={() => setReload(v => v + 1)}>Thử lại</Button>} />;
  if (!rows) return <Skeleton active />;
  return <section className="treatment-sheets"><h2>Phí và thanh toán theo phiếu</h2>
    {!rows.length && <Empty description="Chưa có phiếu trong phạm vi được phép xem." />}
    {rows.map(r => <article key={r.id} className="treatment-sheet-card"><h3>{r.code}</h3>
      <p>Phí đã ghi nhận: {r.summary?.available && r.summary.postedChargeVnd != null ? money(r.summary.postedChargeVnd) : 'Chưa có dữ liệu đối soát'}</p>
      <p>Đã phân bổ: {r.summary?.available && r.summary.allocatedVnd != null ? money(r.summary.allocatedVnd) : 'Chưa có dữ liệu đối soát'}</p>
      <p>Còn nợ: {r.summary?.available && r.summary.outstandingVnd != null ? money(r.summary.outstandingVnd) : 'Chưa có dữ liệu đối soát'}</p>
      <DossierFinancialPanel dossierId={r.id} available={r.summary?.available ?? false} onChanged={() => setReload(v => v + 1)} />
    </article>)}
  </section>;
}
