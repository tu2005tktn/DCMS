import { Alert, Button, Descriptions, Skeleton, Tag } from 'antd';
import { useEffect, useState } from 'react';
import { api } from '../../services/api';
import { formatClock, formatDate, kindLabels, localDate, sourceLabels, statusColors, statusLabels, type StaffAppointmentDetail } from './types';
import StaffAppointmentActions from './StaffAppointmentActions';

export default function AppointmentDetailContent({ id, revision, onChanged }: { id: string; revision: number; onChanged?: () => void }) {
  const [detail, setDetail] = useState<StaffAppointmentDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [retry, setRetry] = useState(0);
  const [notice, setNotice] = useState('');

  useEffect(() => {
    if (!id) return;
    let active = true;
    setDetail(null); setError(null);
    api<StaffAppointmentDetail>(`/api/appointments/${id}`)
      .then(result => { if (active) setDetail(result); })
      .catch(failure => { if (active) setError((failure as Error).message); });
    return () => { active = false; };
  }, [id, revision, retry]);

  const a = detail?.appointment;
  return <>
    {notice && <Alert className="form-alert" type="success" showIcon message={notice} closable onClose={() => setNotice('')} />}
    {error ? <Alert type="error" showIcon message={error} action={<Button onClick={() => setRetry(value => value + 1)}>Thử lại</Button>} /> : !a || !detail ? <Skeleton active /> : <>
      <h3>Lịch hẹn {a.code}</h3>
      <div className="patient-section"><StaffAppointmentActions size="middle" appointment={a} onChanged={message => { setNotice(message); setRetry(value => value + 1); onChanged?.(); }} /></div>
      <Descriptions column={1} bordered size="small" items={[
        { label: 'Trạng thái', children: <Tag color={statusColors[a.status]}>{statusLabels[a.status]}</Tag> },
        { label: 'Thời gian', children: `${formatDate(localDate(a.startAt))} · ${formatClock(a.startAt)}–${formatClock(a.endAt)} (${a.durationMinutes} phút)` },
        { label: 'Chi nhánh / ghế', children: `${a.branchName} · ${a.chairName}` },
        { label: 'Nha sĩ', children: a.dentistName },
        { label: 'Dịch vụ', children: a.serviceName ?? '—' },
        { label: 'Lý do', children: detail.reason ?? '—' },
        { label: 'Người khám', children: `${a.attendeeName} (${a.attendeeRelation === 'Self' ? 'Bản thân' : 'Người thân'})` },
        { label: 'Người đặt', children: `${detail.bookerName} · ${detail.bookerPhoneMasked ?? detail.bookerEmailMasked ?? '—'}` },
        { label: 'Loại / nguồn', children: `${kindLabels[a.kind] ?? a.kind} · ${sourceLabels[a.source] ?? a.source}` },
        ...(detail.exceptionReason ? [{ label: 'Lý do ngoại lệ', children: detail.exceptionReason }] : []),
        { label: 'Phiên bản', children: a.version },
      ]} />
    </>}
  </>;
}
