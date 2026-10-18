import { Alert, Button, Card, Descriptions, Empty, Form, Input, Modal, Space, Spin, Tag, Timeline, Typography } from 'antd';
import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ApiError } from '../../services/api';
import { customerApi } from '../../services/customerApi';
import { useCustomerAuth } from '../customer-auth/CustomerAuthContext';
import { clinicDate, formatClock, formatTime, ineligibleReasons, statusColors, statusLabels,
  type AppointmentDetail, type ChangeResult, type Slot } from './types';

export default function CustomerAppointmentDetailPage() {
  const { code = '' } = useParams<{ code: string }>();
  const { state, expire } = useCustomerAuth();
  const token = state.status === 'authenticated' ? state.accessToken : null;
  const [detail, setDetail] = useState<AppointmentDetail | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(true);
  const [revision, setRevision] = useState(0);
  const [mode, setMode] = useState<'reschedule' | 'cancel' | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const accessError = useCallback((failure: ApiError) => {
    setDetail(null); setMode(null); setNotice(null); setError(failure);
    if (failure.status === 401) expire();
  }, [expire]);
  useEffect(() => {
    if (!token) return;
    let active = true;
    const controller = new AbortController();
    setBusy(true); setDetail(null); setError(null); setNotice(null); setMode(null);
    void customerApi<AppointmentDetail>(`/api/me/appointments/${encodeURIComponent(code)}`, { signal: controller.signal }, token)
      .then(value => { if (active) setDetail(value); })
      .catch(failure => { if (active) accessError(failure as ApiError); })
      .finally(() => { if (active) setBusy(false); });
    return () => { active = false; controller.abort(); };
  }, [code, token, revision, accessError]);

  return <section>
    <Link to="/customer/appointments">← Lịch hẹn của tôi</Link>
    <div className="calendar-toolbar"><Typography.Title level={2}>Chi tiết lịch hẹn</Typography.Title>
      <Button loading={busy} onClick={() => setRevision(value => value + 1)}>Tải lại lịch hẹn</Button></div>
    {notice && <Alert className="form-alert" type="success" showIcon message={notice} />}
    {busy ? <Spin tip="Đang tải lịch hẹn…"><div className="customer-loading" /></Spin> : error ?
      <Alert type="error" showIcon message={error.message}
        action={error.status !== 403 && error.status !== 404 ? <Button onClick={() => setRevision(value => value + 1)}>Thử lại</Button> : undefined} /> :
      detail && <Space direction="vertical" size="middle" style={{ width: '100%' }}>
        <Card title={detail.code} extra={<Tag color={statusColors[detail.status]}>{statusLabels[detail.status]}</Tag>}>
          <Descriptions column={{ xs: 1, sm: 2 }} items={[
            { key: 'time', label: 'Giờ hẹn', children: formatTime(detail.start) },
            { key: 'end', label: 'Kết thúc', children: formatTime(detail.end) },
            { key: 'duration', label: 'Thời lượng', children: `${detail.durationMinutes} phút` },
            { key: 'dentist', label: 'Nha sĩ', children: detail.dentistName },
            { key: 'branch', label: 'Chi nhánh', children: detail.branchName },
            { key: 'service', label: 'Dịch vụ', children: detail.serviceName },
            { key: 'attendee', label: 'Người khám', children: `${detail.attendeeName}${detail.attendeeKind === 'RELATIVE' ? ' (người thân)' : ''}` },
            { key: 'booker', label: 'Người đặt', children: detail.bookerName },
            { key: 'contact', label: 'Liên hệ người đặt', children: detail.bookerContactMasked || '—' },
            { key: 'access', label: 'Nguồn quyền', children: detail.access === 'BOOKED_BY_ME' ? 'Lịch tôi đặt' : 'Lịch của tôi' },
          ]} />
          <Typography.Text type="secondary">Thời gian hiển thị theo giờ Việt Nam (UTC+7).</Typography.Text>
        </Card>
        <Card title="Đổi hoặc hủy lịch">
          {detail.eligibility.canSelfService && detail.status === 'Confirmed' ? <>
            <Typography.Paragraph>Bạn có thể tự đổi hoặc hủy khi lịch còn ít nhất 12 giờ và chưa được tiếp nhận. Đúng mốc 12 giờ vẫn hợp lệ; phòng khám kiểm tra lại khi bạn xác nhận.</Typography.Paragraph>
            <Space wrap><Button type="primary" onClick={() => { setNotice(null); setMode('reschedule'); }}>Đổi lịch</Button>
              <Button danger onClick={() => { setNotice(null); setMode('cancel'); }}>Hủy lịch</Button></Space>
          </> : <Alert type="info" showIcon message={ineligibleReasons[detail.eligibility.reasonCode ?? 'NOT_CONFIRMED'] ?? 'Lịch hẹn này không thể tự đổi hoặc hủy.'} />}
          <Typography.Paragraph className="customer-link-details">Liên hệ phòng khám: <a href={`tel:${detail.clinicContact.phone.replace(/[^+\d]/g, '')}`}>{detail.clinicContact.phone}</a> · {detail.clinicContact.hours}</Typography.Paragraph>
        </Card>
        <Card title="Lịch sử thay đổi">{detail.history.length === 0 ? <Empty description="Chưa có thay đổi lịch hẹn." /> :
          <Timeline items={detail.history.map((item, index) => ({ key: index, children: <>
            <Typography.Text strong>{item.action === 'RESCHEDULE' ? 'Đổi lịch' : 'Hủy lịch'}</Typography.Text>
            {' · '}{formatTime(item.occurredAt)}{' · '}{item.actorKind === 'STAFF' ? 'Phòng khám' : 'Tự phục vụ'}
            <div>{formatTime(item.oldStart)}{item.newStart ? ` → ${formatTime(item.newStart)}` : ''}</div>
            <div>Lý do: {item.reason}</div>
          </> }))} />}</Card>
      </Space>}
    {detail && token && mode && <ChangeDialog key={`${detail.code}-${mode}`} detail={detail} token={token} mode={mode}
      onClose={() => setMode(null)} onAccessError={accessError}
      onReload={() => { setMode(null); setRevision(value => value + 1); }}
      onChanged={result => { setDetail(result.appointment); setMode(null); setNotice(result.notification.message); }} />}
  </section>;
}

