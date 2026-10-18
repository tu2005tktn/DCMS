import { Alert, Descriptions, Drawer, Skeleton, Tag, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { api } from '../../services/api';
import { formatClock, formatDate, kindLabels, localDate, sourceLabels, statusColors, statusLabels, type StaffAppointmentDetail } from './types';

// Minimal read-only detail opened from S10. Actions (check-in, change, cancel) belong to S12 (TV5).
export default function AppointmentDetailDrawer({ id, onClose }: { id: string | null; onClose: () => void }) {
  const [detail, setDetail] = useState<StaffAppointmentDetail | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    let active = true;
    setDetail(null); setError(null);
    api<StaffAppointmentDetail>(`/api/appointments/${id}`)
      .then(result => { if (active) setDetail(result); })
      .catch(failure => { if (active) setError((failure as Error).message); });
    return () => { active = false; };
  }, [id]);

  const a = detail?.appointment;
  return <Drawer open={id !== null} onClose={onClose} width={520} title={a ? `Lịch hẹn ${a.code}` : 'Lịch hẹn'}>
    {error ? <Alert type="error" showIcon message={error} /> : !a || !detail ? <Skeleton active /> : <>
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
      <Typography.Paragraph type="secondary" className="patient-section">
        Tiếp nhận, đổi hoặc hủy lịch thực hiện tại màn S12 (chưa có trong bản này).</Typography.Paragraph>
    </>}
  </Drawer>;
}
