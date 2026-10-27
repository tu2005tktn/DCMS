import { Alert, AutoComplete, Button, Card, Checkbox, Collapse, Drawer, Empty, Form, Input, InputNumber, Modal, Result, Select, Skeleton, Space, Table, Tabs, Tag, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Link, useLocation, useParams, useSearchParams } from 'react-router-dom';
import { api, type ApiError } from '../../services/api';
import type { AppointmentOptions, PaymentTerm, PricePreview, ServicePrice, TreatmentChange, TreatmentDossier, TreatmentItem } from './types';
import { decisionLabel, money } from './types';
import './treatment.css';
import PlanStepsPanel from './PlanStepsPanel';

type ItemDraft = Omit<Pick<TreatmentItem, 'id' | 'serviceId' | 'region' | 'unit' | 'quantity' | 'plannedOrder' |
  'estimatedMinutes' | 'estimatedStart' | 'estimatedEnd' | 'agreedUnitPriceVnd' | 'decision' |
  'counseling' | 'decisionMaker' | 'representativeBasis' | 'consentDocumentId'>, 'id'> & {
    id: string | null; estimatedMinMinutes?: number | null; estimatedMaxMinutes?: number | null;
    reservedMinutes?: number | null; acknowledgeShortReservation?: boolean;
    estimatedSessions?: number | null; returnEarliestOn?: string | null;
    returnTargetOn?: string | null; returnLatestOn?: string | null;
    referenceUnitPriceVnd?: number | null; discount?: TreatmentItem['discount']; clearDiscount?: boolean;
  };
type ScopeForm = Pick<ItemDraft, 'serviceId' | 'unit' | 'agreedUnitPriceVnd' | 'estimatedMinutes' | 'estimatedStart' | 'estimatedEnd'> & {
  regionChoice?: string; regionOther?: string; quantityChoice: number | 'other'; quantityOther?: number;
  estimatedMinMinutes?: number; estimatedMaxMinutes?: number; reservedMinutes?: number;
  estimatedSessions?: number; returnEarliestOn?: string; returnTargetOn?: string; returnLatestOn?: string;
  acknowledgeShortReservation?: boolean; discountType?: 'Fixed' | 'Percent'; discountValue?: number;
  discountReason?: string; discountQualifyingItemId?: string; clearDiscount?: boolean;
};
type DecisionForm = Pick<ItemDraft, 'decision' | 'counseling' | 'decisionMaker' | 'representativeBasis'>;
type TermForm = { milestone?: string; purpose: string; amountVnd: number; dueOn: string; evidence?: string };
type HistoryItem = { version: number; committedAt: string; actorId: number; snapshotJson: string };

const otherRegion = '__other__';
const teeth = [1, 2, 3, 4].flatMap(quadrant => Array.from({ length: 8 }, (_, index) => `${quadrant}${index + 1}`));
const commonRegions = ['Hàm trên', 'Hàm dưới', 'Toàn hàm', 'Vùng khác'];
const regionOptions = [
  { label: 'Răng', options: teeth.map(tooth => ({ label: `Răng ${tooth}`, value: tooth })) },
  { label: 'Vùng điều trị', options: commonRegions.slice(0, -1).map(region => ({ label: region, value: region })) },
  { label: 'Khác', options: [{ label: 'Vùng khác (nhập mô tả)', value: otherRegion }] },
];
const unitOptions = ['răng', 'lần', 'vùng', 'hàm', 'chiếc'].map(unit => ({ label: unit, value: unit }));
const quantityOptions = [...Array.from({ length: 10 }, (_, index) => ({ label: String(index + 1), value: index + 1 })),
  { label: 'Số khác', value: 'other' as const }];