function ChangeDialog({ detail, token, mode, onClose, onChanged, onAccessError, onReload }: {
  detail: AppointmentDetail; token: string; mode: 'reschedule' | 'cancel'; onClose: () => void;
  onChanged: (result: ChangeResult) => void; onAccessError: (error: ApiError) => void; onReload: () => void;
}) {
  const moving = mode === 'reschedule';
  const [form] = Form.useForm<{ reason: string }>();
  const [date, setDate] = useState(clinicDate(Date.parse(detail.start)));
  const [slots, setSlots] = useState<Slot[]>([]);
  const [slot, setSlot] = useState<Slot | null>(null);
  const [slotsBusy, setSlotsBusy] = useState(moving);
  const [slotsError, setSlotsError] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);
  const [reviewing, setReviewing] = useState(false);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const submitting = useRef(false);
  const retry = useRef<{ fingerprint: string; key: string } | null>(null);
  const conflict = error?.code === 'MSG111' || error?.code === 'MSG31';
  useEffect(() => {
    if (!moving) return;
    setSlot(null); setSlots([]); setSlotsError(null); setReviewing(false);
    if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) { setSlotsBusy(false); return; }
    let active = true;
    const controller = new AbortController();
    setSlotsBusy(true);
    void customerApi<{ slots: Slot[] }>(`/api/appointments/${encodeURIComponent(detail.code)}/availability?${new URLSearchParams({ date })}`,
      { signal: controller.signal }, token)
      .then(value => { if (active) setSlots(value.slots); })
      .catch((failure: ApiError) => {
        if (!active) return;
        if ([401, 403, 404].includes(failure.status)) onAccessError(failure);
        else { setSlotsError(failure.message); if (failure.code === 'MSG31') setError(failure); }
      }).finally(() => { if (active) setSlotsBusy(false); });
    return () => { active = false; controller.abort(); };
  }, [date, detail.code, moving, token, revision, onAccessError]);

  async function confirm() {
    if (submitting.current || conflict || (moving && !slot)) return;
    const payload = { expectedVersion: detail.version, reason,
      ...(moving && slot ? { newStart: slot.start, chairId: slot.chairId, scheduleVersion: slot.scheduleVersion,
        assistantScheduleVersion: slot.assistantScheduleVersion } : {}) };
    const fingerprint = JSON.stringify({ code: detail.code, mode, ...payload });
    const requestKey = retry.current?.fingerprint === fingerprint ? retry.current.key : crypto.randomUUID();
    retry.current = { fingerprint, key: requestKey };
    submitting.current = true; setBusy(true); setError(null);
    try {
      onChanged(await customerApi<ChangeResult>(`/api/appointments/${encodeURIComponent(detail.code)}/${mode}`, {
        method: 'POST', body: JSON.stringify({ ...payload, requestKey }),
      }, token));
    } catch (failure) {
      const error = failure as ApiError;
      if ([401, 403, 404].includes(error.status)) { onAccessError(error); return; }
      setError(error);
      if (error.status >= 400 && error.status < 500) retry.current = null;
      if (error.code === 'MSG34') { setReviewing(false); setRevision(value => value + 1); }
    } finally { submitting.current = false; setBusy(false); }
  }

  return <Modal open title={moving ? 'Đổi lịch hẹn' : 'Hủy lịch hẹn'} footer={null} width={640}
    onCancel={() => { if (!busy) onClose(); }} closable={!busy} maskClosable={!busy} keyboard={!busy}>
    {error && <Alert className="form-alert" type="error" showIcon message={error.message}
      action={conflict ? <Button onClick={onReload}>Tải lại lịch hẹn</Button> : undefined} />}
    {reviewing ? <>
      <Typography.Title level={5}>{moving ? 'Xem lại lịch mới' : 'Xác nhận thông tin hủy'}</Typography.Title>
      <Descriptions column={1} items={[
        { key: 'code', label: 'Mã lịch', children: detail.code },
        { key: 'old', label: 'Giờ hẹn hiện tại', children: formatTime(detail.start) },
        ...(moving && slot ? [{ key: 'new', label: 'Giờ hẹn mới', children: formatTime(slot.start) }] : []),
        { key: 'duration', label: 'Thời lượng', children: `${detail.durationMinutes} phút` },
        { key: 'reason', label: 'Lý do', children: reason },
      ]} />
      {!moving && <Alert className="form-alert" type="warning" showIcon message="Sau khi xác nhận, lịch hẹn này sẽ bị hủy." />}
      <Space wrap><Button onClick={() => setReviewing(false)} disabled={busy}>Quay lại chỉnh sửa</Button>
        <Button type="primary" danger={!moving} loading={busy} disabled={Boolean(conflict)} onClick={() => void confirm()}>
          {moving ? 'Xác nhận đổi lịch' : 'Xác nhận hủy lịch'}</Button></Space>
    </> : <Form form={form} layout="vertical" onFinish={values => {
      if (moving && !slot) return;
      setReason(values.reason.trim()); setReviewing(true);
    }}>
      <Typography.Paragraph>Giờ hẹn hiện tại: <strong>{formatTime(detail.start)}</strong> · {detail.durationMinutes} phút.</Typography.Paragraph>
      {moving && <>
        <label htmlFor="reschedule-date">Ngày hẹn mới (giờ Việt Nam)</label>
        <Input id="reschedule-date" type="date" value={date} min={clinicDate()} disabled={busy || Boolean(conflict)} onChange={event => setDate(event.target.value)} />
        {slotsBusy ? <Spin><div className="customer-loading" /></Spin> : slotsError ?
          <Alert className="form-alert" type="error" message={slotsError} action={<Button onClick={() => setRevision(value => value + 1)}>Tải lại khung giờ</Button>} /> :
          date && slots.length === 0 ? <Empty description="Không có khung giờ phù hợp trong ngày này. Vui lòng chọn ngày khác." /> :
            <div className="customer-slots" role="group" aria-label="Khung giờ mới">{slots.map(value =>
              <Button key={`${value.start}-${value.chairId}`} aria-pressed={slot?.start === value.start && slot.chairId === value.chairId}
                type={slot?.start === value.start && slot.chairId === value.chairId ? 'primary' : 'default'}
                onClick={() => setSlot(value)}>{formatClock(value.start)}–{formatClock(value.end)}</Button>)}</div>}
      </>}
      <Form.Item className="customer-link-details" name="reason" label="Lý do" rules={[
        { required: true, whitespace: true, message: 'Vui lòng nhập Lý do.' }, { max: 500, message: 'Lý do không được vượt quá 500 ký tự.' },
      ]}><Input.TextArea rows={3} maxLength={500} showCount /></Form.Item>
      <Typography.Paragraph type="secondary">Không ghi thông tin bệnh lý trong lý do.</Typography.Paragraph>
      <Space><Button onClick={onClose}>Đóng</Button><Button type="primary" htmlType="submit"
        disabled={Boolean(conflict) || (moving && (!slot || slotsBusy))}>Xem lại</Button></Space>
    </Form>}
  </Modal>;
}
