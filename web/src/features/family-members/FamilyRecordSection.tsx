import { Alert, Button, Card, Collapse, Descriptions, Empty, Pagination, Spin, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { ApiError } from '../../services/api';
import { useCustomerAuth } from '../customer-auth/CustomerAuthContext';
import { familyDate, useFamilyApi } from './types';
import { recordDateTime, type RecordPage } from './recordTypes';

type Visit = { visitId: string; arrivalAt: string; dentistName: string; branchName: string; symptoms: string | null; diagnoses: string[]; conclusion: string | null; advice: string | null };
type Treatment = { id: string; code: string; diagnosis: string; state: string; startedOn: string; items: { serviceName: string; region: string | null; progress: string; estimatedStart: string | null; estimatedEnd: string | null }[] };
type Medicine = { medicineName: string; strength: string | null; unit: string; quantity: number; route: string; morningDose: string; noonDose: string; afternoonDose: string; eveningDose: string; mealTiming: string; days: number; instructions: string };
type Prescription = { id: string; finalizedAt: string | null; diagnosis: string | null; generalAdvice: string; lines: Medicine[] };
type RecordDocument = { id: string; fileName: string; documentType: string; sourceDate: string; sizeBytes: number };
type Booking = { code: string; startAt: string; status: string; branchName: string; dentistName: string };
const progressLabels: Record<string, string> = { NotStarted: 'Chưa thực hiện', InProgress: 'Đang thực hiện', Completed: 'Đã hoàn thành', Partial: 'Một phần', Cancelled: 'Đã hủy', Stopped: 'Đã dừng' };
const appointmentLabels: Record<string, string> = { Confirmed: 'Đã đặt', CheckedIn: 'Đã đến khám', Queued: 'Chờ khám', InProgress: 'Đang khám', Completed: 'Đã hoàn thành', Cancelled: 'Đã hủy', NoShow: 'Không đến' };
const meals: Record<string, string> = { BeforeMeal: 'Trước ăn', AfterMeal: 'Sau ăn', WithMeal: 'Cùng bữa ăn' };

export default function FamilyRecordSection({ memberId, section, denied }: { memberId: string; section: string; denied: () => void }) {
  const request = useFamilyApi(); const { state, expire } = useCustomerAuth();
  const [page, setPage] = useState(1), [revision, setRevision] = useState(0), [data, setData] = useState<RecordPage<unknown> | null>(null);
  const [busy, setBusy] = useState(true), [error, setError] = useState(''), [download, setDownload] = useState<string | null>(null);
  const downloadRequest = useRef<AbortController | null>(null);
  const base = `/${memberId}/${section === 'appointments' ? 'appointments' : `record/${section}`}`;
  const deniedRef = useRef(denied); deniedRef.current = denied;
  useEffect(() => () => { downloadRequest.current?.abort(); }, []);
  useEffect(() => {
    let active = true; const controller = new AbortController(); setBusy(true); setError(''); setData(null);
    void request<RecordPage<unknown>>(`${base}?page=${page}&pageSize=10`, { signal: controller.signal }).then(value => { if (active) setData(value); })
      .catch(failure => { if (active) { setError((failure as Error).message); if (failure instanceof ApiError && [403, 404].includes(failure.status)) deniedRef.current(); } })
      .finally(() => { if (active) setBusy(false); });
    return () => { active = false; controller.abort(); };
  }, [base, request, page, revision]);
  async function downloadFile(document: RecordDocument) {
    if (state.status !== 'authenticated' || download) return;
    const controller = new AbortController(); downloadRequest.current = controller;
    setDownload(document.id); setError('');
    try {
      const response = await fetch(`${(import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080').replace(/\/$/, '')}/api/me/family-members/${memberId}/record/documents/${document.id}/content`, {
        headers: { Authorization: `Bearer ${state.accessToken}` }, cache: 'no-store', signal: controller.signal,
      });
      if (!response.ok) {
        if (response.status === 401) expire();
        if ([403, 404].includes(response.status)) { setData(null); deniedRef.current(); }
        throw new Error('Không thể tải tài liệu. Quyền truy cập có thể đã thay đổi, vui lòng tải lại.');
      }
      const blob = await response.blob(); if (controller.signal.aborted) return;
      const url = URL.createObjectURL(blob);
      try { const anchor = window.document.createElement('a'); anchor.href = url; anchor.download = document.fileName; anchor.click(); }
      finally { URL.revokeObjectURL(url); }
    } catch (failure) { if (!controller.signal.aborted) setError((failure as Error).message); }
    finally { if (!controller.signal.aborted) setDownload(null); }
  }
  if (busy) return <Spin />;
  return <div className="family-record-section">
    {error && <Alert type="error" message={error} action={<Button onClick={() => setRevision(v => v + 1)}>Thử lại</Button>} />}
    {!error && !data?.items.length && <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={section === 'appointments' ? 'Chưa có lịch đặt hộ được liên kết với người thân này.' : 'Chưa có nội dung chính thức trong phạm vi đã cấp.'} />}
    {section === 'appointments' && (data?.items as Booking[] | undefined)?.map(item => <Card size="small" key={item.code}><Link to={`/customer/appointments/${item.code}`}>{item.code}</Link><p>{recordDateTime(item.startAt)} · {appointmentLabels[item.status] ?? item.status}</p><span>{item.branchName} · {item.dentistName}</span></Card>)}
    {section === 'examinations' && <Collapse items={(data?.items as Visit[] | undefined)?.map(item => ({ key: item.visitId, label: `${recordDateTime(item.arrivalAt)} · ${item.dentistName}`, children: <Descriptions column={1} items={[
      { label: 'Chi nhánh', children: item.branchName }, { label: 'Triệu chứng', children: item.symptoms || '—' }, { label: 'Chẩn đoán', children: item.diagnoses.join('; ') || '—' },
      { label: 'Kết luận', children: item.conclusion || '—' }, { label: 'Lời dặn', children: item.advice || '—' },
    ]} /> }))} />}
    {section === 'treatment' && (data?.items as Treatment[] | undefined)?.map(item => <Card size="small" key={item.id} title={`${item.code} · ${familyDate(item.startedOn)}`}><p>{item.diagnosis}</p>{item.items.map((line, index) => <p key={index}><strong>{line.serviceName}</strong>{line.region ? ` · ${line.region}` : ''} · {progressLabels[line.progress] ?? line.progress}<br />{familyDate(line.estimatedStart)} – {familyDate(line.estimatedEnd)}</p>)}</Card>)}
    {section === 'prescriptions' && (data?.items as Prescription[] | undefined)?.map(item => <Card size="small" key={item.id} title={`Đơn ngày ${recordDateTime(item.finalizedAt)}`}><p>{item.diagnosis}</p>{item.lines.map((line, index) => <div className="family-medicine-line" key={index}><Typography.Text strong>{line.medicineName} {line.strength} · {line.quantity} {line.unit}</Typography.Text><p>{line.route} · {line.days} ngày · Sáng {line.morningDose}, trưa {line.noonDose}, chiều {line.afternoonDose}, tối {line.eveningDose} · {meals[line.mealTiming]}</p><p>{line.instructions}</p></div>)}{item.generalAdvice && <p><strong>Lời dặn: </strong>{item.generalAdvice}</p>}</Card>)}
    {section === 'documents' && (data?.items as RecordDocument[] | undefined)?.map(item => <Card size="small" key={item.id}><strong>{item.fileName}</strong><p>{familyDate(item.sourceDate)} · {Math.ceil(item.sizeBytes / 1024)} KB</p><Button loading={download === item.id} disabled={download !== null && download !== item.id} onClick={() => void downloadFile(item)}>Tải tài liệu</Button></Card>)}
    {!!data?.total && <Pagination current={page} pageSize={10} total={data.total} showSizeChanger={false} onChange={setPage} />}
  </div>;
}
