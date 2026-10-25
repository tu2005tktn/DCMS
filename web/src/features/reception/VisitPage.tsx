import { Alert, Button, Card, Descriptions, Form, Input, Select, Space, Spin, Table, Tag, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, ApiError } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import { clinicTime, failureText, purposeLabels, visitLabels, type ReceptionView, type Visit, type VisitHistory } from './types';

export default function VisitPage() {
  const { id } = useParams();
  const { user } = useAuth();
  const [visit, setVisit] = useState<Visit>();
  const [history, setHistory] = useState<VisitHistory[]>([]);
  const [queue, setQueue] = useState<Visit[]>([]);
  const [error, setError] = useState<string>();
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [conflict, setConflict] = useState(false);
  const [reason, setReason] = useState('');
  const [beforeVisitId, setBeforeVisitId] = useState<string>();
  const [priority, setPriority] = useState('Urgent');
  const pending = useRef<{ payload: string; key: string } | null>(null);
  async function reload() {
    setLoading(true); setError(undefined);
    try {
      const current = await api<Visit>(`/api/reception/visits/${id}`);
      setVisit(current);
      const date = new Date(Date.parse(current.appointmentAt) + 7 * 3600_000).toISOString().slice(0, 10);
      const [h, view] = await Promise.all([api<VisitHistory[]>(`/api/reception/visits/${id}/history`),
        api<ReceptionView>(`/api/reception?branchId=${current.branchId}&date=${date}&visitStatus=Queued`)]);
      setHistory(h); setQueue(view.queue); setConflict(false);
    } catch (e) { setError(failureText(e)); }
    finally { setLoading(false); }
  }
  useEffect(() => { void reload(); }, [id]);
  async function command(action: 'reorder' | 'priority') {
    if (!visit || busy || !reason.trim() || conflict) return;
    const body = { expectedVersion: visit.version, reason: reason.trim(), ...(action === 'reorder' ? { beforeVisitId: beforeVisitId ?? null } : { priority }) };
    const payload = JSON.stringify({ action, body });
    if (pending.current?.payload !== payload) pending.current = { payload, key: crypto.randomUUID() };
    setBusy(true); setError(undefined);
    try {
      await api<Visit>(`/api/reception/visits/${id}/${action}`, { method: 'POST', body: JSON.stringify({ ...body, requestKey: pending.current.key }) });
      setReason(''); pending.current = null; await reload();
    } catch (e) { setError(failureText(e)); if (e instanceof ApiError && e.status === 409) setConflict(true); }
    finally { setBusy(false); }
  }
  if (!visit && loading) return <Spin aria-label="Đang tải lượt khám" />;
  return <Space direction="vertical" size="large" style={{ width: '100%' }}>
    <Space><Link to="/reception">← Tiếp nhận và hàng chờ</Link><Button onClick={() => void reload()} loading={loading}>Đọc lại lượt khám</Button></Space>
    {error && <Alert type="error" showIcon message={error} />}
    {conflict && <Alert type="warning" message="Đọc lại lượt khám và đối chiếu phiên bản trước khi xác nhận. Lý do vừa nhập được giữ nguyên." />}
    {visit && <>
      <Typography.Title level={2}>Lượt khám {visit.visitCode}</Typography.Title>
      <Card><Descriptions bordered column={2} items={[
        { key: 'patient', label: 'Patient', children: <Link to={`/patients/${visit.patientId}`}>{visit.patientCode} · {visit.patientName}</Link> },
        { key: 'dentist', label: 'Bác sĩ', children: visit.dentistName },
        { key: 'appointment', label: 'Lịch hẹn', children: <Link to={`/appointments/${visit.appointmentId}`}>{visit.appointmentCode} · {visit.appointmentStatus}</Link> },
        { key: 'status', label: 'Trạng thái lượt', children: <Tag color="blue">{visitLabels[visit.visitStatus]}</Tag> },
        { key: 'slot', label: 'Giờ hẹn', children: clinicTime(visit.appointmentAt) }, { key: 'arrival', label: 'Giờ đến thực tế', children: clinicTime(visit.actualArrivalAt) },
        { key: 'entry', label: 'Thời điểm ghi nhận', children: clinicTime(visit.enteredAt) }, { key: 'queue', label: 'Hàng chờ', children: `${visit.queue.position || '—'} · ${visit.queue.waitingMinutes} phút` },
        { key: 'purpose', label: 'Mục đích', children: purposeLabels[visit.purpose] }, { key: 'version', label: 'Phiên bản Visit', children: visit.version },
        { key: 'source', label: 'Nguồn liên kết', span: 2, children: visit.sourceLinks.map(s => <div key={s.id}>{s.label}</div>) },
        { key: 'review', label: 'Lý do kiểm tra', children: visit.reviewReason ?? '—' }, { key: 'concern', label: 'Vấn đề bổ sung', children: visit.additionalConcern ?? '—' },
        { key: 'note', label: 'Ghi chú', children: visit.note ?? '—' }, { key: 'arrivalReason', label: 'Lý do hồi cứu', children: visit.arrivalReason ?? '—' },
        { key: 'priority', label: 'Ưu tiên lâm sàng', span: 2, children: visit.clinicalPriority ? `${visit.clinicalPriority} · bác sĩ #${visit.priorityDentistId}` : 'Chưa có quyết định ưu tiên' },
      ]} /></Card>
      {visit.blockers.map(b => <Alert key={b} type="warning" showIcon message={b} />)}
      <Alert type="info" message="Lượt khám đã được tiếp nhận. Mục đích và nguồn chờ bác sĩ xác nhận khi khám; việc tiếp nhận không phát sinh điều trị hay thu tiền." />
      {visit.visitStatus === 'Queued' && (user?.permissions.includes('reception.reorder') || user?.permissions.includes('reception.priority')) && <Card title="Điều chỉnh có lưu lịch sử">
        <Form layout="vertical"><Form.Item label="Lý do (bắt buộc)"><Input.TextArea aria-label="Lý do điều chỉnh" value={reason} maxLength={500} rows={2} onChange={e => setReason(e.target.value)} /></Form.Item>
          {user?.permissions.includes('reception.reorder') && <Space wrap>
            <Select aria-label="Đưa lên trước lượt" allowClear placeholder="Đưa xuống cuối hàng chờ" style={{ minWidth: 300 }} value={beforeVisitId} onChange={setBeforeVisitId}
              options={queue.filter(v => v.visitId !== visit.visitId).map(v => ({ value: v.visitId, label: `Trước ${v.patientCode} · ${v.patientName}` }))} />
            <Button onClick={() => void command('reorder')} loading={busy} disabled={!reason.trim() || conflict}>Lưu thứ tự hàng chờ</Button>
          </Space>}
          {user?.permissions.includes('reception.priority') && <Space wrap style={{ marginTop: 16 }}>
            <Select aria-label="Quyết định ưu tiên" value={priority} onChange={setPriority} options={[{ value: 'Urgent', label: 'Ưu tiên lâm sàng' }, { value: 'Routine', label: 'Thường quy' }]} />
            <Button onClick={() => void command('priority')} loading={busy} disabled={!reason.trim() || conflict}>Bác sĩ xác nhận ưu tiên</Button>
          </Space>}
        </Form>
      </Card>}
      <Card title="Lịch sử tiếp nhận và hàng chờ"><Table<VisitHistory> rowKey="id" size="small" dataSource={history} pagination={false} scroll={{ x: 700 }} columns={[
        { title: 'Thời điểm', render: (_, h) => clinicTime(h.occurredAt) }, { title: 'Thao tác', dataIndex: 'action' }, { title: 'Người thực hiện', dataIndex: 'actorName' },
        { title: 'Bác sĩ quyết định', render: (_, h) => h.decidingDentistId ?? '—' }, { title: 'Lý do', dataIndex: 'reason' },
      ]} /></Card>
    </>}
  </Space>;
}
