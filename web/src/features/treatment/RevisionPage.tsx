import { Alert, Button, Card, Descriptions, Input, InputNumber, Result, Select, Skeleton, Space, Steps, Tag, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { api, type ApiError } from '../../services/api';
import type { TreatmentDossier, TreatmentItem } from './types';
import { decisionLabel, money } from './types';
import './treatment.css';

type Proposal = Pick<TreatmentItem, 'serviceId' | 'material' | 'region' | 'unit' | 'quantity' |
  'estimatedMinutes' | 'agreedUnitPriceVnd'>;
type Disposition = { stepId: string; outcome: 'Stopped' | 'Cancelled' | 'Skipped'; reason: string };
type Draft = { dossierId: string; itemId: string; actorId: number; basePlanVersion: number;
  version: number; proposed: Proposal; reason: string; counseling: string; decision: TreatmentItem['decision'];
  decisionMaker: string; representativeBasis: string | null; futureDispositions: Disposition[]; updatedAt: string };
type StepImpact = { id: string; version: number; planVersion: number; name: string; outcome: string; held: boolean };
type InstructionImpact = { id: string; version: number; status: string; appointmentId: string | null };
type BookingImpact = { id: string; code: string; version: number; status: string; startAt: string };
type Context = { dossier: TreatmentDossier; item: TreatmentItem; steps: StepImpact[];
  instructions: InstructionImpact[]; bookings: BookingImpact[];
  services: { id: number; name: string }[] };
type SourceState = { available: boolean; versionToken: string | null; reason: string | null;
  commitSafe: boolean; summary?: { postedChargeVnd: number; allocationVnd: number; creditVnd: number;
    dueVnd: number; referencePriceVnd: number; discountVnd: number; adjustmentVnd: number } | null };
type Preview = { dossierId: string; itemId: string; draftVersion: number; dossierVersion: number;
  planVersion: number; current: TreatmentItem; proposed: Proposal; steps: StepImpact[];
  instructions: InstructionImpact[]; bookings: BookingImpact[]; actual: SourceState;
  finance: SourceState; proposedDifferenceVnd: number; canSave: boolean; blockers: string[] };
type Change = { dossier: TreatmentDossier; selectedItemId: string; planVersion: number;
  notification: { code: string; message: string } };

const openStep = (step: StepImpact) => !['Completed', 'Stopped', 'Cancelled', 'Skipped'].includes(step.outcome);
const errors: Record<string, string> = {
  MSG04: 'Kiểm tra nội dung thay đổi và lý do trước khi lưu.',
  MSG111: 'Dữ liệu đã đổi. Giữ bản nháp và tải lại ảnh hưởng trước khi xác nhận.',
  MSG233: 'Có bước, chỉ định hoặc lịch hẹn cần xử lý trước khi lưu.',
  MSG240: 'Cần ghi lại tư vấn và quyết định cho phiên bản mới.',
  MSG245: 'Cần đối chiếu tài chính ở màn hình riêng; chưa có số liệu đã xác thực.',
  MSG144: 'Nguồn dữ liệu cần kiểm chứng chưa khả dụng. Bản nháp vẫn được giữ.',
};

export default function RevisionPage() {
  const { id, dossierId, itemId } = useParams();
  const [search] = useSearchParams();
  const navigate = useNavigate();
  const source = search.get('from') === 'dossier' ? 'dossier' : 'plan';
  const returnPath = source === 'dossier'
    ? `/patients/${id}/dossiers/${dossierId}?item=${itemId}`
    : `/patients/${id}/dossiers/${dossierId}/plan?item=${itemId}`;
  const [context, setContext] = useState<Context | null>(null);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [proposal, setProposal] = useState<Proposal | null>(null);
  const [reason, setReason] = useState('');
  const [counseling, setCounseling] = useState('');
  const [decision, setDecision] = useState<TreatmentItem['decision']>('Proposed');
  const [decisionMaker, setDecisionMaker] = useState('');
  const [representativeBasis, setRepresentativeBasis] = useState('');
  const [dispositions, setDispositions] = useState<Disposition[]>([]);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [previewStale, setPreviewStale] = useState(false);
  const [draftDirty, setDraftDirty] = useState(false);
  const [step, setStep] = useState(0);
  const [busy, setBusy] = useState(false);
  const [loadError, setLoadError] = useState<ApiError | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [reload, setReload] = useState(0);
  const draftKey = useRef(crypto.randomUUID());
  const commitKey = useRef(crypto.randomUUID());

  useEffect(() => {
    if (!dossierId || !itemId) return;
    let live = true;
    setLoadError(null);
    Promise.all([
      api<Context>(`/api/dossiers/${dossierId}/plan/revisions/context?itemId=${itemId}`),
      api<Draft>(`/api/dossiers/${dossierId}/plan/revision-drafts/${itemId}`).catch((failure: ApiError) => {
        if (failure.status === 404) return null;
        throw failure;
      }),
    ]).then(([current, saved]) => {
      if (!live) return;
      setContext(current); setDraft(saved);
      setProposal(saved?.proposed ?? {
        serviceId: current.item.serviceId, material: current.item.material,
        region: current.item.region, unit: current.item.unit, quantity: current.item.quantity,
        estimatedMinutes: current.item.estimatedMinutes,
        agreedUnitPriceVnd: current.item.agreedUnitPriceVnd,
      });
      setReason(saved?.reason ?? ''); setCounseling(saved?.counseling ?? '');
      setDecision(saved?.decision ?? 'Proposed'); setDecisionMaker(saved?.decisionMaker ?? '');
      setRepresentativeBasis(saved?.representativeBasis ?? '');
      setDispositions(saved?.futureDispositions ?? current.steps.filter(openStep).map(x => ({
        stepId: x.id, outcome: 'Stopped' as const, reason: '',
      })));
    }).catch((failure: ApiError) => { if (live) setLoadError(failure); });
    return () => { live = false; };
  }, [dossierId, itemId, reload]);

  function changed() { setDraftDirty(true); setPreviewStale(true); setError(null);
    draftKey.current = crypto.randomUUID(); commitKey.current = crypto.randomUUID(); }
  function updateProposal<K extends keyof Proposal>(key: K, value: Proposal[K]) {
    setProposal(current => current ? { ...current, [key]: value } : current); changed();
  }

  async function saveDraft(): Promise<Draft | null> {
    if (!context || !proposal || !dossierId || !itemId || busy) return null;
    setBusy(true); setError(null);
    try {
      const saved = await api<Draft>(`/api/dossiers/${dossierId}/plan/revision-drafts/${itemId}`, {
        method: 'PUT', body: JSON.stringify({ expectedDraftVersion: draft?.version ?? 0,
          basePlanVersion: context.dossier.planVersion, proposed: proposal, reason, counseling,
          decision, decisionMaker, representativeBasis: representativeBasis || null,
          futureDispositions: dispositions, requestKey: draftKey.current }),
      });
      setDraft(saved); setDraftDirty(false); draftKey.current = crypto.randomUUID(); return saved;
    } catch (failure) { setError((failure as ApiError).message); return null; }
    finally { setBusy(false); }
  }

  async function review() {
    if (!reason.trim()) { setError('Vui lòng nhập lý do điều chỉnh.'); return; }
    const saved = await saveDraft();
    if (!saved || !dossierId) return;
    setBusy(true);
    try {
      const result = await api<Preview>(`/api/dossiers/${dossierId}/plan/revisions/preview`, {
        method: 'POST', body: JSON.stringify({ itemId, draftVersion: saved.version }),
      });
      setPreview(result); setPreviewStale(false); setStep(2); setError(null);
    } catch (failure) { setError((failure as ApiError).message); }
    finally { setBusy(false); }
  }

  async function commit() {
    if (!preview || !draft || !dossierId || busy || !preview.canSave || previewStale) return;
    setBusy(true); setError(null);
    try {
      const result = await api<Change>(`/api/dossiers/${dossierId}/plan/revisions`, {
        method: 'POST', body: JSON.stringify({ itemId, draftVersion: draft.version,
          expectedDossierVersion: preview.dossierVersion, expectedPlanVersion: preview.planVersion,
          steps: preview.steps.map(x => ({ id: x.id, version: x.version })),
          instructions: preview.instructions.map(x => ({ id: x.id, version: x.version })),
          bookings: preview.bookings.map(x => ({ id: x.id, version: x.version })),
          actualVersionToken: preview.actual.versionToken,
          financeVersionToken: preview.finance.versionToken, requestKey: commitKey.current }),
      });
      navigate(returnPath, { replace: true, state: { revisionNotice: result.notification.message,
        selectedItemId: result.selectedItemId } });
    } catch (failure) { setError((failure as ApiError).message); }
    finally { setBusy(false); }
  }

  async function cancel() {
    if (!window.confirm('Bỏ bản nháp điều chỉnh? Kế hoạch đã chốt sẽ không thay đổi.')) return;
    if (draft && dossierId && itemId) {
      setBusy(true);
      try { await api(`/api/dossiers/${dossierId}/plan/revision-drafts/${itemId}`, { method: 'DELETE' }); }
      catch (failure) { setError((failure as ApiError).message); setBusy(false); return; }
    }
    navigate(returnPath);
  }

  async function leaveFor(path: string) {
    if (draftDirty && !await saveDraft()) return;
    navigate(path);
  }

  if (loadError) return <Result status={loadError.status === 403 ? '403' : loadError.status === 404 ? '404' : 'error'}
    title={loadError.message} extra={<Space><Button onClick={() => setReload(x => x + 1)}>Thử lại</Button>
      <Link to={`/patients/${id}`}>Về hồ sơ bệnh nhân</Link></Space>} />;
  if (!context || !proposal) return <Card><Skeleton active /></Card>;
  const current = context.item;
  const services = context.services;
  const serviceOptions = services.some(x => x.id === proposal.serviceId) ? services
    : [{ id: proposal.serviceId, name: current.serviceName }, ...services];

  return <main className="treatment-page treatment-revision">
    <Typography.Paragraph><Link to={returnPath} onClick={event => {
      event.preventDefault(); void leaveFor(returnPath);
    }}>← Về {source === 'dossier' ? 'đợt điều trị' : 'kế hoạch'}</Link></Typography.Paragraph>
    <div className="treatment-page-heading"><div>
      <Typography.Title level={2}>Điều chỉnh kế hoạch điều trị</Typography.Title>
      <Typography.Paragraph>Hồ sơ {context.dossier.code} · Hạng mục {current.serviceName}
        {current.region ? ` · Răng/vùng ${current.region}` : ''}</Typography.Paragraph>
    </div><Tag>Phiên bản {context.dossier.planVersion}</Tag></div>
    {draft && <Alert type="info" showIcon message={`Bản nháp đã lưu · phiên bản ${draft.version}`}
      description="Rời trang để xử lý lịch rồi quay lại, nội dung này vẫn được giữ trên server." />}
    {error && <Alert type="error" showIcon message={error} />}
    <Steps current={step} items={[{ title: 'So sánh phạm vi' }, { title: 'Tư vấn và xác nhận' },
      { title: 'Xem ảnh hưởng và lưu' }]} />
    {step === 0 && <Card title="Phạm vi hiện tại và đề xuất">
      <Descriptions column={{ xs: 1, md: 2 }} items={[
        { label: 'Dịch vụ hiện tại', children: current.serviceName },
        { label: 'Giá hiện tại', children: money(current.agreedUnitPriceVnd) },
        { label: 'Quyết định hiện tại', children: decisionLabel[current.decision] },
        { label: 'Tiến độ', children: current.progress },
      ]} />
      <div className="treatment-revision-form">
        <label>Dịch vụ đề xuất<Select aria-label="Dịch vụ đề xuất" value={proposal.serviceId}
          options={serviceOptions.map(x => ({ value: x.id, label: x.name }))}
          onChange={value => updateProposal('serviceId', value)} /></label>
        <label>Vật liệu<Input value={proposal.material ?? ''} maxLength={160}
          onChange={event => updateProposal('material', event.target.value || null)} /></label>
        <label>Răng hoặc vùng điều trị<Input value={proposal.region ?? ''} maxLength={80}
          onChange={event => updateProposal('region', event.target.value || null)} /></label>
        <label>Số lượng<InputNumber min={0.001} precision={3} value={proposal.quantity}
          onChange={value => updateProposal('quantity', value ?? 1)} /></label>
        <label>Đơn vị<Input value={proposal.unit} maxLength={40}
          onChange={event => updateProposal('unit', event.target.value)} /></label>
        <label>Giá thỏa thuận mỗi đơn vị (VND)<InputNumber min={0} precision={0}
          value={proposal.agreedUnitPriceVnd}
          onChange={value => updateProposal('agreedUnitPriceVnd', value ?? 0)} /></label>
        <label>Thời gian dự kiến (phút)<Select allowClear value={proposal.estimatedMinutes}
          options={[30, 60, 90, 120, 180, 240].map(value => ({ value, label: `${value} phút` }))}
          onChange={value => updateProposal('estimatedMinutes', value ?? null)} /></label>
        <label className="treatment-revision-wide">Lý do điều chỉnh (1–1000 ký tự)
          <Input.TextArea value={reason} maxLength={1000} showCount rows={3}
            onChange={event => { setReason(event.target.value); changed(); }} /></label>
      </div>
      <Button type="primary" loading={busy} onClick={async () => {
        if (!reason.trim()) { setError('Vui lòng nhập lý do điều chỉnh.'); return; }
        if (await saveDraft()) setStep(1);
      }}>Tiếp tục tư vấn</Button>
    </Card>}
    {step === 1 && <Card title="Tư vấn và quyết định cho phiên bản mới">
      <div className="treatment-revision-form">
        <label className="treatment-revision-wide">Phương án, lợi ích/rủi ro, thời gian và điều khoản
          <Input.TextArea rows={5} value={counseling} maxLength={4000} showCount
            onChange={event => { setCounseling(event.target.value); changed(); }} /></label>
        <label>Quyết định<Select value={decision} options={[
          { value: 'Agreed', label: 'Đồng ý' }, { value: 'Refused', label: 'Từ chối' },
          { value: 'Deferred', label: 'Hoãn quyết định' },
        ]} onChange={value => { setDecision(value); changed(); }} /></label>
        <label>Người đưa ra quyết định<Input value={decisionMaker} maxLength={160}
          onChange={event => { setDecisionMaker(event.target.value); changed(); }} /></label>
        <label className="treatment-revision-wide">Căn cứ đại diện (nếu có)
          <Input value={representativeBasis} maxLength={1000}
            onChange={event => { setRepresentativeBasis(event.target.value); changed(); }} /></label>
      </div>
      <Alert type="info" showIcon message="Bác sĩ xác nhận nội dung tư vấn trên Web"
        description="Hệ thống ghi người thực hiện, thời điểm và phiên bản kế hoạch. Đây không phải file hoặc chữ ký S20." />
      <Space wrap><Button onClick={() => setStep(0)}>Quay lại</Button>
        <Button type="primary" loading={busy} onClick={() => void review()}>Xem ảnh hưởng</Button></Space>
    </Card>}
    {step === 2 && <Card title="Ảnh hưởng cần kiểm tra">
      {!preview ? <Alert type="info" showIcon message="Nội dung đã đổi sau lần xem ảnh hưởng gần nhất"
        description={<Button loading={busy} onClick={() => void review()}>Lưu nháp và xem ảnh hưởng lại</Button>} /> : <>
        {previewStale && <Alert type="warning" showIcon message="Bạn đã sửa bản nháp; cần xem ảnh hưởng lại trước khi lưu." />}
        <Descriptions column={{ xs: 1, md: 2 }} items={[
          { label: 'Chênh lệch giá đề xuất', children: money(preview.proposedDifferenceVnd) },
          { label: 'Nguồn actual', children: preview.actual.versionToken === 'not-required'
            ? 'Không cần đối chiếu' : preview.actual.available ? 'Đã kiểm chứng' : preview.actual.reason },
          { label: 'Nguồn tài chính', children: preview.finance.versionToken === 'not-required'
            ? 'Không cần đối chiếu' : preview.finance.available ? 'Đã kiểm chứng' : preview.finance.reason },
        ]} />
        {preview.finance.summary && <Descriptions title="Tài chính đã xác thực (chỉ đọc)"
          column={{ xs: 1, md: 2 }} items={[
            { label: 'Khoản thu đã ghi', children: money(preview.finance.summary.postedChargeVnd) },
            { label: 'Đã phân bổ', children: money(preview.finance.summary.allocationVnd) },
            { label: 'Số dư/credit', children: money(preview.finance.summary.creditVnd) },
            { label: 'Còn phải thu', children: money(preview.finance.summary.dueVnd) },
            { label: 'Giá tham chiếu', children: money(preview.finance.summary.referencePriceVnd) },
            { label: 'Giảm giá đã ghi', children: money(preview.finance.summary.discountVnd) },
            { label: 'Điều chỉnh đã ghi', children: money(preview.finance.summary.adjustmentVnd) },
          ]} />}
        {preview.steps.length > 0 && <Card size="small" title="Bước điều trị tương lai">
          {preview.steps.map(x => <div key={x.id} className="treatment-item-row">
            <span>{x.name} · {x.outcome}{x.held ? ' · Đang tạm dừng' : ''}</span>
            {openStep(x) && <Space wrap>
              <Select aria-label={`Xử lý bước ${x.name}`} value={dispositions.find(d => d.stepId === x.id)?.outcome}
                options={[{ value: 'Stopped', label: 'Dừng' }, { value: 'Cancelled', label: 'Hủy' },
                  { value: 'Skipped', label: 'Bỏ qua' }]}
                onChange={value => { setDispositions(rows => rows.map(d => d.stepId === x.id ?
                  { ...d, outcome: value } : d)); changed(); }} />
              <Input aria-label={`Lý do xử lý bước ${x.name}`} placeholder="Lý do" maxLength={1000}
                value={dispositions.find(d => d.stepId === x.id)?.reason ?? ''}
                onChange={event => { setDispositions(rows => rows.map(d => d.stepId === x.id ?
                  { ...d, reason: event.target.value } : d)); changed(); }} />
            </Space>}</div>)}
          <Typography.Text type="secondary">Sau khi đổi cách xử lý, lưu và xem ảnh hưởng lại.</Typography.Text>
        </Card>}
        {preview.instructions.length > 0 && <Alert type="warning" showIcon
          message={`${preview.instructions.length} chỉ định quay lại liên quan`}
          description="Chỉ định chưa đặt lịch sẽ được supersede khi revision lưu. Chỉ định đã đặt lịch cần xử lý lịch trước." />}
        {preview.bookings.map(x => <Alert key={x.id} type="warning" showIcon
          message={`Lịch ${x.code} · ${new Date(x.startAt).toLocaleString('vi-VN')}`}
          action={<Link to={`/appointments/${x.id}`} onClick={event => {
            event.preventDefault(); void leaveFor(`/appointments/${x.id}`);
          }}><Button>Xử lý tại lịch hẹn</Button></Link>} />)}
        {!preview.finance.available && <Alert type="info" showIcon message="Chưa mở được phần tài chính S38"
          description="Khi module tài chính có API thật, cần đối chiếu ở S38 rồi quay lại xem ảnh hưởng. Không có thao tác thu hoặc hoàn tiền tại đây." />}
        {preview.blockers.map(code => <Alert key={code} type="error" showIcon message={errors[code] ?? code} />)}
        <Space wrap><Button onClick={() => setStep(1)}>Quay lại</Button>
          <Button loading={busy} onClick={() => void review()}>Tải lại ảnh hưởng</Button>
          <Button type="primary" disabled={!preview.canSave || previewStale} loading={busy} onClick={() => void commit()}>
            Lưu phiên bản mới</Button></Space>
      </>}
    </Card>}
    <Space wrap><Button loading={busy} disabled={!draftDirty} onClick={() => void saveDraft()}>
      Lưu bản nháp</Button>
      <Button danger disabled={busy} onClick={() => void cancel()}>Bỏ bản nháp và thoát</Button></Space>
  </main>;
}
