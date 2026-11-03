import { Alert, Button, Card, Skeleton, Tag, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { api, ApiError } from '../../services/api';
import NextVisitBookingDrawer, { type NextVisitCandidate, type NextVisitPatient } from '../appointments/NextVisitBookingDrawer';
import { formatDate } from '../appointments/types';

const line = (c: NextVisitCandidate) => `${c.serviceName}${c.region ? ` · ${c.region}` : ''}`;

// Next plan item that still needs a visit. Only dentists of the branch can read it; others do not see this block.
export default function NextVisitPanel({ patient, active, revision, onBooked }: {
  patient: NextVisitPatient; active: boolean; revision: number; onBooked: () => void;
}) {
  const [items, setItems] = useState<NextVisitCandidate[] | null>(null);
  const [error, setError] = useState<string | null>(null), [hidden, setHidden] = useState(false);
  const [retry, setRetry] = useState(0), [booking, setBooking] = useState<NextVisitCandidate | null>(null);
  useEffect(() => {
    if (!active) return;
    let live = true; setError(null);
    api<{ items: NextVisitCandidate[] }>(`/api/patients/${patient.id}/next-visit-candidates`)
      .then(value => { if (live) { setItems(value.items ?? []); setHidden(false); } })
      .catch(failure => { if (!live) return; if ((failure as ApiError).status === 403) setHidden(true); else setError((failure as Error).message); });
    return () => { live = false; };
  }, [patient.id, active, revision, retry]);
  if (hidden) return null;

  const pending = (items ?? []).filter(x => x.status !== 'Booked'), booked = (items ?? []).filter(x => x.status === 'Booked');
  const next = pending[0];
  return <section className="next-visit" aria-label="Hạng mục kế tiếp cần hẹn">
    <Typography.Title level={5}>Hạng mục kế tiếp cần hẹn</Typography.Title>
    {error ? <Alert type="error" showIcon message={error} action={<Button onClick={() => setRetry(v => v + 1)}>Thử lại</Button>} />
      : !items ? <Skeleton active paragraph={{ rows: 2 }} />
      : <>
        {next ? <Card className="next-visit-card" size="small"><div className="next-visit-main">
          <div><strong>{line(next)}</strong>
            <div className="next-visit-meta">Phiếu {next.dossierCode} · Ngày dự kiến: {next.estimatedStart ? formatDate(next.estimatedStart) : 'chưa có'}
              {' '}· Bác sĩ {next.dentistName} · {next.durationMinutes} phút</div>
            {next.status === 'NeedsBooking' && <Tag color="gold">Đã có chỉ định, chưa có lịch</Tag>}</div>
          <Button type="primary" onClick={() => setBooking(next)}>Đặt lịch</Button></div></Card>
          : <Alert type="info" showIcon message={booked.length > 0 ? 'Mọi hạng mục chưa thực hiện đã có lịch hẹn.' : 'Chưa có hạng mục nào trong kế hoạch cần hẹn.'} />}
      </>}
    <NextVisitBookingDrawer open={!!booking} candidate={booking} patient={patient} onClose={() => setBooking(null)}
      onBooked={() => { setBooking(null); setRetry(v => v + 1); onBooked(); }} />
  </section>;
}
