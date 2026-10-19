import { Alert, Button, Card, Descriptions, Drawer, Empty, Form, Input, Modal, Select, Skeleton, Space, Tag, Timeline, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, ApiError } from '../../services/api';
import BookingDrawer, { type BookingPrefill } from '../appointments/BookingDrawer';
import type { AppointmentOptions, AppointmentResult } from '../appointments/types';
import { careApi } from './care.api';
import {
  channelLabels, escalationColors, escalationLabels, flagColors, flagLabels, formatStamp, newRequestKey, resultLabels, statusColors,
  statusLabels, typeLabels, type CareOptions, type CareTaskDetail, type CareTaskStatus, type ContactChannel, type ContactResult,
} from './types';

type AttemptValues = { attemptedAt: string; channel: ContactChannel; result: ContactResult; notes?: string; reportedSymptoms?: string; nextContactAt?: string };
type EscalationValues = { clinicianId: number; reason: string };
const NOTE_MAX = 1000;
const localNow = () => new Date(Date.now() - new Date().getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
const toIso = (local?: string) => local ? new Date(local).toISOString() : null;

// Shared S25/S60 task detail. It always re-reads the task by taskId, so both screens show the same history, owner and
// status. Every command carries expectedVersion and a requestKey that is reused for a retry after a lost response.
export default function CareTaskDrawer({ taskId, options, onClose, onChanged }: {
  taskId: string | null; options: CareOptions | null; onClose: () => void; onChanged: () => void;
}) {
  const [task, setTask] = useState<CareTaskDetail | null>(null);
  const [loadError, setLoadError] = useState<ApiError | null>(null);
  const [actionError, setActionError] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [booked, setBooked] = useState<AppointmentResult | null>(null);
  const [booking, setBooking] = useState<BookingPrefill | null>(null);
  const [appointmentOptions, setAppointmentOptions] = useState<AppointmentOptions | null>(null);
  const [attemptForm] = Form.useForm<AttemptValues>();
  const [escalationForm] = Form.useForm<EscalationValues>();
  const [modal, modalContext] = Modal.useModal();
  const keys = useRef<Record<string, string>>({});

  async function load() {
    if (!taskId) return;
    setLoadError(null);
    try { setTask(await careApi.detail(taskId)); } catch (e) { setTask(null); setLoadError(e as ApiError); }
  }

  useEffect(() => {
    setTask(null); setActionError(null); setBooked(null); keys.current = {};
    attemptForm.resetFields(); escalationForm.resetFields();
    attemptForm.setFieldsValue({ attemptedAt: localNow(), channel: 'Phone', result: 'Contacted' });
    void load();
  }, [taskId]);

  // A command keeps its key until it succeeds; a fresh key is only issued after a definite answer from the server.
  async function run(kind: string, action: (key: string) => Promise<CareTaskDetail>, after?: () => void) {
    if (!task || busy) return;
    keys.current[kind] ??= newRequestKey();
    setBusy(kind); setActionError(null);
    try {
      setTask(await action(keys.current[kind]));
      delete keys.current[kind];
      after?.();
      onChanged();
    } catch (e) {
      const error = e as ApiError;
      setActionError(error);
      if (error.status !== 0) delete keys.current[kind];
    } finally { setBusy(null); }
  }

  async function saveAttempt() {
    let v: AttemptValues;
    try { v = await attemptForm.validateFields(); } catch { return; }
    if (v.result === 'Rebooked' && !booked) { attemptForm.setFields([{ name: 'result', errors: ['Chỉ chọn "Đã đặt lại lịch" sau khi lịch hẹn đã được lưu.'] }]); return; }
    void run('attempt', key => careApi.recordAttempt(task!.taskId, {
      expectedVersion: task!.version, requestKey: key, attemptedAt: new Date(v.attemptedAt).toISOString(), channel: v.channel, result: v.result,
      notes: v.notes?.trim() || null, reportedSymptoms: v.reportedSymptoms?.trim() || null, nextContactAt: toIso(v.nextContactAt),
      appointmentId: v.result === 'Rebooked' ? booked!.appointmentId : null,
    }), () => { attemptForm.resetFields(); attemptForm.setFieldsValue({ attemptedAt: localNow(), channel: 'Phone', result: 'Contacted' }); setBooked(null); });
  }

  async function saveEscalation() {
    let v: EscalationValues;
    try { v = await escalationForm.validateFields(); } catch { return; }
    void run('escalate', key => careApi.escalate(task!.taskId, { expectedVersion: task!.version, requestKey: key, clinicianId: v.clinicianId, reason: v.reason.trim() }),
      () => escalationForm.resetFields());
  }

  function changeStatus(status: CareTaskStatus) {
    const apply = (reason: string | null) => run(`status-${status}`, key =>
      careApi.changeStatus(task!.taskId, { expectedVersion: task!.version, requestKey: key, status, reason }));
    if (status === 'InProgress') { void apply(null); return; }
    let reason = '';
    modal.confirm({
      title: status === 'Done' ? 'Đánh dấu việc chăm sóc đã xong?' : 'Hủy việc chăm sóc này?',
      content: <>
        <Typography.Paragraph>Chỉ đóng việc chăm sóc; không hoàn thành điều trị, chỉ dẫn hay lịch hẹn lâm sàng.</Typography.Paragraph>
        <Input.TextArea rows={2} maxLength={500} placeholder={status === 'Cancelled' ? 'Lý do hủy (bắt buộc)' : 'Ghi chú (không bắt buộc)'}
          onChange={e => { reason = e.target.value; }} />
      </>,
      okText: status === 'Done' ? 'Đánh dấu xong' : 'Hủy việc', cancelText: 'Quay lại',
      onOk: async () => {
        if (status === 'Cancelled' && !reason.trim()) throw new Error('Vui lòng nhập Lý do hủy.');
        await apply(reason.trim() || null);
      },
    });
  }

  async function openRebook() {
    if (!task) return;
    try { setAppointmentOptions(appointmentOptions ?? await api<AppointmentOptions>('/api/appointments/options')); }
    catch (e) { setActionError(e as ApiError); return; }
    const today = new Date(Date.now() + 7 * 3_600_000).toISOString().slice(0, 10);
    setBooking({ branchId: task.branchId, date: today, patient: { id: task.patient.id, patientCode: task.patient.patientCode,
      fullName: task.patient.fullName, birthDate: task.patient.birthDate ?? null } });
  }

  const can = (a: string) => task?.allowedActions.includes(a as never) ?? false;
  const result = Form.useWatch('result', attemptForm);
  const clinicians = options?.clinicians.filter(c => !task || c.branchId === task.branchId) ?? [];
  const stale = actionError?.code === 'MSG111' || actionError?.status === 409;

  return <Drawer open={taskId !== null} onClose={onClose} width={760} destroyOnClose title={task?.title ?? 'Việc chăm sóc'}
    extra={task && <Space>
      {can('start') && task.status === 'Open' && <Button loading={busy === 'status-InProgress'} onClick={() => changeStatus('InProgress')}>Bắt đầu xử lý</Button>}
      {can('done') && <Button type="primary" onClick={() => changeStatus('Done')}>Đánh dấu xong</Button>}
      {can('cancel') && <Button danger onClick={() => changeStatus('Cancelled')}>Hủy việc</Button>}
    </Space>}>
    {modalContext}
    {loadError ? <Alert type="error" showIcon message={loadError.status === 403 ? 'Bạn không có quyền xem việc chăm sóc này.' : loadError.message}
      action={<Button size="small" onClick={() => void load()}>Thử lại</Button>} />
      : !task ? <Skeleton active /> : <>
        {actionError && <Alert className="form-alert" type="error" showIcon message={actionError.message}
          description={stale ? 'Việc này vừa được cập nhật ở nơi khác. Nội dung bạn nhập vẫn được giữ; tải lại để xem bản mới rồi lưu lại.' : undefined}
          action={stale ? <Button size="small" onClick={() => void load()}>Tải lại việc</Button> : undefined} />}
        <Descriptions bordered size="small" column={{ xs: 1, md: 2 }} items={[
          { label: 'Trạng thái', children: <Tag color={statusColors[task.status]}>{statusLabels[task.status]}</Tag> },
          { label: 'Loại', children: typeLabels[task.type] ?? task.type },
          { label: 'Bệnh nhân', children: <Link to={`/patients/${task.patient.id}`}>{task.patient.patientCode} · {task.patient.fullName}</Link> },
          { label: 'Chi nhánh', children: task.branchName },
          { label: 'Người phụ trách', children: task.owner?.name ?? 'Chưa giao' },
          { label: 'Đến hạn', children: formatStamp(task.dueAt) },
          { label: 'Tình trạng', span: 2, children: task.flags.length ? task.flags.map(f => <Tag key={f} color={flagColors[f]}>{flagLabels[f]}</Tag>) : '—' },
          { label: 'Nội dung', span: 2, children: task.content || '—' },
        ]} />

        <Card size="small" title="Nguồn" className="patient-section">
          {task.source.instruction && <div>Chỉ dẫn: {task.source.instruction.label} <Tag>{task.source.instruction.status}</Tag></div>}
          {task.source.activity && <div>Hoạt động điều trị: {task.source.activity.label}</div>}
          {task.source.appointment && <div>Lịch hẹn: {task.source.appointment.code} · {formatStamp(task.source.appointment.startAt)} <Tag>{task.source.appointment.status}</Tag></div>}
          {!task.source.instruction && !task.source.activity && !task.source.appointment && <Typography.Text type="secondary">Không có nguồn liên kết.</Typography.Text>}
        </Card>

        {task.aftercare && <Card size="small" title="Chăm sóc sau điều trị" className="patient-section">
          <div>Nha sĩ yêu cầu: {task.aftercare.requesterDentist}</div>
          <div>Lượt khám/hoạt động nguồn: {task.aftercare.sourceVisit}</div>
          <div>Hạn đã chọn: {formatStamp(task.aftercare.selectedDue)}</div>
          <div>Câu hỏi đã được duyệt:</div>
          <ol className="patient-changes">{task.aftercare.questions.map(q => <li key={q}>{q}</li>)}</ol>
          <Typography.Text type="secondary">Ghi nguyên văn điều bệnh nhân báo; không gợi ý chẩn đoán hay đổi thuốc. Cần bác sĩ xem thì gửi chuyển bác sĩ bên dưới.</Typography.Text>
        </Card>}

        {can('recordAttempt') && <Card size="small" title="Ghi lần liên hệ" className="patient-section"
          extra={can('rebook') && <Button size="small" onClick={() => void openRebook()}>Đặt lại lịch (S11)</Button>}>
          {booked && <Alert className="form-alert" type="success" showIcon message={`Đã lưu lịch hẹn ${booked.code}. Có thể chọn kết quả "Đã đặt lại lịch".`} />}
          <Form<AttemptValues> form={attemptForm} layout="vertical" disabled={busy !== null}>
            <div className="patient-form-grid">
              <Form.Item name="attemptedAt" label="Thời điểm liên hệ" rules={[{ required: true, message: 'Vui lòng nhập Thời điểm liên hệ.' }]}>
                <Input type="datetime-local" max={localNow()} /></Form.Item>
              <Form.Item name="channel" label="Kênh"><Select options={(Object.keys(channelLabels) as ContactChannel[]).map(v => ({ value: v, label: channelLabels[v] }))} /></Form.Item>
              <Form.Item name="result" label="Kết quả" rules={[{ required: true, message: 'Vui lòng nhập Kết quả.' }]}>
                <Select options={(Object.keys(resultLabels) as ContactResult[]).map(v => ({ value: v, label: resultLabels[v],
                  disabled: v === 'Rebooked' && !booked }))} /></Form.Item>
              <Form.Item name="nextContactAt" label="Liên hệ lại lúc (không phải lịch hẹn)"><Input type="datetime-local" min={localNow()} /></Form.Item>
            </div>
            <Form.Item name="notes" label="Ghi chú" rules={[{ max: NOTE_MAX, message: `Ghi chú không được vượt quá ${NOTE_MAX} ký tự.` }]}>
              <Input.TextArea rows={2} maxLength={NOTE_MAX} showCount /></Form.Item>
            {task.aftercare && <Form.Item name="reportedSymptoms" label="Triệu chứng bệnh nhân tự báo (ghi nguyên văn)"
              rules={[{ max: NOTE_MAX, message: `Nội dung không được vượt quá ${NOTE_MAX} ký tự.` }]}><Input.TextArea rows={2} maxLength={NOTE_MAX} showCount /></Form.Item>}
            <Button type="primary" loading={busy === 'attempt'} onClick={() => void saveAttempt()}>Lưu lần liên hệ</Button>
            {result === 'Rebooked' && booked && <Typography.Text type="secondary" style={{ marginLeft: 12 }}>Gắn lịch {booked.code}</Typography.Text>}
          </Form>
        </Card>}

        <Card size="small" title={`Các lần liên hệ (${task.attempts.length})`} className="patient-section">
          {task.attempts.length === 0 ? <Empty description="Chưa có lần liên hệ nào." /> : <Timeline items={task.attempts.map(a => ({ key: a.id, children: <>
            <Typography.Text strong>{resultLabels[a.result] ?? a.result}</Typography.Text> · {channelLabels[a.channel] ?? a.channel} · {formatStamp(a.attemptedAt)}
            <div><Typography.Text type="secondary">Ghi bởi {a.actorName} lúc {formatStamp(a.recordedAt)}</Typography.Text></div>
            {a.notes && <div>Ghi chú: {a.notes}</div>}
            {a.reportedSymptoms && <div>Bệnh nhân báo: “{a.reportedSymptoms}”</div>}
            {a.appointmentCode && <div>Lịch hẹn đã đặt: {a.appointmentCode}</div>}
            {a.nextContactAt && <div>Liên hệ lại: {formatStamp(a.nextContactAt)}</div>}
          </> }))} />}
        </Card>

        <Card size="small" title="Chuyển bác sĩ xem" className="patient-section">
          {task.escalations.map(e => <div key={e.id} className="care-escalation">
            <Tag color={escalationColors[e.status]}>{escalationLabels[e.status]}</Tag> {e.clinicianName} · gửi {formatStamp(e.createdAt)}
            <div>Lý do: {e.reason}</div>
            {e.acknowledgedAt && <div>Tiếp nhận: {e.acknowledgedBy ?? '—'} · {formatStamp(e.acknowledgedAt)}</div>}
            {e.resolvedAt && <div>Xử lý xong: {formatStamp(e.resolvedAt)}{e.evidence ? ` · ${e.evidence}` : ''}</div>}
          </div>)}
          {can('escalate') ? <Form<EscalationValues> form={escalationForm} layout="vertical" disabled={busy !== null}>
            <div className="patient-form-grid">
              <Form.Item name="clinicianId" label="Bác sĩ" rules={[{ required: true, message: 'Vui lòng nhập Bác sĩ.' }]}>
                <Select options={clinicians.map(c => ({ value: c.id, label: c.name }))} /></Form.Item>
              <Form.Item name="reason" label="Lý do" rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập Lý do.' },
                { max: 500, message: 'Lý do không được vượt quá 500 ký tự.' }]}><Input.TextArea rows={2} maxLength={500} /></Form.Item>
            </div>
            <Button loading={busy === 'escalate'} onClick={() => void saveEscalation()}>Gửi bác sĩ xem</Button>
            <Typography.Paragraph type="secondary" className="patient-section">Trạng thái do bác sĩ cập nhật trên hệ thống; trợ lý không đánh dấu thay.</Typography.Paragraph>
          </Form> : task.escalations.length === 0 && <Typography.Text type="secondary">Chưa có yêu cầu nào.</Typography.Text>}
        </Card>
      </>}
    <BookingDrawer open={booking !== null} prefill={booking} options={appointmentOptions} onClose={() => setBooking(null)}
      onBooked={r => { setBooking(null); setBooked(r); attemptForm.setFieldsValue({ result: 'Rebooked' }); }} />
  </Drawer>;
}
