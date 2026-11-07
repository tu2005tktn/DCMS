import { Alert, Button, Descriptions, Drawer, Empty, Input, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnType } from 'antd/es/table';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import './examinations.css';
import { api } from '../../services/api';
import { arrivalClock, visitLabels } from '../reception/types';
import { diagnosisText, dispositionLabels, findingText, type ExaminationContent, type PatientExamination } from './types';

function ClinicalSummary({ content: c }: { content: ExaminationContent }) {
  return <Descriptions size="small" bordered column={1} style={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }} items={[
    { key: 'symptoms', label: 'Lý do khám / triệu chứng', children: c.symptoms || 'Chưa ghi' },
    { key: 'medicalHistory', label: 'Tiền sử', children: c.medicalHistory?.detail || 'Chưa ghi' },
    { key: 'allergies', label: 'Dị ứng', children: c.allergies?.detail || 'Chưa ghi' },
    { key: 'medications', label: 'Thuốc đang dùng', children: c.medications?.detail || 'Chưa ghi' },
    { key: 'findings', label: 'Kết quả khám', children: c.findings.length ? c.findings.map((f, i) => <div key={i}>
      {f.tooth ? `Răng ${f.tooth}` : f.region || 'Vùng chưa ghi'}{f.surfaces?.length ? ` (${f.surfaces.join(', ')})` : ''}: {findingText(f)}
    </div>) : 'Chưa ghi kết quả theo răng / vùng' },
    { key: 'diagnosis', label: 'Chẩn đoán / kết luận', children: c.conclusion || diagnosisText(c) || 'Chưa ghi' },
    { key: 'advice', label: 'Dặn dò / lời khuyên', children: c.advice || 'Chưa ghi' },
    { key: 'disposition', label: 'Hướng xử trí', children: dispositionLabels[c.disposition ?? ''] || 'Chưa ghi' },
    ...(c.returnNeed ? [{ key: 'returnNeed', label: 'Nhu cầu quay lại', children: c.returnNeed }] : []),
    ...(c.referralDestination ? [{ key: 'referral', label: 'Nơi / lý do chuyển khám', children: c.referralDestination }] : []),
    { key: 'notes', label: 'Ghi chú', children: c.notes || 'Chưa ghi' },
  ]} />;
}

