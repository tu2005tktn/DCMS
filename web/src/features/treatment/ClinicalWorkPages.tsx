import { Alert, Button, Card, Descriptions, Empty, Form, Input, InputNumber, Result, Select, Skeleton, Space, Tag, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { api, type ApiError } from '../../services/api';
import type { TreatmentDossier } from './types';
import { money } from './types';
import { clinicalPreviewHome, clinicalPreviewReview, clinicalPreviewTreatment } from './clinicalPreview';
import './treatment.css';

type Activity = { id: string; visitId: string; patientId: number; branchId: number;
  dossierId: string | null; itemId: string | null; stepId: string | null; planVersion: number | null;
  serviceId: number; region: string | null; unit: string; quantity: number; performedAt: string;
  elapsedMinutes: number | null; outcome: string; nextSteps: string | null;
  chargingBasis: 'Free' | 'Included' | 'Separate'; priceSnapshotVnd: number;
  chargingBasisEvidence: string | null;
  reviewId: string | null; state: 'Draft' | 'Posted'; version: number; postedAt: string | null };
type Context = { visit: { available: boolean; inProgress: boolean; patientId: number;
  branchId: number; version: number }; activities: Activity[] };
type Review = { id: string; visitId: string; sourceActivityId: string | null;
  sourceItemId: string | null; complaint: string; findings: string;
  coverage: 'PendingAssessment' | 'Covered' | 'NotCovered';
  outcome: 'AdviceOnly' | 'FurtherAssessment' | 'CorrectiveCareNeeded' | 'NewProblem';
  coveredComponent: string | null; exclusions: string | null; policy: string | null;
  policyVersion: string | null; basis: string | null; nextAction: string | null;
  recordedAt: string; version: number };
type ActualForm = { itemId?: string; stepId?: string; serviceId: number; region?: string;
  unit: string; quantity: number; performedAt: string; elapsedMinutes?: number;
  outcome: string; nextSteps?: string; chargingBasis: Activity['chargingBasis'];
  chargingBasisEvidence?: string;
  priceSnapshotVnd: number; stepOutcome?: 'Partial' | 'Completed' };
type ReviewForm = { sourceActivityId?: string; sourceItemId?: string; complaint: string;
  findings: string; coverage: Review['coverage']; outcome: Review['outcome'];
  coveredComponent?: string; exclusions?: string; policy?: string;
  policyVersion?: string; basis?: string; nextAction?: string };

function explain(error: ApiError) {
  if (error.status === 503) return 'Nguồn lượt khám hoặc xác minh liên quan chưa được kết nối. Chưa có dữ liệu lâm sàng nào được lưu.';
  if (error.code === 'MSG111') return 'Dữ liệu đã thay đổi. Nội dung nhập vẫn còn; hãy tải lại ngữ cảnh trước khi xác nhận.';
  if (error.code === 'MSG240') return 'Cần kiểm tra lại đồng ý điều trị theo phiên bản hiện tại.';
  if (error.code === 'MSG230') return 'Bước điều trị chưa đủ điều kiện thực hiện. Hãy xem các điều kiện còn thiếu.';
  if (error.code === 'MSG244') return 'Cần ghi căn cứ và phạm vi bảo hành hoặc miễn phí.';
  return error.message;
}

function useClinicalContext(visitId: string | undefined) {
  const [context, setContext] = useState<Context | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [reload, setReload] = useState(0);
  useEffect(() => {
    if (!visitId) return;
    let live = true;
    setError(null); setContext(null);
    api<Context>(`/api/visits/${visitId}/treatment-context`)
      .then(value => { if (live) setContext(value); })
      .catch(failure => { if (live) setError(failure as ApiError); });
    return () => { live = false; };
  }, [visitId, reload]);
  return { context, error, refresh: () => setReload(value => value + 1) };
}

export function ActualTreatmentPage() {
  const preview = import.meta.env.DEV && window.location.pathname.startsWith(clinicalPreviewHome);
  const { id, visitId } = useParams();
  const [search] = useSearchParams();
  const { context, error: loadError, refresh } = useClinicalContext(visitId);
  const [dossier, setDossier] = useState<TreatmentDossier | null>(null);
  const [steps, setSteps] = useState<{ id: string; itemId: string; name: string; version: number; readiness: string }[]>([]);
  const [services, setServices] = useState<{ id: number; name: string }[]>([]);
  const [draft, setDraft] = useState<Activity | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [form] = Form.useForm<ActualForm>();
  const draftKey = useRef(crypto.randomUUID());
  const postKey = useRef(crypto.randomUUID());
  const navigate = useNavigate();
  const dossierId = search.get('dossierId');
  const reviewId = search.get('reviewId');
  const selectedItemQuery = search.get('itemId');
  const itemId = Form.useWatch('itemId', form);
  const chargingBasis = Form.useWatch('chargingBasis', form);
  const selectedStepId = Form.useWatch('stepId', form);
  useEffect(() => {
    if (!context) return;
    let live = true;
    api<typeof services>(`/api/clinical-services?branchId=${context.visit.branchId}`)
      .then(value => { if (live) setServices(value); })
      .catch(failure => { if (live) setError(explain(failure as ApiError)); });
    return () => { live = false; };
  }, [context]);
  useEffect(() => {
    if (!dossierId) return;
    let live = true;
    Promise.all([api<TreatmentDossier>(`/api/dossiers/${dossierId}`),
      api<typeof steps>(`/api/dossiers/${dossierId}/steps`)]).then(([d, s]) => {
        if (!live) return;
        setDossier(d); setSteps(s);
        const selected = d.items.find(x => x.id === selectedItemQuery);
        if (selected) form.setFieldsValue({ itemId: selected.id, serviceId: selected.serviceId,
          region: selected.region ?? undefined, unit: selected.unit,
          priceSnapshotVnd: selected.agreedUnitPriceVnd });
      }).catch(failure => { if (live) setError(explain(failure as ApiError)); });
    return () => { live = false; };
  }, [dossierId, form, selectedItemQuery]);

  async function saveDraft(values: ActualForm) {
    if (!context || !visitId || busy) return;
    setBusy(true); setError(null); setNotice(null);
    try {
      const activity = { visitId, dossierId, itemId: values.itemId ?? null,
        stepId: values.stepId ?? null, planVersion: dossier?.planVersion ?? null,
        serviceId: values.serviceId, region: values.region ?? null, unit: values.unit,
        quantity: values.quantity, performedAt: new Date(values.performedAt).toISOString(),
        elapsedMinutes: values.elapsedMinutes ?? null, outcome: values.outcome,
        nextSteps: values.nextSteps ?? null, chargingBasis: values.chargingBasis,
        chargingBasisEvidence: values.chargingBasisEvidence ?? null,
        priceSnapshotVnd: values.priceSnapshotVnd, reviewId };
      const result = draft
        ? await api<Activity>(`/api/activities/${draft.id}/draft`, { method: 'PUT', body: JSON.stringify({
          activity, expectedVersion: draft.version, expectedVisitVersion: context.visit.version,
          requestKey: draftKey.current }) })
        : await api<Activity>(`/api/visits/${visitId}/activities/drafts`, { method: 'POST', body: JSON.stringify({
          activity, expectedVisitVersion: context.visit.version, requestKey: draftKey.current }) });
      setDraft(result); draftKey.current = crypto.randomUUID();
      setNotice('Đã lưu nháp. Chưa ghi nhận việc điều trị hoặc khoản thu.');
    } catch (failure) { setError(explain(failure as ApiError)); }
    finally { setBusy(false); }
  }

  async function post() {
    if (!draft || !context || busy) return;
    setBusy(true); setError(null); setNotice(null);
    try {
      const values = form.getFieldsValue();
      const step = steps.find(x => x.id === draft.stepId);
      const result = await api<Activity>(`/api/activities/${draft.id}/post`, { method: 'POST',
        body: JSON.stringify({ expectedVersion: draft.version,
          expectedVisitVersion: context.visit.version, expectedStepVersion: step?.version ?? null,
          stepOutcome: step ? values.stepOutcome ?? 'Partial' : null, requestKey: postKey.current }) });
      setDraft(result); postKey.current = crypto.randomUUID();
      setNotice('Đã ghi nhận hoạt động thực tế. Lượt khám và thanh toán vẫn là thao tác riêng.');
      refresh();
    } catch (failure) { setError(explain(failure as ApiError)); }
    finally { setBusy(false); }
  }

  return <div className="treatment-page clinical-work-page">
    <div className="treatment-page-heading"><div><Typography.Title level={2}>Ghi việc đã làm</Typography.Title>
      <Typography.Text type="secondary">S22 · Chỉ ghi công việc bác sĩ xác nhận trong lượt khám hiện tại</Typography.Text></div>
      <Link to={preview ? clinicalPreviewHome : dossierId ? `/patients/${id}/dossiers/${dossierId}/plan` : `/patients/${id}`}>
        {preview ? 'Về trang xem thử' : 'Quay lại hồ sơ'}</Link></div>
    {loadError ? <Result status="warning" title="Chưa tải được lượt khám" subTitle={explain(loadError)}
      extra={<Button onClick={refresh}>Thử lại</Button>} /> : !context ? <Skeleton active /> : <>
      <Card title="Lượt khám và công việc"><Descriptions column={1} size="small"
        items={[{ key: 'patient', label: 'Bệnh nhân', children: `#${context.visit.patientId}` },
          { key: 'visit', label: 'Lượt khám', children: visitId },
          { key: 'state', label: 'Trạng thái', children: <Tag>Đang khám</Tag> }]} />
        {context.activities.length > 0 && <Typography.Text type="secondary">Có {context.activities.length} hoạt động đã ghi trước đây; chỉ nội dung xác nhận mới được tính là đã làm.</Typography.Text>}
      </Card>
      {error && <Alert type="error" showIcon message={error} />}
      {notice && <Alert type="success" showIcon message={notice} />}
      <Card title="Thực tế điều trị"><Form form={form} layout="vertical" onFinish={values => void saveDraft(values)}
        initialValues={{ quantity: 1, chargingBasis: 'Included', priceSnapshotVnd: 0,
          performedAt: new Date().toISOString().slice(0, 16), stepOutcome: 'Partial' }}>
        {dossier && <Form.Item name="itemId" label="Hạng mục đã đồng ý" rules={[{ required: true }]}>
          <Select options={dossier.items.filter(x => x.decision === 'Agreed').map(x => ({ value: x.id,
            label: `${x.serviceName} · ${x.region ?? 'Không ghi vùng'}` }))} onChange={value => {
              const selected = dossier.items.find(x => x.id === value);
              form.setFieldsValue({ serviceId: selected?.serviceId,
                region: selected?.region ?? undefined, unit: selected?.unit,
                priceSnapshotVnd: selected?.agreedUnitPriceVnd, stepId: undefined });
            }} /></Form.Item>}
        {dossier && <Form.Item name="stepId" label="Bước điều trị (nếu có)"><Select allowClear
          options={steps.filter(x => x.itemId === itemId).map(x => ({ value: x.id,
            label: `${x.name} · ${x.readiness}` }))} /></Form.Item>}
        <div className="treatment-revision-form">
          <Form.Item name="serviceId" label="Dịch vụ" rules={[{ required: true }]}><Select
            disabled={!!dossier} options={services.map(x => ({ value: x.id, label: x.name }))} /></Form.Item>
          <Form.Item name="region" label="Răng hoặc vùng"><Input /></Form.Item>
          <Form.Item name="quantity" label="Số lượng đã làm" rules={[{ required: true }]}><InputNumber min={0.001} step={1} className="treatment-number-full" /></Form.Item>
          <Form.Item name="unit" label="Đơn vị" rules={[{ required: true }]}><Select options={['lần', 'răng', 'hàm', 'gói'].map(value => ({ value, label: value }))} /></Form.Item>
          <Form.Item name="performedAt" label="Thời điểm thực hiện" rules={[{ required: true }]}><Input type="datetime-local" /></Form.Item>
          <Form.Item name="elapsedMinutes" label="Thời gian thực tế (phút)"><InputNumber min={1} max={1440} className="treatment-number-full" /></Form.Item>
          <Form.Item name="chargingBasis" label="Cách tính phí" rules={[{ required: true }]}><Select options={[
            { value: 'Included', label: 'Đã gồm trong giá thỏa thuận' },
            { value: 'Free', label: 'Không thu phí, có căn cứ' },
            { value: 'Separate', label: 'Tính phí riêng' }]} /></Form.Item>
          {chargingBasis === 'Free' && <Form.Item className="treatment-revision-wide"
            name="chargingBasisEvidence" label="Căn cứ không thu phí" rules={[{ required: true }]}>
            <Input.TextArea maxLength={1000} /></Form.Item>}
          <Form.Item name="priceSnapshotVnd" label="Giá tham chiếu (VND)" rules={[{ required: true }]}><InputNumber min={0} className="treatment-number-full" disabled={!!dossier} /></Form.Item>
          <Form.Item className="treatment-revision-wide" name="outcome" label="Kết quả thực hiện" rules={[{ required: true }]}><Input.TextArea maxLength={4000} rows={3} showCount /></Form.Item>
          <Form.Item className="treatment-revision-wide" name="nextSteps" label="Hướng xử trí tiếp theo"><Input.TextArea maxLength={2000} rows={2} showCount /></Form.Item>
          {selectedStepId && <Form.Item name="stepOutcome" label="Kết quả bước"><Select options={[
            { value: 'Partial', label: 'Mới làm một phần' },
            { value: 'Completed', label: 'Bác sĩ xác nhận hoàn tất bước' }]} /></Form.Item>}
        </div>
        {draft && <Alert type="info" showIcon message={`Nháp ${draft.id} · ${draft.state}`}
          description={draft.chargingBasis === 'Separate' ? 'Khoản phí riêng cần nguồn tài chính xác thực trước khi post.' :
            `Giá tham chiếu: ${money(draft.priceSnapshotVnd)}. Lưu nháp chưa tạo khoản thu.`} />}
        <Space wrap><Button htmlType="submit" loading={busy} disabled={draft?.state === 'Posted'}>Lưu nháp</Button>
          <Button type="primary" onClick={() => void post()} loading={busy} disabled={!draft || draft.state === 'Posted'}>Xác nhận đã làm</Button></Space>
      </Form></Card>
      {reviewId && <Button onClick={() => navigate(preview ? clinicalPreviewReview :
        `/patients/${id}/visits/${visitId}/review?reviewId=${reviewId}`)}>Xem đánh giá nguồn</Button>}
    </>}
  </div>;
}

export function PostoperativeReviewPage() {
  const preview = import.meta.env.DEV && window.location.pathname.startsWith(clinicalPreviewHome);
  const { id, visitId } = useParams();
  const [search] = useSearchParams();
  const navigate = useNavigate();
  const { context, error: loadError, refresh } = useClinicalContext(visitId);
  const [reviews, setReviews] = useState<Review[]>([]);
  const [dossier, setDossier] = useState<TreatmentDossier | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState<Review | null>(null);
  const [busy, setBusy] = useState(false);
  const [form] = Form.useForm<ReviewForm>();
  const requestKey = useRef(crypto.randomUUID());
  const dossierId = search.get('dossierId');
  const coverage = Form.useWatch('coverage', form);
  const sourceActivityId = Form.useWatch('sourceActivityId', form);
  const sourceItemId = Form.useWatch('sourceItemId', form);
  useEffect(() => {
    if (!dossierId) return;
    let live = true;
    api<TreatmentDossier>(`/api/dossiers/${dossierId}`)
      .then(value => { if (live) setDossier(value); })
      .catch(failure => { if (live) setError(explain(failure as ApiError)); });
    return () => { live = false; };
  }, [dossierId]);
  useEffect(() => {
    if (!visitId || !context) return;
    api<Review[]>(`/api/visits/${visitId}/reviews`).then(setReviews)
      .catch(failure => setError(explain(failure as ApiError)));
  }, [visitId, context]);

  async function save(values: ReviewForm) {
    if (!context || !visitId || busy) return;
    const source = context.activities.find(x => x.id === values.sourceActivityId);
    const sourceItem = dossier?.items.find(x => x.id === values.sourceItemId);
    setBusy(true); setError(null);
    try {
      const result = await api<Review>(`/api/visits/${visitId}/reviews`, { method: 'POST',
        body: JSON.stringify({ ...values, visitId,
          expectedVisitVersion: context.visit.version,
          expectedSourceVersion: source?.version ?? sourceItem?.decisionPlanVersion ?? dossier?.planVersion ?? 0,
          requestKey: requestKey.current }) });
      setSaved(result); setReviews(previous => [result, ...previous]);
      requestKey.current = crypto.randomUUID();
    } catch (failure) { setError(explain(failure as ApiError)); }
    finally { setBusy(false); }
  }

  return <div className="treatment-page clinical-work-page">
    <div className="treatment-page-heading"><div><Typography.Title level={2}>Đánh giá sau điều trị</Typography.Title>
      <Typography.Text type="secondary">S63 · Lưu đánh giá trước, ghi chăm sóc hoặc điều trị thực tế bằng thao tác riêng</Typography.Text></div>
      <Link to={preview ? clinicalPreviewHome : dossierId ? `/patients/${id}/dossiers/${dossierId}` : `/patients/${id}`}>
        {preview ? 'Về trang xem thử' : 'Quay lại hồ sơ'}</Link></div>
    {loadError ? <Result status="warning" title="Chưa tải được lượt khám" subTitle={explain(loadError)}
      extra={<Button onClick={refresh}>Thử lại</Button>} /> : !context ? <Skeleton active /> : <>
      {error && <Alert type="error" showIcon message={error} />}
      {saved && <Alert type="success" showIcon message="Đã lưu đánh giá. Chưa ghi hoạt động điều trị hoặc khoản thu."
        description={<Space wrap><Button onClick={() => navigate(preview ?
          `${clinicalPreviewTreatment}&reviewId=${saved.id}` :
          `/patients/${id}/visits/${visitId}/treatment?reviewId=${saved.id}${dossierId ? `&dossierId=${dossierId}` : ''}`)}>Mở S22 để ghi chăm sóc thực tế</Button>
          <Button onClick={() => navigate(preview ? clinicalPreviewHome : `/patients/${id}`)}>{preview ? 'Về trang xem thử' : 'Về hồ sơ bệnh nhân'}</Button></Space>} />}
      <Card title="Nguồn và đánh giá"><Form form={form} layout="vertical" onFinish={values => void save(values)}
        initialValues={{ coverage: 'PendingAssessment', outcome: 'AdviceOnly',
          sourceActivityId: search.get('sourceActivityId') ?? undefined }}>
        <Form.Item name="sourceActivityId" label="Hoạt động đã hoàn tất" rules={[{
          validator: async (_, value) => { if (!value && !form.getFieldValue('sourceItemId')) throw new Error('Chọn hoạt động hoặc hạng mục nguồn'); },
        }]}>
          <Select placeholder="Chọn hoạt động trước đây" options={context.activities.map(x => ({
            value: x.id, label: `${new Date(x.performedAt).toLocaleDateString('vi-VN')} · Dịch vụ #${x.serviceId} · ${x.region ?? 'Không ghi vùng'}` }))}
            allowClear onChange={value => { if (value) form.setFieldValue('sourceItemId', undefined); }} />
        </Form.Item>
        {dossier && <Form.Item name="sourceItemId" label="Hoặc hạng mục đã hoàn tất"><Select
          placeholder="Chọn hạng mục nguồn" allowClear
          options={dossier.items.filter(x => x.progress === 'Completed').map(x => ({
            value: x.id, label: `${x.serviceName} · ${x.region ?? 'Không ghi vùng'}` }))}
          onChange={value => { if (value) form.setFieldValue('sourceActivityId', undefined); }} /></Form.Item>}
        {context.activities.length === 0 && !dossier?.items.some(x => x.progress === 'Completed') &&
          <Empty description="Chưa có hoạt động hoặc hạng mục đã hoàn tất để đánh giá" />}
        <Form.Item name="complaint" label="Người bệnh phản ánh" rules={[{ required: true }]}><Input.TextArea rows={3} maxLength={4000} showCount /></Form.Item>
        <Form.Item name="findings" label="Bác sĩ đánh giá" rules={[{ required: true }]}><Input.TextArea rows={3} maxLength={4000} showCount /></Form.Item>
        <div className="treatment-revision-form">
          <Form.Item name="coverage" label="Đánh giá bảo hành"><Select options={[
            { value: 'PendingAssessment', label: 'Cần đánh giá thêm' },
            { value: 'Covered', label: 'Thuộc phạm vi bảo hành' },
            { value: 'NotCovered', label: 'Không thuộc phạm vi bảo hành' }]} /></Form.Item>
          <Form.Item name="outcome" label="Hướng xử trí"><Select options={[
            { value: 'AdviceOnly', label: 'Tư vấn' },
            { value: 'FurtherAssessment', label: 'Cần đánh giá thêm' },
            { value: 'CorrectiveCareNeeded', label: 'Cần chăm sóc hoặc điều trị tiếp' },
            { value: 'NewProblem', label: 'Vấn đề mới' }]} /></Form.Item>
          {coverage !== 'PendingAssessment' && <>
            <Form.Item name="coveredComponent" label="Bộ phận được xem xét"><Input maxLength={500} /></Form.Item>
            <Form.Item name="policy" label="Chính sách bảo hành"><Input maxLength={160} /></Form.Item>
            <Form.Item name="policyVersion" label="Phiên bản chính sách"><Input maxLength={40} /></Form.Item>
            <Form.Item name="exclusions" label="Ngoại trừ"><Input.TextArea maxLength={1000} /></Form.Item>
            <Form.Item className="treatment-revision-wide" name="basis" label="Căn cứ quyết định" rules={[{ required: true }]}><Input.TextArea maxLength={1000} /></Form.Item>
          </>}
          <Form.Item className="treatment-revision-wide" name="nextAction" label="Hành động tiếp theo"><Input.TextArea maxLength={2000} /></Form.Item>
        </div>
        <Button type="primary" htmlType="submit" loading={busy}
          disabled={!sourceActivityId && !sourceItemId}>Lưu đánh giá</Button>
      </Form></Card>
      <Card title="Lịch sử đánh giá">{reviews.length === 0 ? <Empty description="Chưa có đánh giá" /> : reviews.map(row =>
        <Card size="small" key={row.id} className="clinical-work-history">
          <Space wrap><Tag>{row.coverage}</Tag><Tag>{row.outcome}</Tag>
            <Typography.Text type="secondary">{new Date(row.recordedAt).toLocaleString('vi-VN')}</Typography.Text></Space>
          <Typography.Paragraph>{row.findings}</Typography.Paragraph>
        </Card>)}</Card>
    </>}
  </div>;
}
