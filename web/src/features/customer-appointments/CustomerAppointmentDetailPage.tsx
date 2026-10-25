import { Alert, Button, Card, Descriptions, Empty, Form, Input, Modal, Select, Space, Spin, Tag, Timeline, Typography } from 'antd';
import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ApiError } from '../../services/api';
import { customerApi } from '../../services/customerApi';
import { useCustomerAuth } from '../customer-auth/CustomerAuthContext';
import { clinicDate, formatClock, formatTime, ineligibleReasons, statusColors, statusLabels,
  type AppointmentDetail, type ChangeResult, type Slot, type SupportResult } from './types';

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
    setBusy(true); setError(null); setMode(null);
    void customerApi<AppointmentDetail>(`/api/me/appointments/${encodeURIComponent(code)}`, { signal: controller.signal }, token)
      .then(value => { if (active) setDetail(value); })
      .catch(failure => { if (active) accessError(failure as ApiError); })
      .finally(() => { if (active) setBusy(false); });
    return () => { active = false; controller.abort(); };
  }, [code, token, revision, accessError]);
  const pendingSupport = detail?.supportRequests?.find(request => request.status === 'Pending');

  return <section className="customer-appointment-detail">
    <Link to="/customer/appointments">← Lịch hẹn của tôi</Link>
    <div className="calendar-toolbar customer-page-heading"><div><span className="customer-kicker">Lịch khám</span>
      <Typography.Title level={2}>Chi tiết lịch hẹn</Typography.Title></div>
      <Button loading={busy} onClick={() => setRevision(value => value + 1)}>Tải lại lịch hẹn</Button></div>
    {notice && <Alert className="form-alert" type="success" showIcon message={notice} />}
    {busy ? <Spin tip="Đang tải lịch hẹn…"><div className="customer-loading" /></Spin> : error ?
      <Alert type="error" showIcon message={error.message}
        action={error.status !== 403 && error.status !== 404 ? <Button onClick={() => setRevision(value => value + 1)}>Thử lại</Button> : undefined} /> :
      detail && <Space direction="vertical" size="middle" style={{ width: '100%' }}>
        <Card className="customer-detail-summary" title={detail.code} extra={<Tag color={statusColors[detail.status]}>{statusLabels[detail.status]}</Tag>}>
          <p className="customer-detail-time">{formatTime(detail.start)}</p>
          <Descriptions column={{ xs: 1, sm: 2 }} items={[
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
        <Card className="customer-change-card" title="Đổi hoặc hủy lịch">
          <div className="customer-change-current"><span>Giờ hẹn hiện tại</span>
            <strong>{formatTime(detail.start)}</strong><span>{detail.dentistName} · {detail.branchName}</span></div>
          {detail.status === 'Confirmed' && !detail.eligibility.canSelfService && detail.eligibility.reasonCode === 'WITHIN_12H' ? <>
            {pendingSupport ? (
              <div className="customer-change-pending" role="status"><strong>Yêu cầu đang chờ xử lý</strong>
                <p>Lịch hẹn hiện tại vẫn có hiệu lực cho đến khi phòng khám xác nhận thay đổi.</p>
                {pendingSupport.proposedStartAt && <p>Giờ đề xuất: <strong>{formatTime(pendingSupport.proposedStartAt)}</strong></p>}
                <a href="#customer-support-requests">Xem và rút yêu cầu</a></div>
            ) : (
              <><p className="customer-change-explanation">Còn dưới 12 giờ trước giờ hẹn. Bạn có thể đề xuất giờ mới; phòng khám sẽ kiểm tra và xác nhận. <strong>Lịch cũ vẫn được giữ nguyên trong lúc chờ.</strong></p>
                <Space wrap><Button type="primary" onClick={() => { setNotice(null); setMode('reschedule'); }}>Yêu cầu đổi lịch</Button>
                  <Button onClick={() => { setNotice(null); setMode('cancel'); }}>Yêu cầu hủy lịch</Button></Space></>
            )}
          </> : detail.eligibility.canSelfService && detail.status === 'Confirmed' ? <>
            <Typography.Paragraph>Bạn có thể đổi hoặc hủy trực tiếp vì lịch còn ít nhất 12 giờ. Phòng khám sẽ kiểm tra lại khi bạn xác nhận.</Typography.Paragraph>
            <Space wrap><Button type="primary" onClick={() => { setNotice(null); setMode('reschedule'); }}>Đổi lịch</Button>
              <Button danger onClick={() => { setNotice(null); setMode('cancel'); }}>Hủy lịch</Button></Space>
          </> : <Alert type="info" showIcon message={ineligibleReasons[detail.eligibility.reasonCode ?? 'NOT_CONFIRMED'] ?? 'Lịch hẹn này không thể tự đổi hoặc hủy.'} />}
          <Typography.Paragraph className="customer-change-contact">Cần hỗ trợ? Gọi <a href={`tel:${detail.clinicContact.phone.replace(/[^+\d]/g, '')}`}>{detail.clinicContact.phone}</a> · {detail.clinicContact.hours}</Typography.Paragraph>
        </Card>
        {detail.supportRequests && detail.supportRequests.length > 0 && (
          <Card id="customer-support-requests" className="customer-support-card" title="Yêu cầu hỗ trợ">
            <Timeline items={detail.supportRequests.map((req, index) => ({
              key: index,
              color: req.status === 'Resolved' ? 'green' : req.status === 'Rejected' ? 'red' : req.status === 'Pending' ? 'blue' : 'gray',
              children: <>
                <Space wrap>
                  <Typography.Text strong>
                    {req.kind === 'RESCHEDULE' || req.kind === 'Change' ? 'Đổi lịch hẹn' : req.kind === 'CANCEL' || req.kind === 'Cancel' ? 'Hủy lịch' : 'Yêu cầu hỗ trợ'}
                  </Typography.Text>
                  <Tag color={req.status === 'Resolved' ? 'green' : req.status === 'Rejected' ? 'red' : req.status === 'Pending' ? 'processing' : 'default'}>
                    {req.status === 'Pending' ? 'Đang chờ xử lý' : req.status === 'Resolved' ? 'Đã duyệt' : req.status === 'Rejected' ? 'Đã từ chối' : 'Đã rút'}
                  </Tag>
                </Space>
                <div>Gửi lúc: {formatTime(req.createdAt)}</div>
                {req.proposedStartAt && <div>Khung giờ đề xuất: <Typography.Text strong>{formatTime(req.proposedStartAt)}</Typography.Text></div>}
                <div>Lý do: {req.reason}</div>
                {req.publicNote && <div>Phản hồi: <Typography.Text type="secondary">{req.publicNote}</Typography.Text></div>}
                {req.status === 'Pending' && (
                  <div style={{ marginTop: 8 }}>
                    <Button size="small" onClick={async () => {
                      try {
                        await customerApi(`/api/support-requests/${encodeURIComponent(req.id)}/withdraw`, {
                          method: 'POST', body: JSON.stringify({ expectedVersion: req.version })
                        }, token ?? undefined);
                        setNotice('Đã rút yêu cầu hỗ trợ.');
                        setRevision(v => v + 1);
                      } catch (err) {
                        setError(err as ApiError);
                      }
                    }}>Rút yêu cầu</Button>
                  </div>
                )}
              </>
            }))} />
          </Card>
        )}
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
      onReload={(successNotice?: string) => {
        setMode(null);
        if (successNotice) setNotice(successNotice);
        setRevision(value => value + 1);
      }}
      onChanged={result => { setDetail(result.appointment); setMode(null); setNotice(result.notification.message); }} />}
  </section>;
}

function ChangeDialog({ detail, token, mode, onClose, onChanged, onAccessError, onReload }: {
  detail: AppointmentDetail; token: string; mode: 'reschedule' | 'cancel'; onClose: () => void;
  onChanged: (result: ChangeResult) => void; onAccessError: (error: ApiError) => void; onReload: (successNotice?: string) => void;
}) {
  const moving = mode === 'reschedule';
  const [form] = Form.useForm<{ reason: string }>();
  const [date, setDate] = useState(clinicDate(Date.parse(detail.start)));
  const [dentistId, setDentistId] = useState(detail.dentistId);
  const [dentists, setDentists] = useState([{ id: detail.dentistId, branchId: detail.branchId, name: detail.dentistName, canDentist: true }]);
  const [dentistsBusy, setDentistsBusy] = useState(moving);
  const [dentistsError, setDentistsError] = useState<string | null>(null);
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
  const selectedDentistName = dentists.find(value => value.id === dentistId)?.name ?? detail.dentistName;
  const slotGroups = [
    { label: 'Buổi sáng', slots: slots.filter(value => Number(formatClock(value.start).slice(0, 2)) < 12) },
    { label: 'Buổi chiều', slots: slots.filter(value => Number(formatClock(value.start).slice(0, 2)) >= 12) },
  ].filter(group => group.slots.length > 0);
  useEffect(() => {
    if (!moving) return;
    let active = true;
    const controller = new AbortController();
    setDentistsBusy(true); setDentistsError(null);
    void customerApi<{ employees: { id: number; branchId: number; name: string; canDentist: boolean }[] }>(
      `/api/availability/options?${new URLSearchParams({ branchId: String(detail.branchId) })}`, { signal: controller.signal }, token)
      .then(value => {
        if (!active) return;
        const available = value.employees.filter(item => item.branchId === detail.branchId && item.canDentist
          && item.id !== detail.assistantId);
        setDentists(available.some(item => item.id === detail.dentistId) ? available
          : [{ id: detail.dentistId, branchId: detail.branchId, name: detail.dentistName, canDentist: true }, ...available]);
      })
      .catch((failure: Error) => { if (active) setDentistsError(failure.message); })
      .finally(() => { if (active) setDentistsBusy(false); });
    return () => { active = false; controller.abort(); };
  }, [moving, detail.branchId, detail.dentistId, detail.dentistName, detail.assistantId, token]);
  useEffect(() => {
    if (!moving) return;
    setSlot(null); setSlots([]); setSlotsError(null); setReviewing(false);
    if (!dentistId || !/^\d{4}-\d{2}-\d{2}$/.test(date)) { setSlotsBusy(false); return; }
    let active = true;
    const controller = new AbortController();
    setSlotsBusy(true);
    void customerApi<{ slots: Slot[] }>(`/api/appointments/${encodeURIComponent(detail.code)}/availability?${new URLSearchParams({ date, dentistId: String(dentistId) })}`,
      { signal: controller.signal }, token)
      .then(value => { if (active) setSlots(value.slots); })
      .catch((failure: ApiError) => {
        if (!active) return;
        if ([401, 403, 404].includes(failure.status)) onAccessError(failure);
        else { setSlotsError(failure.message); if (failure.code === 'MSG31') setError(failure); }
      }).finally(() => { if (active) setSlotsBusy(false); });
    return () => { active = false; controller.abort(); };
  }, [date, dentistId, detail.code, moving, token, revision, onAccessError]);

  const isUnder12h = !detail.eligibility.canSelfService;

  async function confirm() {
    if (submitting.current || conflict || (moving && !slot)) return;
    const payload = isUnder12h
      ? (moving
          ? { reason, newStartAt: slot!.start, dentistId, chairId: slot!.chairId, kind: 'Change' }
          : { reason, kind: 'Cancel' })
      : { expectedVersion: detail.version, reason,
          ...(moving && slot ? { dentistId, newStart: slot.start, chairId: slot.chairId, scheduleVersion: slot.scheduleVersion,
            assistantScheduleVersion: slot.assistantScheduleVersion, bookingSettingsVersion: slot.bookingSettingsVersion } : {}) };
    const fingerprint = JSON.stringify({ code: detail.code, mode, isUnder12h, ...payload });
    const requestKey = retry.current?.fingerprint === fingerprint ? retry.current.key : crypto.randomUUID();
    retry.current = { fingerprint, key: requestKey };
    submitting.current = true; setBusy(true); setError(null);
    try {
      if (isUnder12h) {
        const res = await customerApi<SupportResult>(`/api/appointments/${encodeURIComponent(detail.code)}/support-requests`, {
          method: 'POST', body: JSON.stringify({ ...payload, requestKey }),
        }, token);
        onReload(res.notification.message);
        return;
      }
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

  return <Modal open title={moving ? isUnder12h ? 'Yêu cầu đổi lịch' : 'Đổi lịch hẹn' : 'Hủy lịch hẹn'} footer={null} width={680}
    className="customer-change-dialog"
    onCancel={() => { if (!busy) onClose(); }} closable={!busy} maskClosable={!busy} keyboard={!busy}>
    {moving && <ol className="customer-change-steps" aria-label="Các bước đổi lịch">
      <li aria-current={!reviewing ? 'step' : undefined} className={!reviewing ? 'active' : ''}><span>1</span> Chọn lịch mới</li>
      <li aria-current={reviewing ? 'step' : undefined} className={reviewing ? 'active' : ''}><span>2</span> Kiểm tra yêu cầu</li>
    </ol>}
    {error && <Alert className="form-alert" type="error" showIcon message={error.message}
      action={conflict ? <Button onClick={() => onReload()}>Tải lại lịch hẹn</Button> : undefined} />}
    {reviewing ? <>
      <div className="customer-change-review"><Typography.Title level={5}>{moving ? 'Kiểm tra trước khi gửi' : 'Xác nhận thông tin hủy'}</Typography.Title>
        <dl><div><dt>Mã lịch</dt><dd>{detail.code}</dd></div>
          <div><dt>Giờ hẹn hiện tại</dt><dd>{formatTime(detail.start)}</dd></div>
          {moving && <div><dt>Bác sĩ đề xuất</dt><dd>{selectedDentistName}</dd></div>}
          {moving && slot && <div className="customer-change-proposed"><dt>Giờ đề xuất</dt><dd>{formatTime(slot.start)}</dd></div>}
          <div><dt>Thời lượng</dt><dd>{detail.durationMinutes} phút</dd></div>
          <div><dt>Lý do</dt><dd>{reason}</dd></div></dl></div>
      {isUnder12h && <Alert className="form-alert" type="info" showIcon
        message={moving
          ? "Đây là yêu cầu chờ phòng khám xử lý. Giờ hẹn hiện tại vẫn có hiệu lực cho đến khi phòng khám xác nhận."
          : "Lịch hẹn còn dưới 12 giờ. Sau khi bạn gửi yêu cầu hủy, thông tin sẽ được chuyển đến nhân viên phòng khám để kiểm tra và xác nhận. Lịch hẹn cũ vẫn được giữ nguyên cho đến khi nhân viên duyệt."} />}
      {!moving && !isUnder12h && <Alert className="form-alert" type="warning" showIcon message="Sau khi xác nhận, lịch hẹn này sẽ bị hủy." />}
      <Space className="customer-change-actions" wrap><Button onClick={() => setReviewing(false)} disabled={busy}>Quay lại chỉnh sửa</Button>
        <Button type="primary" danger={!moving} loading={busy} disabled={Boolean(conflict)} onClick={() => void confirm()}>
          {moving ? (isUnder12h ? 'Gửi yêu cầu đổi lịch' : 'Xác nhận đổi lịch') : (isUnder12h ? 'Gửi yêu cầu hủy lịch' : 'Xác nhận hủy lịch')}</Button></Space>
    </> : <Form form={form} layout="vertical" onFinish={values => {
      if (moving && !slot) return;
      setReason(values.reason.trim()); setReviewing(true);
    }}>
      <div className="customer-change-dialog-current"><span>Giờ hẹn hiện tại</span><strong>{formatTime(detail.start)}</strong>
        <span>{detail.dentistName} · {detail.durationMinutes} phút</span></div>
      {moving && isUnder12h && <p className="customer-change-hint">Chọn giờ muốn chuyển tới. Phòng khám sẽ xác nhận yêu cầu; lịch hiện tại chưa thay đổi khi bạn gửi.</p>}
      {moving && <>
        <div className="customer-change-fields"><div><label htmlFor="reschedule-dentist">Bác sĩ mới</label>
          <Select id="reschedule-dentist" aria-label="Bác sĩ mới" value={dentistId} loading={dentistsBusy}
            disabled={busy || Boolean(conflict)} options={dentists.map(value => ({ value: value.id, label: value.name }))}
            onChange={value => { setDentistId(value); setSlot(null); setReviewing(false); }} /></div>
          <div><label htmlFor="reschedule-date">Ngày hẹn mới (giờ Việt Nam)</label>
            <Input id="reschedule-date" type="date" value={date} min={clinicDate()} disabled={busy || Boolean(conflict)} onChange={event => setDate(event.target.value)} /></div></div>
        {dentistsError && <Alert className="form-alert" type="error" message={dentistsError} />}
        <h3 className="customer-change-slot-title">Giờ còn trống <small>Giờ Việt Nam (UTC+7)</small></h3>
        {slotsBusy ? <Spin tip="Đang tìm giờ trống"><div className="customer-loading" /></Spin> : slotsError ?
          <Alert className="form-alert" type="error" message={slotsError} action={<Button onClick={() => setRevision(value => value + 1)}>Tải lại khung giờ</Button>} /> :
          date && slots.length === 0 ? <Empty description="Không có khung giờ phù hợp trong ngày này. Vui lòng chọn ngày khác." /> :
            <div className="customer-change-slot-groups" role="group" aria-label="Khung giờ mới">{slotGroups.map(group =>
              <section key={group.label}><h4>{group.label}</h4><div className="customer-slots">{group.slots.map(value =>
                <Button key={`${value.start}-${value.chairId}`} aria-pressed={slot?.start === value.start && slot.chairId === value.chairId}
                  type={slot?.start === value.start && slot.chairId === value.chairId ? 'primary' : 'default'}
                  onClick={() => setSlot(value)}>{formatClock(value.start)}–{formatClock(value.end)}</Button>)}</div></section>)}</div>}
      </>}
      <Form.Item className="customer-link-details" name="reason" label="Lý do" rules={[
        { required: true, whitespace: true, message: 'Vui lòng nhập Lý do.' }, { max: 500, message: 'Lý do không được vượt quá 500 ký tự.' },
      ]}><Input.TextArea rows={3} maxLength={500} showCount /></Form.Item>
      <Typography.Paragraph type="secondary">Không ghi thông tin bệnh lý trong lý do.</Typography.Paragraph>
      <Space className="customer-change-actions" wrap><Button onClick={onClose}>Đóng</Button><Button type="primary" htmlType="submit"
        disabled={Boolean(conflict) || (moving && (!dentistId || !slot || slotsBusy || dentistsBusy))}>Xem lại</Button></Space>
    </Form>}
  </Modal>;
}
