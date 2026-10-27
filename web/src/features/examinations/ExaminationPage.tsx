import { Alert, Button, Card, Checkbox, Descriptions, Form, Input, Modal, Select, Space, Spin, Tabs, Tag, Typography } from 'antd';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, ApiError } from '../../services/api';
import { arrivalClock, purposeLabels, visitLabels } from '../reception/types';
import DocumentList, { type DocumentFilters } from '../clinical-documents/DocumentList';
import { usePatientDocumentContexts } from '../clinical-documents/PatientDocumentsPage';
import type { DocumentItem } from '../clinical-documents/types';
import type { DossierPage } from '../treatment/types';
import DossierList from '../treatment/DossierList';
import type { ExaminationContent, History, Workspace } from './types';
import './examinations.css';

const rows = [[18,17,16,15,14,13,12,11,21,22,23,24,25,26,27,28], [48,47,46,45,44,43,42,41,31,32,33,34,35,36,37,38],
  [55,54,53,52,51,61,62,63,64,65], [85,84,83,82,81,71,72,73,74,75]];

export default function ExaminationPage() {
  const { visitId } = useParams(); const [w, setW] = useState<Workspace>(); const [history, setHistory] = useState<History[]>([]);
  const [dossiers, setDossiers] = useState<DossierPage>(); const [content, setContent] = useState<ExaminationContent>();
  const [error, setError] = useState<string>(); const [busy, setBusy] = useState(false); const [dirty, setDirty] = useState(false);
  const [tab, setTab] = useState('exam'); const [filters, setFilters] = useState<DocumentFilters>({});
  const [modal, modalContext] = Modal.useModal(); const pendingCommand = useRef<{ fingerprint: string; key: string } | undefined>(undefined);
  const { contexts, error: contextError } = usePatientDocumentContexts(w?.visit.patientId ?? 0);
  const lockedFilters = useMemo(() => ({ ...filters, visitId }), [filters, visitId]);
  useEffect(() => { let active = true; setW(undefined); setContent(undefined); setDirty(false); setError(undefined);
    api<Workspace>(`/api/visits/${visitId}/examination`).then(value => {
      if (active) { setW(value); setContent(value.examination?.content ?? value.initialContent); }
    }).catch(e => { if (active) setError((e as Error).message); }); return () => { active = false; };
  }, [visitId]);
  useEffect(() => { if (!w) return; let active = true;
    Promise.all([api<History[]>(`/api/visits/${visitId}/clinical-history`),
      w.allowedActions.length ? api<DossierPage>(`/api/patients/${w.visit.patientId}/dossiers?branchId=${w.visit.branchId}&page=1&pageSize=100`) : Promise.resolve(undefined)])
      .then(([h,d]) => { if (active) { setHistory(h); setDossiers(d); } }).catch(e => { if (active) setError((e as Error).message); });
    return () => { active = false; };
  }, [w, visitId]);
  useEffect(() => { const warn = (e: BeforeUnloadEvent) => { if (dirty) e.preventDefault(); }; window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn); }, [dirty]);
  function change(patch: Partial<ExaminationContent>) { setContent(c => ({ ...c!, ...patch })); setDirty(true); pendingCommand.current = undefined; }
  async function command(start: boolean, confirm = false) {
    if (!w || !content || busy) return; setBusy(true); setError(undefined);
    try {
      const payload = { patientId: w.visit.patientId, expectedVersion: w.visit.visitVersion, ...(start ? {} : { content, confirm }) };
      const fingerprint = JSON.stringify({ visitId, start, payload });
      if (pendingCommand.current?.fingerprint !== fingerprint) pendingCommand.current = { fingerprint, key: crypto.randomUUID() };
      const result = await api<Workspace>(`/api/visits/${visitId}/${start ? 'start' : 'examination'}`, { method: start ? 'POST' : 'PUT', body: JSON.stringify({
        ...payload, requestKey: pendingCommand.current.key,
      }) }); setW(result); setContent(result.examination?.content ?? result.initialContent); setDirty(false); pendingCommand.current = undefined;
    } catch (e) { setError((e as ApiError).code === 'MSG111' ? 'Lượt đã thay đổi. Bản nhập vẫn còn; tải lại phiên bản trước khi lưu tiếp.' : (e as Error).message); }
    finally { setBusy(false); }
  }
  async function reloadVersion() {
    if (busy) return; setBusy(true);
    try { const latest = await api<Workspace>(`/api/visits/${visitId}/examination`); setW(latest);
      if (!dirty) setContent(latest.examination?.content ?? latest.initialContent); pendingCommand.current = undefined; setError(undefined);
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  async function review(doc: DocumentItem) {
    setError(undefined);
    try { await api(`/api/clinical-documents/${doc.documentId}/review`, { method: 'POST', body: JSON.stringify({ patientId: w!.visit.patientId,
      visitId, expectedVersion: doc.version, requestKey: crypto.randomUUID() }) }); }
    catch (e) { setError((e as Error).message); throw e; }
  }
  function goBack(e: React.MouseEvent) { if (dirty) { e.preventDefault(); modal.confirm({ title: 'Nội dung chưa lưu', content: 'Lưu nháp trước khi rời lượt để giữ nội dung.', okText: 'Ở lại', cancelButtonProps: { style: { display: 'none' } } }); } }
  if (!w || !content) return error ? <Alert type="error" message={error} /> : <Spin />;
  const v = w.visit; const editable = w.allowedActions.includes('save'); const handoff = w.allowedActions.includes('handoff');
  const findings = content.findings;
  function addTooth(tooth: number) { if (!editable || findings.some(f => f.tooth === tooth)) return; change({ findings: [...findings, { tooth, condition: '' }] }); }
  return <>{modalContext}<Space wrap><Typography.Title level={2}>Khám và kết quả lượt hiện tại</Typography.Title>
    <Link to="/reception" onClick={goBack}>Về hàng chờ</Link><Button aria-label="Tải lại phiên bản" disabled={busy} loading={busy} onClick={() => void reloadVersion()}>Tải lại phiên bản</Button></Space>
    {error && <Alert role="alert" type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
    <Card><Typography.Title level={3}>{v.patientCode} · {v.patientName}</Typography.Title><Tag>{visitLabels[v.status] ?? v.status}</Tag>
      <Descriptions column={{ xs: 1, md: 3 }} items={[
        { label: 'Lịch hẹn', children: v.appointmentCode }, { label: 'Bác sĩ', children: v.dentistName }, { label: 'Chi nhánh', children: v.branchName },
        { label: 'Giờ đến', children: arrivalClock(v.arrivalAt) }, { label: 'Mục đích', children: purposeLabels[v.purpose] },
        { label: 'Thông tin tiếp nhận', children: v.arrivalReason ?? v.note ?? 'Chưa có' },
        { label: 'Hồ sơ cũ', children: v.source?.label ?? 'Chưa chọn hồ sơ' },
      ]} />{v.source?.blocker && <Alert type="warning" message={v.source.blocker} />}
      {w.allowedActions.includes('start') && <Button aria-label="Bắt đầu khám" type="primary" disabled={busy} loading={busy} onClick={() => void command(true)}>Bắt đầu khám</Button>}
    </Card>
    <Tabs activeKey={tab} onChange={setTab} items={[
      { key: 'exam', label: 'Khám & sơ đồ răng', children: <Card className={w.examination?.confirmed ? 'clinical-confirmed' : undefined}>
        {w.examination?.confirmed && <Alert type="success" message="Đã xác nhận kết quả khám" />}
        <Form layout="vertical" disabled={!editable || busy}>
          <Form.Item label="Lý do khám / triệu chứng"><Input.TextArea aria-label="Lý do khám / triệu chứng" value={content.symptoms ?? ''} maxLength={4000} onChange={e => change({ symptoms: e.target.value })} /></Form.Item>
          {(['medicalHistory','allergies','medications'] as const).map((name,i) => <Card size="small" key={name} title={['Tiền sử','Dị ứng','Thuốc đang dùng'][i]} className="clinical-fact">
            <Space wrap><Select aria-label={['Tình trạng tiền sử','Tình trạng dị ứng','Tình trạng thuốc'][i]} value={content[name].state} style={{ width: 190 }}
              options={[{ value: 'Unknown', label: 'Chưa biết' }, { value: 'None', label: 'Không có' }, { value: 'Present', label: 'Có' }]}
              onChange={state => change({ [name]: { ...content[name], state, detail: state === 'Present' ? content[name].detail : null } })} />
              <Checkbox checked={content[name].verified} onChange={e => change({ [name]: { ...content[name], verified: e.target.checked } })}>Đã xác minh {['tiền sử','dị ứng','thuốc'][i]}</Checkbox></Space>
            {content[name].state === 'Present' && <Input.TextArea aria-label={['Chi tiết tiền sử','Chi tiết dị ứng','Chi tiết thuốc'][i]} value={content[name].detail ?? ''} maxLength={4000}
              onChange={e => change({ [name]: { ...content[name], detail: e.target.value } })} />}
          </Card>)}
          <Typography.Title level={4}>Sơ đồ răng (FDI)</Typography.Title><div className="tooth-chart">{rows.map((row,i) => <div className="tooth-row" key={i}>{row.map(tooth =>
            <Button key={tooth} aria-label={`Răng ${tooth}`} type={findings.some(f => f.tooth === tooth) ? 'primary' : 'default'} onClick={() => addTooth(tooth)}>{tooth}</Button>)}</div>)}</div>
          <Button onClick={() => change({ findings: [...findings, { region: '', condition: '' }] })}>Thêm vùng khám</Button>
          {findings.map((f,i) => <Space key={i} align="start" className="finding-row">
            {f.tooth ? <Tag>Răng {f.tooth}</Tag> : <Input aria-label={`Vùng ${i+1}`} placeholder="Vùng khám" value={f.region ?? ''} maxLength={200}
              onChange={e => change({ findings: findings.map((x,n) => n === i ? { ...x, region: e.target.value } : x) })} />}
            <Input.TextArea aria-label={`Tình trạng ${i+1}`} placeholder="Tình trạng / kết quả khám" value={f.condition} maxLength={2000}
              onChange={e => change({ findings: findings.map((x,n) => n === i ? { ...x, condition: e.target.value } : x) })} />
            <Button onClick={() => change({ findings: findings.filter((_,n) => i !== n) })}>Bỏ</Button></Space>)}
          <Form.Item label="Chẩn đoán"><Input.TextArea aria-label="Chẩn đoán" value={content.diagnosis ?? ''} maxLength={4000} onChange={e => change({ diagnosis: e.target.value })} /></Form.Item>
          <Form.Item label="Ghi chú khám"><Input.TextArea aria-label="Ghi chú khám" value={content.notes ?? ''} maxLength={8000} onChange={e => change({ notes: e.target.value })} /></Form.Item>
        </Form>
        {editable && <Space><Button aria-label="Lưu nháp" disabled={busy} loading={busy} onClick={() => void command(false)}>Lưu nháp</Button>
          <Button aria-label="Xác nhận kết quả khám" type="primary" disabled={busy} loading={busy} onClick={() => void command(false,true)}>Xác nhận kết quả khám</Button></Space>}
        {handoff && <Space style={{ marginTop: 16 }}><Button type="primary" onClick={() => setTab('counseling')}>Chuyển sang tư vấn</Button>
          <Button onClick={() => setTab('treatment')}>Mở điều trị cũ</Button></Space>}
      </Card> },
      { key: 'documents', label: 'Ảnh & tài liệu', children: contextError ? <Alert type="error" message={contextError.message} /> :
        <DocumentList patientId={v.patientId} contexts={contexts} filters={lockedFilters} onFiltersChange={setFilters} uploadContext={{ visitId }}
          review={v.status === 'InProgress' && (editable || handoff) ? review : undefined} lockedVisitId={visitId} /> },
      { key: 'history', label: 'Lịch sử khám', children: history.length ? history.map(h => <Card key={h.visitId} title={arrivalClock(h.arrivalAt)}>
        <Tag>{h.examination.confirmed ? 'Đã xác nhận' : 'Nháp'}</Tag><Typography.Paragraph>{h.examination.content.diagnosis ?? 'Chưa chẩn đoán'}</Typography.Paragraph>
        {h.examination.content.findings.map((f,i) => <p key={i}>{f.tooth ? `Răng ${f.tooth}` : f.region}: {f.condition}</p>)}
      </Card>) : <Alert type="info" message="Chưa có kết quả khám trước trong chi nhánh." /> },
      { key: 'counseling', label: 'Tư vấn', disabled: !handoff, children: <><Alert type="info" message={`Kết quả khám: ${content.diagnosis ?? ''}`} /><DossierList patientId={v.patientId} visitId={visitId} sourceVisitVersion={w.visit.visitVersion} diagnosis={content.diagnosis} branchId={v.branchId} dentistId={v.dentistId} /></> },
      { key: 'treatment', label: 'Điều trị cũ', disabled: !handoff, children: <Card title="Tiếp tục hồ sơ đã có">
        {dossiers?.items.length ? dossiers.items.map(d => <p key={d.id}><strong>{d.code}</strong> · {d.diagnosis} · <Link to={`/patients/${v.patientId}/dossiers/${d.id}?visitId=${visitId}`}>Mở hồ sơ</Link>{' · '}
          <Link to={`/patients/${v.patientId}/visits/${visitId}/treatment?dossierId=${d.id}`}>Điều trị trong lượt này</Link></p>) : <Alert type="info" message="Chưa có hồ sơ điều trị cũ tại chi nhánh." />}
      </Card> },
    ]} />
  </>;
}
