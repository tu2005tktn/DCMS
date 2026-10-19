import { Alert, Card, Descriptions, Skeleton, Typography } from 'antd';
import { useEffect, useMemo, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { ApiError } from '../../services/api';
import { PreviewBanner } from '../ui-preview/preview';
import DocumentList, { type DocumentFilters } from './DocumentList';
import { documentsApi } from './documents.api';
import { formatDay, type DocumentType, type PatientDocumentContexts } from './types';

// Loads the Patient header and selectable contexts once; shared by the S20 page and the profile tab.
export function usePatientDocumentContexts(patientId: number) {
  const [contexts, setContexts] = useState<PatientDocumentContexts | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  useEffect(() => {
    let active = true;
    setContexts(null); setError(null);
    documentsApi.contexts(patientId).then(c => { if (active) setContexts(c); }).catch(e => { if (active) setError(e as ApiError); });
    return () => { active = false; };
  }, [patientId]);
  return { contexts, error };
}

// S20 page: /patients/:id/documents?visitId=&dossierId=&planId=&investigationId=&consentVersion=&type=&from=&to=
// Filters and context live in the URL so they survive going back and forth. The server rejects any context that does
// not belong to the Patient; the UI never treats such a request as success.
export default function PatientDocumentsPage() {
  const { id } = useParams();
  const patientId = Number(id);
  const [search, setSearch] = useSearchParams();
  const { contexts, error } = usePatientDocumentContexts(patientId);
  const filters: DocumentFilters = useMemo(() => ({
    type: (search.get('type') as DocumentType) || undefined, from: search.get('from') || undefined,
    to: search.get('to') || undefined, visitId: search.get('visitId') || undefined,
  }), [search]);
  const uploadContext = { visitId: search.get('visitId') || undefined, dossierId: search.get('dossierId') || undefined,
    planId: search.get('planId') || undefined, investigationId: search.get('investigationId') || undefined,
    consentVersion: search.get('consentVersion') || undefined };

  function changeFilters(next: DocumentFilters) {
    const params = new URLSearchParams(search);
    for (const key of ['type', 'from', 'to', 'visitId'] as const) {
      if (next[key]) params.set(key, next[key]!); else params.delete(key);
    }
    setSearch(params, { replace: true });
  }

  const visit = contexts?.visits.find(v => v.id === uploadContext.visitId);
  const dossier = contexts?.dossiers.find(d => d.id === uploadContext.dossierId);
  const plan = contexts?.plans.find(p => p.id === uploadContext.planId);

  return <>
    <Typography.Paragraph><Link to={`/patients/${patientId}`}>← Hồ sơ bệnh nhân</Link></Typography.Paragraph>
    <Typography.Title level={2}>Hình ảnh và tài liệu</Typography.Title>
    <PreviewBanner />
    <Card className="patient-banner">
      {error ? <Alert type="error" showIcon message={error.status === 403 ? 'Bạn không có quyền xem tài liệu của bệnh nhân này.'
        : error.status === 404 ? 'Chưa kết nối được API tài liệu (DCMS-95) hoặc không tìm thấy bệnh nhân.' : error.message} />
        : !contexts ? <Skeleton active paragraph={{ rows: 1 }} />
        : <Descriptions size="small" column={{ xs: 1, md: 3 }} items={[
          { label: 'Mã bệnh nhân', children: contexts.patient.patientCode },
          { label: 'Họ tên', children: contexts.patient.fullName },
          { label: 'Ngày sinh', children: contexts.patient.birthDate ? formatDay(contexts.patient.birthDate) : 'Chưa rõ' },
          ...(visit ? [{ label: 'Lượt khám', children: visit.label }] : []),
          ...(dossier ? [{ label: 'Hồ sơ điều trị', children: dossier.label }] : []),
          ...(plan ? [{ label: 'Kế hoạch', children: plan.label }] : []),
          ...(uploadContext.investigationId ? [{ label: 'Chỉ định cận lâm sàng', children: uploadContext.investigationId }] : []),
          ...(uploadContext.consentVersion ? [{ label: 'Phiên bản đồng thuận', children: uploadContext.consentVersion }] : []),
        ]} />}
    </Card>
    <Card>
      <DocumentList patientId={patientId} contexts={contexts} filters={filters} onFiltersChange={changeFilters} uploadContext={uploadContext} />
    </Card>
  </>;
}
