import { Alert, Button, Empty, Modal, Spin } from 'antd';
import { useEffect, useState } from 'react';
import { api } from '../../services/api';
import BookingDrawer, { type BookingPrefill } from './BookingDrawer';
import { localDate, localMinute, type AppointmentListItem, type AppointmentOptions, type AvailabilitySlot } from './types';

export default function ReplacementBooking({ source, onClose, onBooked }: {
  source: AppointmentListItem; onClose: () => void; onBooked: (message: string) => void;
}) {
  const [data, setData] = useState<{ options: AppointmentOptions; prefill: BookingPrefill } | null>(null);
  const [error, setError] = useState(''), [empty, setEmpty] = useState(false), [revision, setRevision] = useState(0);
  useEffect(() => {
    let active = true; const controller = new AbortController(); setError(''); setEmpty(false); setData(null);
    void (async () => {
      const options = await api<AppointmentOptions>('/api/appointments/options', { signal: controller.signal });
      const service = options.services.find(value => value.branchId === source.branchId);
      if (!service) throw new Error('Chi nhánh chưa có dịch vụ Khám để đặt lịch mới.');
      const query = new URLSearchParams({ branchId: String(source.branchId), dentistId: String(source.dentistId), serviceId: String(service.id), date: localDate(source.startAt), durationMinutes: '1' });
      const result = await api<{ slots: AvailabilitySlot[] }>(`/api/availability?${query}`, { signal: controller.signal });
      if (!active) return;
      const first = result.slots.find(value => Date.parse(value.start) > Date.now() && Date.parse(value.end) <= Date.parse(source.endAt));
      if (!first) { setEmpty(true); return; }
      const remaining = Math.floor((Date.parse(source.endAt) - Date.parse(first.start)) / 60000);
      setData({ options, prefill: { branchId: source.branchId, dentistId: source.dentistId, date: localDate(source.startAt), minute: localMinute(first.start),
        kind: 'WalkIn', source: 'InPerson', serviceId: service.id, durationMinutes: Math.min(service.publicDurationMinutes, remaining) } });
    })().catch(failure => { if (active) setError((failure as Error).message); });
    return () => { active = false; controller.abort(); };
  }, [source.id, revision]);
  if (data) return <BookingDrawer open prefill={data.prefill} options={data.options} replacement={source} onClose={onClose} onBooked={result => onBooked(`${result.notification.message} Mã lịch mới: ${result.code}`)} />;
  return <Modal open title="Đặt lịch thay thế" width={560} centered footer={null} onCancel={onClose}>
    {error ? <Alert type="error" message={error} action={<Button onClick={() => setRevision(value => value + 1)}>Thử lại</Button>} />
      : empty ? <Empty description="Khung giờ còn lại không đủ để đặt lịch thay thế. Bạn có thể đặt một lịch mới trên lịch chung." /> : <Spin />}
  </Modal>;
}