export default function PlanPage() {
  const { id, dossierId } = useParams();
  const [search] = useSearchParams();
  const location = useLocation();
  const selectedItemId = search.get('item');
  const revisionNotice = (location.state as { revisionNotice?: string } | null)?.revisionNotice;
  const [dossier, setDossier] = useState<TreatmentDossier | null>(null);
  const [options, setOptions] = useState<AppointmentOptions | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [revision, setRevision] = useState(0);
  const [goals, setGoals] = useState('');
  const [items, setItems] = useState<ItemDraft[]>([]);
  const [terms, setTerms] = useState<PaymentTerm[]>([]);
  const [dirty, setDirty] = useState(false);
  const [busy, setBusy] = useState(false);
  const [saveError, setSaveError] = useState<ApiError | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [scopeEditing, setScopeEditing] = useState<number | null>(null);
  const [decisionEditing, setDecisionEditing] = useState<number | null>(null);
  const [termOpen, setTermOpen] = useState(false);
  const [referencePrice, setReferencePrice] = useState<ServicePrice | null>(null);
  const [pricePreview, setPricePreview] = useState<PricePreview | null>(null);
  const [tab, setTab] = useState('items');
  const [history, setHistory] = useState<HistoryItem[] | null>(null);
  const [scopeForm] = Form.useForm<ScopeForm>();
  const [decisionForm] = Form.useForm<DecisionForm>();
  const [termForm] = Form.useForm<TermForm>();
  const requestKey = useRef(crypto.randomUUID());
  const regionChoice = Form.useWatch('regionChoice', scopeForm);
  const quantityChoice = Form.useWatch('quantityChoice', scopeForm);
  const quantityOther = Form.useWatch('quantityOther', scopeForm);
  const unitPrice = Form.useWatch('agreedUnitPriceVnd', scopeForm);
  const selectedServiceId = Form.useWatch('serviceId', scopeForm);
  const durationMax = Form.useWatch('estimatedMaxMinutes', scopeForm);
  const reservedDuration = Form.useWatch('reservedMinutes', scopeForm);
  const discountType = Form.useWatch('discountType', scopeForm);
  const decision = Form.useWatch('decision', decisionForm);

  function changed() { setDirty(true); setSaveError(null); requestKey.current = crypto.randomUUID(); }
  useEffect(() => {
    let active = true;
    setError(null); setDossier(null);
    Promise.all([api<TreatmentDossier>(`/api/dossiers/${dossierId}`),
      api<AppointmentOptions>('/api/appointments/options')])
      .then(([result, available]) => { if (!active) return;
        setDossier(result); setOptions(available); setGoals(result.goals ?? '');
        setItems(result.items.map(x => ({ id: x.id, serviceId: x.serviceId, region: x.region, unit: x.unit,
          quantity: x.quantity, plannedOrder: x.plannedOrder, estimatedMinutes: x.estimatedMinutes,
          estimatedMinMinutes: x.estimatedMinMinutes, estimatedMaxMinutes: x.estimatedMaxMinutes,
          reservedMinutes: x.reservedMinutes, acknowledgeShortReservation: Boolean(x.shortReservationAcknowledgedAt),
          estimatedSessions: x.estimatedSessions, returnEarliestOn: x.returnEarliestOn,
          returnTargetOn: x.returnTargetOn, returnLatestOn: x.returnLatestOn,
          referenceUnitPriceVnd: x.referenceUnitPriceVnd, discount: x.discount,
          estimatedStart: x.estimatedStart, estimatedEnd: x.estimatedEnd,
          agreedUnitPriceVnd: x.agreedUnitPriceVnd, decision: x.decision, counseling: x.counseling,
          decisionMaker: x.decisionMaker, representativeBasis: x.representativeBasis,
          consentDocumentId: x.consentDocumentId })));
        setTerms(result.paymentTerms); setDirty(false);
      }).catch(failure => { if (active) setError(failure as ApiError); });
    return () => { active = false; };
  }, [dossierId, revision]);
  useEffect(() => {
    if (!dossier) return;
    let active = true;
    api<PricePreview>(`/api/dossiers/${dossier.id}/plan/preview`)
      .then(value => { if (active) setPricePreview(value); })
      .catch(() => { if (active) setPricePreview(null); });
    return () => { active = false; };
  }, [dossier?.id, dossier?.version]);
  useEffect(() => {
    if (scopeEditing === null || !selectedServiceId) { setReferencePrice(null); return; }
    let active = true;
    api<ServicePrice>(`/api/service-prices/${selectedServiceId}`)
      .then(value => { if (active) setReferencePrice(value); })
      .catch(() => { if (active) setReferencePrice(null); });
    return () => { active = false; };
  }, [scopeEditing, selectedServiceId]);
  useEffect(() => { if (tab !== 'history' || !dossierId) return;
    let active = true;
    api<HistoryItem[]>(`/api/dossiers/${dossierId}/plan/history`)
      .then(result => { if (active) setHistory(result); }).catch(() => { if (active) setHistory([]); });
    return () => { active = false; };
  }, [tab, dossierId, dossier?.planVersion]);
  useEffect(() => {
    if (!dossier || !selectedItemId) return;
    const frame = requestAnimationFrame(() => document.querySelector('.treatment-selected-item')
      ?.scrollIntoView({ block: 'center' }));
    return () => cancelAnimationFrame(frame);
  }, [dossier, selectedItemId]);

  if (error && !dossier) return <Result status={error.status === 403 ? '403' : error.status === 404 ? '404' : 'error'}
    title={error.message} extra={<Space><Button onClick={() => setRevision(x => x + 1)}>Thử lại</Button>
      <Link to={`/patients/${id}/dossiers/${dossierId}`}>Về đợt điều trị</Link></Space>} />;
  if (!dossier) return <Card><Skeleton active /></Card>;
  const editable = dossier.state === 'Open' && dossier.planState !== 'Committed';
  const editedItem = items[scopeEditing ?? items.length];

  function openScope(index: number) {
    scopeForm.resetFields();
    setScopeEditing(index);
    const item = items[index];
    const knownRegion = item?.region && (teeth.includes(item.region) || commonRegions.includes(item.region));
    scopeForm.setFieldsValue(item ? { ...item,
      regionChoice: knownRegion ? item.region! : item.region ? otherRegion : undefined,
      regionOther: !knownRegion ? item.region ?? undefined : undefined,
      quantityChoice: Number.isInteger(item.quantity) && item.quantity >= 1 && item.quantity <= 10 ? item.quantity : 'other',
      quantityOther: item.quantity,
      estimatedMinutes: item.estimatedMinutes ?? undefined, estimatedStart: item.estimatedStart ?? undefined,
      estimatedEnd: item.estimatedEnd ?? undefined,
      estimatedMinMinutes: item.estimatedMinMinutes ?? undefined,
      estimatedMaxMinutes: item.estimatedMaxMinutes ?? undefined,
      reservedMinutes: item.reservedMinutes ?? undefined, estimatedSessions: item.estimatedSessions ?? undefined,
      returnEarliestOn: item.returnEarliestOn ?? undefined,
      returnTargetOn: item.returnTargetOn ?? undefined, returnLatestOn: item.returnLatestOn ?? undefined,
      acknowledgeShortReservation: item.acknowledgeShortReservation,
      discountType: item.discount?.type, discountValue: item.discount?.value,
      discountReason: item.discount?.reason, discountQualifyingItemId: item.discount?.qualifyingItemId ?? undefined }
      : { serviceId: undefined, regionChoice: undefined, regionOther: undefined, unit: undefined,
        quantityChoice: 1, quantityOther: undefined, estimatedMinutes: undefined,
        estimatedStart: undefined, estimatedEnd: undefined, agreedUnitPriceVnd: undefined });
  }

  function openDecision(index: number) {
    decisionForm.resetFields();
    decisionForm.setFieldsValue({ decision: items[index].decision,
      counseling: items[index].counseling ?? undefined,
      decisionMaker: items[index].decisionMaker ?? undefined,
      representativeBasis: items[index].representativeBasis ?? undefined });
    setDecisionEditing(index);
  }

  function moveItem(index: number, direction: -1 | 1) {
    const ordered = [...items].sort((a, b) => a.plannedOrder - b.plannedOrder);
    const nextIndex = index + direction;
    if (nextIndex < 0 || nextIndex >= ordered.length) return;
    [ordered[index], ordered[nextIndex]] = [ordered[nextIndex], ordered[index]];
    setItems(ordered.map((item, position) => ({ ...item, plannedOrder: position + 1 })));
    changed();
  }

  async function saveDraft() {
    if (!dossier || !editable || busy) return;
    setBusy(true); setSaveError(null);
    try {
      const result = await api<TreatmentChange>(`/api/dossiers/${dossier.id}/plan/draft`, {
        method: 'POST', body: JSON.stringify({ expectedVersion: dossier.version, goals, items: items.map(x => ({
          ...x, id: x.id || null, sourceDiagnosisId: null,
        })), paymentTerms: terms, requestKey: requestKey.current }),
      });
      setDossier(result.dossier); setItems(result.dossier.items.map(x => ({ ...x })));
      setTerms(result.dossier.paymentTerms); setDirty(false); setNotice(result.notification.message);
      requestKey.current = crypto.randomUUID();
    } catch (failure) { setSaveError(failure as ApiError); }
    finally { setBusy(false); }
  }

  async function commit() {
    if (!dossier || !editable || busy || dirty || dossier.planState !== 'Draft') return;
    setBusy(true); setSaveError(null);
    try {
      const result = await api<TreatmentChange>(`/api/dossiers/${dossier.id}/plan/commit`, {
        method: 'POST', body: JSON.stringify({ expectedVersion: dossier.version, requestKey: requestKey.current }),
      });
      setDossier(result.dossier); setNotice(result.notification.message);
      requestKey.current = crypto.randomUUID();
    } catch (failure) { setSaveError(failure as ApiError); }
    finally { setBusy(false); }
  }

  return <main className="treatment-page">
    <Typography.Paragraph><Link to={`/patients/${id}/dossiers/${dossier.id}`}>← Đợt điều trị {dossier.code}</Link></Typography.Paragraph>
    <div className="treatment-page-heading"><div><Typography.Title level={2}>Kế hoạch điều trị</Typography.Title>
      <Typography.Paragraph>{dossier.diagnosis}</Typography.Paragraph></div>
      <Space wrap><Tag>{dossier.planState === 'Committed' ? `Đã chốt · phiên bản ${dossier.planVersion}` : dossier.planState === 'Draft' ? 'Bản nháp' : 'Chưa có kế hoạch'}</Tag>
        {editable && <Link to={`/commercial/treatment?dossier=${dossier.id}`}><Button>Giá và duyệt giảm trừ</Button></Link>}</Space></div>
    {(notice || revisionNotice) && <Alert type="success" showIcon className="form-alert"
      message={notice || revisionNotice} />}
    {saveError && <Alert type="error" showIcon className="form-alert" message={saveError.message}
      description={saveError.code === 'MSG111' ? 'Bản nháp của bạn vẫn ở đây. Đối chiếu phiên bản mới trước khi lưu lại.' : undefined}
      />}
    <Card className="treatment-plan-intro"><label htmlFor="treatment-goals"><strong>Mục tiêu và phương án</strong></label>
      <Input.TextArea id="treatment-goals" rows={3} maxLength={4000} showCount value={goals}
        disabled={!editable} onChange={event => { setGoals(event.target.value); changed(); }} />
      <Typography.Paragraph type="secondary">Kế hoạch là dự kiến đã tư vấn; việc thực hiện và khoản thu được ghi ở màn hình riêng.</Typography.Paragraph>
    </Card>
    <Tabs activeKey={tab} onChange={setTab} items={[
      { key: 'items', label: `Hạng mục (${items.length})`, children: <Card title="Hạng mục điều trị"
        extra={editable && <Button type="primary" onClick={() => openScope(items.length)}>Thêm hạng mục</Button>}>
        {items.length === 0 ? <Empty description="Chưa có hạng mục. Hãy chọn dịch vụ và vùng điều trị." />
          : <Table rowKey={(item: ItemDraft, index: number | undefined) => item.id || String(index)} pagination={false}
              rowClassName={item => item.id === selectedItemId ? 'treatment-selected-item' : ''}
              scroll={{ x: 750 }} dataSource={items} columns={[
                { title: 'Thứ tự dự kiến', width: 112, render: (_: unknown, item: ItemDraft, index: number) =>
                  <Space size={4}><strong>{item.plannedOrder}</strong>{editable && <span className="treatment-order-actions">
                    <Button size="small" aria-label={`Đưa hạng mục ${item.plannedOrder} lên`} disabled={index === 0}
                      onClick={() => moveItem(index, -1)}>↑</Button>
                    <Button size="small" aria-label={`Đưa hạng mục ${item.plannedOrder} xuống`} disabled={index === items.length - 1}
                      onClick={() => moveItem(index, 1)}>↓</Button></span>}</Space> },
                { title: 'Dịch vụ và vùng', render: (_, item: ItemDraft) => <div>
                  <strong>{options?.services.find(x => x.id === item.serviceId)?.name ?? `Dịch vụ #${item.serviceId}`}</strong>
                  <div>{item.region || 'Chưa chọn vùng'}</div></div> },
                { title: 'Số lượng', render: (_, item: ItemDraft) => `${item.quantity} ${item.unit}` },
                { title: 'Giá dự kiến', render: (_, item: ItemDraft) => <div>
                  <strong>{money(Math.round(item.quantity * item.agreedUnitPriceVnd))}</strong>
                  <div className="treatment-muted">{item.quantity} {item.unit} × {money(item.agreedUnitPriceVnd)}</div>
                  {item.referenceUnitPriceVnd != null && <div className="treatment-muted">Tham chiếu: {money(item.referenceUnitPriceVnd)}/đơn vị</div>}
                  {item.discount && <Tag color={item.discount.status === 'Approved' ? 'green' : 'orange'}>
                    Giảm trừ {item.discount.status === 'Approved' ? 'đã duyệt' : 'chờ duyệt'}
                  </Tag>}</div> },
                { title: 'Quyết định', render: (_, item: ItemDraft) => <Tag>{decisionLabel[item.decision]}</Tag> },
                { title: 'Tiến độ', render: (_, item: ItemDraft) => {
                  const current = dossier.items.find(x => x.id === item.id);
                  return current?.progress ?? 'Chưa thực hiện';
                } },
                { title: 'Thao tác', render: (_: unknown, _item: ItemDraft, index: number) => editable
                  ? <Space wrap><Button onClick={() => openScope(index)}>Sửa hạng mục</Button>
                    <Button onClick={() => openDecision(index)}>Ghi quyết định</Button></Space>
                  : <Link to={`/patients/${id}/dossiers/${dossier.id}/plan/revise/${_item.id}?from=plan`}>
                    <Button>Điều chỉnh kế hoạch</Button></Link> },
              ]} />}
        {pricePreview && <div className="treatment-price-summary" aria-live="polite">
          <span>Tổng giá thỏa thuận: {money(pricePreview.grossVnd)}</span>
          <span>Giảm trừ đã duyệt: {money(pricePreview.approvedDiscountVnd)}</span>
          <strong>Tổng các hạng mục đã đồng ý, trước thuế: {money(pricePreview.netBeforeTaxVnd)}</strong>
          {dirty && <Typography.Text type="secondary">Số liệu sẽ cập nhật sau khi lưu nháp.</Typography.Text>}
        </div>}
        {dossier.planState === 'Committed' && <Alert type="info" showIcon className="treatment-section-note"
          message="Thay đổi hạng mục đã chốt cần phiên bản mới ở S62. Ghi điều trị thực tế tại S22." />}
      </Card> },
      { key: 'steps', label: 'Các bước', children: <PlanStepsPanel dossier={dossier} dirty={dirty}
        onChanged={() => setRevision(value => value + 1)} /> },
      { key: 'terms', label: `Điều khoản thanh toán (${terms.length})`, children: <Card title="Mốc thanh toán dự kiến"
        extra={editable && <Button onClick={() => { termForm.resetFields(); setTermOpen(true); }}>Thêm mốc</Button>}>
        <Typography.Paragraph type="secondary">Ngày đến hạn không tạo khoản phải thu hoặc xác nhận đã trả.</Typography.Paragraph>
        {terms.length === 0 ? <Empty description="Chưa có mốc thanh toán được thỏa thuận." />
          : terms.map((term, index) => <div key={`${term.purpose}-${index}`} className="treatment-item-row">
            <div><strong>{term.milestone || term.purpose}</strong><div>{term.purpose} · Đến hạn {term.dueOn} · {money(term.amountVnd)}</div>
              {term.evidence && <div className="treatment-muted">Căn cứ: {term.evidence}</div>}</div>
            {editable && <Button onClick={() => { setTerms(current => current.filter((_, i) => i !== index)); changed(); }}>Bỏ</Button>}
          </div>)}
      </Card> },
      { key: 'history', label: 'Lịch sử', children: <Card>{history === null ? <Skeleton active />
        : history.length === 0 ? <Empty description="Chưa có phiên bản kế hoạch chính thức." />
        : history.map(entry => <div className="treatment-item-row" key={entry.version}>
          <strong>Phiên bản {entry.version}</strong><span>{new Date(entry.committedAt).toLocaleString('vi-VN')}</span></div>)}</Card> },
    ]} />
    {editable && <Space wrap className="treatment-plan-actions">
      <Button onClick={() => void saveDraft()} loading={busy}>Lưu nháp kế hoạch</Button>
      <Button type="primary" onClick={() => void commit()} loading={busy}
        disabled={dirty || dossier.planState !== 'Draft' || dossier.items.some(x => x.discount?.status === 'Proposed')}>Chốt kế hoạch</Button>
      {dirty && <Typography.Text type="secondary">Lưu nháp trước khi chốt.</Typography.Text>}
      {dossier.items.some(x => x.discount?.status === 'Proposed') &&
        <Typography.Text type="warning">Đề xuất giảm trừ cần được duyệt trước khi chốt.</Typography.Text>}
    </Space>}
    <Drawer open={scopeEditing !== null} title={scopeEditing === items.length ? 'Thêm hạng mục' : 'Sửa hạng mục'}
      width={520} onClose={() => setScopeEditing(null)}
      extra={<Button type="primary" onClick={() => scopeForm.submit()}>Lưu hạng mục</Button>}>
      <Typography.Paragraph type="secondary">Chọn dịch vụ và phạm vi điều trị. Quyết định của bệnh nhân được ghi riêng sau khi tư vấn.</Typography.Paragraph>
      <Form form={scopeForm} layout="vertical" onFinish={values => {
        const min = values.estimatedMinMinutes;
        const max = values.estimatedMaxMinutes;
        const selected = values.estimatedMinutes;
        const reserved = values.reservedMinutes;
        if ((min != null) !== (max != null) ||
          (min != null && (selected == null || min > selected || selected > max!)) ||
          (reserved != null && (selected == null || reserved < selected)) ||
          (max != null && reserved != null && reserved < max && !values.acknowledgeShortReservation)) {
          scopeForm.setFields([{ name: 'estimatedMinutes', errors: ['Kiểm tra khoảng thời gian, thời gian giữ ghế và xác nhận cảnh báo.'] }]);
          return;
        }
        const returnDates = [values.returnEarliestOn, values.returnTargetOn, values.returnLatestOn];
        if (returnDates.some(Boolean) && (returnDates.some(value => !value) ||
          returnDates[0]! > returnDates[1]! || returnDates[1]! > returnDates[2]!)) {
          scopeForm.setFields([{ name: 'returnTargetOn', errors: ['Chọn đủ ba ngày theo thứ tự sớm nhất, mục tiêu, muộn nhất.'] }]);
          return;
        }
        if (!values.clearDiscount && (values.discountType || values.discountValue || values.discountReason ||
          values.discountQualifyingItemId) && (!values.discountType || !values.discountValue || !values.discountReason?.trim())) {
          scopeForm.setFields([{ name: 'discountReason', errors: ['Chọn loại, mức giảm và nhập lý do giảm trừ.'] }]);
          return;
        }
        const index = scopeEditing ?? items.length;
        const previous = items[index];
        const region = values.regionChoice === otherRegion ? values.regionOther?.trim() || null : values.regionChoice || null;
        const quantity = values.quantityChoice === 'other' ? values.quantityOther! : values.quantityChoice;
        const next: ItemDraft = {
          id: previous?.id ?? null, serviceId: values.serviceId, region,
          unit: values.unit, quantity,
          plannedOrder: previous?.plannedOrder ?? Math.max(0, ...items.map(item => item.plannedOrder)) + 1,
          agreedUnitPriceVnd: values.agreedUnitPriceVnd,
          estimatedMinutes: values.estimatedMinutes ?? null, estimatedStart: values.estimatedStart ?? null,
          estimatedEnd: values.estimatedEnd ?? null,
          estimatedMinMinutes: values.estimatedMinMinutes ?? null,
          estimatedMaxMinutes: values.estimatedMaxMinutes ?? null,
          reservedMinutes: values.reservedMinutes ?? null,
          acknowledgeShortReservation: Boolean(values.acknowledgeShortReservation),
          estimatedSessions: values.estimatedSessions ?? null,
          returnEarliestOn: values.returnEarliestOn ?? null,
          returnTargetOn: values.returnTargetOn ?? null, returnLatestOn: values.returnLatestOn ?? null,
          referenceUnitPriceVnd: previous?.referenceUnitPriceVnd ?? referencePrice?.referenceUnitPriceVnd ?? null,
          discount: values.clearDiscount ? null : values.discountType && values.discountValue && values.discountReason
            ? { type: values.discountType, value: values.discountValue, reason: values.discountReason,
              qualifyingItemId: values.discountQualifyingItemId ?? null, status: 'Proposed',
              decidedBy: null, decidedAt: null, amountVnd: 0 }
            : previous?.discount ?? null,
          clearDiscount: Boolean(values.clearDiscount),
          decision: previous?.decision ?? 'Proposed', counseling: previous?.counseling ?? null,
          decisionMaker: previous?.decisionMaker ?? null, representativeBasis: previous?.representativeBasis ?? null,
          consentDocumentId: previous?.consentDocumentId ?? null,
        };
        setItems(current => index < current.length ? current.map((x, i) => i === index ? next : x) : [...current, next]);
        changed(); setScopeEditing(null);
      }}>
        <Form.Item name="serviceId" label="Dịch vụ" rules={[{ required: true, message: 'Chọn dịch vụ.' }]}>
          <Select showSearch optionFilterProp="label" placeholder="Chọn dịch vụ" options={options?.services.filter(x => x.branchId === dossier.branchId)
            .map(x => ({ value: x.id, label: x.name })) ?? []} />
        </Form.Item>
        <Form.Item name="regionChoice" label="Răng hoặc vùng điều trị">
          <Select showSearch optionFilterProp="label" allowClear placeholder="Chọn răng hoặc vùng" options={regionOptions}
            onChange={value => { if (teeth.includes(value) && scopeEditing === items.length) scopeForm.setFieldValue('unit', 'răng'); }} />
        </Form.Item>
        {regionChoice === otherRegion && <Form.Item name="regionOther" label="Mô tả vùng điều trị"
          rules={[{ required: true, whitespace: true, message: 'Nhập vùng điều trị khi chọn Vùng khác.' }]}>
          <Input maxLength={80} /></Form.Item>}
        <div className="treatment-scope-grid">
          <Form.Item name="quantityChoice" label="Số lượng" rules={[{ required: true, message: 'Chọn số lượng.' }]}>
            <Select options={quantityOptions} /></Form.Item>
          <Form.Item name="unit" label="Đơn vị" rules={[{ required: true, message: 'Chọn đơn vị.' }]}>
            <Select placeholder="Chọn đơn vị" options={items[scopeEditing ?? items.length]?.unit &&
              !unitOptions.some(option => option.value === items[scopeEditing ?? items.length].unit)
              ? [...unitOptions, { label: items[scopeEditing ?? items.length].unit, value: items[scopeEditing ?? items.length].unit }]
              : unitOptions} /></Form.Item>
        </div>
        {quantityChoice === 'other' && <Form.Item name="quantityOther" label="Số lượng khác"
          rules={[{ required: true, message: 'Nhập số lượng.' }]}>
          <InputNumber min={0.001} max={999999999} precision={3} className="treatment-number-full" /></Form.Item>}
        <Form.Item name="agreedUnitPriceVnd" label="Giá thỏa thuận mỗi đơn vị (VND)"
          extra="Giá này là dự kiến đã thỏa thuận, chưa tạo khoản phải thu."
          rules={[{ required: true, message: 'Nhập giá đã thỏa thuận.' }]}>
          <InputNumber min={0} precision={0} className="treatment-number-full" /></Form.Item>
        <Alert type="info" showIcon className="treatment-section-note"
          message={referencePrice?.referenceUnitPriceVnd == null
            ? 'Dịch vụ chưa có giá tham chiếu' : `Giá tham chiếu hiện tại: ${money(referencePrice.referenceUnitPriceVnd)}`}
          description={editedItem?.referenceUnitPriceVnd != null
            ? `Giá tham chiếu đã lưu cho hạng mục: ${money(editedItem.referenceUnitPriceVnd)}. Đổi giá catalog không sửa snapshot.`
            : 'Giá thỏa thuận có thể khác giá tham chiếu; chưa ghi nhận khoản thu.'} />
        {typeof unitPrice === 'number' && <div className="treatment-estimate" role="status">
          <span>Thành tiền dự kiến</span><strong>{money(Math.round((quantityChoice === 'other' ? Number(quantityOther || 0) : Number(quantityChoice || 0)) * unitPrice))}</strong>
        </div>}
        <Collapse ghost items={[{ key: 'timing', label: 'Dự kiến thực hiện (không bắt buộc)', children: <>
          <Typography.Paragraph type="secondary">Thời gian và ngày dự kiến không tự tạo lịch hẹn.</Typography.Paragraph>
          <div className="treatment-scope-grid">
            <Form.Item name="estimatedMinMinutes" label="Ước lượng ngắn nhất (phút)"><InputNumber min={1} max={480} className="treatment-number-full" /></Form.Item>
            <Form.Item name="estimatedMaxMinutes" label="Ước lượng dài nhất (phút)"><InputNumber min={1} max={480} className="treatment-number-full" /></Form.Item>
          </div>
          <Form.Item name="estimatedMinutes" label="Thời gian bác sĩ chọn (phút)"><InputNumber min={1} max={480} className="treatment-number-full" /></Form.Item>
          <Form.Item name="reservedMinutes" label="Thời gian dự kiến giữ ghế"
            extra="Chọn theo bước 15 phút. Đây không phải giờ kết thúc đã cam kết.">
            <Select allowClear placeholder="Chọn thời gian" options={Array.from({ length: 32 }, (_, index) => ({
              value: (index + 1) * 15, label: `${(index + 1) * 15} phút`,
            }))} /></Form.Item>
          {durationMax && reservedDuration && reservedDuration < durationMax && <>
            <Alert type="warning" showIcon message="Thời gian giữ ghế ngắn hơn mức ước lượng dài nhất." />
            <Form.Item name="acknowledgeShortReservation" valuePropName="checked"
              rules={[{ validator: (_, value) => value ? Promise.resolve() : Promise.reject(new Error('Xác nhận trước khi lưu.')) }]}>
              <Checkbox>Tôi đã cân nhắc và xác nhận thời gian giữ ghế này</Checkbox>
            </Form.Item>
          </>}
          <Form.Item name="estimatedSessions" label="Số buổi ước lượng">
            <Select allowClear placeholder="Chưa ước lượng" options={Array.from({ length: 12 }, (_, index) => ({
              value: index + 1, label: `${index + 1} buổi`,
            }))} /></Form.Item>
          <Typography.Text strong>Cửa sổ quay lại dự kiến</Typography.Text>
          <div className="treatment-scope-grid">
            <Form.Item name="returnEarliestOn" label="Sớm nhất"><Input type="date" /></Form.Item>
            <Form.Item name="returnTargetOn" label="Mục tiêu"><Input type="date" /></Form.Item>
            <Form.Item name="returnLatestOn" label="Muộn nhất"><Input type="date" /></Form.Item>
          </div>
          <Space align="start" wrap>
            <Form.Item name="estimatedStart" label="Bắt đầu dự kiến"><Input type="date" /></Form.Item>
            <Form.Item name="estimatedEnd" label="Kết thúc dự kiến"><Input type="date" /></Form.Item>
          </Space></> }]}/>
        <Collapse ghost items={[{ key: 'discount', label: 'Đề xuất giảm trừ (nếu có)', children: <>
          <Typography.Paragraph type="secondary">Bác sĩ trong chi nhánh được phân công có thể tự duyệt giảm trừ ở mục “Giá và duyệt giảm trừ” trước khi chốt. Giảm trừ không tạo khoản thu.</Typography.Paragraph>
          <Form.Item name="discountType" label="Loại giảm trừ"><Select allowClear options={[
            { value: 'Fixed', label: 'Số tiền VND' }, { value: 'Percent', label: 'Phần trăm' },
          ]} /></Form.Item>
          <Form.Item name="discountValue" label="Mức giảm"><InputNumber min={0}
            max={discountType === 'Percent' ? 100 : undefined}
            precision={discountType === 'Fixed' ? 0 : 2} className="treatment-number-full" /></Form.Item>
          <Form.Item name="discountReason" label="Lý do"><Input.TextArea rows={2} maxLength={1000} /></Form.Item>
          <Form.Item name="discountQualifyingItemId" label="Hạng mục cần có để được giảm (nếu có)">
            <Select allowClear options={items.filter(x => x.id && x.id !== editedItem?.id).map(x => ({
              value: x.id!, label: `${options?.services.find(s => s.id === x.serviceId)?.name ?? x.serviceId} · ${x.region ?? 'Chưa chọn vùng'}`,
            }))} /></Form.Item>
          {editedItem?.discount && <Form.Item name="clearDiscount" valuePropName="checked"><Checkbox>Bỏ đề xuất giảm trừ</Checkbox></Form.Item>}
        </> }]} />
      </Form>
    </Drawer>
    <Drawer open={decisionEditing !== null} title="Ghi quyết định hạng mục" width={520}
      onClose={() => setDecisionEditing(null)}
      extra={<Button type="primary" onClick={() => decisionForm.submit()}>Lưu quyết định</Button>}>
      {decisionEditing !== null && <Typography.Paragraph type="secondary">
        {options?.services.find(x => x.id === items[decisionEditing]?.serviceId)?.name ?? 'Hạng mục điều trị'}
        {items[decisionEditing]?.region ? ` · ${items[decisionEditing].region}` : ''}
      </Typography.Paragraph>}
      <Form form={decisionForm} layout="vertical" onFinish={values => {
        const index = decisionEditing;
        if (index === null) return;
        setItems(current => current.map((item, position) => position === index ? {
          ...item, decision: values.decision, counseling: values.counseling?.trim() || null,
          decisionMaker: values.decisionMaker?.trim() || null,
          representativeBasis: values.representativeBasis?.trim() || null,
        } : item));
        changed(); setDecisionEditing(null);
      }}>
        <Form.Item name="decision" label="Quyết định cho hạng mục" rules={[{ required: true }]}>
          <Select options={Object.entries(decisionLabel).map(([value, label]) => ({ value, label }))} /></Form.Item>
        <Form.Item name="counseling" label="Tư vấn: phương án, lợi ích/rủi ro, vật liệu và số buổi"
          rules={[{ required: decision !== 'Proposed', whitespace: true,
            message: 'Ghi nội dung đã tư vấn trước khi lưu quyết định.' }, { max: 4000 }]}>
          <Input.TextArea rows={4} maxLength={4000} /></Form.Item>
        <Form.Item name="decisionMaker" label="Người đưa ra quyết định"
          rules={[{ required: decision !== 'Proposed', whitespace: true, message: 'Chọn hoặc nhập người quyết định.' }]}>
          <AutoComplete options={[{ value: 'Bệnh nhân' }, { value: 'Người đại diện' }]}
            placeholder="Chọn bệnh nhân hoặc người đại diện" maxLength={160} /></Form.Item>
        <Form.Item name="representativeBasis" label="Căn cứ đại diện (nếu có)"><Input maxLength={1000} /></Form.Item>
        <Alert type="info" showIcon message="File đồng ý được quản lý tại S20 và phải gắn đúng phiên bản kế hoạch."
          description="S20 chưa được nối trong màn này; không đánh dấu đã có file đồng ý khi chưa xác thực nguồn." />
      </Form>
    </Drawer>
    <Modal open={termOpen} title="Thêm mốc thanh toán dự kiến" okText="Thêm mốc" onOk={() => termForm.submit()}
      onCancel={() => setTermOpen(false)}>
      <Form form={termForm} layout="vertical" onFinish={value => {
        setTerms(current => [...current, { milestone: value.milestone ?? null,
          purpose: value.purpose, amountVnd: value.amountVnd,
          dueOn: value.dueOn, evidence: value.evidence ?? null, planVersion: null }]); changed(); setTermOpen(false);
      }}>
        <Form.Item name="milestone" label="Mốc thanh toán"><Input maxLength={160} placeholder="Ví dụ: Sau buổi thứ hai" /></Form.Item>
        <Form.Item name="purpose" label="Mục đích/mốc" rules={[{ required: true, whitespace: true }]}><Input maxLength={160} /></Form.Item>
        <Form.Item name="amountVnd" label="Số tiền dự kiến (VND)" rules={[{ required: true }]}>
          <InputNumber min={0} precision={0} className="treatment-number-full" /></Form.Item>
        <Form.Item name="dueOn" label="Ngày đến hạn" rules={[{ required: true }]}><Input type="date" /></Form.Item>
        <Form.Item name="evidence" label="Căn cứ thỏa thuận"><Input.TextArea rows={2} maxLength={1000} /></Form.Item>
      </Form>
    </Modal>
  </main>;
}
