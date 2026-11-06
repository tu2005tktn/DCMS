import { Alert, Button, Spin, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../services/api';
import type { ExaminationContent } from './types';
import DossierList from '../treatment/DossierList';

type Handoff = { patientId: number; branchId: number; visitId: string; version: number; examinationVersion: number; visitStatus: string; examination: ExaminationContent };
export default function ExaminationTreatmentHandoff({ patientId, visitId, version }: { patientId: number; visitId: string; version: string | null }) {
  const [source, setSource] = useState<Handoff>(); const [error, setError] = useState<string>(); const [retry, setRetry] = useState(0);
  useEffect(() => {
    let current = true; setSource(undefined); setError(undefined);
    api<Handoff>(`/api/visits/${visitId}/clinical-handoff`).then(value => {
      if (!current) return;
      if (value.patientId !== patientId || value.visitId !== visitId || version && Number(version) !== value.examinationVersion) {
        setError('Nguồn khám đã thay đổi hoặc không khớp bệnh nhân. Mở lại phiếu khám để kiểm tra trước khi chuyển tiếp.'); return;
      }
      setSource(value);
    }).catch(e => { if (current) setError((e as Error).message); });
    return () => { current = false; };
  }, [patientId, visitId, version, retry]);
  if (error) return <Alert type="error" showIcon message={error} description={<Link to={`/visits/${visitId}`}>Mở lại phiếu khám</Link>} action={<Button onClick={() => setRetry(r => r + 1)}>Thử lại</Button>} />;
  if (!source) return <Spin />;
  if(source.visitStatus !== 'Completed') return <Alert type="info" showIcon message="Cần kết thúc lượt khám trước khi lập kế hoạch điều trị từ phiếu này." description={<Link to={`/visits/${visitId}`}>Về lượt khám</Link>}/>;
  return <><Alert type="info" showIcon message={`Chuyển từ phiếu khám · phiên bản ${source.examinationVersion}`}
    description={<><Typography.Paragraph>{source.examination.diagnosis}</Typography.Paragraph><Link to={`/visits/${visitId}`}>Xem kết quả khám gốc</Link></>} />
    <DossierList patientId={patientId} visitId={visitId} sourceVisitVersion={source.version} diagnosis={source.examination.diagnosis} branchId={source.branchId} />
  </>;
}
