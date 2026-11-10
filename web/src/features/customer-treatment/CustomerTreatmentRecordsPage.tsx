import { Alert, Button, Card, Descriptions, Empty, List, Modal, Spin, Tabs, Tag, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { ApiError } from '../../services/api';
import { customerApi } from '../../services/customerApi';
import { useCustomerAuth } from '../customer-auth/CustomerAuthContext';

type Patient = { patientCode: string; fullName: string; birthDate?: string; gender?: string; phone?: string; address?: string; guardianName?: string };
type Visit = { visitId: string; arrivalAt: string; branchName: string; dentistName: string; visitStatus: string; diagnosisSummary?: string };
type VisitDetail = { examination: { symptoms?: string; diagnoses: string[]; conclusion?: string; advice?: string;
  returnNeed?: string; disposition?: string; referralDestination?: string;
  findings: { tooth?: number; region?: string; condition: string; diagnoses: string[] }[] };
  investigations: { id: string; name: string; state: string; resultText?: string; interpretation?: string }[] };
type Document = { documentId: string; fileName: string; contentType: string; sourceDate: string };
type Prescription = { id: string; prescribedOn: string; diagnosis: string; prescriberName: string; items: { id: string; medicineName: string; quantity: number; unit: string; instructions: string }[] };
type Dossier = { dossierId: string; code: string; state: string; diagnosis: string; startedOn: string;
  agreedItems: { id: string; serviceName: string; region?: string; quantity: number; unit: string; progress: string }[];
  performedItems: { id: string; performedAt: string; serviceName: string; region?: string; quantity: number; unit: string; outcome?: string }[];
  payments: { id: string; paidOn: string; method: string; totalVnd: number }[] };
type Data = { patient: Patient; visits: Visit[]; documents: Document[]; prescriptions: Prescription[]; dossiers: Dossier[] };
const date = (value?: string) => value ? new Date(value).toLocaleDateString('vi-VN') : '—';
async function allPages<T>(path: string, token: string): Promise<T[]> {
  const items: T[] = [];
  for (let page = 1; ; page++) {
    const result = await customerApi<{ items: T[]; total: number }>(`${path}?page=${page}&pageSize=50`, {}, token);
    items.push(...result.items);
    if (!result.items.length || items.length >= result.total) return items;
  }
}

export default function CustomerTreatmentRecordsPage() {
  const { state, expire } = useCustomerAuth();
  const token = state.status === 'authenticated' ? state.accessToken : null;
  const [data, setData] = useState<Data | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);
  const [visitDetails, setVisitDetails] = useState<Record<string, VisitDetail>>({});
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  useEffect(() => {
    if (!token) return;
    let active = true;
    setData(null); setError(null);
    void Promise.all([
      customerApi<Patient>('/api/patients/me', {}, token),
      allPages<Visit>('/api/patients/me/visits', token),
      allPages<Document>('/api/patients/me/documents', token),
      customerApi<Prescription[]>('/api/patients/me/prescriptions', {}, token),
      customerApi<Dossier[]>('/api/patients/me/treatment-summary', {}, token),
    ]).then(([patient, visits, documents, prescriptions, dossiers]) => {
      if (active) setData({ patient, visits, documents, prescriptions, dossiers });
    }).catch((failure: unknown) => {
      if (!active) return;
      if (failure instanceof ApiError && failure.status === 401) expire();
      else setError(failure instanceof ApiError && failure.status === 403 ? 'Hãy xác minh liên kết hồ sơ để xem bệnh án.' : (failure as Error).message);
    });
    return () => { active = false; };
  }, [token, revision, expire]);

  async function openDocument(item: Document) {
    if (!token) return;
    try {
      const base = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080').replace(/\/$/, '');
      const response = await fetch(`${base}/api/patients/me/documents/${encodeURIComponent(item.documentId)}/content`,
        { headers: { Authorization: `Bearer ${token}` }, cache: 'no-store' });
      if (!response.ok) throw new Error('Không mở được hình ảnh.');
      const url = URL.createObjectURL(await response.blob());
      setPreviewUrl(url);
    } catch (failure) { setError((failure as Error).message); }
  }
  async function openVisit(id: string) {
    if (!token || visitDetails[id]) return;
    try {
      const detail = await customerApi<VisitDetail>(`/api/patients/me/visits/${encodeURIComponent(id)}`, {}, token);
      setVisitDetails(previous => ({ ...previous, [id]: detail }));
    } catch (failure) { setError((failure as Error).message); }
  }

  return <section className="customer-page" style={{ maxWidth: 1050, margin: '0 auto', padding: 24 }}>
    <Modal open={!!previewUrl} footer={null} onCancel={() => { if (previewUrl) URL.revokeObjectURL(previewUrl); setPreviewUrl(null); }}>
      {previewUrl && <img src={previewUrl} alt="Hình ảnh hồ sơ bệnh án" style={{ width: '100%' }} />}
    </Modal>
    <Typography.Title level={2}>Hồ sơ bệnh án</Typography.Title>
    {error && <Alert type="error" showIcon message={error} action={<Button onClick={() => setRevision(x => x + 1)}>Thử lại</Button>} />}
    {!error && !data && <Spin />}
    {data && <><Card style={{ marginBottom: 20 }}><Typography.Title level={4}>{data.patient.fullName}</Typography.Title>
      <Typography.Text type="secondary">Mã bệnh nhân: {data.patient.patientCode}</Typography.Text></Card>
      <Tabs items={[
        { key: 'basic', label: 'Thông tin cơ bản', children: <Card><Descriptions bordered size="small" column={{ xs: 1, sm: 2 }} items={[
          { key: 'code', label: 'Mã bệnh nhân', children: data.patient.patientCode },
          { key: 'name', label: 'Họ tên', children: data.patient.fullName },
          { key: 'birth', label: 'Ngày sinh', children: date(data.patient.birthDate) },
          { key: 'gender', label: 'Giới tính', children: data.patient.gender || '—' },
          { key: 'phone', label: 'Số điện thoại', children: data.patient.phone || '—' },
          { key: 'address', label: 'Địa chỉ', children: data.patient.address || '—' },
          { key: 'guardian', label: 'Người giám hộ', children: data.patient.guardianName || '—' },
        ]} /></Card> },
        { key: 'visits', label: 'Khám', children: <Card>{data.visits.length ? <List dataSource={data.visits} renderItem={visit =>
          <List.Item><div><strong>{date(visit.arrivalAt)} · {visit.branchName}</strong><div>Bác sĩ {visit.dentistName} · {visit.visitStatus}</div>
            {visit.diagnosisSummary && <div>Chẩn đoán: {visit.diagnosisSummary}</div>}
            <Button type="link" onClick={() => void openVisit(visit.visitId)}>Xem kết quả khám</Button>
            {visitDetails[visit.visitId] && <div>
              <div>Triệu chứng: {visitDetails[visit.visitId].examination.symptoms || '—'}</div>
              <div>Chẩn đoán: {visitDetails[visit.visitId].examination.diagnoses.join(', ') || '—'}</div>
              <div>Kết luận: {visitDetails[visit.visitId].examination.conclusion || '—'}</div>
              <div>Lời dặn: {visitDetails[visit.visitId].examination.advice || '—'}</div>
              <div>Tái khám: {visitDetails[visit.visitId].examination.returnNeed || '—'}</div>
              <div>Hướng xử trí: {visitDetails[visit.visitId].examination.disposition || '—'}</div>
              {visitDetails[visit.visitId].examination.referralDestination && <div>Chuyển khám: {visitDetails[visit.visitId].examination.referralDestination}</div>}
              {visitDetails[visit.visitId].examination.findings.map((finding, index) =>
                <div key={index}>{finding.tooth ? `Răng ${finding.tooth}` : finding.region || 'Vùng khám'}: {finding.condition}
                  {finding.diagnoses.length ? ` · ${finding.diagnoses.join(', ')}` : ''}</div>)}
              {visitDetails[visit.visitId].investigations.map(result => <div key={result.id}>{result.name}: {result.resultText || result.state}
                {result.interpretation ? ` · ${result.interpretation}` : ''}</div>)}
            </div>}
          </div></List.Item>} /> : <Empty description="Chưa có lượt khám" />}</Card> },
        { key: 'treatment', label: 'Điều trị & Thanh toán', children: data.dossiers.length ? data.dossiers.map(dossier =>
          <Card key={dossier.dossierId} title={`${dossier.code} · ${dossier.diagnosis}`} style={{ marginBottom: 16 }}
            extra={<Tag color={dossier.state === 'Open' ? 'blue' : 'green'}>{dossier.state === 'Open' ? 'Đang điều trị' : 'Đã kết thúc'}</Tag>}>
            <Typography.Text type="secondary">Bắt đầu {date(dossier.startedOn)}</Typography.Text>
            <Typography.Title level={5}>Hạng mục bác sĩ đã làm</Typography.Title>
            {dossier.performedItems?.length ? <List dataSource={dossier.performedItems} renderItem={item => <List.Item><div>
              <strong>{item.serviceName}{item.region ? ` · ${item.region}` : ''}</strong>
              <div>{date(item.performedAt)} · {item.quantity} {item.unit}</div>{item.outcome && <div>Kết quả: {item.outcome}</div>}
            </div></List.Item>} /> : <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chưa có hạng mục đã ghi nhận" />}
            <Typography.Title level={5}>Kế hoạch đã đồng ý</Typography.Title>
            {dossier.agreedItems?.length ? <List dataSource={dossier.agreedItems} renderItem={item =>
              <List.Item><span>{item.serviceName}{item.region ? ` · ${item.region}` : ''} · {item.quantity} {item.unit}</span>
                <Tag>{item.progress}</Tag></List.Item>} /> : <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chưa có hạng mục đã đồng ý" />}
            <Typography.Title level={5}>Thanh toán đã ghi</Typography.Title>
            {dossier.payments?.length ? <List dataSource={dossier.payments} renderItem={payment =>
              <List.Item><span>{date(payment.paidOn)} · {payment.method}</span><strong>{payment.totalVnd.toLocaleString('vi-VN')} đ</strong></List.Item>} />
              : <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chưa có khoản thanh toán trên phiếu điều trị" />}
          </Card>) : <Empty description="Chưa có hồ sơ điều trị đã xác nhận" /> },
        { key: 'images', label: 'Hình ảnh', children: <Card>{data.documents.filter(item => item.contentType.startsWith('image/')).length
          ? <List dataSource={data.documents.filter(item => item.contentType.startsWith('image/'))} renderItem={item =>
            <List.Item actions={[<Button key="open" onClick={() => void openDocument(item)}>Xem ảnh</Button>]}>{item.fileName} · {date(item.sourceDate)}</List.Item>} />
          : <Empty description="Chưa có hình ảnh đã công bố" />}</Card> },
        { key: 'rx', label: 'Đơn thuốc', children: <Card>{data.prescriptions.length ? <List dataSource={data.prescriptions} renderItem={rx =>
          <List.Item><div><strong>{date(rx.prescribedOn)} · Bác sĩ {rx.prescriberName}</strong><div>{rx.diagnosis}</div>
            {rx.items.map(item => <div key={item.id}>{item.medicineName} · {item.quantity} {item.unit} · {item.instructions}</div>)}</div></List.Item>} />
          : <Empty description="Chưa có đơn thuốc" />}</Card> },
      ]} />
    </>}
  </section>;
}
