import { Alert, Button, Descriptions, Space, Spin, Tag, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../services/api';
import { arrivalClock, purposeLabels, visitLabels, type VisitResult } from './types';

// Shared, authorized reception handoff. Examination commands/content are owned by DCMS-112.
export default function VisitPage() {
  const { visitId } = useParams(); const [visit, setVisit] = useState<VisitResult>();
  const [error, setError] = useState<string>(); const [revision, setRevision] = useState(0);
  useEffect(() => { let active = true; setVisit(undefined); setError(undefined);
    api<VisitResult>(`/api/visits/${visitId}`).then(value => { if (active) setVisit(value); }).catch(e => { if (active) setError((e as Error).message); });
    return () => { active = false; };
  }, [visitId, revision]);
  return <><Space wrap><Typography.Title level={2}>Lượt khám</Typography.Title><Link to="/reception">Về hàng chờ</Link><Button onClick={() => setRevision(x => x + 1)}>Tải lại</Button></Space>
    {error ? <Alert type="error" showIcon message={error} /> : !visit ? <Spin /> : <>
      <Typography.Title level={3}>{visit.patientCode} · {visit.patientName}</Typography.Title>
      <Tag color={visit.status === 'Queued' ? 'blue' : undefined}>{visitLabels[visit.status] ?? visit.status}</Tag>
      <Descriptions bordered column={{ xs: 1, sm: 2 }} style={{ marginTop: 16 }} items={[
        { key: 'appointment', label: 'Lịch hẹn', children: visit.appointmentCode }, { key: 'branch', label: 'Chi nhánh', children: visit.branchName },
        { key: 'dentist', label: 'Bác sĩ', children: visit.dentistName }, { key: 'arrival', label: 'Giờ đến', children: arrivalClock(visit.arrivalAt) },
        { key: 'purpose', label: 'Lý do đến', children: purposeLabels[visit.purpose] ?? visit.purpose },
        { key: 'note', label: 'Ghi chú tiếp nhận', children: visit.note ?? 'Không có' },
        { key: 'source', label: 'Đợt điều trị', children: visit.source?.label ?? 'Bác sĩ xác nhận khi khám' },
        { key: 'recorded', label: 'Giờ ghi nhận', children: arrivalClock(visit.recordedAt) },
      ]} />
      {visit.source?.blocker && <Alert type="warning" message={visit.source.blocker} />}
      <Alert style={{ marginTop: 20 }} type="info" message="Thông tin tiếp nhận của lượt hiện tại"
        description="Chức năng bắt đầu khám và lưu kết quả khám chưa được tích hợp vào màn này." />
    </>}
  </>;
}
