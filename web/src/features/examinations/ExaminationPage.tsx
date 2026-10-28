import {useAuth} from '../authentication/AuthContext';
import ExaminationFeePanel from '../finance/ExaminationFeePanel';
import type {ExaminationFeeChoice} from '../finance/examinationFeeTypes';
﻿import { Alert, Button, Card, Checkbox, Descriptions, Form, Input, Modal, Select, Space, Spin, Tabs, Tag, Typography } from 'antd';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, Navigate, useParams } from 'react-router-dom';
import { api, ApiError } from '../../services/api';
import { arrivalClock, purposeLabels, visitLabels } from '../reception/types';
import { formatDate } from '../appointments/types';
import { genderLabels } from '../patients/types';
import DocumentList, { type DocumentFilters } from '../clinical-documents/DocumentList';
import { usePatientDocumentContexts } from '../clinical-documents/PatientDocumentsPage';
import type { DocumentItem } from '../clinical-documents/types';
import { dispositionLabels, selectedDiagnoses, unscopedDiagnoses, uniqueDiagnoses, findingText, diagnosisText, type ExaminationContent, type ExaminationVersion, type History, type Workspace, type Investigation, type InvestigationPage } from './types';
import ToothChart from './ToothChart';
import InvestigationsPanel from './InvestigationsPanel';
import { surfaceOptions, surfaceText } from './odontogram';
import './examinations.css';
import { useDiagnosisOptions } from './useDiagnosisOptions';
import type { ReferenceSnapshot } from './diagnosisReferences';

type CompletionIssue = { tab: 'exam' | 'summary' | 'investigations'; label: string; target: string };
function completionIssues(c: ExaminationContent, investigations: Investigation[] = []) {
  const issues: CompletionIssue[] = [];
  if (!c.symptoms?.trim()) issues.push({ tab: 'exam', label: 'Lý do khám / triệu chứng', target: 'exam-symptoms' });
  if (!c.findings.length && !(selectedDiagnoses(c).length === 1 && selectedDiagnoses(c)[0] === 'Chưa phát hiện bất thường')) issues.push({ tab: 'exam', label: 'Ít nhất một kết quả khám răng hoặc vùng', target: 'exam-add-region' });
  c.findings.forEach((finding, index) => {
    if (!finding.tooth && !finding.region?.trim()) issues.push({ tab: 'exam', label: `Vùng khám cho kết quả số ${index + 1}`, target: `exam-region-${index}` });
    if (!finding.condition?.trim() && !finding.diagnoses?.length) issues.push({ tab: 'exam', label: finding.tooth ? `Kết quả khám răng ${finding.tooth}` : `Kết quả khám vùng ${finding.region || index + 1}`, target: `exam-condition-${index}` });
  });
  if (!selectedDiagnoses(c).length) issues.push({ tab: 'summary', label: 'Chẩn đoán / Kết luận cuối cùng', target: 'exam-diagnoses' });
  if (!c.advice?.trim()) issues.push({ tab: 'summary', label: 'Dặn dò / lời khuyên', target: 'exam-advice' });
  if (!c.disposition) issues.push({ tab: 'summary', label: 'Hướng xử trí', target: 'exam-disposition' });
  if (c.disposition === 'FollowUp' && !c.returnNeed?.trim()) issues.push({ tab: 'summary', label: 'Nhu cầu quay lại', target: 'exam-return-need' });
  if (c.disposition === 'Referral' && !c.referralDestination?.trim()) issues.push({ tab: 'summary', label: 'Nơi / lý do chuyển khám', target: 'exam-referral' });
  const pending = investigations.filter(order => order.requiredForDiagnosis && order.state !== 'Reviewed' && order.state !== 'Cancelled');
  if (pending.length) issues.push({ tab: 'investigations', label: `Cận lâm sàng cần nhận và đọc kết quả: ${pending.map(order => order.name).join(', ')}`, target: 'exam-investigations' });
  return issues;
}