export default function PatientExaminationsTab({ patientId, active, refreshKey = 0 }: { patientId: number; active: boolean; refreshKey?: number }) {
  const [rows, setRows] = useState<PatientExamination[]>();
  const [error, setError] = useState<string>();
  const [revision, setRevision] = useState(0);
  const [keyword, setKeyword] = useState('');
  const [state, setState] = useState<string>();
  const [selected, setSelected] = useState<string>();
  useEffect(() => {
    if (!active) return;
    const refresh = () => { if (document.visibilityState === 'visible') setRevision(value => value + 1); };
    window.addEventListener('focus', refresh);
    document.addEventListener('visibilitychange', refresh);
    return () => { window.removeEventListener('focus', refresh); document.removeEventListener('visibilitychange', refresh); };
  }, [active]);
  useEffect(() => {
    setRows(undefined); setError(undefined);
    if (!active) return;
    let current = true;
    api<PatientExamination[]>(`/api/patients/${patientId}/examinations`).then(value => { if (current) setRows(value); })
      .catch(e => { if (current) setError((e as Error).message); });
    return () => { current = false; };
  }, [patientId, active, revision, refreshKey]);
  const visible = rows?.filter(r => (!state || (state === 'completed' ? r.visitStatus === 'Completed' && r.examination?.confirmed
    : state === 'new' ? !r.examination : state === 'final' ? r.examination?.confirmed : r.examination && !r.examination.confirmed))
    && `${r.appointmentCode} ${r.dentistName} ${r.examination ? diagnosisText(r.examination.content) : ''}`.toLocaleLowerCase('vi').includes(keyword.toLocaleLowerCase('vi')));
  const detail = rows?.find(row => row.visitId === selected);
  return <section aria-label="Phiếu khám của bệnh nhân">
    <Typography.Title level={4}>Lịch sử khám</Typography.Title>
    <Space wrap style={{ marginBottom: 16 }}>
      <Input aria-label="Tìm phiếu khám" placeholder="Lịch hẹn, bác sĩ hoặc chẩn đoán" value={keyword} onChange={e => setKeyword(e.target.value)} />
      <Select aria-label="Trạng thái phiếu khám" allowClear placeholder="Tất cả phiếu" style={{ width: 210 }} value={state} onChange={setState}
        options={[{ value: 'new', label: 'Chưa lập phiếu' }, { value: 'draft', label: 'Bản nháp' }, { value: 'final', label: 'Đã hoàn tất phiếu khám' }, { value: 'completed', label: 'Đã kết thúc lượt khám' }]} />
      <Button onClick={() => setRevision(r => r + 1)}>Tải lại phiếu khám</Button>
      <Link to="/reception">Mở hàng chờ tiếp nhận</Link>
    </Space>
    {error ? <Alert type="error" showIcon message={error} action={<Button onClick={() => setRevision(r => r + 1)}>Thử lại</Button>} /> :
      <Table rowKey="visitId" className="exam-history-table" loading={!rows} dataSource={visible} pagination={{ defaultPageSize: 10, showSizeChanger: true }}
        locale={{ emptyText: <Empty description="Chưa có lượt khám trong các chi nhánh được cấp quyền." /> }} columns={([
          { title: 'Nội dung', width: 90, render: (_, r: PatientExamination) => r.examination && <Button size="small"
            aria-label={`Xem nội dung phiếu ${r.appointmentCode}`} onClick={() => setSelected(r.visitId)}>Xem</Button> },
          { title: 'Ngày đến / lịch hẹn', render: (_, r: PatientExamination) => <>{arrivalClock(r.arrivalAt)}<br />{r.appointmentCode}</> },
          { title: 'Bác sĩ / chi nhánh', render: (_, r: PatientExamination) => <>{r.dentistName}<br /><Typography.Text type="secondary">{r.branchName}</Typography.Text></> },
          { title: 'Phiếu khám', render: (_, r: PatientExamination) => <><Tag color={r.examination?.confirmed ? 'green' : 'default'}>{!r.examination ? 'Chưa lập' : r.examination.confirmed ? 'Đã hoàn tất khám' : 'Bản nháp'}</Tag>{r.examination && <div>Phiên bản {r.examination.version ?? 0}</div>}</> },
          { title: 'Chẩn đoán / hướng xử trí', render: (_, r: PatientExamination) => <>{r.examination ? diagnosisText(r.examination.content) || 'Chưa có chẩn đoán' : 'Chưa có chẩn đoán'}<br />{dispositionLabels[r.examination?.content.disposition ?? '']}</> },
          { title: 'Lượt đến / kết thúc', render: (_, r: PatientExamination) => <><Tag color={r.visitStatus === 'Completed' ? 'green' : undefined}>{r.visitStatus === 'Completed' ? 'Đã kết thúc lượt khám' : visitLabels[r.visitStatus] ?? r.visitStatus}</Tag>
            {r.visitStatus === 'Completed' && <div>{r.completedAt ? arrivalClock(r.completedAt) : 'Chưa ghi thời điểm kết thúc'}</div>}</> },
          { title: 'Thao tác', render: (_, r: PatientExamination) => <Link to={`/visits/${r.visitId}`}>{r.visitStatus === 'Queued' ? 'Mở khám' : 'Xem phiếu khám'}</Link> },
        ] as ColumnType<PatientExamination>[]).map(c => ({ ...c, onCell: () => ({ 'data-label': String(c.title ?? '') } as object) }))} />}
    <Drawer title={`Nội dung khám · ${detail?.appointmentCode ?? ''}`} open={!!detail?.examination} onClose={() => setSelected(undefined)}
      width="min(720px, 100vw)">
      {detail?.examination && <><Typography.Paragraph>{detail.dentistName} · {detail.branchName}<br />{arrivalClock(detail.arrivalAt)}</Typography.Paragraph>
        <ClinicalSummary content={detail.examination.content} />
        <Typography.Paragraph style={{ marginTop: 16 }}><Link to={`/visits/${detail.visitId}`}>Mở phiếu khám gốc</Link></Typography.Paragraph></>}
    </Drawer>
    {rows?.length === 100 && <Typography.Paragraph type="secondary">Đang hiển thị 100 lượt gần nhất trong quyền truy cập.</Typography.Paragraph>}
  </section>;
}
