import { Alert, Button, Card, Col, Empty, Form, Input, InputNumber, Modal, Row, Select, Space, Spin, Table, Tag, Timeline, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import { usePermissions } from '../authentication/usePermissions';
import PrescriptionPrintModal from './PrescriptionPrintModal';
import {
  doseSummary, mealLabel,
  type ClinicalAllergyWarning, type MedicineOption, type Prescription, type PrescriptionLine,
  type PrescriptionSummary, type VisitPrescription,
} from './types';

type FormValues = {
  medicineId?: string;
  customMedicineName?: string;
  customUnit?: string;
  route: string;
  morningDose: string;
  noonDose: string;
  afternoonDose: string;
  eveningDose: string;
  mealTiming: string;
  days: number;
  quantity: number;
  instructions?: string;
};

type QuickMedicineValues = {
  code: string;
  name: string;
  activeIngredient?: string;
  strength?: string;
  dosageForm?: string;
  baseUnit: string;
  packaging?: string;
  referencePrice?: number | null;
  instructions?: string;
};

type CreatedMedicine = MedicineOption & { route?: string; instructions?: string | null };

type Props = {
  visitId: string;
  branchId: number;
  readOnly?: boolean;
  /** Signed prescriptions may be corrected while the visit is InProgress or Completed (docs/specs/PRESCRIPTION-MANAGEMENT.md §1.4). */
  correctionAllowed?: boolean;
  disabled?: boolean;
  initiallyExpanded?: boolean;
  onDirtyChange?: (dirty: boolean) => void;
  onBusyChange?: (busy: boolean) => void;
};

const statusTag = (status: string) =>
  status === 'Finalized' ? <Tag color="success">ĐÃ HOÀN TẤT</Tag>
    : status === 'Superseded' ? <Tag>ĐÃ ĐƯỢC THAY THẾ</Tag>
    : <Tag color="processing">BẢN NHÁP</Tag>;

const numericDose = (value?: string) => {
  const n = Number((value ?? '').replace(',', '.').trim());
  return Number.isFinite(n) && n >= 0 ? n : NaN;
};

export default function PrescriptionPanel({
  visitId,
  branchId,
  readOnly = false,
  correctionAllowed = false,
  disabled = false,
  initiallyExpanded = false,
  onDirtyChange,
  onBusyChange,
}: Props) {
  const has = usePermissions();
  const { user } = useAuth();
  const canManage = has('prescriptions.manage');
  const canAddMedicine = user?.permissions.includes('catalog.medicines.manage') ?? false;

  const [rx, setRx] = useState<Prescription | null>();
  const [history, setHistory] = useState<PrescriptionSummary[]>([]);
  const [loadError, setLoadError] = useState<string>();
  const [expanded, setExpanded] = useState(initiallyExpanded);
  const [options, setOptions] = useState<MedicineOption[]>([]);
  const [query, setQuery] = useState('');
  const [optionsLoading, setOptionsLoading] = useState(false);
  const [optionsError, setOptionsError] = useState<string>();

  const [editing, setEditing] = useState<PrescriptionLine>();
  const [saveError, setSaveError] = useState<ApiError>();
  const [notice, setNotice] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [advice, setAdvice] = useState('');
  const [adviceDirty, setAdviceDirty] = useState(false);

  const [printModalOpen, setPrintModalOpen] = useState(false);
  const [correctionOpen, setCorrectionOpen] = useState(false);
  const [correctionReason, setCorrectionReason] = useState('');
  const [quickAddOpen, setQuickAddOpen] = useState(false);
  const [catalogAddOpen, setCatalogAddOpen] = useState(false);
  const [catalogSaving, setCatalogSaving] = useState(false);
  const [catalogError, setCatalogError] = useState<string>();
  const [safetyWarnings, setSafetyWarnings] = useState<ClinicalAllergyWarning[]>([]);

  const [form] = Form.useForm<FormValues>();
  const [quickAddForm] = Form.useForm<{ name: string; unit: string; instructions?: string }>();
  const [catalogMedicineForm] = Form.useForm<QuickMedicineValues>();
  const watchedMedicineId = Form.useWatch('medicineId', form);
  const watchedCustomName = Form.useWatch('customMedicineName', form);
  const watchedCustomUnit = Form.useWatch('customUnit', form);
  const generation = useRef(0);
  const saving = useRef(false);
  // One requestKey per distinct command payload so network retries replay instead of duplicating.
  const pending = useRef<{ fingerprint: string; key: string } | undefined>(undefined);
  const pendingMedicineCreate = useRef<{ fingerprint: string; key: string } | undefined>(undefined);
  const base = `/api/prescriptions/visits/${visitId}`;

  // undefined = loading, null = no prescription yet (first line creates the draft).
  const lines = rx === undefined ? null : rx?.lines ?? [];
  const isDraft = rx == null || rx.status === 'Draft';
  const canEditDraft = canManage && isDraft && (!readOnly || (correctionAllowed && (rx?.revision ?? 1) > 1));
  const canCorrect = canManage && correctionAllowed && rx?.status === 'Finalized';
  const anyDirty = dirty || adviceDirty;

  useEffect(() => { onDirtyChange?.(anyDirty); }, [anyDirty, onDirtyChange]);
  useEffect(() => () => { onDirtyChange?.(false); }, [onDirtyChange]);
  useEffect(() => { onBusyChange?.(busy); }, [busy, onBusyChange]);
  useEffect(() => () => { onBusyChange?.(false); }, [onBusyChange]);

  const keyFor = (fingerprint: string) => {
    if (pending.current?.fingerprint !== fingerprint) pending.current = { fingerprint, key: crypto.randomUUID() };
    return pending.current.key;
  };

  const checkSafety = async (token = generation.current) => {
    try {
      const warnings = await api<ClinicalAllergyWarning[]>(`${base}/safety-check`);
      if (token === generation.current) setSafetyWarnings(warnings);
    } catch {
      // Warnings support the doctor's review; a failed check never blocks prescribing.
    }
  };

  const load = async (token = generation.current, keepAdvice = false) => {
    setLoadError(undefined);
    try {
      const value = await api<VisitPrescription>(`${base}/prescription`);
      if (token !== generation.current) return;
      setRx(value.current);
      setHistory(value.history);
      if (!keepAdvice) { setAdvice(value.current?.generalAdvice ?? ''); setAdviceDirty(false); }
      setExpanded(v => v || (value.current?.lines.length ?? 0) > 0);
      void checkSafety(token);
    } catch (e) {
      if (token === generation.current) setLoadError((e as Error).message);
    }
  };

  useEffect(() => {
    const token = ++generation.current;
    setRx(undefined);
    setHistory([]);
    setEditing(undefined);
    setDirty(false);
    setAdviceDirty(false);
    setBusy(false);
    saving.current = false;
    pending.current = undefined;
    setSaveError(undefined);
    setNotice(undefined);
    setExpanded(initiallyExpanded);
    setQuery('');
    setOptions([]);
    setSafetyWarnings([]);
    form.resetFields();
    void load(token);
    return () => { generation.current++; };
  }, [visitId, branchId]);

  useEffect(() => {
    if (!canEditDraft) {
      setEditing(undefined);
      setDirty(false);
      form.resetFields();
    }
  }, [canEditDraft, form]);

  useEffect(() => {
    if (!expanded || !canEditDraft) return;
    let active = true;
    setOptionsLoading(true);
    setOptionsError(undefined);
    const timer = window.setTimeout(() => {
      void api<MedicineOption[]>(`/api/prescriptions/medicine-options?${new URLSearchParams({ branchId: String(branchId), q: query })}`)
        .then(rows => { if (active) setOptions(rows); })
        .catch(e => { if (active) { setOptions([]); setOptionsError((e as Error).message); } })
        .finally(() => { if (active) setOptionsLoading(false); });
    }, 250);
    return () => { active = false; window.clearTimeout(timer); };
  }, [expanded, canEditDraft, branchId, visitId, query]);

  // Runs one write; on MSG111 the form stays as typed and the server copy is reloaded for comparison.
  const run = async <T,>(work: () => Promise<T>, onDone: (value: T) => void | Promise<void>) => {
    if (saving.current || disabled) return;
    const token = generation.current;
    saving.current = true;
    setBusy(true);
    setSaveError(undefined);
    setNotice(undefined);
    try {
      const value = await work();
      if (token !== generation.current) return;
      pending.current = undefined;
      await onDone(value);
    } catch (e) {
      if (token !== generation.current) return;
      const error = e as ApiError;
      setSaveError(error);
      if (error.code === 'MSG111' || error.code === 'MSG205') void load(token, true);
    } finally {
      if (token === generation.current) {
        saving.current = false;
        setBusy(false);
      }
    }
  };

  const resetLineForm = () => {
    form.resetFields();
    setEditing(undefined);
    setDirty(false);
    setSaveError(undefined);
  };

  const edit = (line: PrescriptionLine) => {
    if (!canEditDraft) return;
    if (dirty && !window.confirm('Bỏ phần kê đơn chưa lưu để sửa dòng thuốc này?')) return;
    setExpanded(true);
    setEditing(line);
    setSaveError(undefined);
    setDirty(false);
    form.setFieldsValue({
      medicineId: line.medicineId ?? undefined,
      customMedicineName: line.medicineId ? undefined : line.medicineName,
      customUnit: line.medicineId ? undefined : line.unit,
      route: line.route, morningDose: line.morningDose, noonDose: line.noonDose,
      afternoonDose: line.afternoonDose, eveningDose: line.eveningDose, mealTiming: line.mealTiming,
      days: line.days, quantity: line.quantity, instructions: line.instructions,
    });
  };

  const deleteLine = (line: PrescriptionLine) => {
    if (!canEditDraft || !window.confirm(`Xóa thuốc "${line.medicineName}" khỏi đơn nháp?`)) return;
    const key = keyFor(JSON.stringify({ op: 'delete', id: line.id, version: line.version }));
    void run(
      () => api(`${base}/lines/${line.id}?${new URLSearchParams({ requestKey: key, expectedVersion: String(line.version) })}`, { method: 'DELETE' }),
      async () => { if (editing?.id === line.id) resetLineForm(); await load(generation.current, adviceDirty); },
    );
  };

  // Total = ceil(sum of numeric doses per day × days); non-numeric doses ("1/2 gói") leave the quantity to the doctor.
  const recalculate = () => {
    const v = form.getFieldsValue();
    const doses = [v.morningDose, v.noonDose, v.afternoonDose, v.eveningDose].map(numericDose);
    if (doses.some(Number.isNaN)) return;
    const daily = doses.reduce((a, b) => a + b, 0);
    if (daily > 0 && v.days > 0) form.setFieldValue('quantity', Math.min(10000, Math.ceil(daily * v.days)));
  };

  const save = (values: FormValues) => {
    if (!canEditDraft || lines == null || loadError) return;
    const body = {
      expectedVersion: editing?.version ?? 0,
      medicineId: values.medicineId || null,
      customMedicineName: values.medicineId ? null : values.customMedicineName,
      customUnit: values.medicineId ? null : values.customUnit,
      route: values.route || '',
      morningDose: values.morningDose || '0',
      noonDose: values.noonDose || '0',
      afternoonDose: values.afternoonDose || '0',
      eveningDose: values.eveningDose || '0',
      mealTiming: values.mealTiming || 'AfterMeal',
      days: values.days || 1,
      quantity: values.quantity,
      instructions: values.instructions ?? '',
    };
    const target = editing ? `${base}/lines/${editing.id}` : `${base}/lines`;
    const key = keyFor(JSON.stringify({ target, ...body }));
    void run(
      () => api<PrescriptionLine>(target, { method: editing ? 'PUT' : 'POST', body: JSON.stringify({ ...body, requestKey: key }) }),
      async () => { resetLineForm(); await load(generation.current, adviceDirty); },
    );
  };

  const saveAdvice = () => {
    if (!canEditDraft) return;
    const body = { expectedVersion: rx?.version ?? 0, generalAdvice: advice.trim() };
    const key = keyFor(JSON.stringify({ op: 'advice', ...body }));
    void run(
      () => api<Prescription>(`${base}/prescription`, { method: 'PUT', body: JSON.stringify({ ...body, requestKey: key }) }),
      async value => { setAdviceDirty(false); setRx(value); await load(generation.current); },
    );
  };

  const createCatalogMedicine = async () => {
    if (!canAddMedicine || catalogSaving) return;
    let values: QuickMedicineValues;
    try { values = await catalogMedicineForm.validateFields(); } catch { return; }
    const entry = {
      code: values.code.trim(),
      name: values.name.trim(),
      activeIngredient: values.activeIngredient?.trim() || null,
      strength: values.strength?.trim() || null,
      dosageForm: values.dosageForm?.trim() || null,
      baseUnit: values.baseUnit.trim(),
      route: null,
      packaging: values.packaging?.trim() || null,
      contraindications: null,
      allergyWarnings: null,
      instructions: values.instructions?.trim() || null,
      referencePrice: values.referencePrice ?? null,
      prescriptionEligible: true,
      internalStockTracked: false,
    };
    const fingerprint = JSON.stringify(entry);
    if (pendingMedicineCreate.current?.fingerprint !== fingerprint) {
      pendingMedicineCreate.current = { fingerprint, key: crypto.randomUUID() };
    }
    setCatalogSaving(true);
    setCatalogError(undefined);
    try {
      const created = await api<CreatedMedicine>('/api/system-settings/settings/catalogs/medicines', {
        method: 'POST',
        body: JSON.stringify({ requestKey: pendingMedicineCreate.current.key, entry }),
      });
      pendingMedicineCreate.current = undefined;
      const option: MedicineOption = {
        ...created,
        route: created.route ?? '',
        defaultInstructions: created.instructions ?? '',
      };
      setOptions(current => [option, ...current.filter(item => item.id !== option.id)]);
      form.setFieldsValue({
        medicineId: option.id,
        customMedicineName: undefined,
        customUnit: undefined,
        route: option.route,
        instructions: option.defaultInstructions ?? '',
      });
      setDirty(true);
      setQuery(option.name);
      setCatalogAddOpen(false);
      catalogMedicineForm.resetFields();
    } catch (error) {
      setCatalogError((error as Error).message || 'Không thể lưu thuốc vào danh mục.');
    } finally {
      setCatalogSaving(false);
    }
  };

  const finalizePrescription = () => {
    if (!rx || rx.lines.length === 0) return;
    if (adviceDirty) { Modal.warning({ title: 'Lời dặn chung chưa lưu', content: 'Lưu hoặc hoàn tác lời dặn chung trước khi hoàn tất kê đơn.' }); return; }
    if (dirty) { Modal.warning({ title: 'Dòng thuốc chưa lưu', content: 'Lưu hoặc hủy dòng thuốc đang nhập trước khi hoàn tất kê đơn.' }); return; }
    Modal.confirm({
      title: 'Hoàn tất kê đơn?',
      content: 'Đơn đã hoàn tất sẽ bị khóa sửa, bệnh nhân xem được trên ứng dụng. Muốn thay đổi sau đó phải tạo bản điều chỉnh có lý do.',
      okText: 'Hoàn tất kê đơn',
      cancelText: 'Quay lại',
      onOk: () => {
        const body = { expectedVersion: rx.version, clinicalNote: null };
        const key = keyFor(JSON.stringify({ op: 'finalize', ...body }));
        return run(
          () => api<Prescription>(`${base}/finalize`, { method: 'POST', body: JSON.stringify({ ...body, requestKey: key }) }),
          async () => { setNotice('Đã hoàn tất kê đơn. Có thể in đơn hoặc để bệnh nhân xem trên ứng dụng.'); await load(generation.current); },
        );
      },
    });
  };

  const createCorrection = () => {
    if (!rx) return;
    const reason = correctionReason.trim();
    if (!reason) return;
    const body = { expectedVersion: rx.version, reason };
    const key = keyFor(JSON.stringify({ op: 'correction', ...body }));
    void run(
      () => api<Prescription>(`${base}/corrections`, { method: 'POST', body: JSON.stringify({ ...body, requestKey: key }) }),
      async () => { setCorrectionOpen(false); setCorrectionReason(''); setExpanded(true); await load(generation.current); },
    );
  };

  const selectedOptions = [...options];
  if (editing?.medicineId && !selectedOptions.some(o => o.id === editing.medicineId)) {
    selectedOptions.push({
      id: editing.medicineId, code: editing.medicineCode, name: editing.medicineName, strength: editing.strength,
      dosageForm: editing.dosageForm, baseUnit: editing.unit, route: editing.route,
    });
  }
  const selectedMedicine = selectedOptions.find(o => o.id === watchedMedicineId);

  const errorMessage = (e: ApiError) =>
    e.code === 'MSG111' ? 'Đơn thuốc vừa được thay đổi ở nơi khác. Phần bạn đang nhập vẫn được giữ; đơn trên máy chủ đã được tải lại để so sánh trước khi lưu tiếp.'
      : e.code === 'MSG205' ? 'Đơn thuốc đã hoàn tất hoặc lượt khám không còn cho phép sửa. Dữ liệu đã được tải lại.'
      : e.message;

  return (
    <section aria-label="Đơn thuốc khám">
      <Card
        size="small"
        title={
          <Space wrap>
            <span>Đơn thuốc (mua ngoài)</span>
            {rx && statusTag(rx.status)}
            {rx && rx.revision > 1 && <Tag color="orange">Bản điều chỉnh {rx.revision - 1}</Tag>}
          </Space>
        }
        style={{ margin: '16px 0', borderColor: rx?.status === 'Finalized' ? '#52c41a' : undefined }}
        extra={
          lines && !loadError ? (
            <Space wrap>
              {lines.length > 0 && <Button size="small" onClick={() => setPrintModalOpen(true)}>In đơn thuốc</Button>}
              {canEditDraft && lines.length > 0 && (
                <Button size="small" type="primary" onClick={finalizePrescription} loading={busy} disabled={disabled}>Hoàn tất kê đơn</Button>
              )}
              {canCorrect && <Button size="small" disabled={busy || disabled} onClick={() => setCorrectionOpen(true)}>Tạo bản điều chỉnh</Button>}
              <Button type="link" size="small" disabled={busy} aria-expanded={expanded} onClick={() => setExpanded(v => !v)}>
                {expanded ? 'Thu gọn' : lines.length ? 'Xem đơn thuốc' : canEditDraft ? 'Kê đơn thuốc' : 'Xem'}
              </Button>
            </Space>
          ) : undefined
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
          Đơn thuốc mua ngoài tại hiệu thuốc. Phòng khám không bán thuốc tại chỗ.
        </Typography.Paragraph>

        {rx?.correctionReason && (
          <Alert type="info" showIcon style={{ marginBottom: 12 }} message="Lý do điều chỉnh" description={rx.correctionReason} />
        )}

        {safetyWarnings.map((w, idx) => (
          <Alert
            key={idx}
            type={w.warningType === 'Allergy' ? 'error' : 'warning'}
            showIcon
            message={w.warningType === 'Allergy' ? 'Cảnh báo dị ứng' : w.warningType === 'DuplicateIngredient' ? 'Trùng hoạt chất' : 'Chống chỉ định cần đối chiếu'}
            description={w.message}
            style={{ marginBottom: 8 }}
          />
        ))}

        {notice && <Alert type="success" showIcon closable onClose={() => setNotice(undefined)} message={notice} style={{ marginBottom: 12 }} />}
        {saveError && <Alert role="alert" type="error" showIcon message={errorMessage(saveError)} style={{ marginBottom: 12 }} />}

        {loadError ? (
          <Alert type="error" showIcon message="Không tải được đơn thuốc" description={loadError}
            action={<Button onClick={() => void load()}>Thử tải lại</Button>} />
        ) : lines == null ? (
          <Spin />
        ) : !expanded ? (
          <Typography.Text type="secondary">{lines.length ? `Đã kê ${lines.length} thuốc.` : 'Chưa kê thuốc cho lượt khám này.'}</Typography.Text>
        ) : (
          <>
            {lines.length > 0 ? (
              <Table
                size="small"
                rowKey="id"
                pagination={false}
                dataSource={lines}
                scroll={{ x: 760 }}
                columns={[
                  { title: 'STT', width: 50, render: (_, __, idx) => idx + 1 },
                  {
                    title: 'Thuốc',
                    render: (_, line: PrescriptionLine) => (
                      <>
                        <strong>{line.medicineName}</strong>
                        <div>{[line.medicineCode, line.activeIngredient, line.strength, line.dosageForm, line.route].filter(Boolean).join(' · ')}</div>
                      </>
                    ),
                  },
                  {
                    title: 'Liều dùng',
                    render: (_, line: PrescriptionLine) => (
                      <div>
                        {doseSummary(line)}
                        <div><Tag color="blue">{mealLabel(line.mealTiming)}</Tag><Tag>{line.days} ngày</Tag></div>
                      </div>
                    ),
                  },
                  { title: 'Số lượng', width: 110, render: (_, line: PrescriptionLine) => <strong>{line.quantity} {line.unit}</strong> },
                  { title: 'Lời dặn', dataIndex: 'instructions' },
                  ...(canEditDraft
                    ? [{
                        title: 'Thao tác',
                        width: 130,
                        render: (_: unknown, line: PrescriptionLine) => (
                          <Space size="small">
                            <Button size="small" disabled={busy || disabled} onClick={() => edit(line)}>Sửa</Button>
                            <Button size="small" danger disabled={busy || disabled} onClick={() => deleteLine(line)}>Xóa</Button>
                          </Space>
                        ),
                      }]
                    : []),
                ]}
              />
            ) : (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chưa có thuốc trong đơn" />
            )}

            <div style={{ marginTop: 16 }}>
              <Typography.Text strong>Lời dặn chung của bác sĩ</Typography.Text>
              {canEditDraft ? (
                <>
                  <Input.TextArea
                    aria-label="Lời dặn chung của bác sĩ"
                    rows={2}
                    maxLength={1000}
                    showCount
                    disabled={busy || disabled}
                    value={advice}
                    placeholder="Ví dụ: Uống nhiều nước, không uống rượu bia trong thời gian dùng thuốc. Tái khám nếu sưng đau tăng."
                    onChange={e => { setAdvice(e.target.value); setAdviceDirty(true); }}
                    style={{ marginTop: 4 }}
                  />
                  {adviceDirty && (
                    <Space style={{ marginTop: 6 }}>
                      <Button size="small" type="primary" loading={busy} disabled={disabled} onClick={saveAdvice}>Lưu lời dặn</Button>
                      <Button size="small" disabled={busy} onClick={() => { setAdvice(rx?.generalAdvice ?? ''); setAdviceDirty(false); }}>Hoàn tác</Button>
                    </Space>
                  )}
                </>
              ) : (
                <Typography.Paragraph style={{ whiteSpace: 'pre-wrap', marginTop: 4 }}>{rx?.generalAdvice || '—'}</Typography.Paragraph>
              )}
            </div>

            {canEditDraft && (
              <div style={{ marginTop: 16, paddingTop: 16, borderTop: '1px solid #f0f0f0' }}>
                <Typography.Title level={5}>{editing ? `Sửa dòng thuốc: ${editing.medicineName}` : 'Thêm thuốc vào đơn'}</Typography.Title>
                {optionsError && <Alert type="error" message="Không tải được danh mục thuốc" description={optionsError} style={{ marginBottom: 12 }} />}

                <Form
                  form={form}
                  name={`prescription-${visitId}`}
                  layout="vertical"
                  initialValues={{ route: '', morningDose: '1', noonDose: '0', afternoonDose: '0', eveningDose: '1', mealTiming: 'AfterMeal', days: 5, quantity: 10 }}
                  disabled={busy || disabled}
                  onValuesChange={changed => {
                    setDirty(true);
                    if (['morningDose', 'noonDose', 'afternoonDose', 'eveningDose', 'days'].some(k => k in changed)) recalculate();
                  }}
                  onFinish={save}
                >
                  <Row gutter={8} align="bottom">
                    <Col flex="auto">
                      <Form.Item name="medicineId" label="Tên thuốc" rules={[{ validator: (_, value) => value || form.getFieldValue('customMedicineName') ? Promise.resolve() : Promise.reject(new Error('Chọn thuốc hoặc nhập thuốc tùy chỉnh.')) }]}>
                        <Select
                          showSearch
                          filterOption={false}
                          loading={optionsLoading}
                          onSearch={value => setQuery(value.slice(0, 100))}
                          placeholder="Tìm theo mã, tên biệt dược hoặc hoạt chất..."
                          notFoundContent={optionsLoading ? <Spin size="small" /> : 'Không có thuốc phù hợp'}
                          options={selectedOptions.map(o => ({
                            value: o.id,
                            label: [o.code, o.name, o.activeIngredient, o.strength, o.dosageForm].filter(Boolean).join(' · '),
                          }))}
                          onChange={medicineId => {
                            const opt = options.find(item => item.id === medicineId);
                            form.setFieldsValue({ customMedicineName: undefined, customUnit: undefined });
                            if (opt) form.setFieldsValue({ route: opt.route || '', instructions: opt.defaultInstructions ?? '' });
                          }}
                        />
                      </Form.Item>
                    </Col>
                    {canAddMedicine && <Col>
                      <Button size="small" shape="circle" aria-label="Thêm thuốc vào danh mục" title="Thêm thuốc vào danh mục" disabled={busy || disabled} onClick={() => { setCatalogError(undefined); setCatalogAddOpen(true); }}>+</Button>
                    </Col>}
                  </Row>
                  <Button type="link" size="small" style={{ paddingInline: 0, marginTop: -14, marginBottom: 8 }} disabled={busy || disabled}
                    onClick={() => { quickAddForm.setFieldsValue({ name: watchedCustomName || '', unit: watchedCustomUnit || 'viên', instructions: form.getFieldValue('instructions') || '' }); setQuickAddOpen(true); }}>
                    Nhập thuốc chỉ dùng cho đơn này
                  </Button>
                  <Form.Item name="customMedicineName" hidden><Input /></Form.Item>
                  <Form.Item name="customUnit" hidden><Input /></Form.Item>
                  {!watchedMedicineId && watchedCustomName && (
                    <Alert type="warning" showIcon message={`Thuốc tùy chỉnh: ${watchedCustomName} (${watchedCustomUnit || 'đơn vị'})`} description="Tên thuốc này chỉ được lưu trong đơn hiện tại, không thêm vào danh mục thuốc." style={{ marginBottom: 12 }} />
                  )}
                  {selectedMedicine?.allergyWarnings && (
                    <Alert type="warning" showIcon style={{ marginBottom: 12 }} message="Cảnh báo dị ứng của thuốc" description={selectedMedicine.allergyWarnings} />
                  )}
                  {selectedMedicine?.contraindications && (
                    <Alert type="warning" showIcon style={{ marginBottom: 12 }} message="Chống chỉ định" description={selectedMedicine.contraindications} />
                  )}

                  <Row gutter={12}>
                    <Col xs={12} sm={6}>
                      <Form.Item name="route" label="Đường dùng" rules={[{ max: 40, message: 'Tối đa 40 ký tự.' }]}>
                        <Select
                          allowClear
                          placeholder="Theo danh mục"
                          options={['Uống', 'Bôi', 'Súc miệng', 'Ngậm', 'Nhỏ', 'Đặt'].map(value => ({ value, label: value }))}
                        />
                      </Form.Item>
                    </Col>
                    <Col xs={12} sm={6}>
                      <Form.Item name="mealTiming" label="Thời điểm dùng">
                        <Select options={[{ value: 'AfterMeal', label: 'Sau ăn' }, { value: 'BeforeMeal', label: 'Trước ăn' }, { value: 'WithMeal', label: 'Trong bữa ăn' }]} />
                      </Form.Item>
                    </Col>
                    <Col xs={12} sm={6}>
                      <Form.Item name="days" label="Số ngày dùng" rules={[{ required: true, message: 'Nhập số ngày.' }]}>
                        <InputNumber min={1} max={365} precision={0} style={{ width: '100%' }} />
                      </Form.Item>
                    </Col>
                    <Col xs={12} sm={6}>
                      <Form.Item name="quantity" label="Tổng số lượng" tooltip="Tự tính từ liều số × số ngày; có thể sửa tay."
                        rules={[{ required: true, message: 'Nhập số lượng.' }, { type: 'integer', min: 1, max: 10000, message: '1–10.000' }]}>
                        <InputNumber min={1} max={10000} precision={0} style={{ width: '100%' }} addonAfter={selectedMedicine?.baseUnit ?? (watchedCustomName ? watchedCustomUnit : undefined)} />
                      </Form.Item>
                    </Col>
                  </Row>

                  <Row gutter={12}>
                    {([['morningDose', 'Sáng'], ['noonDose', 'Trưa'], ['afternoonDose', 'Chiều'], ['eveningDose', 'Tối']] as const).map(([name, label]) => (
                      <Col xs={12} sm={6} key={name}>
                        <Form.Item name={name} label={label} rules={[{ max: 20, message: 'Tối đa 20 ký tự.' }]}>
                          <Input placeholder="0" />
                        </Form.Item>
                      </Col>
                    ))}
                  </Row>

                  <Form.Item name="instructions" label="Lời dặn cho thuốc này" rules={[{ max: 1000, message: 'Tối đa 1.000 ký tự.' }]}>
                    <Input.TextArea rows={2} maxLength={1000} placeholder="Ví dụ: Uống với nhiều nước; ngừng thuốc và báo bác sĩ nếu nổi mẩn." />
                  </Form.Item>

                  <Space wrap>
                    <Button type="primary" size="large" htmlType="submit" loading={busy} disabled={busy || disabled}>{editing ? 'Lưu thay đổi' : '+ THÊM'}</Button>
                    {(editing || dirty) && <Button disabled={busy || disabled} onClick={resetLineForm}>{editing ? 'Hủy sửa' : 'Bỏ phần nhập'}</Button>}
                  </Space>
                </Form>
              </div>
            )}

            {history.length > 1 && (
              <div style={{ marginTop: 16 }}>
                <Typography.Text strong>Lịch sử phiên bản đơn</Typography.Text>
                <Timeline
                  style={{ marginTop: 8 }}
                  items={history.map(h => ({
                    color: h.status === 'Finalized' ? 'green' : h.status === 'Draft' ? 'blue' : 'gray',
                    children: (
                      <div>
                        <Space wrap>
                          <span>Phiên bản {h.revision}</span>
                          {statusTag(h.status)}
                          <span>{h.lineCount} thuốc</span>
                          {h.doctorName && <span>· {h.doctorName}</span>}
                          <span>· {new Date(h.finalizedAt ?? h.createdAt).toLocaleString('vi-VN')}</span>
                        </Space>
                        {h.correctionReason && <div><Typography.Text type="secondary">Lý do: {h.correctionReason}</Typography.Text></div>}
                      </div>
                    ),
                  }))}
                />
              </div>
            )}
          </>
        )}
      </Card>

      <PrescriptionPrintModal visitId={visitId} open={printModalOpen} onClose={() => setPrintModalOpen(false)} />
      <Modal
        open={catalogAddOpen}
        title="Thuốc, dược phẩm, sản phẩm y tế"
        okText="Lưu lại"
        cancelText="Quay lại"
        confirmLoading={catalogSaving}
        okButtonProps={{ disabled: !canAddMedicine || busy || disabled }}
        onCancel={() => { if (!catalogSaving) { setCatalogAddOpen(false); setCatalogError(undefined); } }}
        onOk={() => void createCatalogMedicine()}
      >
        {!canAddMedicine && <Alert type="error" showIcon message="Tài khoản chưa có quyền quản lý danh mục thuốc." style={{ marginBottom: 16 }} />}
        {catalogError && <Alert type="error" showIcon message="Không lưu được thuốc" description={catalogError} style={{ marginBottom: 16 }} />}
        <Form form={catalogMedicineForm} layout="horizontal" labelCol={{ span: 7 }} wrapperCol={{ span: 17 }} initialValues={{ referencePrice: 0 }} preserve={false}>
          <Form.Item name="name" label="Tên" rules={[{ required: true, whitespace: true, message: 'Nhập tên thuốc.' }, { max: 200, message: 'Tối đa 200 ký tự.' }]}>
            <Input autoFocus maxLength={200} placeholder="Ví dụ: Paracetamol (Panadol) 500mg" />
          </Form.Item>
          <Form.Item name="code" label="Mã thuốc/mã sản phẩm" rules={[{ required: true, whitespace: true, message: 'Nhập mã thuốc hoặc mã sản phẩm.' }, { max: 40, message: 'Tối đa 40 ký tự.' }, { pattern: /^[A-Za-z0-9][A-Za-z0-9._-]*$/, message: 'Mã chỉ gồm chữ, số, dấu chấm, gạch dưới hoặc gạch ngang.' }]}>
            <Input maxLength={40} placeholder="Ví dụ: PARA-500" onBlur={event => catalogMedicineForm.setFieldValue('code', event.target.value.trim().toUpperCase())} />
          </Form.Item>
          <Form.Item name="activeIngredient" label="Hoạt chất/biệt dược" rules={[{ max: 200, message: 'Tối đa 200 ký tự.' }]}>
            <Input maxLength={200} />
          </Form.Item>
          <Form.Item name="strength" label="Hàm lượng" rules={[{ max: 200, message: 'Tối đa 200 ký tự.' }]}>
            <Input maxLength={200} placeholder="Ví dụ: 500mg" />
          </Form.Item>
          <Form.Item name="dosageForm" label="Dạng bào chế" rules={[{ max: 200, message: 'Tối đa 200 ký tự.' }]}>
            <Input maxLength={200} placeholder="Ví dụ: Viên nén" />
          </Form.Item>
          <Form.Item name="baseUnit" label="Đơn vị tính/đóng gói" rules={[{ required: true, whitespace: true, message: 'Nhập đơn vị tính.' }, { max: 40, message: 'Tối đa 40 ký tự.' }]}>
            <Input maxLength={40} placeholder="Ví dụ: viên, gói, ml" />
          </Form.Item>
          <Form.Item name="packaging" label="Quy cách đóng gói" rules={[{ max: 160, message: 'Tối đa 160 ký tự.' }]}>
            <Input maxLength={160} placeholder="Ví dụ: Hộp 20 viên" />
          </Form.Item>
          <Form.Item name="referencePrice" label="Đơn giá (₫)" rules={[{ type: 'integer', min: 0, max: 1_000_000_000_000, message: 'Nhập giá nguyên không âm.' }]}>
            <InputNumber min={0} precision={0} step={1000} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="instructions" label="Hướng dẫn sử dụng" rules={[{ max: 1000, message: 'Tối đa 1.000 ký tự.' }]}>
            <Input.TextArea rows={2} maxLength={1000} showCount />
          </Form.Item>
        </Form>
      </Modal>
      <Modal
        open={quickAddOpen}
        title="Thêm thuốc tùy chỉnh vào dòng kê"
        okText="Dùng thuốc này"
        cancelText="Hủy"
        confirmLoading={busy}
        onCancel={() => setQuickAddOpen(false)}
        onOk={async () => {
          const values = await quickAddForm.validateFields();
          form.setFieldsValue({ medicineId: '', customMedicineName: values.name.trim(), customUnit: values.unit.trim(), instructions: values.instructions?.trim() || '' });
          setDirty(true);
          setQuickAddOpen(false);
          form.submit();
        }}
      >
        <Form form={quickAddForm} layout="vertical" initialValues={{ unit: 'viên' }}>
          <Form.Item name="name" label="Tên thuốc tùy chỉnh" rules={[{ required: true, whitespace: true, message: 'Nhập tên thuốc.' }, { max: 200, message: 'Tối đa 200 ký tự.' }]}>
            <Input autoFocus maxLength={200} placeholder="Nhập tên thuốc" />
          </Form.Item>
          <Form.Item name="unit" label="Đơn vị" rules={[{ required: true, whitespace: true, message: 'Nhập đơn vị tính.' }, { max: 100, message: 'Tối đa 100 ký tự.' }]}>
            <Input maxLength={100} placeholder="Ví dụ: viên, gói, ml" />
          </Form.Item>
          <Form.Item name="instructions" label="Hướng dẫn / ghi chú riêng cho thuốc này" rules={[{ max: 1000, message: 'Tối đa 1.000 ký tự.' }]}>
            <Input.TextArea rows={3} maxLength={1000} placeholder="Ví dụ: Uống sau khi ăn no; dùng khi đau nhiều." />
          </Form.Item>
        </Form>
      </Modal>
      <Modal
        open={correctionOpen}
        title="Tạo bản điều chỉnh đơn thuốc"
        okText="Tạo bản điều chỉnh"
        cancelText="Hủy"
        confirmLoading={busy}
        okButtonProps={{ disabled: !correctionReason.trim() }}
        onOk={createCorrection}
        onCancel={() => { if (!busy) setCorrectionOpen(false); }}
      >
        <Typography.Paragraph>
          Đơn đã hoàn tất không được sửa trực tiếp. Bản điều chỉnh sao chép đơn hiện tại thành bản nháp mới; khi hoàn tất, bản cũ được đánh dấu đã thay thế và vẫn lưu trong lịch sử.
        </Typography.Paragraph>
        <Input.TextArea
          aria-label="Lý do lâm sàng"
          rows={3}
          maxLength={500}
          showCount
          value={correctionReason}
          placeholder="Lý do lâm sàng (bắt buộc), ví dụ: Bệnh nhân báo dị ứng amoxicillin sau khi kê đơn."
          onChange={e => setCorrectionReason(e.target.value)}
        />
      </Modal>
    </section>
  );
}