export default function ExaminationPage() {
  const {user}=useAuth();
  const { visitId } = useParams(); const [w, setW] = useState<Workspace>(); const [history, setHistory] = useState<History[]>([]);
  const [content, setContent] = useState<ExaminationContent>(); const [versions, setVersions] = useState<ExaminationVersion[]>([]);
  const [amending, setAmending] = useState(false); const [amendReason, setAmendReason] = useState('');
  const [ending, setEnding] = useState(false); const [workReviewed, setWorkReviewed] = useState(false);
  const generation = useRef(0);
  const [error, setError] = useState<string>(); const [busy, setBusy] = useState(false); const [dirty, setDirty] = useState(false);
  const [tab, setTab] = useState('exam'); const [filters, setFilters] = useState<DocumentFilters>({});
  const [validationAttempt, setValidationAttempt] = useState(0);
  const summaryFeedback = useRef<HTMLDivElement>(null);
  const validationIssues = useMemo(() => validationAttempt && content ? completionIssues(content, w?.investigations) : [], [validationAttempt, content, w?.investigations]);
  const currentWorkspace = useRef(w); currentWorkspace.current = w;
  const pendingCommand = useRef<{ fingerprint: string; key: string } | undefined>(undefined);
  const { contexts, error: contextError } = usePatientDocumentContexts(w?.visit.patientId ?? 0);
  const { options: diagnosisOptions, error: diagnosisOptionsError, reload: reloadDiagnosisOptions } = useDiagnosisOptions(w?.visit.branchId);
  const lockedFilters = useMemo(() => ({ ...filters, visitId }), [filters, visitId]);
  useEffect(() => { let active = true; generation.current++; setW(undefined); setContent(undefined); setDirty(false); setError(undefined);
    setAmending(false); setAmendReason(''); setEnding(false); setBusy(false); setValidationAttempt(0); setHistory([]); setVersions([]); setTab('exam'); pendingCommand.current = undefined;
    api<Workspace>(`/api/visits/${visitId}/examination`).then(value => {
      if (active) { setW(value); setContent(value.examination?.content ?? value.initialContent); }
    }).catch(e => { if (active) setError((e as Error).message); }); return () => { active = false; };
  }, [visitId]);
  useEffect(() => { if (!w) return; let active = true;
    Promise.all([api<History[]>(`/api/visits/${visitId}/clinical-history`), api<ExaminationVersion[]>(`/api/visits/${visitId}/examination/versions`)])
      .then(([h, v]) => { if (active) { setHistory(h); setVersions(v); } }).catch(e => { if (active) setError((e as Error).message); });
    return () => { active = false; };
  }, [w, visitId]);
  useEffect(() => {
    const warn = (e: BeforeUnloadEvent) => { if (dirty || busy) e.preventDefault(); };
    const navigate = (e: MouseEvent) => {
      if ((dirty || busy) && (e.target as HTMLElement).closest('a') && (busy || !window.confirm('Rời phiếu khám và bỏ thay đổi chưa lưu?'))) { e.preventDefault(); e.stopPropagation(); }
    };
    window.addEventListener('beforeunload', warn); document.addEventListener('click', navigate, true);
    return () => { window.removeEventListener('beforeunload', warn); document.removeEventListener('click', navigate, true); };
  }, [dirty, busy]);
  useEffect(() => {
    if (tab === 'summary' && (error || (validationAttempt > 0 && validationIssues.length > 0))) {
      summaryFeedback.current?.focus({ preventScroll: true });
      summaryFeedback.current?.scrollIntoView({ block: 'nearest' });
    }
  }, [validationAttempt, error, tab]);
  function focusIssue(issue: CompletionIssue) {
    setTab(issue.tab);
    requestAnimationFrame(() => {
      const field = document.getElementById(issue.target);
      field?.focus({ preventScroll: true });
      field?.scrollIntoView({ block: 'center' });
    });
  }
  function change(patch: Partial<ExaminationContent>) {
    setContent(current => {
      if (!current) return current;
      const next = { ...current, ...patch };
      if ('unscopedDiagnoses' in patch || ('findings' in patch && (current.unscopedDiagnoses != null || current.findings.some(f => f.diagnoses?.length) || next.findings.some(f => f.diagnoses?.length)))) {
        next.unscopedDiagnoses = patch.unscopedDiagnoses ?? unscopedDiagnoses(current);
        next.diagnoses = selectedDiagnoses(next); next.diagnosis = next.diagnoses.join('; '); next.conclusion = next.diagnosis;
        next.diagnosisReferences = (patch.diagnosisReferences ?? current.diagnosisReferences ?? []).filter(ref=>next.diagnoses!.some(name=>name.toLocaleLowerCase('vi')===ref.name.toLocaleLowerCase('vi')));
      }
      return next;
    }); setDirty(true); pendingCommand.current = undefined;
  }
  function addReference(reference?: ReferenceSnapshot) {
    if(!reference)return;
    setContent(current=>current?{...current,diagnosisReferences:[...(current.diagnosisReferences??[]).filter(ref=>ref.id!==reference.id||ref.name!==reference.name),reference]}:current);
    setDirty(true);pendingCommand.current=undefined;
  }
  async function command(start: boolean, confirm = false) {
    if (!w || !content || busy) return;
    if(confirm) {
      setValidationAttempt(attempt => attempt + 1);
      if (completionIssues(content, w.investigations).length) { setError(undefined); return; }
    }
    if (confirm && amending && !amendReason.trim()) { setError('Nhập lý do đính chính phiếu khám.'); return; }
    const token = generation.current; setBusy(true); setError(undefined);
    try {
      const payload = { patientId: w.visit.patientId, expectedVersion: w.visit.visitVersion, ...(start ? {} : { content, confirm }), ...(amending ? { reason: amendReason } : {}) };
      const fingerprint = JSON.stringify({ visitId, start, payload });
      if (pendingCommand.current?.fingerprint !== fingerprint) pendingCommand.current = { fingerprint, key: crypto.randomUUID() };
      const result = await api<Workspace>(`/api/visits/${visitId}/${start ? 'start' : amending ? 'examination/amend' : 'examination'}`, { method: start || amending ? 'POST' : 'PUT', body: JSON.stringify({
        ...payload, requestKey: pendingCommand.current.key,
      }) }); if (token !== generation.current) return;
      setW(result); setContent(result.examination?.content ?? result.initialContent); setDirty(false); setValidationAttempt(0); setAmending(false); setAmendReason(''); pendingCommand.current = undefined;
      if (confirm) setTab('summary');
    } catch (e) { if (token === generation.current) setError((e as ApiError).code === 'MSG111' ? 'Lượt đã thay đổi. Bản nhập vẫn còn; tải lại phiên bản trước khi lưu tiếp.' : (e as Error).message); }
    finally { if (token === generation.current) setBusy(false); }
  }
  async function investigationsChanged(page: InvestigationPage, mutation: boolean) {
    if (page.visitId !== visitId) return;
    if (!mutation) {
      setW(current => current?.visit.visitId === page.visitId ? { ...current, investigations: page.items } : current);
      return;
    }
    const token = generation.current;
    const latest = await api<Workspace>(`/api/visits/${visitId}/examination`);
    if (token !== generation.current) return;
    const current = currentWorkspace.current;
    if (!current || current.visit.visitId !== page.visitId || latest.visit.visitVersion < current.visit.visitVersion) return;
    if ((current.examination?.version ?? 0) !== (latest.examination?.version ?? 0)) {
      setW(value => value?.visit.visitId === page.visitId ? { ...value, investigations: latest.investigations } : value);
      setError('Phiếu khám đã thay đổi ở phiên khác. Bản nhập vẫn còn; dùng Tải lại phiên bản trước khi lưu tiếp.');
      return;
    }
    setW(latest); pendingCommand.current = undefined;
  }
  async function reloadVersion() {
    if (busy) return; const token = generation.current; setBusy(true);
    try { const latest = await api<Workspace>(`/api/visits/${visitId}/examination`); if (token !== generation.current) return; setW(latest);
      if (!dirty) setContent(latest.examination?.content ?? latest.initialContent); pendingCommand.current = undefined; setError(undefined);
    } catch (e) { if (token === generation.current) setError((e as Error).message); } finally { if (token === generation.current) setBusy(false); }
  }
  async function review(doc: DocumentItem) {
    setError(undefined);
    try { await api(`/api/clinical-documents/${doc.documentId}/review`, { method: 'POST', body: JSON.stringify({ patientId: w!.visit.patientId,
      visitId, expectedVersion: doc.version, requestKey: crypto.randomUUID() }) }); }
    catch (e) { setError((e as Error).message); throw e; }
  }
  const [examinationFee,setExaminationFee]=useState<ExaminationFeeChoice>();
  async function completeVisit() {
    if (!w || busy || !workReviewed) return;
    const token = generation.current; setBusy(true); setError(undefined);
    const payload = { patientId: w.visit.patientId, expectedVersion: w.visit.visitVersion, workReviewed, examinationFee:examinationFee??null };
    const fingerprint = JSON.stringify({ visitId, complete: payload });
    if (pendingCommand.current?.fingerprint !== fingerprint) pendingCommand.current = { fingerprint, key: crypto.randomUUID() };
    try {
      const result = await api<Workspace>(`/api/visits/${visitId}/complete`, { method: 'POST', body: JSON.stringify({ ...payload, requestKey: pendingCommand.current.key }) });
      if (token !== generation.current) return;
      setW(result); setEnding(false); setWorkReviewed(false); pendingCommand.current = undefined;
    } catch (e) { if (token === generation.current) setError((e as Error).message); }
    finally { if (token === generation.current) setBusy(false); }
  }
  if (!w || !content) return error ? <Alert type="error" message={error} /> : <Spin />;
  if(w.visit.purpose === 'TreatmentContinuation') return w.visit.source?.priorCourseId ? <Navigate replace to={`/patients/${w.visit.patientId}/dossiers/${w.visit.source.priorCourseId}`} /> : <Alert type="info" message="Lượt điều trị chưa được liên kết hồ sơ khi tiếp nhận. Cần chọn đúng hồ sơ điều trị, không tạo thêm phiếu khám mới." description={<Link to={`/patients/${w.visit.patientId}?tab=treatment`}>Mở các hồ sơ điều trị</Link>}/>;
  const v = w.visit; const editable = w.allowedActions.includes('save') || amending; const handoff = w.allowedActions.includes('handoff');
  const findings = content.findings;
  function applyTeeth(teeth: number[], condition: string, surfaces: string[], diagnoses: string[], references: ReferenceSnapshot[]) {
    if (!editable) return;
    const next = findings.map(f => ({ ...f }));
    for (const tooth of teeth) {
      if (next.some(f => f.tooth === tooth && f.condition.trim() === condition && JSON.stringify(f.diagnoses ?? []) === JSON.stringify(diagnoses) && [...(f.surfaces ?? [])].sort().join() === [...surfaces].sort().join())) continue;
      const empty = next.findIndex(f => f.tooth === tooth && !f.condition.trim() && !f.diagnoses?.length);
      const finding = { tooth, condition, surfaces: [...surfaces], diagnoses: uniqueDiagnoses(diagnoses) };
      if (empty >= 0) next[empty] = finding; else next.push(finding);
    }
    change({ findings: next, diagnosisReferences: [...(content!.diagnosisReferences??[]),...references].filter((ref,index,all)=>all.findIndex(other=>other.id===ref.id&&other.version===ref.version&&other.name===ref.name)===index) });
  }
  return <><div className="examination-heading"><div><Typography.Title level={2}>Khám</Typography.Title>
    <Typography.Paragraph type="secondary">Phiếu khám · Kết quả, chẩn đoán và hướng xử trí</Typography.Paragraph></div><Space wrap>
    <Link to={`/patients/${v.patientId}?tab=examinations`}>Các phiếu khám</Link><Link to="/reception">Về hàng chờ</Link>
    <Button aria-label="Tải lại phiên bản" disabled={busy} loading={busy} onClick={() => void reloadVersion()}>Tải lại phiên bản</Button></Space></div>
    {error && tab !== 'summary' && <Alert role="alert" type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
    <section aria-label="Thông tin bệnh nhân"><Card>
      <Typography.Title level={3}>{v.patientCode} · {v.patientName}</Typography.Title>
      <Descriptions className="examination-demographics" size="small" column={{ xs: 1, sm: 3 }} items={[
        { key: 'age', label: 'Tuổi', children: w.patient?.ageYears == null ? 'Chưa rõ' : w.patient.ageYears === 0 ? 'Dưới 1 tuổi' : `${w.patient.ageYears} tuổi` },
        { key: 'gender', label: 'Giới tính', children: w.patient?.gender ? genderLabels[w.patient.gender] ?? 'Chưa rõ' : 'Chưa rõ' },
        { key: 'birthDate', label: 'Ngày sinh', children: w.patient?.birthDate ? formatDate(w.patient.birthDate) : 'Chưa rõ' },
      ]} />
      <Space wrap><Tag color={w.examination?.confirmed ? 'green' : 'blue'}>{!w.examination ? 'Chưa lập phiếu' : w.examination.confirmed ? 'Đã hoàn tất khám' : 'Phiếu nháp'}</Tag>
        {w.examination && <Tag>Phiên bản {w.examination.version ?? 0}</Tag>}<Tag>Lượt đến: {visitLabels[v.status] ?? v.status}</Tag>{w.investigations?.some(order => order.requiredForDiagnosis && order.state === 'Ordered') ? <Tag color="gold">Chờ kết quả cận lâm sàng</Tag> : w.investigations?.some(order => order.requiredForDiagnosis && order.state === 'ResultAvailable') ? <Tag color="gold">Chờ bác sĩ đọc kết quả</Tag> : null}</Space>
      <Descriptions column={{ xs: 1, md: 3 }} items={[
        { label: 'Lịch hẹn', children: v.appointmentCode }, { label: 'Bác sĩ', children: v.dentistName }, { label: 'Chi nhánh', children: v.branchName },
        { label: 'Giờ đến', children: arrivalClock(v.arrivalAt) }, { label: 'Mục đích', children: purposeLabels[v.purpose] },
        { label: 'Thông tin tiếp nhận', children: v.arrivalReason ?? v.note ?? 'Chưa có' },
        { label: 'Hồ sơ cũ', children: v.source?.label ?? 'Chưa chọn hồ sơ' },
      ]} />{v.source?.blocker && <Alert type="warning" message={v.source.blocker} />}
      {w.allowedActions.includes('start') && <Button aria-label="Bắt đầu khám" type="primary" disabled={busy} loading={busy} onClick={() => void command(true)}>Bắt đầu khám</Button>}
    </Card></section>
    <Tabs activeKey={tab} onChange={setTab} items={[
      { key: 'exam', label: 'Khám & sơ đồ răng', children: <Card className={w.examination?.confirmed ? 'clinical-confirmed' : undefined}>
        {w.examination?.confirmed && !amending && <Alert type="success" message="Đã hoàn tất khám" description="Kết quả đã được lưu. Xem tổng kết để chọn bước tiếp theo." />}
        <Form layout="vertical" disabled={!editable || busy}>
          <Form.Item label="Lý do khám / triệu chứng" required><Input.TextArea id="exam-symptoms" aria-label="Lý do khám / triệu chứng" value={content.symptoms ?? ''} maxLength={4000} onChange={e => change({ symptoms: e.target.value })} /></Form.Item>
          {(['medicalHistory','allergies','medications'] as const).map((name,i) => <Card size="small" key={name} title={['Tiền sử','Dị ứng','Thuốc đang dùng'][i]} className="clinical-fact">
            <Input.TextArea aria-label={['Tiền sử','Dị ứng','Thuốc đang dùng'][i]} placeholder="Có thông tin thì ghi vào, không có thì để trống" rows={2} value={content[name].detail ?? ''} maxLength={4000}
              onChange={e => change({ [name]: { detail: e.target.value, state: e.target.value.trim() ? 'Present' : 'Unknown', verified: false } })} />
          </Card>)}
          {diagnosisOptionsError && <Alert type="error" message="Không tải được danh mục chẩn đoán" description={diagnosisOptionsError} action={<Button onClick={reloadDiagnosisOptions}>Thử lại</Button>} />}
          <ToothChart key={visitId} findings={findings} disabled={!editable || busy} onApply={applyTeeth} diagnosisOptions={diagnosisOptions} />
          {editable && <Typography.Paragraph type="secondary">Ghi mô tả hoặc chọn chẩn đoán tại răng/vùng. Tổng kết tự tập hợp chẩn đoán; không cần nhập lại. Khám bình thường có thể chọn Chưa phát hiện bất thường ở Tổng kết.</Typography.Paragraph>}
          <Button id="exam-add-region" onClick={() => change({ findings: [...findings, { region: '', condition: '' }] })}>Thêm vùng khám</Button>
          <Typography.Paragraph type="secondary">Khám tổng quát: thêm vùng “Toàn hàm”, ghi mô tả hoặc chọn chẩn đoán tại cùng dòng.</Typography.Paragraph>
          {findings.map((f,i) => <Space key={i} align="start" className="finding-row">
            {f.tooth ? <Tag>Răng {f.tooth}</Tag> : <Input id={`exam-region-${i}`} aria-label={`Vùng ${i+1}`} placeholder="Vùng khám, ví dụ: Toàn hàm" value={f.region ?? ''} maxLength={200}
              onChange={e => change({ findings: findings.map((x,n) => n === i ? { ...x, region: e.target.value } : x) })} />}
            <Input.TextArea id={`exam-condition-${i}`} aria-label={`Tình trạng ${i+1}`} placeholder="Mô tả bổ sung (nếu có)" value={f.condition} maxLength={2000}
              onChange={e => change({ findings: findings.map((x,n) => n === i ? { ...x, condition: e.target.value } : x) })} />
            <Select id={`exam-finding-diagnoses-${i}`} aria-label={`Chẩn đoán ${f.tooth ? `răng ${f.tooth}` : `vùng ${i+1}`}`} mode="tags" style={{ minWidth: 240, flex: 1 }} placeholder="Chẩn đoán (nếu đã xác định)" options={diagnosisOptions} onSelect={(_,option)=>addReference(option.reference)} value={f.diagnoses ?? []}
              onChange={values => change({ findings: findings.map((x,n) => n === i ? { ...x, diagnoses: uniqueDiagnoses(values) } : x) })} />
            {f.tooth && <Select aria-label={`Bề mặt ${i+1}`} mode="multiple" placeholder="Bề mặt (nếu cần)" options={surfaceOptions} value={f.surfaces ?? []} style={{ minWidth: 180 }} onChange={surfaces => change({ findings: findings.map((x,n) => n === i ? { ...x, surfaces } : x) })} />}
            <Button onClick={() => change({ findings: findings.filter((_,n) => i !== n) })}>Bỏ</Button></Space>)}
          <Form.Item label="Ghi chú khám"><Input.TextArea aria-label="Ghi chú khám" value={content.notes ?? ''} maxLength={8000} onChange={e => change({ notes: e.target.value })} /></Form.Item>
        </Form>
        {editable && <Space>{!amending && <Button aria-label="Lưu nháp" disabled={busy} loading={busy && tab === 'exam'} onClick={() => void command(false)}>Lưu nháp</Button>}
          <Button type="primary" onClick={() => setTab('summary')}>Chẩn đoán & hướng xử trí</Button></Space>}
      </Card> },
      { key: 'investigations', label: `Cận lâm sàng${w.investigations?.length ? ` (${w.investigations.length})` : ''}`, children: <Card><InvestigationsPanel key={visitId} workspace={w} contexts={contexts} busy={busy} onBusy={setBusy} onChanged={investigationsChanged} amending={amending} amendmentReason={amendReason} onAmendmentReason={value=>{setAmendReason(value);pendingCommand.current=undefined;}} /></Card> },
      { key: 'documents', label: 'Ảnh & tài liệu', children: contextError ? <Alert type="error" message={contextError.message} /> :
        <DocumentList patientId={v.patientId} contexts={contexts} filters={lockedFilters} onFiltersChange={setFilters} uploadContext={{ visitId }}
          review={v.status === 'InProgress' && (editable || handoff) ? review : undefined} lockedVisitId={visitId} /> },
      { key: 'summary', label: 'Tổng kết khám', children: <Card title="Chẩn đoán và hướng xử trí" className={`examination-summary ${w.examination?.confirmed ? 'clinical-confirmed' : ''}`}>
        <Form layout="vertical" disabled={!editable || busy}>
          <section aria-label="Chẩn đoán đã ghi tại răng và vùng" className="scoped-diagnoses-summary">
            <Typography.Title level={5}>Chẩn đoán đã ghi khi khám</Typography.Title>
            {findings.flatMap((f,i) => (f.diagnoses ?? []).map(diagnosis => <div key={`${i}-${diagnosis}`}><Tag color="blue">{f.tooth ? `Răng ${f.tooth}` : f.region}</Tag> {diagnosis}
              {editable && <Button type="link" onClick={() => focusIssue({ tab: 'exam', label: diagnosis, target: `exam-finding-diagnoses-${i}` })}>Sửa tại {f.tooth ? `răng ${f.tooth}` : f.region}</Button>}</div>))}
            {!findings.some(f => f.diagnoses?.length) && <Typography.Text type="secondary">Chưa có chẩn đoán gắn răng/vùng.</Typography.Text>}
          </section>
          <Form.Item label="Chẩn đoán bổ sung / không gắn răng" extra="Không cần chọn lại các bệnh đã ghi ở răng/vùng. Ca khám bình thường chọn Chưa phát hiện bất thường.">
            <Select id="exam-diagnoses" aria-label="Chẩn đoán" mode="tags" placeholder="Chọn loại bệnh / chẩn đoán" value={unscopedDiagnoses(content)} options={diagnosisOptions} onSelect={(_,option)=>addReference(option.reference)} optionFilterProp="label" style={{ width: '100%' }} onChange={(values: string[]) => {
              const diagnoses = [...new Map(values.map(value => [value.trim().toLocaleLowerCase('vi'), value.trim()])).values()].filter(Boolean);
              const text = diagnoses.join('; ');
              if (diagnoses.length > 30 || text.length > 4000) { setError('Tối đa 30 chẩn đoán, tổng nội dung tối đa 4000 ký tự.'); return; }
              change({ unscopedDiagnoses: diagnoses });
            }} />
          </Form.Item>
          {content.diagnoses == null && content.conclusion?.trim() && content.conclusion !== content.diagnosis && <Alert type="info" message="Kết luận bổ sung từ phiếu cũ" description={content.conclusion} style={{ marginBottom: 16 }} />}
          <Form.Item label="Dặn dò / lời khuyên" required><Input.TextArea id="exam-advice" aria-label="Dặn dò / lời khuyên" rows={3} maxLength={4000} value={content.advice ?? ''} onChange={e => change({ advice: e.target.value })} /></Form.Item>
          <Form.Item label="Hướng xử trí" required><Select id="exam-disposition" aria-label="Hướng xử trí" placeholder="Chọn hướng xử trí sau khám" value={content.disposition ?? undefined}
            options={Object.entries(dispositionLabels).map(([value, label]) => ({ value, label }))} onChange={disposition => change({ disposition })} /></Form.Item>
          {content.disposition === 'FollowUp' && <Form.Item label="Nhu cầu quay lại" required><Input.TextArea id="exam-return-need" aria-label="Nhu cầu quay lại" value={content.returnNeed ?? ''} maxLength={2000} onChange={e => change({ returnNeed: e.target.value })} />
            <Typography.Text type="secondary">Ghi nhận nhu cầu chưa tạo lịch hẹn. Phòng khám cần sắp lịch riêng.</Typography.Text></Form.Item>}
          {content.disposition === 'Referral' && <Form.Item label="Nơi / lý do chuyển khám" required><Input.TextArea id="exam-referral" aria-label="Nơi / lý do chuyển khám" value={content.referralDestination ?? ''} maxLength={1000} onChange={e => change({ referralDestination: e.target.value })} /></Form.Item>}
        </Form>
        {amending && <Form.Item label="Lý do đính chính" required><Input.TextArea aria-label="Lý do đính chính" disabled={busy} maxLength={1000} value={amendReason} onChange={e => { setAmendReason(e.target.value); pendingCommand.current = undefined; }} /></Form.Item>}
        {(validationIssues.length > 0 || error) && <div ref={summaryFeedback} tabIndex={-1} className="examination-completion-feedback">
          <Alert role="alert" type="error" showIcon message={validationIssues.length ? 'Chưa thể hoàn tất khám' : error}
            description={validationIssues.length ? <><p>Cần bổ sung các mục dưới đây. Bấm từng mục để đến đúng ô nhập.</p>
              <ul>{validationIssues.map(issue => <li key={issue.target}><Button type="link" onClick={() => focusIssue(issue)} aria-label={`Bổ sung: ${issue.label}`}>{issue.label}</Button></li>)}</ul>
            </> : undefined} />
        </div>}
        <Space wrap>{editable && <>
          {!amending && <Button disabled={busy} onClick={() => void command(false)}>Lưu nháp</Button>}
          <Button key="finalize-examination" aria-label={amending ? 'Lưu đính chính' : 'Hoàn tất khám'} aria-busy={busy} type="primary" loading={busy && tab === 'summary'} disabled={busy} onClick={() => void command(false, true)}>{amending ? 'Lưu đính chính' : 'Hoàn tất khám'}</Button>
          {amending && <Button disabled={busy} onClick={() => { setContent(w.examination!.content); setAmending(false); setDirty(false); setAmendReason(''); }}>Hủy đính chính</Button>}
        </>}
        {!amending && w.allowedActions.includes('amend') && <Button key="amend-examination" aria-label="Đính chính kết quả" disabled={busy} onClick={() => { setAmending(true); setDirty(true); }}>Đính chính kết quả</Button>}
        {!amending && handoff && <Link className="ant-btn ant-btn-primary" to={`/patients/${v.patientId}?tab=treatment&sourceExaminationVisitId=${visitId}&examinationVersion=${w.examination?.version ?? 0}`}>Lập kế hoạch điều trị</Link>}
        {!amending && v.status === 'Completed' && w.examination?.confirmed && user?.permissions.includes('finance.read') && <Link className="ant-btn" to={`/visits/${visitId}/payments`}>Chuyển thu ngân</Link>}
        {!amending && w.allowedActions.includes('complete') && <Button disabled={busy} onClick={() => { setWorkReviewed(false); setExaminationFee(undefined); setEnding(true); }}>Kết thúc lượt khám</Button>}</Space>
        {w.examination?.confirmed && v.status === 'InProgress' && <Typography.Paragraph type="secondary" style={{ marginTop: 16 }}>Phiếu khám đã hoàn tất. Kết thúc lượt khám trước khi chuyển sang phiếu điều trị.</Typography.Paragraph>}
      </Card> },
      { key: 'history', label: 'Lịch sử khám', children: <><Typography.Title level={4}>Các phiên bản phiếu hiện tại</Typography.Title>
        {versions.length ? versions.map(version => <Card key={version.version} size="small" title={`Phiên bản ${version.version} · ${version.confirmed ? 'Đã xác nhận' : 'Nháp'}`}>
          <Typography.Text type="secondary">{arrivalClock(version.recordedAt)}</Typography.Text><p>{diagnosisText(version.content)}</p>{version.content.diagnoses == null && version.content.conclusion !== version.content.diagnosis && <p>{version.content.conclusion}</p>}{version.reason && <p>Lý do đính chính: {version.reason}</p>}
          <details><summary>Xem toàn bộ nội dung phiên bản</summary><Descriptions column={1} size="small" items={[
            { label: 'Lý do khám / triệu chứng', children: version.content.symptoms ?? 'Chưa ghi' },
            ...(['medicalHistory', 'allergies', 'medications'] as const).map((name, i) => ({ label: ['Tiền sử', 'Dị ứng', 'Thuốc đang dùng'][i],
              children: version.content[name].detail?.trim() ? version.content[name].detail : 'Chưa ghi' })),
            { label: 'Kết quả khám', children: version.content.findings.map((f, i) => <div key={i}>{f.tooth ? `Răng ${f.tooth}` : f.region || 'Chưa chọn vùng'}: {findingText(f) || 'Chưa ghi tình trạng'} {surfaceText(f.surfaces) && `(${surfaceText(f.surfaces)})`}</div>) },
            { label: 'Dặn dò / lời khuyên', children: version.content.advice ?? 'Chưa ghi' },
            { label: 'Hướng xử trí', children: dispositionLabels[version.content.disposition ?? ''] ?? 'Chưa ghi' },
            { label: 'Nhu cầu quay lại', children: version.content.returnNeed ?? 'Chưa ghi' },
            { label: 'Nơi / lý do chuyển khám', children: version.content.referralDestination ?? 'Chưa ghi' },
            { label: 'Ghi chú khám', children: version.content.notes ?? 'Chưa ghi' },
          ]} /></details>
        </Card>) : <Typography.Paragraph>Chưa có phiên bản đã lưu.</Typography.Paragraph>}
        <Typography.Title level={4}>Các lần khám trước</Typography.Title>{history.length ? history.map(h => <Card key={h.visitId} title={arrivalClock(h.arrivalAt)}>
        <Tag>{h.examination.confirmed ? 'Đã xác nhận' : 'Nháp'}</Tag><Typography.Paragraph>{diagnosisText(h.examination.content) || 'Chưa chẩn đoán'}</Typography.Paragraph>
        {h.examination.content.findings.map((f,i) => <p key={i}>{f.tooth ? `Răng ${f.tooth}` : f.region}: {findingText(f)} {surfaceText(f.surfaces) && `(${surfaceText(f.surfaces)})`}</p>)}
        <Link to={`/visits/${h.visitId}`}>Xem phiếu khám</Link>
      </Card>) : <Alert type="info" message="Chưa có kết quả khám trước trong chi nhánh." />}</> },
    ]} />
    <Modal open={ending} title="Kết thúc lượt khám" okText="Xác nhận kết thúc lượt" confirmLoading={busy} okButtonProps={{ disabled: !workReviewed || busy }}
      onOk={() => void completeVisit()} onCancel={() => { if (!busy) setEnding(false); }}>
      <Typography.Paragraph>Xác nhận kết quả, dặn dò và công việc của lượt khám này. Chưa thực hiện điều trị không cản kết thúc khám. Kế hoạch và các buổi điều trị sẽ được theo dõi riêng, công nợ hiện có vẫn được giữ.</Typography.Paragraph>
      {ending&&user?.permissions.includes('finance.read')&&<ExaminationFeePanel visitId={visitId!} completion disabled={busy} onChoice={setExaminationFee}/>}
      <Checkbox checked={workReviewed} disabled={busy} onChange={e => setWorkReviewed(e.target.checked)}>Đã kiểm tra công việc và nhu cầu quay lại của lượt này</Checkbox>
      {error && <Alert type="error" message={error} />}
    </Modal>
  </>;
}
